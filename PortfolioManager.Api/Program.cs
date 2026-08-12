using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using dotenv.net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Workers;

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// --- 1. MONGODB CONFIGURATION ---
var mongoUri =
    Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration["DATABASE_URL"]
    ?? "mongodb://localhost:27017";
var settings = MongoClientSettings.FromConnectionString(mongoUri);
settings.ServerSelectionTimeout = TimeSpan.FromSeconds(30);
settings.ConnectTimeout = TimeSpan.FromSeconds(30);
settings.MaxConnectionPoolSize = 50;

var mongoClient = new MongoClient(settings);
var databaseName = "KineticCapitalDB";

builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddScoped(sp => mongoClient.GetDatabase(databaseName));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMongoDB(mongoClient, databaseName)
);

// --- 2. CACHING CONFIGURATION ---
builder.Services.AddMemoryCache();
var redisConnectionString =
    Environment.GetEnvironmentVariable("REDIS_URL")
    ?? builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrEmpty(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "Kinetic_";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

// --- 3. RESPONSE COMPRESSION ---
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

// --- 4. CORS & AUTH ---
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "FrontendPolicy",
        policy =>
        {
            policy
                .WithOrigins("http://localhost:5173", "https://kinetic-capital.vercel.app")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    );
});

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// --- 5. DEPENDENCY INJECTION ---
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PortfolioHealthService>();
builder.Services.AddScoped<StockDetailsService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<NewsService>();
builder.Services.AddScoped<IStockAnalysisService, StockAnalysisService>();
builder.Services.AddScoped<IPromptService, PromptService>();
builder.Services.AddScoped<PeerComparisonService>();

builder.Services.AddHttpClient<MarketService>();
builder.Services.AddSingleton<MarketService>();

builder
    .Services.AddHttpClient<StockPriceService>()
    .ConfigurePrimaryHttpMessageHandler(() =>
        new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        }
    );
builder
    .Services.AddHttpClient<NewsService>()
    .ConfigurePrimaryHttpMessageHandler(() =>
        new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        }
    );

builder.Services.AddHostedService<MarketScannerWorker>();
builder.Services.AddMemoryCache();

// --- 6. JWT AUTHENTICATION ---
var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") ?? builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey))
    throw new Exception("JWT Key missing.");

builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };
    });
builder.Services.AddAuthorization();

// --- 7. SWAGGER ---
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "PortfolioManager", Version = "v1" });
    c.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Description = "Please enter token",
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            BearerFormat = "JWT",
            Scheme = "bearer",
        }
    );
    c.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer",
                    },
                },
                Array.Empty<string>()
            },
        }
    );
});

var app = builder.Build();

// --- 8. MIDDLEWARE PIPELINE ---
app.UseResponseCompression();
app.UseCors("FrontendPolicy");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Run($"http://0.0.0.0:{port}");
