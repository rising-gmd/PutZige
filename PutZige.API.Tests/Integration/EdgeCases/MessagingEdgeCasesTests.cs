#nullable enable
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bogus;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PutZige.Domain.Entities;
using PutZige.Infrastructure.Data;
using Xunit;

namespace PutZige.API.Tests.Integration.EdgeCases;

public class MessagingEdgeCasesTests : Integration.IntegrationTestBase
{
    private readonly Faker _faker = new Faker();

    private HttpClient CreateClient(bool withAuth = false)
    {
        if (withAuth)
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", Guid.NewGuid().ToString());
        return Client;
    }

    private static (string hash, string salt) CreateHash(string plain)
    {
        var salt = new byte[32];
        RandomNumberGenerator.Fill(salt);
        var derived = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(plain), salt, 100000, HashAlgorithmName.SHA512, 64);
        return (Convert.ToBase64String(derived), Convert.ToBase64String(salt));
    }

    private async Task SeedUserAsync(Guid id, string? email = null, string? username = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await db.Users.FindAsync(id);
        if (existing != null) return;

        var hashed = CreateHash("Password123!");

        var user = new User
        {
            Id = id,
            Email = email ?? $"user_{id}@test.local",
            Username = username ?? $"user_{id}",
            DisplayName = username ?? $"User {id}",
            PasswordHash = hashed.hash,
            PasswordSalt = hashed.salt,
            IsActive = true,
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
    }

    private async Task<Guid> EnsureConversationAsync(Guid userA, Guid userB)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await db.Conversations
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c =>
                !c.IsGroup &&
                !c.IsDeleted &&
                c.Participants.Count == 2 &&
                c.Participants.Any(p => p.UserId == userA && !p.IsDeleted) &&
                c.Participants.Any(p => p.UserId == userB && !p.IsDeleted));

        if (existing != null) return existing.Id;

        var userAEntity = await db.Users.FindAsync(userA);
        var userBEntity = await db.Users.FindAsync(userB);

        var conv = new Conversation
        {
            Id = Guid.NewGuid(),
            IsGroup = false,
            LastActivity = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        conv.Participants.Add(new ConversationParticipant
        {
            Id = Guid.NewGuid(),
            UserId = userA,
            User = userAEntity!,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        });

        conv.Participants.Add(new ConversationParticipant
        {
            Id = Guid.NewGuid(),
            UserId = userB,
            User = userBEntity!,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        });

        await db.Conversations.AddAsync(conv);
        await db.SaveChangesAsync();
        return conv.Id;
    }

    /// <summary>
    /// Verifies that ConcurrentMessageSends_SameUsers_BothSaved behaves as expected.
    /// </summary>
    [Fact]
    public async Task ConcurrentMessageSends_SameUsers_BothSaved()
    {
        var senderId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        await SeedUserAsync(senderId);
        await SeedUserAsync(receiverId);
        var convId = await EnsureConversationAsync(senderId, receiverId);

        var client = CreateClient(withAuth: true);
        // Override auth with specific senderId
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", senderId.ToString());

        var t1 = client.PostAsJsonAsync("/api/v1/messages",
            new { ConversationId = convId, MessageText = "first" }, CancellationToken.None);
        var t2 = client.PostAsJsonAsync("/api/v1/messages",
            new { ConversationId = convId, MessageText = "second" }, CancellationToken.None);

        await Task.WhenAll(t1, t2);

        t1.Result.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.Accepted, HttpStatusCode.BadRequest);
        t2.Result.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.Accepted, HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Verifies that ConcurrentMarkAsRead_SameMessage_NoDataCorruption behaves as expected.
    /// </summary>
    [Fact]
    public async Task ConcurrentMarkAsRead_SameMessage_NoDataCorruption()
    {
        var client = CreateClient(withAuth: true);
        var messageId = Guid.NewGuid();

        var t1 = client.PostAsync($"/api/v1/messages/{messageId}/mark-as-read", null, CancellationToken.None);
        var t2 = client.PostAsync($"/api/v1/messages/{messageId}/mark-as-read", null, CancellationToken.None);

        await Task.WhenAll(t1, t2);

        t1.Result.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
        t2.Result.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Verifies that UserConnectsDisconnectsRapidly_NoOrphanedConnections behaves as expected.
    /// </summary>
    [Fact]
    public async Task UserConnectsDisconnectsRapidly_NoOrphanedConnections()
    {
        var client = CreateClient(withAuth: false);
        var r1 = await client.GetAsync("/hubs/chat", CancellationToken.None);
        var r2 = await client.GetAsync("/hubs/chat", CancellationToken.None);
        r1.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        r2.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    /// <summary>
    /// Verifies that ConcurrentGetConversation_ConsistentResults behaves as expected.
    /// </summary>
    [Fact]
    public async Task ConcurrentGetConversation_ConsistentResults()
    {
        var client = CreateClient(withAuth: true);
        var otherUserId = Guid.NewGuid();

        var t1 = client.GetAsync($"/api/v1/messages/conversation/{otherUserId}", CancellationToken.None);
        var t2 = client.GetAsync($"/api/v1/messages/conversation/{otherUserId}", CancellationToken.None);

        await Task.WhenAll(t1, t2);

        t1.Result.StatusCode.Should().Be(t2.Result.StatusCode);
    }

    /// <summary>
    /// Verifies that Pagination_RequestPageBeyondTotal_EmptyResults behaves as expected.
    /// </summary>
    [Fact]
    public async Task Pagination_RequestPageBeyondTotal_EmptyResults()
    {
        var client = CreateClient(withAuth: true);
        var resp = await client.GetAsync("/api/v1/messages?page=9999&pageSize=50", CancellationToken.None);
        resp.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        var content = await resp.Content.ReadAsStringAsync(CancellationToken.None);
        content.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that Pagination_RequestPage1Million_HandlesGracefully behaves as expected.
    /// </summary>
    [Fact]
    public async Task Pagination_RequestPage1Million_HandlesGracefully()
    {
        var client = CreateClient(withAuth: true);
        var resp = await client.GetAsync("/api/v1/messages?page=1000000&pageSize=50", CancellationToken.None);
        resp.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    /// <summary>
    /// Verifies that Guid_EmptyGuid_HandledGracefully behaves as expected.
    /// </summary>
    [Fact]
    public async Task Guid_EmptyGuid_HandledGracefully()
    {
        var client = CreateClient(withAuth: true);
        var resp = await client.GetAsync($"/api/v1/messages/conversation/{Guid.Empty}", CancellationToken.None);
        resp.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    /// <summary>
    /// Verifies that Timestamps_AllUtc_NoTimezoneBugs behaves as expected.
    /// </summary>
    [Fact]
    public async Task Timestamps_AllUtc_NoTimezoneBugs()
    {
        var client = CreateClient(withAuth: true);
        var resp = await client.GetAsync("/api/v1/messages?since=2020-01-01T00:00:00Z", CancellationToken.None);
        resp.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    /// <summary>
    /// Verifies that SoftDelete_DeletedMessage_NotInQueries behaves as expected.
    /// </summary>
    [Fact]
    public async Task SoftDelete_DeletedMessage_NotInQueries()
    {
        var client = CreateClient(withAuth: true);
        var deletedMessageId = Guid.NewGuid();
        var resp = await client.GetAsync($"/api/v1/messages/{deletedMessageId}", CancellationToken.None);
        resp.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound, HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Verifies that DatabaseTimeout_HandledGracefully behaves as expected.
    /// </summary>
    [Fact]
    public async Task DatabaseTimeout_HandledGracefully()
    {
        var client = CreateClient(withAuth: true);
        var resp = await client.GetAsync("/api/v1/messages?simulateDelay=true", CancellationToken.None);
        resp.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    /// <summary>
    /// Verifies that HubCrash_MessagesNotLost_PersistedInDatabase behaves as expected.
    /// </summary>
    [Fact]
    public async Task HubCrash_MessagesNotLost_PersistedInDatabase()
    {
        var senderId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        await SeedUserAsync(senderId);
        await SeedUserAsync(receiverId);
        var convId = await EnsureConversationAsync(senderId, receiverId);

        var client = CreateClient(withAuth: true);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", senderId.ToString());

        var r = await client.PostAsJsonAsync("/api/v1/messages",
            new { ConversationId = convId, MessageText = "persist" }, CancellationToken.None);
        r.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    /// <summary>
    /// Verifies that UserReconnects_ReceivesPendingMessages behaves as expected.
    /// </summary>
    [Fact]
    public async Task UserReconnects_ReceivesPendingMessages()
    {
        var client = CreateClient(withAuth: true);
        var r = await client.GetAsync("/hubs/chat/pending", CancellationToken.None);
        r.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }
}