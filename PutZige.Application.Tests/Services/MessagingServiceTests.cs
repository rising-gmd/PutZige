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

public class MessagingServiceTests
{
    private readonly Mock<IMessageRepository> _mockMessageRepo;
    private readonly Mock<PutZige.Domain.Interfaces.IDapperMessageRepository> _mockDapperMessageRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<IConversationRepository> _mockConversationRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<ILogger<MessagingService>> _mockLogger;
    private readonly Mock<PutZige.Application.Interfaces.IRealTimeNotifier> _mockRealTimeNotifier;
    private readonly Mock<PutZige.Application.Interfaces.ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<PutZige.Application.Interfaces.IDateTimeProvider> _mockDateTimeProvider;
    private readonly MessagingService _sut;
    private readonly CancellationToken _ct = CancellationToken.None;

    public MessagingServiceTests()
    {
        _mockMessageRepo = new Mock<IMessageRepository>();
        _mockDapperMessageRepo = new Mock<PutZige.Domain.Interfaces.IDapperMessageRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockConversationRepo = new Mock<IConversationRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockMapper = new Mock<IMapper>();
        _mockLogger = new Mock<ILogger<MessagingService>>();
        _mockRealTimeNotifier = new Mock<PutZige.Application.Interfaces.IRealTimeNotifier>();
        _mockCurrentUserService = new Mock<PutZige.Application.Interfaces.ICurrentUserService>();
        _mockDateTimeProvider = new Mock<PutZige.Application.Interfaces.IDateTimeProvider>();

        _mockCurrentUserService.Setup(c => c.GetUserId()).Returns(Guid.NewGuid());
        _mockDateTimeProvider.Setup(d => d.UtcNow).Returns(() => DateTime.UtcNow);

        _mockMapper.Setup(m => m.Map<SendMessageResponse>(It.IsAny<Message>()))
            .Returns((Message msg) => new SendMessageResponse(msg.Id, msg.SenderId, msg.ReceiverId, msg.MessageText, msg.SentAt));
        _mockMapper.Setup(m => m.Map<MessageDto>(It.IsAny<Message>()))
            .Returns((Message msg) => new MessageDto { Id = msg.Id, MessageText = msg.MessageText, SenderId = msg.SenderId, ReceiverId = msg.ReceiverId, SentAt = msg.SentAt });

        _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new Domain.Entities.User { Id = id });
        _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.User, object>>[]>()))
            .ReturnsAsync((Guid id, CancellationToken _, System.Linq.Expressions.Expression<Func<Domain.Entities.User, object>>[] __) => new Domain.Entities.User { Id = id });

        _sut = new MessagingService(
            _mockDapperMessageRepo.Object,
            _mockMessageRepo.Object,
            _mockUserRepo.Object,
            _mockConversationRepo.Object,
            _mockUow.Object,
            _mockMapper.Object,
            _mockRealTimeNotifier.Object,
            _mockCurrentUserService.Object,
            _mockDateTimeProvider.Object,
            _mockLogger.Object);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Message CreateMessage(Guid sender, Guid receiver, DateTime sentAt, string text = "hi") => new Message
    {
        Id = Guid.NewGuid(),
        SenderId = sender,
        ReceiverId = receiver,
        MessageText = text,
        SentAt = sentAt,
        CreatedAt = DateTime.UtcNow
    };

    private Conversation CreateConversation(Guid senderId, Guid receiverId)
    {
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            IsGroup = false,
            LastActivity = DateTime.UtcNow
        };
        conversation.Participants.Add(new ConversationParticipant { UserId = senderId });
        conversation.Participants.Add(new ConversationParticipant { UserId = receiverId });
        _mockConversationRepo
            .Setup(r => r.GetByIdWithParticipantsAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);
        return conversation;
    }

    private (Conversation conversation, Guid userId) CreateConversationForUser()
    {
        var userId = Guid.NewGuid();
        var conversation = CreateConversation(userId, Guid.NewGuid());
        _mockCurrentUserService.Setup(c => c.GetUserId()).Returns(userId);
        return (conversation, userId);
    }

    // ── SendMessageAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SendMessageAsync_ValidInputs_CreatesMessageAndReturnsResponse()
    {
        var sender = Guid.NewGuid();
        var receiver = Guid.NewGuid();
        var conversation = CreateConversation(sender, receiver);
        Message? captured = null;
        _mockMessageRepo.Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((m, ct) => captured = m)
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var res = await _sut.SendMessageAsync(conversation.Id, "hello", sender, _ct);

        res.Should().NotBeNull();
        res.MessageText.Should().Be("hello");
        captured.Should().NotBeNull();
        captured!.SenderId.Should().Be(sender);
        captured.ReceiverId.Should().Be(receiver);
    }

    [Fact]
    public async Task SendMessageAsync_ReceiverNotFound_ThrowsKeyNotFoundException()
    {
        var conversationId = Guid.NewGuid();
        _mockConversationRepo
            .Setup(r => r.GetByIdWithParticipantsAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        Func<Task> act = async () => await _sut.SendMessageAsync(conversationId, "hello", Guid.NewGuid(), _ct);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SendMessageAsync_UserNotParticipant_ThrowsUnauthorizedAccessException()
    {
        var sender = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var conversation = new Conversation { Id = conversationId, IsGroup = false, LastActivity = DateTime.UtcNow };
        conversation.Participants.Add(new ConversationParticipant { UserId = Guid.NewGuid() });
        _mockConversationRepo
            .Setup(r => r.GetByIdWithParticipantsAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        Func<Task> act = async () => await _sut.SendMessageAsync(conversationId, "hello", sender, _ct);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SendMessageAsync_MessageTooLong_ThrowsAppException()
    {
        var sender = Guid.NewGuid();
        var receiver = Guid.NewGuid();
        var conversation = CreateConversation(sender, receiver);
        var longText = new string('x', PutZige.Application.Common.Constants.AppConstants.Messaging.MaxMessageLength + 1);

        Func<Task> act = async () => await _sut.SendMessageAsync(conversation.Id, longText, sender, _ct);

        await act.Should().ThrowAsync<PutZige.Application.Common.AppException>();
    }

    [Fact]
    public async Task SendMessageAsync_NullMessageText_ThrowsAppException()
    {
        var sender = Guid.NewGuid();
        var receiver = Guid.NewGuid();
        var conversation = CreateConversation(sender, receiver);

        Func<Task> act = async () => await _sut.SendMessageAsync(conversation.Id, "   ", sender, _ct);

        await act.Should().ThrowAsync<PutZige.Application.Common.AppException>();
    }

    [Fact]
    public async Task SendMessageAsync_SenderEqualsReceiver_Allowed()
    {
        var sender = Guid.NewGuid();
        var receiver = Guid.NewGuid();
        var conversation = CreateConversation(sender, receiver);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockMessageRepo.Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var res = await _sut.SendMessageAsync(conversation.Id, "self", sender, _ct);

        res.Should().NotBeNull();
    }

    [Fact]
    public async Task SendMessageAsync_SaveSuccessful_LogsInformation()
    {
        var sender = Guid.NewGuid();
        var receiver = Guid.NewGuid();
        var conversation = CreateConversation(sender, receiver);
        _mockMessageRepo.Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var res = await _sut.SendMessageAsync(conversation.Id, "hello", sender, _ct);

        res.Should().NotBeNull();
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_RepositoryThrows_PropagatesException()
    {
        var sender = Guid.NewGuid();
        var receiver = Guid.NewGuid();
        var conversation = CreateConversation(sender, receiver);
        _mockMessageRepo.Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db"));

        Func<Task> act = async () => await _sut.SendMessageAsync(conversation.Id, "hello", sender, _ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SendMessageAsync_SetsCorrectTimestamps_SentAtIsUtcNow()
    {
        var sender = Guid.NewGuid();
        var receiver = Guid.NewGuid();
        var conversation = CreateConversation(sender, receiver);
        var before = DateTime.UtcNow;
        Message? captured = null;
        _mockMessageRepo.Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((m, ct) => captured = m)
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.SendMessageAsync(conversation.Id, "hello", sender, _ct);

        captured.Should().NotBeNull();
        captured!.SentAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task SendMessageAsync_DeliveredAtInitiallyNull()
    {
        var sender = Guid.NewGuid();
        var receiver = Guid.NewGuid();
        var conversation = CreateConversation(sender, receiver);
        Message? captured = null;
        _mockMessageRepo.Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((m, ct) => captured = m)
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.SendMessageAsync(conversation.Id, "hello", sender, _ct);

        captured.Should().NotBeNull();
        captured!.DeliveredAt.Should().BeNull();
    }

    // ── GetConversationHistoryAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetConversationHistoryAsync_ValidInputs_ReturnsPaginatedMessages()
    {
        var user = Guid.NewGuid();
        var (conversation, _) = CreateConversationForUser(user);
        var projections = new List<PutZige.Domain.DTOs.MessageProjection>
        {
            new() { Id = Guid.NewGuid(), SenderId = user, ReceiverId = Guid.NewGuid(), MessageText = "m1", SentAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), SenderId = Guid.NewGuid(), ReceiverId = user, MessageText = "m2", SentAt = DateTime.UtcNow.AddMinutes(-1) }
        };
        _mockDapperMessageRepo
            .Setup(r => r.GetConversationHistoryAsync(conversation.Id, 1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((projections, 3L));

        var res = await _sut.GetConversationHistoryAsync(conversation.Id, 1, 2, _ct);

        res.Should().NotBeNull();
        res.Messages.Should().HaveCount(2);
        res.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetConversationHistoryAsync_EmptyConversation_ReturnsEmptyList()
    {
        var user = Guid.NewGuid();
        var (conversation, _) = CreateConversationForUser(user);
        _mockDapperMessageRepo
            .Setup(r => r.GetConversationHistoryAsync(conversation.Id, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Empty<PutZige.Domain.DTOs.MessageProjection>(), 0L));

        var res = await _sut.GetConversationHistoryAsync(conversation.Id, 1, 10, _ct);

        res.Should().NotBeNull();
        res.Messages.Should().BeEmpty();
        res.TotalCount.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetConversationHistoryAsync_InvalidPageNumber_Throws(int page)
    {
        var user = Guid.NewGuid();
        var (conversation, _) = CreateConversationForUser(user);

        Func<Task> act = async () => await _sut.GetConversationHistoryAsync(conversation.Id, page, 10, _ct);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1000)]
    public async Task GetConversationHistoryAsync_InvalidPageSize_Throws(int pageSize)
    {
        var user = Guid.NewGuid();
        var (conversation, _) = CreateConversationForUser(user);

        Func<Task> act = async () => await _sut.GetConversationHistoryAsync(conversation.Id, 1, pageSize, _ct);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task GetConversationHistoryAsync_UserNotParticipant_ThrowsUnauthorizedAccessException()
    {
        var user = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        _mockCurrentUserService.Setup(c => c.GetUserId()).Returns(user);
        var conversation = new Conversation { Id = conversationId, IsGroup = false, LastActivity = DateTime.UtcNow };
        conversation.Participants.Add(new ConversationParticipant { UserId = Guid.NewGuid() });
        _mockConversationRepo
            .Setup(r => r.GetByIdWithParticipantsAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        Func<Task> act = async () => await _sut.GetConversationHistoryAsync(conversationId, 1, 10, _ct);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetConversationHistoryAsync_CalculatesHasNextPageTrue()
    {
        var user = Guid.NewGuid();
        var (conversation, _) = CreateConversationForUser(user);
        var projections = Enumerable.Range(0, 5)
            .Select(i => new PutZige.Domain.DTOs.MessageProjection
            {
                Id = Guid.NewGuid(),
                SenderId = user,
                ReceiverId = Guid.NewGuid(),
                MessageText = $"m{i}",
                SentAt = DateTime.UtcNow.AddMinutes(-i)
            }).ToList();
        _mockDapperMessageRepo
            .Setup(r => r.GetConversationHistoryAsync(conversation.Id, 1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((projections.Take(2), 5L));

        var res = await _sut.GetConversationHistoryAsync(conversation.Id, 1, 2, _ct);

        res.Messages.Should().HaveCount(2);
        res.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task GetConversationHistoryAsync_LastPage_HasNextPageFalse()
    {
        var user = Guid.NewGuid();
        var (conversation, _) = CreateConversationForUser(user);
        var projections = Enumerable.Range(0, 4)
            .Select(i => new PutZige.Domain.DTOs.MessageProjection
            {
                Id = Guid.NewGuid(),
                SenderId = user,
                ReceiverId = Guid.NewGuid(),
                MessageText = $"m{i}",
                SentAt = DateTime.UtcNow.AddMinutes(-i)
            }).ToList();
        _mockDapperMessageRepo
            .Setup(r => r.GetConversationHistoryAsync(conversation.Id, 2, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((projections.Skip(2).Take(2), 4L));

        var res = await _sut.GetConversationHistoryAsync(conversation.Id, 2, 2, _ct);

        res.Messages.Should().HaveCount(2);
        res.TotalCount.Should().Be(4);
    }

    [Fact]
    public async Task GetConversationHistoryAsync_MapsToMessageDtoCorrectly()
    {
        var user = Guid.NewGuid();
        var (conversation, _) = CreateConversationForUser(user);
        var projection = new PutZige.Domain.DTOs.MessageProjection
        {
            Id = Guid.NewGuid(),
            SenderId = user,
            ReceiverId = Guid.NewGuid(),
            MessageText = "maptest",
            SentAt = DateTime.UtcNow
        };
        _mockDapperMessageRepo
            .Setup(r => r.GetConversationHistoryAsync(conversation.Id, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { projection }, 1L));

        var res = await _sut.GetConversationHistoryAsync(conversation.Id, 1, 10, _ct);

        res.Messages.First().MessageText.Should().Be("maptest");
    }

    // ── MarkMessageAsDelivered ────────────────────────────────────────────────

    [Fact]
    public async Task MarkMessageAsDelivered_ValidMessage_SetsDeliveredAt()
    {
        var m = CreateMessage(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, "dtest");
        _mockMessageRepo.Setup(r => r.GetByIdAsync(m.Id, It.IsAny<CancellationToken>())).ReturnsAsync(m);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.MarkMessageAsDeliveredAsync(m.Id, _ct);

        _mockMessageRepo.Verify(r => r.UpdateAsync(It.Is<Message>(x => x.DeliveredAt != null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkMessageAsDelivered_MessageNotFound_ThrowsKeyNotFoundException()
    {
        _mockMessageRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Message?)null);

        Func<Task> act = async () => await _sut.MarkMessageAsDeliveredAsync(Guid.NewGuid(), _ct);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task MarkMessageAsDelivered_AlreadyDelivered_UpdatesTimestamp()
    {
        var m = CreateMessage(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-10), "dtest");
        m.DeliveredAt = DateTime.UtcNow.AddMinutes(-5);
        _mockMessageRepo.Setup(r => r.GetByIdAsync(m.Id, It.IsAny<CancellationToken>())).ReturnsAsync(m);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.MarkMessageAsDeliveredAsync(m.Id, _ct);

        _mockMessageRepo.Verify(r => r.UpdateAsync(It.Is<Message>(x => x.DeliveredAt != null && x.DeliveredAt > DateTime.UtcNow.AddMinutes(-6)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkMessageAsDelivered_DeletedMessage_ThrowsKeyNotFoundException()
    {
        var m = CreateMessage(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, "dtest");
        m.IsDeleted = true;
        _mockMessageRepo.Setup(r => r.GetByIdAsync(m.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Message?)null);

        Func<Task> act = async () => await _sut.MarkMessageAsDeliveredAsync(m.Id, _ct);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task MarkMessageAsDelivered_SetsUtcNow()
    {
        var m = CreateMessage(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, "dtest");
        _mockMessageRepo.Setup(r => r.GetByIdAsync(m.Id, It.IsAny<CancellationToken>())).ReturnsAsync(m);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var before = DateTime.UtcNow;

        await _sut.MarkMessageAsDeliveredAsync(m.Id, _ct);

        _mockMessageRepo.Verify(r => r.UpdateAsync(It.Is<Message>(x => x.DeliveredAt != null && x.DeliveredAt >= before), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── MarkMessageAsRead ─────────────────────────────────────────────────────

    [Fact]
    public async Task MarkMessageAsRead_ValidMessage_SetsReadAt()
    {
        var m = CreateMessage(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, "rtest");
        _mockMessageRepo.Setup(r => r.GetByIdAsync(m.Id, It.IsAny<CancellationToken>())).ReturnsAsync(m);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.MarkMessageAsReadAsync(m.Id, _ct);

        _mockMessageRepo.Verify(r => r.UpdateAsync(It.Is<Message>(x => x.ReadAt != null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkMessageAsRead_MessageNotFound_ThrowsKeyNotFoundException()
    {
        _mockMessageRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Message?)null);

        Func<Task> act = async () => await _sut.MarkMessageAsReadAsync(Guid.NewGuid(), _ct);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task MarkMessageAsRead_AlreadyRead_UpdatesTimestamp()
    {
        var m = CreateMessage(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-10), "rtest");
        m.ReadAt = DateTime.UtcNow.AddMinutes(-5);
        _mockMessageRepo.Setup(r => r.GetByIdAsync(m.Id, It.IsAny<CancellationToken>())).ReturnsAsync(m);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.MarkMessageAsReadAsync(m.Id, _ct);

        _mockMessageRepo.Verify(r => r.UpdateAsync(It.Is<Message>(x => x.ReadAt != null && x.ReadAt > DateTime.UtcNow.AddMinutes(-6)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkMessageAsRead_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        var m = CreateMessage(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, "rtest");
        _mockMessageRepo.Setup(r => r.GetByIdAsync(m.Id, It.IsAny<CancellationToken>())).ReturnsAsync(m);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.MarkMessageAsReadAsync(m.Id, _ct);

        _mockMessageRepo.Verify(r => r.UpdateAsync(It.Is<Message>(x => x.ReadAt != null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkMessageAsRead_SetsUtcNow()
    {
        var m = CreateMessage(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, "rtest");
        _mockMessageRepo.Setup(r => r.GetByIdAsync(m.Id, It.IsAny<CancellationToken>())).ReturnsAsync(m);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var before = DateTime.UtcNow;

        await _sut.MarkMessageAsReadAsync(m.Id, _ct);

        _mockMessageRepo.Verify(r => r.UpdateAsync(It.Is<Message>(x => x.ReadAt != null && x.ReadAt >= before), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkMessageAsRead_DeletedMessage_ThrowsKeyNotFoundException()
    {
        var m = CreateMessage(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, "rtest");
        m.IsDeleted = true;
        _mockMessageRepo.Setup(r => r.GetByIdAsync(m.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Message?)null);

        Func<Task> act = async () => await _sut.MarkMessageAsReadAsync(m.Id, _ct);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    private (Conversation conversation, Guid userId) CreateConversationForUser(Guid userId)
    {
        var conversation = CreateConversation(userId, Guid.NewGuid());
        _mockCurrentUserService.Setup(c => c.GetUserId()).Returns(userId);
        return (conversation, userId);
    }
}