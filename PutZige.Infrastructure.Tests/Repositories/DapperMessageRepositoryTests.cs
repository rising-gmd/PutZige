// PutZige.Infrastructure.Tests/Repositories/DapperMessageRepositoryTests.cs
#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Dapper;
using Microsoft.Extensions.Logging;
using PutZige.Infrastructure.Repositories.Dapper;
using PutZige.Infrastructure.Data.Dapper;
using PutZige.Domain.DTOs;
using Xunit;
using System.Data;

namespace PutZige.Infrastructure.Tests.Repositories
{
    public class DapperMessageRepositoryTests
    {
        [Fact(Skip = "Requires real SQL Server connection - integration test only")]
        public async Task GetConversationHistoryAsync_WithConversationId_Works()
        {
            // Arrange
            var dbSettings = new PutZige.Infrastructure.Settings.DatabaseSettings { ConnectionString = "Server=dummy;Database=dummy;" };
            var options = Microsoft.Extensions.Options.Options.Create(dbSettings);
            var context = new DapperContext(options);
            var logger = Mock.Of<ILogger<DapperMessageRepository>>();
            var repo = new DapperMessageRepository(context, logger);

            // When using a dummy connection, QueryAsync should return empty for unknown SQL; ensure method handles it
            var conversationId = Guid.Empty;

            // Act
            var (messages, count) = await repo.GetConversationHistoryAsync(conversationId, 1, 10, CancellationToken.None);

            // Assert
            messages.Should().NotBeNull();
            count.Should().BeGreaterThanOrEqualTo(0L);
        }
    }
}
