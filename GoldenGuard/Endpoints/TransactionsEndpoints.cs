using GoldenGuard.Application.Services;
using GoldenGuard.Data;
using GoldenGuard.Domain.DTOs;
using GoldenGuard.Domain.Entities;
using GoldenGuard.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace GoldenGuard.Endpoints;

public readonly record struct ImportJsonResult(int Inserted);

public static class TransactionsEndpoints
{
    private static readonly HashSet<string> AllowedKinds =
        new(StringComparer.OrdinalIgnoreCase) { "DEPOSIT", "WITHDRAWAL", "OTHER" };

    public static IEndpointRouteBuilder MapTransactions(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/transactions")
                       .WithTags("Transactions")
                       .RequireAuthorization();

        // LISTAR por usuário
        group.MapGet("/by-user/{userId:long}",
            async (long userId, DateTime? from, DateTime? to, ITransactionRepository repo)
                => TypedResults.Ok(await repo.ListByUserAsync(userId, from, to)));

        // OBTER por ID
        group.MapGet("/{id:long}",
            async Task<Results<Ok<Transaction>, NotFound>> (long id, ITransactionRepository repo) =>
            {
                var tx = await repo.GetAsync(id);
                return tx is null ? TypedResults.NotFound() : TypedResults.Ok(tx);
            });

        // CRIAR (admin)
        group.MapPost("/",
            async Task<Results<Created<object>, BadRequest<string>>> (
                CreateTransactionDto dto,
                ITransactionRepository repo,
                FileStorageService files) =>
            {
                var err = ValidateCreate(dto);
                if (err is not null) return TypedResults.BadRequest(err);

                var id = await repo.CreateAsync(new Transaction
                {
                    UserId = dto.UserId,
                    Operator = dto.Operator.Trim(),
                    Kind = dto.Kind.Trim().ToUpperInvariant(),
                    Amount = dto.Amount,
                    OccurredAt = dto.OccurredAt,
                    RawLabel = dto.RawLabel
                });

                await files.AppendAuditAsync($"CREATE TX id={id} user={dto.UserId} amount={dto.Amount}");
                object payload = new { id };
                return TypedResults.Created($"/api/transactions/{id}", payload);
            })
            .RequireAuthorization("Admin");

        // ATUALIZAR (admin)
        group.MapPut("/{id:long}",
            async Task<Results<NoContent, NotFound, BadRequest<string>>> (
                long id,
                UpdateTransactionDto dto,
                ITransactionRepository repo,
                FileStorageService files) =>
            {
                var existing = await repo.GetAsync(id);
                if (existing is null) return TypedResults.NotFound();

                if (dto.Amount is < 0) return TypedResults.BadRequest("Amount não pode ser negativo.");
                if (dto.Kind is not null && !AllowedKinds.Contains(dto.Kind))
                    return TypedResults.BadRequest("Kind inválido. Use: DEPOSIT, WITHDRAWAL ou OTHER.");

                existing.Operator = dto.Operator?.Trim() ?? existing.Operator;
                existing.Kind = dto.Kind is null ? existing.Kind : dto.Kind.Trim().ToUpperInvariant();
                existing.Amount = dto.Amount ?? existing.Amount;
                existing.OccurredAt = dto.OccurredAt ?? existing.OccurredAt;
                existing.RawLabel = dto.RawLabel ?? existing.RawLabel;

                var ok = await repo.UpdateAsync(id, existing);
                if (ok) await files.AppendAuditAsync($"UPDATE TX id={id}");
                return ok ? TypedResults.NoContent() : TypedResults.BadRequest("Falha ao atualizar.");
            })
            .RequireAuthorization("Admin");

        // EXCLUIR (admin)
        group.MapDelete("/{id:long}",
            async Task<Results<NoContent, NotFound>> (
                long id,
                ITransactionRepository repo,
                FileStorageService files) =>
            {
                var ok = await repo.DeleteAsync(id);
                if (ok) await files.AppendAuditAsync($"DELETE TX id={id}");
                return ok ? TypedResults.NoContent() : TypedResults.NotFound();
            })
            .RequireAuthorization("Admin");

        // IMPORT JSON (admin)
        group.MapPost("/import-json",
            async Task<Results<Ok<ImportJsonResult>, BadRequest<string>>> (
                List<CreateTransactionDto> items,
                ITransactionRepository repo,
                FileStorageService files) =>
            {
                if (items is null || items.Count == 0)
                    return TypedResults.BadRequest("Payload vazio.");

                var inserted = 0;
                foreach (var dto in items)
                {
                    var err = ValidateCreate(dto);
                    if (err is not null) return TypedResults.BadRequest($"Linha {inserted + 1}: {err}");

                    await repo.CreateAsync(new Transaction
                    {
                        UserId = dto.UserId,
                        Operator = dto.Operator.Trim(),
                        Kind = dto.Kind.Trim().ToUpperInvariant(),
                        Amount = dto.Amount,
                        OccurredAt = dto.OccurredAt,
                        RawLabel = dto.RawLabel
                    });
                    inserted++;
                }

                await files.WriteJsonAsync(items);
                await files.AppendAuditAsync($"IMPORT JSON count={inserted}");
                return TypedResults.Ok(new ImportJsonResult(inserted));
            })
            .RequireAuthorization("Admin");

        // EXPORT JSON por usuário
        group.MapGet("/export-json/{userId:long}",
            async (long userId, ITransactionRepository repo, FileStorageService files) =>
            {
                var txs = await repo.ListByUserAsync(userId);
                await files.WriteJsonAsync(txs);
                await files.AppendAuditAsync($"EXPORT JSON user={userId} count={txs.Count()}");
                return TypedResults.Ok(txs);
            });

        // KPI de risco (Application Service)
        group.MapGet("/risk/{userId:long}/{year:int}/{month:int}",
            async (long userId, int year, int month, TransactionService service) =>
            {
                var ratio = await service.GetMonthlyBettingRatioAsync(userId, year, month);
                return TypedResults.Ok(new
                {
                    ratioPercent = ratio,
                    above30 = TransactionService.IsAboveRiskThreshold(ratio)
                });
            });

        // 📊 Estatística mensal (LINQ/EF) para Chart.js
        group.MapGet("/stats/monthly/{userId:long}",
            async (long userId, int? year, GgDbContext db) =>
            {
                var y = year ?? DateTime.UtcNow.Year;

                var rows = await db.Transactions
                    .AsNoTracking()
                    .Where(t => t.UserId == userId && t.OccurredAt.Year == y)
                    .GroupBy(t => new { t.OccurredAt.Year, t.OccurredAt.Month })
                    .Select(g => new
                    {
                        YEAR_MONTH = $"{g.Key.Year:D4}-{g.Key.Month:D2}",
                        DEPOSITS = g.Where(x => x.Kind.ToUpper() == "DEPOSIT").Sum(x => x.Amount),
                        WITHDRAWALS = g.Where(x => x.Kind.ToUpper() == "WITHDRAWAL").Sum(x => x.Amount)
                    })
                    .OrderBy(x => x.YEAR_MONTH)
                    .ToListAsync();

                return TypedResults.Ok(rows);
            });

        return app;
    }

    private static string? ValidateCreate(CreateTransactionDto dto)
    {
        if (dto.UserId <= 0) return "UserId inválido.";
        if (string.IsNullOrWhiteSpace(dto.Operator)) return "Operator é obrigatório.";
        if (string.IsNullOrWhiteSpace(dto.Kind)) return "Kind é obrigatório.";
        if (!AllowedKinds.Contains(dto.Kind)) return "Kind inválido. Use: DEPOSIT, WITHDRAWAL ou OTHER.";
        if (dto.Amount < 0) return "Amount não pode ser negativo.";
        if (dto.OccurredAt == default) return "OccurredAt inválido.";
        return null;
    }
}