using Veil.Domain.Messages;

namespace Veil.Domain.Conversations;

public enum ConversationType
{
    Direct = 1,
    // Reserved for a future feature. The schema already supports N participants.
    Group = 2,
}

public class Conversation
{
    public Guid Id { get; set; }

    public ConversationType Type { get; set; }

    /// <summary>
    /// For direct conversations: "{smallerUserId}:{largerUserId}". A unique
    /// index on this column guarantees that two users can only ever have one
    /// direct conversation, even if both create it at the same time.
    /// Null for group conversations.
    /// </summary>
    public string? DirectKey { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Updated whenever a message is sent; used to sort the chat list.</summary>
    public DateTime LastActivityAtUtc { get; set; }

    public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();

    public ICollection<Message> Messages { get; set; } = new List<Message>();

    public static string BuildDirectKey(Guid a, Guid b)
    {
        if (a == b)
        {
            throw new ArgumentException("A direct conversation requires two different users.");
        }

        return a.CompareTo(b) < 0 ? $"{a:N}:{b:N}" : $"{b:N}:{a:N}";
    }
}
