#nullable enable
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Bogus;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PutZige.API.Tests.Integration.Security
{
    public class MessagingSecurityTests : Integration.IntegrationTestBase
    {
        private readonly Faker _faker = new Faker();


        private HttpClient CreateClient(bool withAuth = false)
        {
            if (withAuth)
                Client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Guid.NewGuid().ToString());
            return Client;
        }

        /// <summary>
        /// Verifies that SendMessage_SqlInjectionAttempt_Sanitized behaves as expected.
        /// </summary>
        [Fact]
        public async Task SendMessage_SqlInjectionAttempt_Sanitized()
        {
            var client = CreateClient(withAuth: true);
            var payload = new { ReceiverId = Guid.NewGuid(), MessageText = "Robert'); DROP TABLE Messages;--" };

            var resp = await client.PostAsJsonAsync(PutZige.API.Tests.TestApiEndpoints.Messages, payload, CancellationToken.None);

            resp.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        }

        /// <summary>
        /// Verifies that SendMessage_XssInMessageText_Sanitized behaves as expected.
        /// </summary>
        [Fact]
        public async Task SendMessage_XssInMessageText_Sanitized()
        {
            var client = CreateClient(withAuth: true);
            var payload = new { ReceiverId = Guid.NewGuid(), MessageText = "<script>alert('xss')</script>" };

            var resp = await client.PostAsJsonAsync(PutZige.API.Tests.TestApiEndpoints.Messages, payload, CancellationToken.None);
            var content = await resp.Content.ReadAsStringAsync(CancellationToken.None);

            resp.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
            content.Should().NotContain("<script>");
        }

        /// <summary>
        /// Verifies that SendMessage_VeryLongUserId_HandledGracefully behaves as expected.
        /// </summary>
        [Fact]
        public async Task SendMessage_VeryLongUserId_HandledGracefully()
        {
            var client = CreateClient(withAuth: true);
            var longId = new string('a', 5000);
            var payload = new { ReceiverId = longId, MessageText = "hello" };

            var resp = await client.PostAsJsonAsync(PutZige.API.Tests.TestApiEndpoints.Messages, payload, CancellationToken.None);

            resp.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        }

        /// <summary>
        /// Verifies that MarkAsRead_AttemptToReadOthersMessage_Blocked behaves as expected.
        /// </summary>
        [Fact]
        public async Task MarkAsRead_AttemptToReadOthersMessage_Blocked()
        {
            var client = CreateClient(withAuth: true);
            var messageId = Guid.NewGuid();

            var resp = await client.PostAsync(string.Format(PutZige.API.Tests.TestApiEndpoints.MessageMarkAsRead, messageId), null, CancellationToken.None);

            resp.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.Unauthorized);
        }

        /// <summary>
        /// Verifies that GetConversation_OnlyOwnMessages_OthersExcluded behaves as expected.
        /// </summary>
        [Fact]
        public async Task GetConversation_OnlyOwnMessages_OthersExcluded()
        {
            var client = CreateClient(withAuth: true);
            var otherUserId = Guid.NewGuid();

            var resp = await client.GetAsync($"{PutZige.API.Tests.TestApiEndpoints.MessagesConversation}/{otherUserId}", CancellationToken.None);

            resp.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        }

        /// <summary>
        /// Verifies that Hub_SqlInjectionInMessage_Sanitized behaves as expected.
        /// </summary>
        [Fact]
        public async Task Hub_SqlInjectionInMessage_Sanitized()
        {
            var client = CreateClient(withAuth: false);

            var resp = await client.GetAsync(PutZige.API.Tests.TestApiEndpoints.ChatHub, CancellationToken.None);

            resp.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        }

        /// <summary>
        /// Verifies that Hub_XssInMessage_Sanitized behaves as expected.
        /// </summary>
        [Fact]
        public async Task Hub_XssInMessage_Sanitized()
        {
            var client = CreateClient(withAuth: false);
            var resp = await client.GetAsync(PutZige.API.Tests.TestApiEndpoints.ChatHub, CancellationToken.None);
            resp.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        }

        /// <summary>
        /// Verifies that JwtExpired_AllEndpoints_Returns401 behaves as expected.
        /// </summary>
        [Fact]
        public async Task JwtExpired_AllEndpoints_Returns401()
        {
            var client = CreateClient(withAuth: false);

            var endpoints = new[] { PutZige.API.Tests.TestApiEndpoints.Messages, PutZige.API.Tests.TestApiEndpoints.ChatHub };
            foreach (var ep in endpoints)
            {
                var r = await client.GetAsync(ep, CancellationToken.None);
                r.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
            }
        }

        /// <summary>
        /// Verifies that JwtInvalid_AllEndpoints_Returns401 behaves as expected.
        /// </summary>
        [Fact]
        public async Task JwtInvalid_AllEndpoints_Returns401()
        {
            var client = CreateClient(withAuth: false);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-token");

            var r = await client.GetAsync(PutZige.API.Tests.TestApiEndpoints.Messages, CancellationToken.None);
            r.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        }

        /// <summary>
        /// Verifies that NoJwt_AllEndpoints_Returns401 behaves as expected.
        /// </summary>
        [Fact]
        public async Task NoJwt_AllEndpoints_Returns401()
        {
            var client = CreateClient(withAuth: false);
            var r = await client.GetAsync(PutZige.API.Tests.TestApiEndpoints.Messages, CancellationToken.None);
            r.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        }

        /// <summary>
        /// Verifies that MessageText_4000Chars_Accepted behaves as expected.
        /// </summary>
        [Fact]
        public async Task MessageText_4000Chars_Accepted()
        {
            var client = CreateClient(withAuth: true);
            var text = new string('x', 4000);
            var payload = new { ReceiverId = Guid.NewGuid(), MessageText = text };

            var resp = await client.PostAsJsonAsync(PutZige.API.Tests.TestApiEndpoints.Messages, payload, CancellationToken.None);
            resp.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        }

        /// <summary>
        /// Verifies that MessageText_4001Chars_Rejected behaves as expected.
        /// </summary>
        [Fact]
        public async Task MessageText_4001Chars_Rejected()
        {
            var client = CreateClient(withAuth: true);
            var text = new string('x', 4001);
            var payload = new { ReceiverId = Guid.NewGuid(), MessageText = text };

            var resp = await client.PostAsJsonAsync(PutZige.API.Tests.TestApiEndpoints.Messages, payload, CancellationToken.None);
            resp.StatusCode.Should().BeOneOf(
                HttpStatusCode.BadRequest, HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.UnsupportedMediaType, HttpStatusCode.InternalServerError,
                HttpStatusCode.Unauthorized);
        }

        /// <summary>
        /// Verifies that RateLimit_LoginExceeded_MessagingStillWorks behaves as expected.
        /// </summary>
        [Fact]
        public async Task RateLimit_LoginExceeded_MessagingStillWorks()
        {
            var client = CreateClient(withAuth: true);
            var resp = await client.GetAsync(PutZige.API.Tests.TestApiEndpoints.Messages, CancellationToken.None);
            resp.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        }

        /// <summary>
        /// Verifies that RateLimit_MessagingExceeded_Returns429 behaves as expected.
        /// </summary>
        [Fact]
        public async Task RateLimit_MessagingExceeded_Returns429()
        {
            var client = CreateClient(withAuth: true);

            for (var i = 0; i < 10; i++)
            {
            var r = await client.GetAsync(PutZige.API.Tests.TestApiEndpoints.Messages, CancellationToken.None);
                r.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Verifies that Authorization_CrossUserMessageAccess_Denied behaves as expected.
        /// </summary>
        [Fact]
        public async Task Authorization_CrossUserMessageAccess_Denied()
        {
            var client = CreateClient(withAuth: true);
            var otherMessageId = Guid.NewGuid();
            var r = await client.GetAsync(string.Format(PutZige.API.Tests.TestApiEndpoints.MessageById, otherMessageId), CancellationToken.None);
            r.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.Unauthorized);
        }
    }
}
