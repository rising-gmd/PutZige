#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using PutZige.Application.Services;
using PutZige.Application.Interfaces;
using PutZige.Domain.Entities;
using PutZige.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using AutoMapper;
using Microsoft.Extensions.Options;
using PutZige.Application.Settings;

namespace PutZige.Application.Tests.Services
{
    public class AuthServiceLogoutTests
    {
        [Fact]
        public async Task LogoutAsync_ClearsSessionInDatabase()
        {
            var userId = Guid.NewGuid();

            var user = new User
            {
                Id = userId,
                Session = new UserSession
                {
                    RefreshTokenHash = "hash",
                    RefreshTokenSalt = "salt",
                    RefreshTokenExpiry = DateTime.UtcNow.AddDays(1),
                    IsOnline = true
                }
            };

            var mockUserRepo = new Mock<IUserRepository>();
            mockUserRepo.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            var mockUow = new Mock<IUnitOfWork>();
            mockUow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var mockJwt = new Mock<IJwtTokenService>();
            var mockUserService = new Mock<IUserService>();
            var mockMapper = new Mock<IMapper>();
            var mockClient = new Mock<IClientInfoService>();
            var mockHash = new Mock<IHashingService>();
            var mockDate = new Mock<IDateTimeProvider>();

            var jwtSettings = Options.Create(new JwtSettings { Secret = new string('x', 32), Issuer = "i", Audience = "a", AccessTokenExpiryMinutes = 15, RefreshTokenExpiryDays = 7 });

            var svc = new AuthService(mockUserRepo.Object, mockUow.Object, mockJwt.Object, mockUserService.Object, mockMapper.Object, jwtSettings, mockClient.Object, mockHash.Object, mockDate.Object, null, Mock.Of<ILogger<AuthService>>(), null);

            await svc.LogoutAsync(userId, CancellationToken.None);

            Assert.Null(user.Session.RefreshTokenHash);
            Assert.Null(user.Session.RefreshTokenSalt);
            Assert.Null(user.Session.RefreshTokenExpiry);
            Assert.False(user.Session.IsOnline);

            mockUow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LogoutAsync_WithNullSession_DoesNotThrow()
        {
            var userId = Guid.NewGuid();
            var user = new User { Id = userId, Session = null };

            var mockUserRepo = new Mock<IUserRepository>();
            mockUserRepo.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            var mockUow = new Mock<IUnitOfWork>();
            var mockJwt = new Mock<IJwtTokenService>();
            var mockUserService = new Mock<IUserService>();
            var mockMapper = new Mock<IMapper>();
            var mockClient = new Mock<IClientInfoService>();
            var mockHash = new Mock<IHashingService>();
            var mockDate = new Mock<IDateTimeProvider>();

            var jwtSettings = Options.Create(new JwtSettings { Secret = new string('x', 32), Issuer = "i", Audience = "a", AccessTokenExpiryMinutes = 15, RefreshTokenExpiryDays = 7 });

            var svc = new AuthService(mockUserRepo.Object, mockUow.Object, mockJwt.Object, mockUserService.Object, mockMapper.Object, jwtSettings, mockClient.Object, mockHash.Object, mockDate.Object, null, Mock.Of<ILogger<AuthService>>(), null);

            await svc.LogoutAsync(userId, CancellationToken.None);

            mockUow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
