using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RentalManagement.Infrastructure.Persistence;

namespace RentalManagement.Tests;

/// <summary>
/// Keeps one open SQLite in-memory connection alive so multiple AppDbContext instances
/// can share the same database — needed to simulate two independent save operations
/// racing against each other for the optimistic-concurrency tests.
/// </summary>
public sealed class SqliteAppDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteAppDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options);
    }

    public void Dispose() => _connection.Dispose();
}
