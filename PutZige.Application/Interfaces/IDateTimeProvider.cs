using System;

namespace PutZige.Application.Interfaces
{
    /// <summary>
    /// Provides access to system time for improved testability.
    /// </summary>
    public interface IDateTimeProvider
    {
        /// <summary>
        /// Gets the current UTC date and time.
        /// </summary>
        DateTime UtcNow { get; }

        /// <summary>
        /// Gets the current local date and time.
        /// </summary>
        DateTime Now { get; }

        /// <summary>
        /// Gets the current UTC date and time with offset.
        /// </summary>
        DateTimeOffset UtcNowOffset { get; }
    }
}