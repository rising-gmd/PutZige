using System;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using PutZige.Infrastructure.Settings;

namespace PutZige.Infrastructure.Data.Dapper;

/// <summary>
/// Lightweight Dapper connection factory. Scoped lifetime.
/// </summary>
public sealed class DapperContext : IDisposable
{
    private readonly string _connectionString;
    private IDbConnection? _connection;
    private bool _disposed;

    public DapperContext(IOptions<DatabaseSettings> options)
    {
        _connectionString = options?.Value?.ConnectionString ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Creates or returns an open connection. Caller should not dispose the returned connection.
    /// </summary>
    public IDbConnection GetOpenConnection()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DapperContext));

        if (_connection == null)
        {
            _connection = new SqlConnection(_connectionString);
        }

        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }

        return _connection;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _connection?.Close();
            _connection?.Dispose();
        }
        catch
        {
            // swallow on dispose - nothing we can do. Keep minimal surface.
        }
    }
}
