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
            var context = new Mock<DapperContext>(MockBehavior.Strict, null as Microsoft.Extensions.Options.IOptions<PutZige.Infrastructure.Settings.DatabaseSettings>);
            var logger = Mock.Of<ILogger<DapperUserRepository>>();

            var repo = new DapperUserRepository(context.Object, logger);

            var res = await repo.SearchUsersAsync("", Guid.NewGuid(), 20, CancellationToken.None);
            res.Should().BeEmpty();
        }
    }
}
