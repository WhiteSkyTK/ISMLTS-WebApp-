using ISMLTS_WebApp_.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ISMLTS.Tests
{
    // A fresh in-memory database per test for services that need real EF queries
    public sealed class SqliteTestDb : IDisposable
    {
        private readonly SqliteConnection _connection = new("DataSource=:memory:");

        public SqliteTestDb()
        {
            _connection.Open();
            using var db = NewContext();
            db.Database.EnsureCreated();
        }

        public ApplicationDbContext NewContext() =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options);

        public void Dispose() => _connection.Dispose();
    }
}
