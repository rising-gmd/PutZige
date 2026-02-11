#nullable enable
using System;
using System.Threading.Tasks;
using System.Threading;
using Xunit;
using Moq;
using PutZige.Application.Services;
using PutZige.Application.Interfaces;
using PutZige.Domain.Interfaces;
using PutZige.Domain.Entities;
using Microsoft.Extensions.Logging;
using AutoMapper;
using PutZige.Application.DTOs.Messaging;

namespace PutZige.Application.Tests.Services
{
    public class MessagingServiceNotifierTests
    {
        [Fact]
        public async Task MarkMessageAsDelivered_CallsRealTimeNotifier()
        {
            var messageId = Guid.NewGuid();
            var message = new Message { Id = messageId, SenderId = Guid.NewGuid(), ReceiverId = Guid.NewGuid(), SentAt = DateTime.UtcNow };

            var mockMsgRepo = new Mock<IMessageRepository>();
            mockMsgRepo.Setup(m => m.GetByIdAsync(messageId, It.IsAny<CancellationToken>())).ReturnsAsync(message);
            mockMsgRepo.Setup(m => m.UpdateAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var mockUserRepo = new Mock<IUserRepository>();
            var mockUow = new Mock<IUnitOfWork>();
            mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var mockMapper = new Mock<IMapper>();

            var mockNotifier = new Mock<IRealTimeNotifier>();
            mockNotifier.Setup(n => n.TryNotifyMessageDeliveredAsync(message.SenderId, messageId, It.IsAny<DateTime>())).Returns(Task.CompletedTask).Verifiable();

            var svc = new MessagingService(mockMsgRepo.Object, mockUserRepo.Object, mockUow.Object, mockMapper.Object, mockNotifier.Object, new Mock<PutZige.Application.Interfaces.ICurrentUserService>().Object, new Mock<PutZige.Application.Interfaces.IDateTimeProvider>().Object, Mock.Of<ILogger<MessagingService>>());

            await svc.MarkMessageAsDeliveredAsync(messageId);

            mockNotifier.Verify();
        }

        [Fact]
        public async Task MarkMessageAsRead_CallsRealTimeNotifier()
        {
            var messageId = Guid.NewGuid();
            var message = new Message { Id = messageId, SenderId = Guid.NewGuid(), ReceiverId = Guid.NewGuid(), SentAt = DateTime.UtcNow };

            var mockMsgRepo = new Mock<IMessageRepository>();
            mockMsgRepo.Setup(m => m.GetByIdAsync(messageId, It.IsAny<CancellationToken>())).ReturnsAsync(message);
            mockMsgRepo.Setup(m => m.UpdateAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var mockUserRepo = new Mock<IUserRepository>();
            var mockUow = new Mock<IUnitOfWork>();
            mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var mockMapper = new Mock<IMapper>();

            var mockNotifier = new Mock<IRealTimeNotifier>();
            mockNotifier.Setup(n => n.TryNotifyMessageReadAsync(message.SenderId, messageId, It.IsAny<DateTime>())).Returns(Task.CompletedTask).Verifiable();

            var svc = new MessagingService(mockMsgRepo.Object, mockUserRepo.Object, mockUow.Object, mockMapper.Object, mockNotifier.Object, new Mock<PutZige.Application.Interfaces.ICurrentUserService>().Object, new Mock<PutZige.Application.Interfaces.IDateTimeProvider>().Object, Mock.Of<ILogger<MessagingService>>());

            await svc.MarkMessageAsReadAsync(messageId);

            mockNotifier.Verify();
        }
    }
}
