using GoldenGuard.Domain.Entities;
using GoldenGuard.Infrastructure.Repositories;

namespace GoldenGuard.Application.Services;

public sealed class TransactionService(
    ITransactionRepository txRepo,
    IUserRepository userRepo)
{
    // Ex.: calcula % do total depositado no mês vs renda mensal do usuário
    public async Task<decimal> GetMonthlyBettingRatioAsync(long userId, int year, int month)
    {
        var user = await userRepo.GetAsync(userId)
                   ?? throw new KeyNotFoundException("User not found");

        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1);

        var txs = await txRepo.ListByUserAsync(userId, from, to);
        var deposits = txs.Where(t => t.Kind.Equals("DEPOSIT", StringComparison.OrdinalIgnoreCase))
                          .Sum(t => t.Amount);

        if (user.MonthlyIncome <= 0) return 0;
        return Math.Round(deposits / user.MonthlyIncome * 100m, 2);
    }

    public static bool IsAboveRiskThreshold(decimal ratioPercent) => ratioPercent >= 30m;
}
