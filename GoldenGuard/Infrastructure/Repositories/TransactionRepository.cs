using Dapper;
using GoldenGuard.Data;
using GoldenGuard.Domain.Entities;
using Oracle.ManagedDataAccess.Client;

namespace GoldenGuard.Infrastructure.Repositories;

public sealed class TransactionRepository(IOracleConnectionFactory factory) : ITransactionRepository
{
    public async Task<long> CreateAsync(Transaction tx)
    {
        using var conn = factory.Create();
        if (conn is OracleConnection oc) oc.BindByName = true;

        var sql = @"INSERT INTO TRANSACTIONS (USER_ID, OPERATOR, KIND, AMOUNT, OCCURRED_AT, RAW_LABEL)
                    VALUES (:UserId, :Operator, :Kind, :Amount, :OccurredAt, :RawLabel)
                    RETURNING ID INTO :Id";

        var p = new DynamicParameters();
        p.Add("UserId", tx.UserId);
        p.Add("Operator", tx.Operator);
        p.Add("Kind", tx.Kind);
        p.Add("Amount", tx.Amount);
        p.Add("OccurredAt", tx.OccurredAt);
        p.Add("RawLabel", tx.RawLabel);
        p.Add("Id", dbType: System.Data.DbType.Int64, direction: System.Data.ParameterDirection.Output);

        await conn.ExecuteAsync(sql, p);
        return p.Get<long>("Id");
    }

    public async Task<Transaction?> GetAsync(long id)
    {
        using var conn = factory.Create();
        if (conn is OracleConnection oc) oc.BindByName = true;

        return await conn.QuerySingleOrDefaultAsync<Transaction>(
            @"SELECT ID, USER_ID, OPERATOR, KIND, AMOUNT, OCCURRED_AT, RAW_LABEL, CREATED_AT
              FROM TRANSACTIONS
              WHERE ID = :id",
            new { id });
    }

    public async Task<IEnumerable<Transaction>> ListByUserAsync(long userId, DateTime? from = null, DateTime? to = null)
    {
        using var conn = factory.Create();
        if (conn is OracleConnection oc) oc.BindByName = true;

        var sql = @"SELECT ID, USER_ID, OPERATOR, KIND, AMOUNT, OCCURRED_AT, RAW_LABEL, CREATED_AT
                    FROM TRANSACTIONS
                    WHERE USER_ID = :userId
                      AND (:fromDate IS NULL OR OCCURRED_AT >= :fromDate)
                      AND (:toDate   IS NULL OR OCCURRED_AT <  :toDate)
                    ORDER BY OCCURRED_AT DESC";

        return await conn.QueryAsync<Transaction>(sql, new { userId, fromDate = from, toDate = to });
    }

    public async Task<bool> UpdateAsync(long id, Transaction tx)
    {
        using var conn = factory.Create();
        if (conn is OracleConnection oc) oc.BindByName = true;

        var rows = await conn.ExecuteAsync(
            @"UPDATE TRANSACTIONS
              SET OPERATOR = :Operator,
                  KIND = :Kind,
                  AMOUNT = :Amount,
                  OCCURRED_AT = :OccurredAt,
                  RAW_LABEL = :RawLabel
              WHERE ID = :Id",
            new { tx.Operator, tx.Kind, tx.Amount, tx.OccurredAt, tx.RawLabel, Id = id });
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        using var conn = factory.Create();
        if (conn is OracleConnection oc) oc.BindByName = true;

        var rows = await conn.ExecuteAsync(
            "DELETE FROM TRANSACTIONS WHERE ID = :id", new { id });
        return rows > 0;
    }
}