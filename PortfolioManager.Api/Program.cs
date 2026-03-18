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

var builder = WebApplication.CreateBuilder(args);

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var mongoUri =
    Environment.GetEnvironmentVariable("DATABASE_URL") ?? builder.Configuration["DATABASE_URL"];

if (string.IsNullOrEmpty(mongoUri))
{
    mongoUri = "mongodb://localhost:27017"; 
}

var mongoClient = new MongoClient(mongoUri);
var databaseName = "KineticCapitalDB";

builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddScoped(sp => mongoClient.GetDatabase(databaseName));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMongoDB(mongoClient, databaseName)
);

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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMemoryCache();

builder.Services.AddScoped<PortfolioHealthService>();
builder.Services.AddScoped<StockDetailsService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<StockPriceService>();
builder.Services.AddScoped<NewsService>();
builder.Services.AddScoped<IStockAnalysisService, StockAnalysisService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<MarketService>();
builder.Services.AddControllers();

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

var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") ?? builder.Configuration["Jwt:Key"];

if (string.IsNullOrEmpty(jwtKey))
{
    throw new Exception("JWT Key is missing. Check your .env file.");
}

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

app.UseSwagger();
app.UseSwaggerUI();

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
if (!app.Environment.IsDevelopment())
{
    app.Urls.Add($"http://0.0.0.0:{port}");
}

app.UseHttpsRedirection();
app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
