using System.ComponentModel.DataAnnotations;

namespace Veil.Contracts;

public static class ConversationTypes
{
    public const string Direct = "direct";
    public const string Group = "group";
}

public sealed record CreateDirectConversationRequest
{
    [NotEmptyGuid]
    public Guid UserId { get; init; }
}

public sealed record ParticipantDto
{
    public required Guid UserId { get; init; }
    public required string UserName { get; init; }
    public required string DisplayName { get; init; }
    public required long LastReadSequence { get; init; }
}

public sealed record ConversationDto
{
    public required Guid Id { get; init; }
    public required string Type { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset LastActivityAt { get; init; }
    public required IReadOnlyList<ParticipantDto> Participants { get; init; }
    public MessageDto? LastMessage { get; init; }
    public required int UnreadCount { get; init; }
}

public sealed record PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required bool HasMore { get; init; }
}

public sealed record SendMessageRequest
{
    /// <summary>Client-generated idempotency key. Reuse it when retrying the same message.</summary>
    [NotEmptyGuid]
    public Guid ClientMessageId { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(Limits.MessageBodyMaxLength, MinimumLength = 1)]
    public string Body { get; init; } = string.Empty;

    [StringLength(Limits.ContentTypeMaxLength)]
    public string ContentType { get; init; } = "text/plain";
}

public sealed record MessageDto
{
    public required Guid Id { get; init; }
    public required long Sequence { get; init; }
    public required Guid ConversationId { get; init; }
    public required Guid SenderId { get; init; }
    public required Guid ClientMessageId { get; init; }
    public required string ContentType { get; init; }
    public required string Body { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Messages in ascending sequence order (oldest first).</summary>
public sealed record MessagePage
{
    public required IReadOnlyList<MessageDto> Items { get; init; }
    public required bool HasMore { get; init; }
}

public sealed record MarkReadRequest
{
    [Range(0, long.MaxValue)]
    public long LastReadSequence { get; init; }
}

public sealed record ReadReceiptDto
{
    public required Guid ConversationId { get; init; }
    public required Guid UserId { get; init; }
    public required long LastReadSequence { get; init; }
}

public sealed record TypingDto
{
    public required Guid ConversationId { get; init; }
    public required Guid UserId { get; init; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public NotEmptyGuidAttribute() : base("The {0} field must be a non-empty GUID.") { }

    public override bool IsValid(object? value) => value is Guid guid && guid != Guid.Empty;
}
