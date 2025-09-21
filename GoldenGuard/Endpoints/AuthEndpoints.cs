using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dapper;
using GoldenGuard.Data;
using Microsoft.IdentityModel.Tokens;

namespace GoldenGuard.WebApi.Endpoints;

public static class AuthEndpoints
{
    public record LoginDto(string Username, string Password);


    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");


        group.MapPost("/login", async (LoginDto dto, IOracleConnectionFactory factory, IConfiguration cfg) =>
        {
            using var conn = factory.Create();
            var acc = await conn.QuerySingleOrDefaultAsync<(long UserId, string Username, string Password, string Role)>(
            "SELECT USER_ID as UserId, USERNAME, PASSWORD_HASH as Password, ROLE FROM USER_ACCOUNTS WHERE USERNAME = :u",
            new { u = dto.Username });


            if (acc == default || acc.Password != dto.Password)
                return Results.Unauthorized();


            // Claims
            var claims = new List<Claim>
            {
            new(ClaimTypes.NameIdentifier, acc.UserId.ToString()),
            new(ClaimTypes.Name, acc.Username),
            new(ClaimTypes.Role, acc.Role)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
            issuer: cfg["Jwt:Issuer"],
            audience: cfg["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(int.Parse(cfg["Jwt:ExpireMinutes"]!)),
            signingCredentials: creds
            );

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);
            return Results.Ok(new { token = jwt, role = acc.Role, userId = acc.UserId });
        });

        return app;
    }
}