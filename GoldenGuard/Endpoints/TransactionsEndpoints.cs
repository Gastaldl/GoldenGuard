using Dapper;
using GoldenGuard.Application.Services;
using GoldenGuard.Data;
using GoldenGuard.Domain.DTOs;
using GoldenGuard.Domain.Entities;
using GoldenGuard.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http.HttpResults;
using Oracle.ManagedDataAccess.Client;

namespace GoldenGuard.Endpoints;

public readonly record struct ImportJsonResult(int Inserted);

public static class TransactionsEndpoints
{
    private static readonly HashSet<string> AllowedKinds =
        new(StringComparer.OrdinalIgnoreCase) { "DEPOSIT", "WITHDRAWAL", "OTHER" };

    public static IEndpointRouteBuilder MapTransactions(this IEndpointRouteBuilder app)
    {
        // Todo o grupo exige estar autenticado
        var group = app.MapGroup("/api/transactions")
                       .WithTags("Transactions")
                       .RequireAuthorization();

        // LISTAR por usuário (autenticado)
        group.MapGet("/by-user/{userId:long}",
            async (long userId, DateTime? from, DateTime? to, ITransactionRepository repo)
                => TypedResults.Ok(await repo.ListByUserAsync(userId, from, to)));

        // OBTER por ID (autenticado)
        group.MapGet("/{id:long}",
            async Task<Results<Ok<Transaction>, NotFound>> (long id, ITransactionRepository repo) =>
            {
                var tx = await repo.GetAsync(id);
                return tx is null ? TypedResults.NotFound() : TypedResults.Ok(tx);
            });

        // CRIAR (somente admin)
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

                // Força o payload para object para casar com Created<object>
                object payload = new { id };
                return TypedResults.Created($"/api/transactions/{id}", payload);
            })
            .RequireAuthorization("Admin");

        // ATUALIZAR (somente admin)
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

        // EXCLUIR (somente admin)
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

        // IMPORT JSON (somente admin)
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

                await files.WriteJsonAsync(items); // snapshot
                await files.AppendAuditAsync($"IMPORT JSON count={inserted}");
                return TypedResults.Ok(new ImportJsonResult(inserted));
            })
            .RequireAuthorization("Admin");

        // EXPORT JSON por usuário (autenticado)
        group.MapGet("/export-json/{userId:long}",
            async (long userId, ITransactionRepository repo, FileStorageService files) =>
            {
                var txs = await repo.ListByUserAsync(userId);
                await files.WriteJsonAsync(txs);
                await files.AppendAuditAsync($"EXPORT JSON user={userId} count={txs.Count()}");
                return TypedResults.Ok(txs);
            });

        // MÉTRICA DE RISCO (autenticado)
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

        // ESTATÍSTICA MENSAL (autenticado) — para o gráfico (Chart.js)
        group.MapGet("/stats/monthly/{userId:long}",
            async (long userId, int? year, IOracleConnectionFactory factory) =>
            {
                var y = year ?? DateTime.UtcNow.Year;
                using var conn = factory.Create();

                // Habilita BindByName quando for OracleConnection
                if (conn is OracleConnection oc) oc.BindByName = true;

                var sql = @"
                    WITH M AS (
                      SELECT TRUNC(OCCURRED_AT, 'MM') AS YM,
                             SUM(CASE WHEN UPPER(KIND) = 'DEPOSIT'    THEN AMOUNT ELSE 0 END) AS DEPOSITS,
                             SUM(CASE WHEN UPPER(KIND) = 'WITHDRAWAL' THEN AMOUNT ELSE 0 END) AS WITHDRAWALS
                      FROM TRANSACTIONS
                      WHERE USER_ID = :p_user_id
                        AND EXTRACT(YEAR FROM OCCURRED_AT) = :p_year
                      GROUP BY TRUNC(OCCURRED_AT, 'MM')
                    )
                    SELECT TO_CHAR(YM, 'YYYY-MM') AS YEAR_MONTH,
                           NVL(DEPOSITS,0) AS DEPOSITS,
                           NVL(WITHDRAWALS,0) AS WITHDRAWALS
                    FROM M
                    ORDER BY YM";

                var rows = await conn.QueryAsync(sql, new { p_user_id = userId, p_year = y });
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