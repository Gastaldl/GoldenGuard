using GoldenGuard.Domain.DTOs;
using GoldenGuard.Domain.Entities;
using GoldenGuard.Infrastructure.Repositories;

namespace GoldenGuard.Endpoints;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsers(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users");

        group.MapGet("/", async (IUserRepository repo) => Results.Ok(await repo.ListAsync()));

        group.MapGet("/{id:long}", async (long id, IUserRepository repo) =>
        {
            var user = await repo.GetAsync(id);
            return user is null ? Results.NotFound() : Results.Ok(user);
        });

        group.MapPost("/", async (CreateUserDto dto, IUserRepository repo) =>
        {
            var id = await repo.CreateAsync(new UserProfile
            {
                Name = dto.Name,
                Email = dto.Email,
                MonthlyIncome = dto.MonthlyIncome
            });
            return Results.Created($"/api/users/{id}", new { id });
        });

        group.MapPut("/{id:long}", async (long id, UpdateUserDto dto, IUserRepository repo) =>
        {
            var existing = await repo.GetAsync(id);
            if (existing is null) return Results.NotFound();

            existing.Name = dto.Name ?? existing.Name;
            existing.Email = dto.Email ?? existing.Email;
            existing.MonthlyIncome = dto.MonthlyIncome ?? existing.MonthlyIncome;

            var ok = await repo.UpdateAsync(id, existing);
            return ok ? Results.NoContent() : Results.BadRequest();
        });

        group.MapDelete("/{id:long}", async (long id, IUserRepository repo) =>
        {
            var ok = await repo.DeleteAsync(id);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
