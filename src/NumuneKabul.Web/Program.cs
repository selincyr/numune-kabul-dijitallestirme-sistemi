using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NumuneKabul.Application.Interfaces;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Infrastructure.Data;
using NumuneKabul.Infrastructure.Services.Extraction;
using NumuneKabul.Infrastructure.Services.Integration;
using NumuneKabul.Infrastructure.Services.Ocr;
using NumuneKabul.Infrastructure.Services.Pdf;
using NumuneKabul.Infrastructure.Services.Xml;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection bulunamadı.");

var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDirectory);

connectionString = connectionString.Replace("|DataDirectory|", dataDirectory);

var databaseProvider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key appsettings içinde bulunamadı.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "NumuneKabul";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "NumuneKabulClient";

builder.Services.AddRazorPages();
builder.Services.AddControllers();

builder.Services.AddScoped<IPdfRenderer, PdfRenderer>();
builder.Services.AddScoped<IOcrService, TesseractOcrService>();
builder.Services.AddScoped<IFieldExtractionService, RegexFieldExtractionService>();
builder.Services.AddScoped<IXmlGenerationService, XmlGenerationService>();
builder.Services.AddScoped<IIntegrationService, MockRestIntegrationService>();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        var sqlServerConnectionString = builder.Configuration.GetConnectionString("SqlServerConnection")
            ?? throw new InvalidOperationException("SqlServerConnection bulunamadı.");

        options.UseSqlServer(sqlServerConnectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole(UserRoles.Admin));

    options.AddPolicy("PersonnelOrAdmin", policy =>
        policy.RequireRole(UserRoles.Admin, UserRoles.Personnel));

    options.AddPolicy("IntegrationOnly", policy =>
        policy.RequireRole(UserRoles.IntegrationService));

    options.AddPolicy("IntegrationOrAdmin", policy =>
        policy.RequireRole(UserRoles.Admin, UserRoles.IntegrationService));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.MapStaticAssets();

app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages()
    .WithStaticAssets();

app.MapControllers();

app.Run();