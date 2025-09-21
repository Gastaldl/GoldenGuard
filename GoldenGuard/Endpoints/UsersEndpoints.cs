using GoldenGuard.Domain.DTOs;
using GoldenGuard.Domain.Entities;
using GoldenGuard.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http.HttpResults;

namespace GoldenGuard.Endpoints;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsers(this IEndpointRouteBuilder app)
    {
        // Grupo /api/users protegido: precisa estar autenticado (qualquer papel)
        var group = app.MapGroup("/api/users")
                       .WithTags("Users")
                       .RequireAuthorization();

        // LISTAR
        group.MapGet("/", async (IUserRepository repo) =>
        {
            var users = await repo.ListAsync();
            return TypedResults.Ok(users);
        });

        // OBTER POR ID
        group.MapGet("/{id:long}", async Task<Results<Ok<UserProfile>, NotFound>> (long id, IUserRepository repo) =>
        {
            var user = await repo.GetAsync(id);
            return user is null ? TypedResults.NotFound() : TypedResults.Ok(user);
        });

        // CRIAR (somente admin)
        group.MapPost("/", async Task<Results<Created, BadRequest<string>>> (CreateUserDto dto, IUserRepository repo) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name)) return TypedResults.BadRequest("Name é obrigatório.");
            if (string.IsNullOrWhiteSpace(dto.Email)) return TypedResults.BadRequest("Email é obrigatório.");
            if (dto.MonthlyIncome < 0) return TypedResults.BadRequest("MonthlyIncome não pode ser negativo.");

            var id = await repo.CreateAsync(new UserProfile
            {
                Name = dto.Name.Trim(),
                Email = dto.Email.Trim(),
                MonthlyIncome = dto.MonthlyIncome
            });

            return TypedResults.Created($"/api/users/{id}");
        })
        .RequireAuthorization("Admin");

        // ATUALIZAR (somente admin)
        group.MapPut("/{id:long}", async Task<Results<NoContent, NotFound, BadRequest<string>>> (long id, UpdateUserDto dto, IUserRepository repo) =>
        {
            var existing = await repo.GetAsync(id);
            if (existing is null) return TypedResults.NotFound();

            if (dto.MonthlyIncome is < 0)
                return TypedResults.BadRequest("MonthlyIncome não pode ser negativo.");

            existing.Name = dto.Name?.Trim() ?? existing.Name;
            existing.Email = dto.Email?.Trim() ?? existing.Email;
            existing.MonthlyIncome = dto.MonthlyIncome ?? existing.MonthlyIncome;

            var ok = await repo.UpdateAsync(id, existing);
            return ok ? TypedResults.NoContent() : TypedResults.BadRequest("Falha ao atualizar.");
        })
        .RequireAuthorization("Admin");

        // EXCLUIR (somente admin)
        group.MapDelete("/{id:long}", async Task<Results<NoContent, NotFound>> (long id, IUserRepository repo) =>
        {
            var ok = await repo.DeleteAsync(id);
            return ok ? TypedResults.NoContent() : TypedResults.NotFound();
        })
        .RequireAuthorization("Admin");

        return app;
    }
}