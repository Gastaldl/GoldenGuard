using System.Data;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace GoldenGuard.Data;

public interface IOracleConnectionFactory
{
    IDbConnection Create();
}

public sealed class OracleConnectionFactory(IConfiguration cfg) : IOracleConnectionFactory
{
    private readonly string _cs = cfg.GetConnectionString("Oracle")
                                  ?? throw new InvalidOperationException("ConnectionStrings:Oracle not found.");

    public IDbConnection Create()
    {
        var conn = new OracleConnection(_cs);
        conn.Open();
        return conn;
    }
}
