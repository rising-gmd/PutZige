using PutZige.Application.Interfaces;
using System;

namespace PutZige.Application.Services
{
    // Internal adapter used only to provide a default implementation when callers don't supply IDateTimeProvider.
    // This keeps the public API backwards-compatible for tests that haven't been updated yet.
    internal class SystemDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime Now => DateTime.Now;
        public DateTimeOffset UtcNowOffset => DateTimeOffset.UtcNow;
    }
}
