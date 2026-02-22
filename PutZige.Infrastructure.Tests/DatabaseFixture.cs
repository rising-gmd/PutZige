#nullable enable
using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using PutZige.Infrastructure.Data;

namespace PutZige.Infrastructure.Tests.Repositories
{
    public class DatabaseFixture : IDisposable
    {
        private readonly string _connectionString;

        public DatabaseFixture()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false, reloadOnChange: false)
                .Build();

            _connectionString = config.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection missing in test appsettings.json");

            using var ctx = CreateContext();
            ctx.Database.EnsureCreated();
        }

        public AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(_connectionString)
                .Options;

            return new AppDbContext(options);
        }

        public void Dispose()
        {
            try
            {
                using var ctx = CreateContext();
                ctx.Database.EnsureDeleted();
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}
