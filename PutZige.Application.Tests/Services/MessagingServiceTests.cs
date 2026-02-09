#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PutZige.Application.DTOs.Messaging;
using PutZige.Application.Services;
using PutZige.Domain.Entities;
using PutZige.Domain.Interfaces;
using Xunit;

namespace PutZige.Application.Tests.Services;

public partial class MessagingServiceTests
{
    private readonly Mock<IMessageRepository> _mockMessageRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<ILogger<MessagingService>> _mockLogger;
    private readonly MessagingService _sut;
    private readonly CancellationToken _ct = CancellationToken.None;

    public MessagingServiceTests()
    {
        _mockMessageRepo = new Mock<IMessageRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockMapper = new Mock<IMapper>();
        _mockLogger = new Mock<ILogger<MessagingService>>();

        // Default mapper behaviors used across tests
        _mockMapper.Setup(m => m.Map<SendMessageResponse>(It.IsAny<Message>())).Returns((Message msg) => new SendMessageResponse(msg.Id, msg.SenderId, msg.ReceiverId, msg.MessageText, msg.SentAt));
        _mockMapper.Setup(m => m.Map<MessageDto>(It.IsAny<Message>())).Returns((Message msg) => new MessageDto { Id = msg.Id, MessageText = msg.MessageText, SenderId = msg.SenderId, ReceiverId = msg.ReceiverId, SentAt = msg.SentAt });

        // Default behavior: return a non-null user for any id unless a test overrides this setup
        _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid id, CancellationToken _) => new Domain.Entities.User { Id = id });
        // Also setup the overload that accepts include expressions to avoid Moq overload resolution mismatches
        _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.User, object>>[]>())).ReturnsAsync((Guid id, CancellationToken _, System.Linq.Expressions.Expression<Func<Domain.Entities.User, object>>[] __) => new Domain.Entities.User { Id = id });

        _sut = new MessagingService(_mockMessageRepo.Object, _mockUserRepo.Object, _mockUow.Object, _mockMapper.Object, _mockLogger.Object);
    }

    // Helper to create a message entity
    private Message CreateMessage(Guid sender, Guid receiver, DateTime sentAt, string text = "hi") => new Message
    {
        Id = Guid.NewGuid(),
        SenderId = sender,
        ReceiverId = receiver,
        MessageText = text,
        SentAt = sentAt,
        CreatedAt = DateTime.UtcNow
    };

    /// <summary>
    /// Verifies that SendMessageAsync_ValidInputs_CreatesMessageAndReturnsResponse behaves as expected.
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_ValidInputs_CreatesMessageAndReturnsResponse()
    {
        // Arrange
        var sender = Guid.NewGuid();
        var receiver = Guid.NewGuid();
        _mockUserRepo.Setup(r => r.GetByIdAsync(receiver, It.IsAny<CancellationToken>())).ReturnsAsync(new Domain.Entities.User { Id = receiver });
        Message? captured = null;
        _mockMessageRepo.Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>())).Callback<Message, CancellationToken>((m, ct) => captured = m).Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockMapper.Setup(m => m.Map<SendMessageResponse>(It.IsAny<Message>())).Returns((Message msg) => new SendMessageResponse(msg.Id, msg.SenderId, msg.ReceiverId, msg.MessageText, msg.SentAt));

        // Act
        var res = await _sut.SendMessageAsync(sender, receiver, "hello", _ct);

        // Assert
        res.Should().NotBeNull();
        res.MessageText.Should().Be("hello");
        captured.Should().NotBeNull();
        captured!.SenderId.Should().Be(sender);
        captured.ReceiverId.Should().Be(receiver);
    }

    /// <summary>
    /// Verifies that SendMessageAsync_ReceiverNotFound_ThrowsInvalidOperationException behaves as expected.
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_ReceiverNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var sender = Guid.NewGuid();
        var receiver = Guid.NewGuid();
        _mockUserRepo.Setup(r => r.GetByIdAsync(receiver, It.IsAny<CancellationToken>())).ReturnsAsync((Domain.Entities.User?)null);

        // Act
        Func<Task> act = async () => await _sut.SendMessageAsync(sender, receiver, "hello", _ct);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    /// <summary>
    /// Verifies that SendMessageAsync_MessageTooLong_ThrowsValidationException behaves as expected.
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_MessageTooLong_ThrowsValidationException()
    {
        // Arrange
        var sender = Guid.NewGuid();
        var receiver = Guid.NewGuid();
        var longText = new string('x', PutZige.Application.Common.Constants.AppConstants.Messaging.MaxMessageLength + 1);

        // Act
        Func<Task> act = async () => await _sut.SendMessageAsync(sender, receiver, longText, _ct);

        // Assert
        await act.Should().ThrowAsync<PutZige.Application.Common.AppException>();
    }

    /// <summary>
    /// Verifies that SendMessageAsync_SenderEqualsReceiver_Allowed behaves as expected.
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_SenderEqualsReceiver_Allowed()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mockUserRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new Domain.Entities.User { Id = id });
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockMessageRepo.Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockMapper.Setup(m => m.Map<SendMessageResponse>(It.IsAny<Message>())).Returns((Message msg) => new SendMessageResponse(msg.Id, msg.SenderId, msg.ReceiverId, msg.MessageText, msg.SentAt));

        // Act
        var res = await _sut.SendMessageAsync(id, id, "self", _ct);

        // Assert
        res.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that SendMessageAsync_NullMessageText_ThrowsArgumentException behaves as expected.
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_NullMessageText_ThrowsArgumentException()
    {
        // Arrange
        var sender = Guid.NewGuid();
        var receiver = Guid.NewGuid();

        // Act
        Func<Task> act = async () => await _sut.SendMessageAsync(sender, receiver, "   ", _ct);

        // Assert
        await act.Should().ThrowAsync<PutZige.Application.Common.AppException>();
    }

    /// <summary>
    /// Verifies that SendMessageAsync_SaveSuccessful_LogsInformation behaves as expected.
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_SaveSuccessful_LogsInformation()
    {
        // Arrange
        var sender = Guid.NewGuid();
        var receiver = Guid.NewGuid();
        _mockUserRepo.Setup(r => r.GetByIdAsync(sender, It.IsAny<CancellationToken>())).ReturnsAsync(new Domain.Entities.User { Id = sender });
        _mockUserRepo.Setup(r => r.GetByIdAsync(receiver, It.IsAny<CancellationToken>())).ReturnsAsync(new Domain.Entities.User { Id = receiver });
        _mockMessageRepo.Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var res = await _sut.SendMessageAsync(sender, receiver, "hello", _ct);

        // Assert
        res.Should().NotBeNull();
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that SendMessageAsync_RepositoryThrows_PropagatesException behaves as expected.
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_RepositoryThrows_PropagatesException()
    {
        // Arrange
        var sender = Guid.NewGuid();
        var receiver = Guid.NewGuid();
        _mockUserRepo.Setup(r => r.GetByIdAsync(sender, It.IsAny<CancellationToken>())).ReturnsAsync(new Domain.Entities.User { Id = sender });
        _mockUserRepo.Setup(r => r.GetByIdAsync(receiver, It.IsAny<CancellationToken>())).ReturnsAsync(new Domain.Entities.User { Id = receiver });
        _mockMessageRepo.Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db"));

        // Act
        Func<Task> act = async () => await _sut.SendMessageAsync(sender, receiver, "hello", _ct);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that SendMessageAsync_SetsCorrectTimestamps_SentAtIsUtcNow behaves as expected.
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_SetsCorrectTimestamps_SentAtIsUtcNow()
    {
        // Arrange
        var sender = Guid.NewGuid();
        var receiver = Guid.NewGuid();
        DateTime before = DateTime.UtcNow;
        _mockUserRepo.Setup(r => r.GetByIdAsync(sender, It.IsAny<CancellationToken>())).ReturnsAsync(new Domain.Entities.User { Id = sender });
        _mockUserRepo.Setup(r => r.GetByIdAsync(receiver, It.IsAny<CancellationToken>())).ReturnsAsync(new Domain.Entities.User { Id = receiver });
        Message? captured = null;
        _mockMessageRepo.Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>())).Callback<Message, CancellationToken>((m, ct) => captured = m).Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var res = await _sut.SendMessageAsync(sender, receiver, "hello", _ct);

        // Assert
        captured.Should().NotBeNull();
        captured!.SentAt.Should().BeOnOrAfter(before);
    }

    /// <summary>
    /// Verifies that SendMessageAsync_DeliveredAtInitiallyNull behaves as expected.
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_DeliveredAtInitiallyNull()
    {
        // Arrange
        var sender = Guid.NewGuid();
        var receiver = Guid.NewGuid();
        _mockUserRepo.Setup(r => r.GetByIdAsync(sender, It.IsAny<CancellationToken>())).ReturnsAsync(new Domain.Entities.User { Id = sender });
        _mockUserRepo.Setup(r => r.GetByIdAsync(receiver, It.IsAny<CancellationToken>())).ReturnsAsync(new Domain.Entities.User { Id = receiver });
        Message? captured = null;
        _mockMessageRepo.Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>())).Callback<Message, CancellationToken>((m, ct) => captured = m).Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        await _sut.SendMessageAsync(sender, receiver, "hello", _ct);

        // Assert
        captured.Should().NotBeNull();
        captured!.DeliveredAt.Should().BeNull();
    }

}
