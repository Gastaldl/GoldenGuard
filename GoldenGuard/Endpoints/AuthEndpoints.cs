using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GoldenGuard.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace GoldenGuard.WebApi.Endpoints;

public static class AuthEndpoints
{
    public record LoginDto(string Username, string Password);

    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (LoginDto dto, GgDbContext db, IConfiguration cfg) =>
        {
            // DEV: senha em texto. Em produção, use hash (BCrypt/Argon2).
            var acc = await db.UserAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Username == dto.Username);

            if (acc is null || acc.PasswordHash != dto.Password)
                return Results.Unauthorized();

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, acc.UserId.ToString()),
                new(ClaimTypes.Name, acc.Username),
                new(ClaimTypes.Role, acc.Role)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expireMinutes = int.TryParse(cfg["Jwt:ExpireMinutes"], out var m) ? m : 60;

            var token = new JwtSecurityToken(
                issuer: cfg["Jwt:Issuer"],
                audience: cfg["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expireMinutes),
                signingCredentials: creds
            );

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);
            return Results.Ok(new { token = jwt, role = acc.Role, userId = acc.UserId });
        });

        return app;
    }
}