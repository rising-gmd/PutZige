#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using PutZige.Application.Common.Messages;

namespace PutZige.Application.DTOs.Common
{
    /// <summary>
    /// Standard API response wrapper used across the application.
    /// </summary>
    /// <typeparam name="T">Type of the data payload.</typeparam>
    public sealed class ApiResponse<T>
    {
        /// <summary>
        /// Indicates whether the operation was successful.
        /// </summary>
        [JsonPropertyName("success")]
        public bool IsSuccess { get; init; }

        /// <summary>
        /// The returned data when the operation is successful.
        /// </summary>
        public T? Data { get; init; }

        /// <summary>
        /// A human readable message intended for clients.
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// <summary>
        /// Optional validation or domain errors keyed by field.
        /// </summary>
        public Dictionary<string, string[]>? Errors { get; init; }

        /// <summary>
        /// Canonical response code that clients can use for i18n and handling.
        /// </summary>
        public string ResponseCode { get; init; } = string.Empty;

        /// <summary>
        /// Optional metadata for additional structured information.
        /// </summary>
        public Dictionary<string, object>? Metadata { get; init; }

        /// <summary>
        /// Optional HTTP status code associated with the response.
        /// </summary>
        public int? StatusCode { get; init; }

        /// <summary>
        /// The server timestamp when the response was created (UTC).
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Creates a successful ApiResponse containing data.
        /// </summary>
        public static ApiResponse<T> Success(T data, string responseCode, string? message = null, Dictionary<string, object>? metadata = null)
            => new()
            {
                IsSuccess = true,
                Data = data,
                ResponseCode = responseCode ?? string.Empty,
                Message = string.IsNullOrWhiteSpace(message) ? SuccessMessages.General.OperationSuccessful : message ?? string.Empty,
                Metadata = metadata,
                StatusCode = StatusCodes.Status200OK,
                Timestamp = DateTime.UtcNow
            };

        /// <summary>
        /// Creates an error ApiResponse with optional errors dictionary and status code.
        /// </summary>
        public static ApiResponse<T> Error(string responseCode, string? message = null, Dictionary<string, string[]>? errors = null, int statusCode = StatusCodes.Status400BadRequest, Dictionary<string, object>? metadata = null)
            => new()
            {
                IsSuccess = false,
                Data = default,
                ResponseCode = responseCode ?? string.Empty,
                Message = string.IsNullOrWhiteSpace(message) ? ErrorMessages.General.InternalServerError : message ?? string.Empty,
                Errors = errors,
                Metadata = metadata,
                StatusCode = statusCode,
                Timestamp = DateTime.UtcNow
            };
    }
}
