using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PortfolioManager.Api.Services;
using System.Text;
using System.Net;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Configuration (Updated for MongoDB)
var mongoUri = builder.Configuration["DATABASE_URL"] ?? "mongodb://localhost:27017";
var mongoClient = new MongoClient(mongoUri);
var databaseName = "KineticCapitalDB";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMongoDB(mongoClient, databaseName));

// 2. CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "https://kinetic-capital.vercel.app") 
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 3.  Application Servces
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMemoryCache();

builder.Services.AddScoped<PortfolioHealthService>();
builder.Services.AddScoped<StockDetailsService>();
builder.Services.AddScoped<IEmailService, EmailService>(); 
builder.Services.AddScoped<StockPriceService>(); 
builder.Services.AddScoped<NewsService>();      

// Typed HttpClients for External APIs
builder.Services.AddHttpClient<StockPriceService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        UseCookies = true,
        CookieContainer = new CookieContainer(),
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    });

builder.Services.AddHttpClient<NewsService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        UseCookies = true,
        CookieContainer = new CookieContainer(),
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    });

// 4. Swagger Configuration
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "PortfolioManager", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// 5. Authentication Configuration
var jwtKey = builder.Configuration["Jwt:Key"] ?? builder.Configuration["Jwt__Key"];
if (string.IsNullOrEmpty(jwtKey))
{
    throw new Exception("JWT Key is missing. Check environment variables.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false, 
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero 
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// 6. Database Initialization (Removed EnsureCreated for MongoDB)

// 7. Middleware Pipeline
app.UseSwagger();
app.UseSwaggerUI();

// Render Dynamic Port Binding
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Add($"http://0.0.0.0:{port}");

app.UseHttpsRedirection();
app.UseCors("FrontendPolicy");

app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllers();

app.Run();