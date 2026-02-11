using System;
using PutZige.Application.Interfaces;

namespace PutZige.Infrastructure.Services
{
    /// <summary>
    /// Production implementation returning real system time.
    /// </summary>
    public sealed class DateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;

        public DateTime Now => DateTime.Now;

        public DateTimeOffset UtcNowOffset => DateTimeOffset.UtcNow;
    }
}