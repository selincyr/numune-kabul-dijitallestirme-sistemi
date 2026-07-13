using Microsoft.EntityFrameworkCore;
using NumuneKabul.Infrastructure.Data;
using NumuneKabul.Application.Interfaces;
using NumuneKabul.Infrastructure.Services.Pdf;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection bulunamadı.");

var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDirectory);

connectionString = connectionString.Replace("|DataDirectory|", dataDirectory);

builder.Services.AddRazorPages();

builder.Services.AddScoped<IPdfRenderer, PdfRenderer>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();