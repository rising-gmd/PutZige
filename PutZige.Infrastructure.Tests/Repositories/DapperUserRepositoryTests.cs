using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using Dapper;
using Microsoft.Data.SqlClient;
using PutZige.Infrastructure.Repositories.Dapper;
using PutZige.Infrastructure.Data.Dapper;
using Microsoft.Extensions.Logging;
using PutZige.Domain.DTOs;
using System.Data;

namespace PutZige.Infrastructure.Tests.Repositories
{
    public class DapperUserRepositoryTests
    {
        [Fact]
        public async Task SearchUsersAsync_WithEmptyQuery_ReturnsEmpty()
        {
            // Arrange - create a real DapperContext with a dummy connection string (won't be used for empty query)
            var dbSettings = new PutZige.Infrastructure.Settings.DatabaseSettings { ConnectionString = "Server=dummy;Database=dummy;" };
            var options = Microsoft.Extensions.Options.Options.Create(dbSettings);
            var context = new DapperContext(options);
            var logger = Mock.Of<ILogger<DapperUserRepository>>();

            var repo = new DapperUserRepository(context, logger);

            // Act
            var res = await repo.SearchUsersAsync("", Guid.NewGuid(), 20, CancellationToken.None);
            
            // Assert
            res.Should().BeEmpty();
        }
    }
}
