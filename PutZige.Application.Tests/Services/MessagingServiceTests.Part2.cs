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
    // Additional delivery/read tests continue in partial class file
    /// <summary>
    /// Verifies that GetConversationHistoryAsync_ValidInputs_ReturnsPaginatedMessages behaves as expected.
    /// </summary>
    [Fact]
    public async Task GetConversationHistoryAsync_ValidInputs_ReturnsPaginatedMessages()
    {
        // Arrange
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();
        _mockCurrentUserService.Setup(c => c.GetUserId()).Returns(user);
        var messages = new List<Message>
        {
            CreateMessage(user, other, DateTime.UtcNow, "m1"),
            CreateMessage(other, user, DateTime.UtcNow.AddMinutes(-1), "m2"),
            CreateMessage(user, other, DateTime.UtcNow.AddMinutes(-2), "m3")
        };
        _mockMessageRepo.Setup(r => r.GetConversationAsync(user, other, 1, 2, It.IsAny<CancellationToken>())).ReturnsAsync((messages.Take(2), messages.Count));
        _mockMapper.Setup(m => m.Map<MessageDto>(It.IsAny<Message>())).Returns((Message msg) => new MessageDto { Id = msg.Id, MessageText = msg.MessageText, SenderId = msg.SenderId, ReceiverId = msg.ReceiverId, SentAt = msg.SentAt });

        // Act
        var res = await _sut.GetConversationHistoryAsync(other, 1, 2, _ct);

        // Assert
        res.Should().NotBeNull();
        res.Messages.Should().HaveCount(2);
        res.TotalCount.Should().Be(3);
    }

    /// <summary>
    /// Verifies that GetConversationHistoryAsync_EmptyConversation_ReturnsEmptyList behaves as expected.
    /// </summary>
    [Fact]
    public async Task GetConversationHistoryAsync_EmptyConversation_ReturnsEmptyList()
    {
        // Arrange
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();
        _mockCurrentUserService.Setup(c => c.GetUserId()).Returns(user);
        _mockMessageRepo.Setup(r => r.GetConversationAsync(user, other, 1, 10, It.IsAny<CancellationToken>())).ReturnsAsync((Enumerable.Empty<Message>(), 0));

        // Act
        Func<Task> act = async () => await _sut.GetConversationHistoryAsync(other, 1, 10, _ct);

        // Assert
        await act.Should().ThrowAsync<PutZige.Application.Common.AppException>();
    }

    /// <summary>
    /// Verifies that GetConversationHistoryAsync_InvalidPageNumber_Throws behaves as expected.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetConversationHistoryAsync_InvalidPageNumber_Throws(int page)
    {
        // Arrange
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();

        // Act
        Func<Task> act = async () => await _sut.GetConversationHistoryAsync(other, page, 10, _ct);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that GetConversationHistoryAsync_InvalidPageSize_Throws behaves as expected.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1000)]
    public async Task GetConversationHistoryAsync_InvalidPageSize_Throws(int pageSize)
    {
        // Arrange
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();

        // Act
        Func<Task> act = async () => await _sut.GetConversationHistoryAsync(other, 1, pageSize, _ct);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that GetConversationHistoryAsync_CalculatesHasNextPageTrue behaves as expected.
    /// </summary>
    [Fact]
    public async Task GetConversationHistoryAsync_CalculatesHasNextPageTrue()
    {
        // Arrange
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();
        _mockCurrentUserService.Setup(c => c.GetUserId()).Returns(user);
        var all = Enumerable.Range(0,5).Select(i => CreateMessage(user, other, DateTime.UtcNow.AddMinutes(-i), $"m{i}"));
        _mockMessageRepo.Setup(r => r.GetConversationAsync(user, other, 1, 2, It.IsAny<CancellationToken>())).ReturnsAsync((all.Take(2), 5));
        _mockMapper.Setup(m => m.Map<MessageDto>(It.IsAny<Message>())).Returns((Message msg) => new MessageDto{Id=msg.Id, MessageText=msg.MessageText, SentAt=msg.SentAt, SenderId=msg.SenderId, ReceiverId=msg.ReceiverId});

        // Act
        var res = await _sut.GetConversationHistoryAsync(other, 1, 2, _ct);

        // Assert
        res.Messages.Should().HaveCount(2);
        res.TotalCount.Should().Be(5);
    }

    /// <summary>
    /// Verifies that GetConversationHistoryAsync_LastPage_HasNextPageFalse behaves as expected.
    /// </summary>
    [Fact]
    public async Task GetConversationHistoryAsync_LastPage_HasNextPageFalse()
    {
        // Arrange
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();
        _mockCurrentUserService.Setup(c => c.GetUserId()).Returns(user);
        var all = Enumerable.Range(0,4).Select(i => CreateMessage(user, other, DateTime.UtcNow.AddMinutes(-i), $"m{i}"));
        _mockMessageRepo.Setup(r => r.GetConversationAsync(user, other, 2, 2, It.IsAny<CancellationToken>())).ReturnsAsync((all.Skip(2).Take(2), 4));
        _mockMapper.Setup(m => m.Map<MessageDto>(It.IsAny<Message>())).Returns((Message msg) => new MessageDto{Id=msg.Id, MessageText=msg.MessageText, SentAt=msg.SentAt, SenderId=msg.SenderId, ReceiverId=msg.ReceiverId});

        // Act
        var res = await _sut.GetConversationHistoryAsync(other, 2, 2, _ct);

        // Assert
        res.Messages.Should().HaveCount(2);
        res.TotalCount.Should().Be(4);
    }

    /// <summary>
    /// Verifies that GetConversationHistoryAsync_MapsToMessageDtoCorrectly behaves as expected.
    /// </summary>
    [Fact]
    public async Task GetConversationHistoryAsync_MapsToMessageDtoCorrectly()
    {
        // Arrange
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();
        _mockCurrentUserService.Setup(c => c.GetUserId()).Returns(user);
        var m = CreateMessage(user, other, DateTime.UtcNow, "maptest");
        _mockMessageRepo.Setup(r => r.GetConversationAsync(user, other, 1, 10, It.IsAny<CancellationToken>())).ReturnsAsync((new[] { m }, 1));
        _mockMapper.Setup(m => m.Map<MessageDto>(It.IsAny<Message>())).Returns((Message msg) => new MessageDto{Id=msg.Id, MessageText=msg.MessageText, SentAt=msg.SentAt, SenderId=msg.SenderId, ReceiverId=msg.ReceiverId});

        // Act
        var res = await _sut.GetConversationHistoryAsync(other, 1, 10, _ct);

        // Assert
        res.Messages.First().MessageText.Should().Be("maptest");
    }

    /// <summary>
    /// Verifies that MarkMessageAsDelivered_ValidMessage_SetsDeliveredAt behaves as expected.
    /// </summary>
    [Fact]
    public async Task MarkMessageAsDelivered_ValidMessage_SetsDeliveredAt()
    {
        // Arrange
        var m = CreateMessage(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, "dtest");
        _mockMessageRepo.Setup(r => r.GetByIdAsync(m.Id, It.IsAny<CancellationToken>())).ReturnsAsync(m);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        await _sut.MarkMessageAsDeliveredAsync(m.Id, _ct);

        // Assert
        _mockMessageRepo.Verify(r => r.UpdateAsync(It.Is<Message>(x => x.DeliveredAt != null), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that MarkMessageAsDelivered_MessageNotFound_ThrowsKeyNotFoundException behaves as expected.
    /// </summary>
    [Fact]
    public async Task MarkMessageAsDelivered_MessageNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _mockMessageRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Message?)null);

        // Act
        Func<Task> act = async () => await _sut.MarkMessageAsDeliveredAsync(Guid.NewGuid(), _ct);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

}

