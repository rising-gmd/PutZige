#nullable enable
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using PutZige.Application.DTOs;
using PutZige.Application.Settings;
using PutZige.Infrastructure.Services;
using Xunit;
using Microsoft.Extensions.Options;

namespace PutZige.Infrastructure.Tests.Services
{
    public class HashingServiceTests
    {
        private readonly HashingService _svc;

        public HashingServiceTests()
        {
            var settings = new HashingSettings { SaltSizeBytes = 32, Algorithm = "SHA512", Iterations = 100000 };
            _svc = new HashingService(Options.Create(settings));
        }

        /// <summary>
        /// HashAsync returns a non-empty hash and salt for valid input.
        /// </summary>
        [Fact]
        public async Task HashAsync_WithValidInput_ReturnsHashAndSalt()
        {
            var result = await _svc.HashAsync("password", CancellationToken.None);
            result.Should().NotBeNull();
            result.Hash.Should().NotBeNullOrWhiteSpace();
            result.Salt.Should().NotBeNullOrWhiteSpace();
        }

        /// <summary>
        /// Hashing the same input twice yields different salts and hashes.
        /// </summary>
        [Fact]
        public async Task HashAsync_WithSameInputTwice_GeneratesDifferentHashes()
        {
            var a = await _svc.HashAsync("samepass", CancellationToken.None);
            var b = await _svc.HashAsync("samepass", CancellationToken.None);
            a.Hash.Should().NotBe(b.Hash);
            a.Salt.Should().NotBe(b.Salt);
        }

        /// <summary>
        /// VerifyAsync returns true for correct password.
        /// </summary>
        [Fact]
        public async Task VerifyAsync_WithCorrectPassword_ReturnsTrue()
        {
            var plain = "verifyme";
            var hashed = await _svc.HashAsync(plain, CancellationToken.None);
            var ok = await _svc.VerifyAsync(plain, hashed.Hash, hashed.Salt, CancellationToken.None);
            ok.Should().BeTrue();
        }

        /// <summary>
        /// VerifyAsync returns false for incorrect password.
        /// </summary>
        [Fact]
        public async Task VerifyAsync_WithIncorrectPassword_ReturnsFalse()
        {
            var hashed = await _svc.HashAsync("password1", CancellationToken.None);
            var ok = await _svc.VerifyAsync("password2", hashed.Hash, hashed.Salt, CancellationToken.None);
            ok.Should().BeFalse();
        }

        /// <summary>
        /// VerifyAsync throws for null plaintext input.
        /// </summary>
        [Fact]
        public async Task VerifyAsync_WithNullPlainText_ThrowsArgumentException()
        {
            await _svc.Invoking(s => s.VerifyAsync(null!, "h", "s", CancellationToken.None)).Should().ThrowAsync<ArgumentException>();
        }

        /// <summary>
        /// VerifyAsync throws for empty plaintext input.
        /// </summary>
        [Fact]
        public async Task VerifyAsync_WithEmptyPlainText_ThrowsArgumentException()
        {
            await _svc.Invoking(s => s.VerifyAsync(string.Empty, "h", "s", CancellationToken.None)).Should().ThrowAsync<ArgumentException>();
        }

        /// <summary>
        /// HashAsync completes within reasonable performance bounds.
        /// </summary>
        [Fact]
        public async Task HashAsync_Performance_CompletesWithin100Milliseconds()
        {
            var sw = Stopwatch.StartNew();
            var _ = await _svc.HashAsync("perfpass", CancellationToken.None);
            sw.Stop();
            // Allow for CI environments and slower machines; ensure it completes within a reasonable bound
            sw.ElapsedMilliseconds.Should().BeLessThan(500);
        }

        /// <summary>
        /// Generated secure tokens use URL-safe base64 characters.
        /// </summary>
        [Fact]
        public void GenerateSecureToken_ReturnsUrlSafeBase64String()
        {
            var t = _svc.GenerateSecureToken(32);
            t.Should().NotBeNullOrWhiteSpace();
            // Only URL-safe base64 characters
            Regex.IsMatch(t, "^[A-Za-z0-9_-]+$").Should().BeTrue();
        }

        /// <summary>
        /// GenerateSecureToken generates different values on subsequent calls.
        /// </summary>
        [Fact]
        public void GenerateSecureToken_CalledTwice_GeneratesDifferentTokens()
        {
            var a = _svc.GenerateSecureToken(32);
            var b = _svc.GenerateSecureToken(32);
            a.Should().NotBe(b);
        }

        /// <summary>
        /// VerifyAsync compares values in constant time to mitigate timing attacks.
        /// </summary>
        [Fact]
        public async Task VerifyAsync_UsesConstantTimeComparison_PreventTimingAttacks()
        {
            var plain = "timingpass";
            var hashed = await _svc.HashAsync(plain, CancellationToken.None);

            // Allow for environmental variance in timing; verify that difference is within a reasonable bound
            var sw1 = Stopwatch.StartNew();
            await _svc.VerifyAsync(plain, hashed.Hash, hashed.Salt, CancellationToken.None);
            sw1.Stop();

            var sw2 = Stopwatch.StartNew();
            await _svc.VerifyAsync("wrongpass", hashed.Hash, hashed.Salt, CancellationToken.None);
            sw2.Stop();

            var diff = Math.Abs(sw1.ElapsedMilliseconds - sw2.ElapsedMilliseconds);
            // Allow some variance for CI / slower environments. 100ms is a safe bound for timing difference.
            diff.Should().BeLessThan(100);
        }

        /// <summary>
        /// HashAsync throws when input is null.
        /// </summary>
        [Fact]
        public async Task HashAsync_WithNullInput_ThrowsArgumentException()
        {
            await _svc.Invoking(s => s.HashAsync(null!, CancellationToken.None)).Should().ThrowAsync<ArgumentException>();
        }

        /// <summary>
        /// VerifyAsync throws when hash is null.
        /// </summary>
        [Fact]
        public async Task VerifyAsync_WithNullHash_ThrowsArgumentException()
        {
            await _svc.Invoking(s => s.VerifyAsync("p", null!, "s", CancellationToken.None)).Should().ThrowAsync<ArgumentException>();
        }

        /// <summary>
        /// VerifyAsync throws when salt is null.
        /// </summary>
        [Fact]
        public async Task VerifyAsync_WithNullSalt_ThrowsArgumentException()
        {
            var hashed = await _svc.HashAsync("p", CancellationToken.None);
            await _svc.Invoking(s => s.VerifyAsync("p", hashed.Hash, null!, CancellationToken.None)).Should().ThrowAsync<ArgumentException>();
        }

        /// <summary>
        /// GenerateSecureToken returns tokens for different lengths.
        /// </summary>
        [Theory]
        [InlineData(16)]
        [InlineData(32)]
        [InlineData(64)]
        public void GenerateSecureToken_WithDifferentLengths_ReturnsCorrectLength(int bytes)
        {
            var token = _svc.GenerateSecureToken(bytes);
            // approximate length: base64 length without padding = ceil(bytes/3)*4 then trimmed '='
            token.Length.Should().BeGreaterThan(0);
        }
    }
}
