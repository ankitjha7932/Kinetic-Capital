using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using dotenv.net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

DotEnv.Load();
Console.WriteLine(
    $"DEBUG: BREVO KEY LOADED: {!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BREVO_API_KEY"))}"
);

var builder = WebApplication.CreateBuilder(args);

// Clear default mapping to allow Custom Claims
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

// --- DATABASE CONFIGURATION (UPDATED FOR RENDER STABILITY) ---
var mongoUri =
    Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration["DATABASE_URL"]
    ?? "mongodb://localhost:27017";

// Configure settings to handle DNS transient errors on Render/Atlas
var settings = MongoClientSettings.FromConnectionString(mongoUri);

// Minimal change: Add timeouts to prevent startup crash (Status 139) during DNS resolution
settings.ServerSelectionTimeout = TimeSpan.FromSeconds(30);
settings.ConnectTimeout = TimeSpan.FromSeconds(30);

var mongoClient = new MongoClient(settings);
var databaseName = "KineticCapitalDB";

builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddScoped(sp => mongoClient.GetDatabase(databaseName));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMongoDB(mongoClient, databaseName)
);

// 1. CORS Policy Setup
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

// Services Registration
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMemoryCache();

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PortfolioHealthService>();
builder.Services.AddScoped<StockDetailsService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<StockPriceService>();
builder.Services.AddScoped<NewsService>();
builder.Services.AddScoped<IStockAnalysisService, StockAnalysisService>();
builder.Services.AddScoped<MarketService>();
builder.Services.AddScoped<IPromptService, PromptService>();
builder.Services.AddScoped<PeerComparisonService>();
builder.Services.AddHostedService<MarketScannerWorker>();

// HttpClient Configurations
builder
    .Services.AddHttpClient<StockPriceService>()
    .ConfigurePrimaryHttpMessageHandler(() =>
        new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = new CookieContainer(),
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        }
    );

builder
    .Services.AddHttpClient<NewsService>()
    .ConfigurePrimaryHttpMessageHandler(() =>
        new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = new CookieContainer(),
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        }
    );

// Swagger/OpenAPI
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "PortfolioManager", Version = "v1" });
    c.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme.",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
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

// JWT Authentication
var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") ?? builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey))
    throw new Exception("JWT Key is missing. Check your .env file.");

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

var app = builder.Build();

// --- MIDDLEWARE PIPELINE ---
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

// Minimal change: Port Binding logic optimized for Render to prevent 502/Port errors
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");
