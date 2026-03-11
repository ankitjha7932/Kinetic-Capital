using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

var builder = WebApplication.CreateBuilder(args);

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

// 1. Database Configuration
// This gets your Atlas string from appsettings.json
var mongoUri = builder.Configuration["DATABASE_URL"];
if (string.IsNullOrEmpty(mongoUri))
{
    // Fallback only if environment variable is missing
    mongoUri = "mongodb://localhost:27017";
}

var mongoClient = new MongoClient(mongoUri);
var databaseName = "KineticCapitalDB";

// Register the client and database for Dependency Injection
builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddScoped(sp => mongoClient.GetDatabase(databaseName));

// EF Core Configuration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMongoDB(mongoClient, databaseName)
);

// 2. CORS Configuration
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

// 3. Application Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMemoryCache();

builder.Services.AddScoped<PortfolioHealthService>();
builder.Services.AddScoped<StockDetailsService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<StockPriceService>();
builder.Services.AddScoped<NewsService>();

// REMOVED the line: builder.Services.AddSingleton<IMongoClient>(new MongoClient("mongodb://localhost:27017"));
// That line was causing the localhost timeout error!

// Typed HttpClients for External APIs
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

// 4. Swagger Configuration
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "PortfolioManager", Version = "v1" });
    c.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Description =
                "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
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

// 5. Authentication Configuration
var jwtKey = builder.Configuration["Jwt:Key"] ?? builder.Configuration["Jwt__Key"];
if (string.IsNullOrEmpty(jwtKey))
{
    throw new Exception("JWT Key is missing. Check environment variables.");
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

// 6. Middleware Pipeline
app.UseSwagger();
app.UseSwaggerUI();

// Render Dynamic Port Binding
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
