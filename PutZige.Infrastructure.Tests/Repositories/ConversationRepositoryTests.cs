// PutZige.Infrastructure.Tests/Repositories/ConversationRepositoryTests.cs
#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using PutZige.Domain.Entities;
using PutZige.Infrastructure.Repositories;
using Xunit;

namespace PutZige.Infrastructure.Tests.Repositories;

public class ConversationRepositoryTests : DatabaseTestBase
{
    private readonly ConversationRepository _sut;

    public ConversationRepositoryTests() : base()
    {
        _sut = new ConversationRepository(Context);
    }

    [Fact]
    public async Task GetOrCreateDirectConversationAsync_CreatesNewWhenNoneExists()
    {
        // Arrange
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        // Act
        var conv = await _sut.GetOrCreateDirectConversationAsync(a, b, default);

        // Assert
        conv.Should().NotBeNull();
        conv.IsGroup.Should().BeFalse();
        conv.Participants.Should().HaveCount(2);
        conv.Participants.Select(p => p.UserId).Should().Contain(new[] { a, b });

        // Also ensure persisted in DB
        var persisted = Context.Conversations.Find(conv.Id);
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task GetOrCreateDirectConversationAsync_ReturnsExisting()
    {
        // Arrange
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var existing = new Conversation { Id = Guid.NewGuid(), IsGroup = false, LastActivity = DateTime.UtcNow };
        existing.Participants.Add(new ConversationParticipant { UserId = a });
        existing.Participants.Add(new ConversationParticipant { UserId = b });

        await Context.Conversations.AddAsync(existing);
        await Context.SaveChangesAsync();

        // Act
        var conv = await _sut.GetOrCreateDirectConversationAsync(a, b, default);

        // Assert
        conv.Should().NotBeNull();
        conv.Id.Should().Be(existing.Id);
    }
}
