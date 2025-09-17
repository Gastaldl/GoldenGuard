using GoldenGuard.Application.Services;
using GoldenGuard.Domain.DTOs;
using GoldenGuard.Domain.Entities;
using GoldenGuard.Infrastructure.Repositories;

namespace GoldenGuard.Endpoints;

public static class TransactionsEndpoints
{
    public static IEndpointRouteBuilder MapTransactions(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/transactions").WithTags("Transactions");

        group.MapGet("/by-user/{userId:long}", async (long userId, DateTime? from, DateTime? to, ITransactionRepository repo)
            => Results.Ok(await repo.ListByUserAsync(userId, from, to)));

        group.MapGet("/{id:long}", async (long id, ITransactionRepository repo) =>
        {
            var tx = await repo.GetAsync(id);
            return tx is null ? Results.NotFound() : Results.Ok(tx);
        });

        group.MapPost("/", async (CreateTransactionDto dto, ITransactionRepository repo, FileStorageService files) =>
        {
            var id = await repo.CreateAsync(new Transaction
            {
                UserId = dto.UserId,
                Operator = dto.Operator,
                Kind = dto.Kind,
                Amount = dto.Amount,
                OccurredAt = dto.OccurredAt,
                RawLabel = dto.RawLabel
            });
            await files.AppendAuditAsync($"CREATE TX id={id} user={dto.UserId} amount={dto.Amount}");
            return Results.Created($"/api/transactions/{id}", new { id });
        });

        group.MapPut("/{id:long}", async (long id, UpdateTransactionDto dto, ITransactionRepository repo, FileStorageService files) =>
        {
            var existing = await repo.GetAsync(id);
            if (existing is null) return Results.NotFound();

            existing.Operator = dto.Operator ?? existing.Operator;
            existing.Kind = dto.Kind ?? existing.Kind;
            existing.Amount = dto.Amount ?? existing.Amount;
            existing.OccurredAt = dto.OccurredAt ?? existing.OccurredAt;
            existing.RawLabel = dto.RawLabel ?? existing.RawLabel;

            var ok = await repo.UpdateAsync(id, existing);
            if (ok) await files.AppendAuditAsync($"UPDATE TX id={id}");
            return ok ? Results.NoContent() : Results.BadRequest();
        });

        group.MapDelete("/{id:long}", async (long id, ITransactionRepository repo, FileStorageService files) =>
        {
            var ok = await repo.DeleteAsync(id);
            if (ok) await files.AppendAuditAsync($"DELETE TX id={id}");
            return ok ? Results.NoContent() : Results.NotFound();
        });

        // Importa/Exporta JSON para manipulação em arquivos
        group.MapPost("/import-json", async (List<CreateTransactionDto> items, ITransactionRepository repo, FileStorageService files) =>
        {
            var inserted = 0;
            foreach (var dto in items)
            {
                await repo.CreateAsync(new Transaction
                {
                    UserId = dto.UserId,
                    Operator = dto.Operator,
                    Kind = dto.Kind,
                    Amount = dto.Amount,
                    OccurredAt = dto.OccurredAt,
                    RawLabel = dto.RawLabel
                });
                inserted++;
            }
            await files.WriteJsonAsync(items); // snapshot
            await files.AppendAuditAsync($"IMPORT JSON count={inserted}");
            return Results.Ok(new { inserted });
        });

        group.MapGet("/export-json/{userId:long}", async (long userId, ITransactionRepository repo, FileStorageService files) =>
        {
            var txs = await repo.ListByUserAsync(userId);
            await files.WriteJsonAsync(txs);
            await files.AppendAuditAsync($"EXPORT JSON user={userId} count={txs.Count()}");
            return Results.Ok(txs);
        });

        // Métrica de risco (percentual do mês vs renda)
        group.MapGet("/risk/{userId:long}/{year:int}/{month:int}", async (long userId, int year, int month, TransactionService service) =>
        {
            var ratio = await service.GetMonthlyBettingRatioAsync(userId, year, month);
            return Results.Ok(new { ratioPercent = ratio, above30 = TransactionService.IsAboveRiskThreshold(ratio) });
        });

        return app;
    }
}
