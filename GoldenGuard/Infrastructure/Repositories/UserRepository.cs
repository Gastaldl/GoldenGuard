using Dapper;
using GoldenGuard.Data;
using GoldenGuard.Domain.Entities;
using Oracle.ManagedDataAccess.Client;

namespace GoldenGuard.Infrastructure.Repositories;

public sealed class UserRepository(IOracleConnectionFactory factory) : IUserRepository
{
    public async Task<long> CreateAsync(UserProfile user)
    {
        using var conn = factory.Create();
        if (conn is OracleConnection oc) oc.BindByName = true;

        var sql = @"INSERT INTO USERS (NAME, EMAIL, MONTHLY_INCOME)
                    VALUES (:Name, :Email, :MonthlyIncome)
                    RETURNING ID INTO :Id";

        var p = new DynamicParameters();
        p.Add("Name", user.Name);
        p.Add("Email", user.Email);
        p.Add("MonthlyIncome", user.MonthlyIncome);
        p.Add("Id", dbType: System.Data.DbType.Int64, direction: System.Data.ParameterDirection.Output);

        await conn.ExecuteAsync(sql, p);
        return p.Get<long>("Id");
    }

    public async Task<UserProfile?> GetAsync(long id)
    {
        using var conn = factory.Create();
        if (conn is OracleConnection oc) oc.BindByName = true;

        return await conn.QuerySingleOrDefaultAsync<UserProfile>(
            @"SELECT ID, NAME, EMAIL, MONTHLY_INCOME, CREATED_AT
              FROM USERS
              WHERE ID = :id",
            new { id });
    }

    public async Task<IEnumerable<UserProfile>> ListAsync()
    {
        using var conn = factory.Create();
        if (conn is OracleConnection oc) oc.BindByName = true;

        return await conn.QueryAsync<UserProfile>(
            @"SELECT ID, NAME, EMAIL, MONTHLY_INCOME, CREATED_AT
              FROM USERS
              ORDER BY ID DESC");
    }

    public async Task<bool> UpdateAsync(long id, UserProfile user)
    {
        using var conn = factory.Create();
        if (conn is OracleConnection oc) oc.BindByName = true;

        var rows = await conn.ExecuteAsync(
            @"UPDATE USERS
              SET NAME = :Name,
                  EMAIL = :Email,
                  MONTHLY_INCOME = :MonthlyIncome
              WHERE ID = :Id",
            new { user.Name, user.Email, user.MonthlyIncome, Id = id });
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        using var conn = factory.Create();
        if (conn is OracleConnection oc) oc.BindByName = true;

        var rows = await conn.ExecuteAsync(
            "DELETE FROM USERS WHERE ID = :id", new { id });
        return rows > 0;
    }
}