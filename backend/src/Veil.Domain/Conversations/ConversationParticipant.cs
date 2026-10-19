using Veil.Domain.Users;

namespace Veil.Domain.Conversations;

/// <summary>
/// Membership row. Every conversation and message operation checks for one of
/// these rows before allowing access.
/// </summary>
public class ConversationParticipant
{
    public Guid ConversationId { get; set; }

    public Conversation Conversation { get; set; } = null!;

    public Guid UserId { get; set; }

    public AppUser User { get; set; } = null!;

    public DateTime JoinedAtUtc { get; set; }

    /// <summary>
    /// Highest message sequence this participant has read. Drives read
    /// receipts and unread counts without a row per message.
    /// </summary>
    public long LastReadSequence { get; set; }
}
