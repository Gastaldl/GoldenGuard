using GoldenGuard.Data;
using Microsoft.EntityFrameworkCore;

namespace GoldenGuard.Endpoints;

public static class InsightsEndpoints
{
    public static IEndpointRouteBuilder MapInsights(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/insights").WithTags("Insights");

        // GET /api/insights/risk-summary/{userId}?periodMonths=3
        group.MapGet("/risk-summary/{userId:long}", async Task<IResult> (long userId, int? periodMonths, GgDbContext db) =>
        {
            var months = periodMonths.GetValueOrDefault(3);
            if (months <= 0 || months > 24) months = 3;

            var user = await db.Users.AsNoTracking()
                                     .Where(u => u.Id == userId)
                                     .Select(u => new { u.Id, u.Name, u.Email, u.MonthlyIncome })
                                     .FirstOrDefaultAsync();

            if (user is null) return Results.BadRequest("Usuário não encontrado.");

            var from = DateTime.UtcNow.AddMonths(-months);

            var totals = await db.Transactions.AsNoTracking()
                .Where(t => t.UserId == userId && t.OccurredAt >= from)
                .GroupBy(t => 1)
                .Select(g => new
                {
                    deposits = g.Where(t => t.Kind.ToUpper() == "DEPOSIT").Sum(t => (decimal?)t.Amount) ?? 0m,
                    withdrawals = g.Where(t => t.Kind.ToUpper() == "WITHDRAWAL").Sum(t => (decimal?)t.Amount) ?? 0m
                })
                .FirstOrDefaultAsync() ?? new { deposits = 0m, withdrawals = 0m };

            // Placeholder de "AI summary" (sem OpenAI aqui para evitar compile issues)
            var aiSummary =
                $"Nos últimos {months} meses: Depósitos {totals.deposits:C} / Saques {totals.withdrawals:C}. " +
                $"Renda mensal cadastrada: {user.MonthlyIncome:C}.";

            object payload = new
            {
                user,
                periodMonths = months,
                totals,
                aiSummary
            };
            return Results.Ok(payload);
        });

        return app;
    }
}