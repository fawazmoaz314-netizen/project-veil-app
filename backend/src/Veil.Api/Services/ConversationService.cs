using Microsoft.EntityFrameworkCore;
using Veil.Api.Common;
using Veil.Contracts;
using Veil.Domain.Conversations;
using Veil.Infrastructure.Data;

namespace Veil.Api.Services;

public sealed class ConversationService(VeilDbContext db, IRealtimeNotifier notifier, TimeProvider clock)
{
    public Task<bool> IsParticipantAsync(Guid conversationId, Guid userId, CancellationToken ct) =>
        db.ConversationParticipants.AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId, ct);

    public async Task<PagedResult<ConversationDto>> ListAsync(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, Limits.ConversationPageMaxSize);

        var conversations = db.Conversations
            .Where(c => c.Participants.Any(p => p.UserId == userId))
            .OrderByDescending(c => c.LastActivityAtUtc)
            .ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize + 1);

        var rows = await Project(conversations, userId).ToListAsync(ct);

        return new PagedResult<ConversationDto>
        {
            Items = rows.Take(pageSize).Select(ToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            HasMore = rows.Count > pageSize,
        };
    }

    /// <summary>Returns null when the conversation does not exist OR the user is not a participant (no existence leak).</summary>
    public async Task<ConversationDto?> GetAsync(Guid conversationId, Guid userId, CancellationToken ct)
    {
        var query = db.Conversations.Where(c => c.Id == conversationId && c.Participants.Any(p => p.UserId == userId));
        var row = await Project(query, userId).SingleOrDefaultAsync(ct);
        return row is null ? null : ToDto(row);
    }

    public async Task<ServiceResult<(ConversationDto Conversation, bool Created)>> GetOrCreateDirectAsync(
        Guid userId, Guid otherUserId, CancellationToken ct)
    {
        if (userId == otherUserId)
        {
            return ServiceError.Validation("You cannot start a conversation with yourself.");
        }

        if (!await db.Users.AnyAsync(u => u.Id == otherUserId, ct))
        {
            return ServiceError.NotFound("User not found.");
        }

        var directKey = Conversation.BuildDirectKey(userId, otherUserId);
        var existingId = await FindDirectAsync(directKey, ct);
        if (existingId is not null)
        {
            var existing = await GetAsync(existingId.Value, userId, ct);
            if (existing is null)
            {
                return ServiceError.NotFound("Conversation not found.");
            }

            return (existing, false);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Direct,
            DirectKey = directKey,
            CreatedAtUtc = now,
            LastActivityAtUtc = now,
            Participants =
            {
                new ConversationParticipant { UserId = userId, JoinedAtUtc = now },
                new ConversationParticipant { UserId = otherUserId, JoinedAtUtc = now },
            },
        };

        db.Conversations.Add(conversation);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // Both users started the chat at the same moment; the other request won.
            db.ChangeTracker.Clear();
            var winnerId = await FindDirectAsync(directKey, ct);
            var winner = winnerId is null ? null : await GetAsync(winnerId.Value, userId, ct);
            if (winner is null)
            {
                return ServiceError.Conflict("Please retry.");
            }

            return (winner, false);
        }

        var forOther = await GetAsync(conversation.Id, otherUserId, ct);
        if (forOther is not null)
        {
            await notifier.ConversationCreatedAsync(otherUserId, forOther);
        }

        return ((await GetAsync(conversation.Id, userId, ct))!, true);
    }

    private Task<Guid?> FindDirectAsync(string directKey, CancellationToken ct) =>
        db.Conversations.Where(c => c.DirectKey == directKey).Select(c => (Guid?)c.Id).SingleOrDefaultAsync(ct);

    private static IQueryable<ConversationRow> Project(IQueryable<Conversation> query, Guid userId) =>
        query.Select(c => new ConversationRow
        {
            Id = c.Id,
            Type = c.Type,
            CreatedAtUtc = c.CreatedAtUtc,
            LastActivityAtUtc = c.LastActivityAtUtc,
            Participants = c.Participants
                .OrderBy(p => p.JoinedAtUtc)
                .Select(p => new ParticipantDto
                {
                    UserId = p.UserId,
                    UserName = p.User.UserName!,
                    DisplayName = p.User.DisplayName,
                    LastReadSequence = p.LastReadSequence,
                })
                .ToList(),
            LastMessage = c.Messages
                .OrderByDescending(m => m.Sequence)
                .Select(m => new MessageRow
                {
                    Id = m.Id,
                    Sequence = m.Sequence,
                    SenderId = m.SenderId,
                    ClientMessageId = m.ClientMessageId,
                    ContentType = m.ContentType,
                    Body = m.Body,
                    CreatedAtUtc = m.CreatedAtUtc,
                })
                .FirstOrDefault(),
            UnreadCount = c.Messages.Count(m =>
                m.SenderId != userId &&
                m.Sequence > c.Participants.Where(p => p.UserId == userId).Select(p => p.LastReadSequence).FirstOrDefault()),
        });

    private static ConversationDto ToDto(ConversationRow row) => new()
    {
        Id = row.Id,
        Type = row.Type == ConversationType.Group ? ConversationTypes.Group : ConversationTypes.Direct,
        CreatedAt = row.CreatedAtUtc.AsUtc(),
        LastActivityAt = row.LastActivityAtUtc.AsUtc(),
        Participants = row.Participants,
        UnreadCount = row.UnreadCount,
        LastMessage = row.LastMessage is null ? null : new MessageDto
        {
            Id = row.LastMessage.Id,
            Sequence = row.LastMessage.Sequence,
            ConversationId = row.Id,
            SenderId = row.LastMessage.SenderId,
            ClientMessageId = row.LastMessage.ClientMessageId,
            ContentType = row.LastMessage.ContentType,
            Body = row.LastMessage.Body,
            CreatedAt = row.LastMessage.CreatedAtUtc.AsUtc(),
        },
    };

    private sealed class ConversationRow
    {
        public Guid Id { get; init; }
        public ConversationType Type { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime LastActivityAtUtc { get; init; }
        public List<ParticipantDto> Participants { get; init; } = [];
        public MessageRow? LastMessage { get; init; }
        public int UnreadCount { get; init; }
    }

    private sealed class MessageRow
    {
        public Guid Id { get; init; }
        public long Sequence { get; init; }
        public Guid SenderId { get; init; }
        public Guid ClientMessageId { get; init; }
        public string ContentType { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public DateTime CreatedAtUtc { get; init; }
    }
}
