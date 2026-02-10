using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using FluentAssertions;
using PutZige.Application.Services;
using PutZige.Application.Interfaces;
using PutZige.Domain.Interfaces;
using PutZige.Domain.DTOs;
using AutoMapper;
using Microsoft.Extensions.Logging;

namespace PutZige.Application.Tests.Services
{
    public class UserSearchTests
    {
        [Fact]
        public async Task SearchUsersAsync_ReturnsResults()
        {
            var mockUserRepo = new Mock<IUserRepository>();
            var mockUnit = new Mock<IUnitOfWork>();
            var mockMapper = new Mock<IMapper>();
            var mockHash = new Mock<PutZige.Application.Interfaces.IHashingService>();
            var mockDate = new Mock<PutZige.Application.Interfaces.IDateTimeProvider>();
            var mockCurrent = new Mock<PutZige.Application.Interfaces.ICurrentUserService>();
            var mockDapper = new Mock<IDapperUserRepository>();
            var logger = Mock.Of<ILogger<UserService>>();

            var user = new UserSearchProjection { Id = Guid.NewGuid(), Username = "alice", Email = "a@x.com", DisplayName = "Alice" };
            mockDapper.Setup(d => d.SearchUsersAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new [] { user });

            var svc = new UserService(mockUserRepo.Object, mockUnit.Object, mockMapper.Object, mockHash.Object, mockDate.Object, mockCurrent.Object, mockDapper.Object, null, logger);

            var results = await svc.SearchUsersAsync("ali", CancellationToken.None);

            results.Should().NotBeNull();
            results.Should().ContainSingle();
            results[0].Username.Should().Be("alice");
        }
    }
}
