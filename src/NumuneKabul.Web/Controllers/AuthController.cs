using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Infrastructure.Data;

namespace NumuneKabul.Web.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public AuthController(
        AppDbContext dbContext,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _environment = environment;
    }

    [HttpPost("seed-test-users")]
    public async Task<IActionResult> SeedTestUsersAsync()
    {
        if (!_environment.IsDevelopment())
        {
            return BadRequest(new
            {
                message = "Test kullanıcıları yalnızca Development ortamında oluşturulabilir."
            });
        }

        if (await _dbContext.AppUsers.AnyAsync())
        {
            return Ok(new
            {
                message = "Test kullanıcıları zaten mevcut."
            });
        }

        var users = new List<AppUser>
        {
            new()
            {
                UserName = "admin",
                FullName = "Sistem Yöneticisi",
                PasswordHash = ComputeSha256Hash("admin123"),
                Role = UserRoles.Admin,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            },
            new()
            {
                UserName = "personel",
                FullName = "Numune Kabul Personeli",
                PasswordHash = ComputeSha256Hash("personel123"),
                Role = UserRoles.Personnel,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            },
            new()
            {
                UserName = "entegrasyon",
                FullName = "Entegrasyon Servisi",
                PasswordHash = ComputeSha256Hash("entegrasyon123"),
                Role = UserRoles.IntegrationService,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            }
        };

        _dbContext.AppUsers.AddRange(users);

        AddAuditLog(
            "SeedTestUsers",
            "JWT test kullanıcıları oluşturuldu.");

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            message = "Test kullanıcıları oluşturuldu.",
            users = users.Select(x => new
            {
                x.UserName,
                x.FullName,
                x.Role
            })
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Kullanıcı adı ve şifre zorunludur."
            });
        }

        var userName = request.UserName.Trim();

        var user = await _dbContext.AppUsers
            .FirstOrDefaultAsync(x => x.UserName == userName);

        if (user is null || !user.IsActive)
        {
            return Unauthorized(new
            {
                message = "Kullanıcı adı veya şifre hatalı."
            });
        }

        var passwordHash = ComputeSha256Hash(request.Password);

        if (!string.Equals(user.PasswordHash, passwordHash, StringComparison.Ordinal))
        {
            return Unauthorized(new
            {
                message = "Kullanıcı adı veya şifre hatalı."
            });
        }

        user.LastLoginDate = DateTime.UtcNow;

        AddAuditLog(
            "JwtLogin",
            $"{user.UserName} kullanıcısı JWT ile giriş yaptı.");

        await _dbContext.SaveChangesAsync();

        var token = GenerateJwtToken(user);

        return Ok(new
        {
            message = "Giriş başarılı.",
            token,
            user = new
            {
                user.Id,
                user.UserName,
                user.FullName,
                user.Role
            }
        });
    }

    private string GenerateJwtToken(AppUser user)
    {
        var key = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key appsettings içinde bulunamadı.");

        var issuer = _configuration["Jwt:Issuer"] ?? "NumuneKabul";
        var audience = _configuration["Jwt:Audience"] ?? "NumuneKabulClient";

        var expireMinutesText = _configuration["Jwt:ExpireMinutes"];
        var expireMinutes = double.TryParse(expireMinutesText, out var parsedExpireMinutes)
            ? parsedExpireMinutes
            : 120;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.Role),
            new("fullName", user.FullName)
        };

        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(key));

        var credentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expireMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string ComputeSha256Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return Convert.ToHexString(bytes);
    }

    private void AddAuditLog(string action, string description)
    {
        _dbContext.AuditLogs.Add(new AuditLog
        {
            Action = action,
            Description = description,
            Date = DateTime.UtcNow
        });
    }

    public class LoginRequest
    {
        public string UserName { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }
}