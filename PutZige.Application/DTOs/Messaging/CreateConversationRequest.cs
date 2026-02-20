#nullable enable
using System;

namespace PutZige.Application.DTOs.Messaging;

/// <summary>
/// Request to create or retrieve a direct conversation with another user.
/// </summary>
public sealed record CreateConversationRequest(Guid OtherUserId);
