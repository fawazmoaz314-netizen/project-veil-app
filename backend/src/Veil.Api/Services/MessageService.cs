using Microsoft.EntityFrameworkCore;
using Veil.Api.Common;
using Veil.Contracts;
using Veil.Domain.Messages;
using Veil.Infrastructure.Data;

namespace Veil.Api.Services;

public sealed class MessageService(
    VeilDbContext db,
    ConversationService conversations,
    IRealtimeNotifier notifier,
    UserActionLimiter limiter,
    TimeProvider clock)
{
    /// <summary>
    /// Pages messages in ascending order. Use <paramref name="after"/> to catch
    /// up after a reconnect, or <paramref name="before"/> to load older history.
    /// </summary>
    public async Task<ServiceResult<MessagePage>> GetPageAsync(
        Guid conversationId, Guid userId, long? before, long? after, int limit, CancellationToken ct)
    {
        if (before is not null && after is not null)
        {
            return ServiceError.Validation("Specify either 'before' or 'after', not both.");
        }

        if (!await conversations.IsParticipantAsync(conversationId, userId, ct))
        {
            return ServiceError.NotFound("Conversation not found.");
        }

        limit = Math.Clamp(limit, 1, Limits.MessagePageMaxSize);
        var query = db.Messages.AsNoTracking().Where(m => m.ConversationId == conversationId);

        List<Message> rows;
        if (after is not null)
        {
            rows = await query.Where(m => m.Sequence > after).OrderBy(m => m.Sequence).Take(limit + 1).ToListAsync(ct);
        }
        else
        {
            if (before is not null)
            {
                query = query.Where(m => m.Sequence < before);
            }

            rows = await query.OrderByDescending(m => m.Sequence).Take(limit + 1).ToListAsync(ct);
            var hasMoreOlder = rows.Count > limit;
            rows = rows.Take(limit).Reverse().ToList();
            return new MessagePage { Items = rows.Select(m => m.ToDto()).ToList(), HasMore = hasMoreOlder };
        }

        return new MessagePage
        {
            Items = rows.Take(limit).Select(m => m.ToDto()).ToList(),
            HasMore = rows.Count > limit,
        };
    }

    /// <summary>
    /// Persists then broadcasts. Idempotent on (sender, ClientMessageId): a retry
    /// returns the original message with Created = false and is not re-broadcast.
    /// </summary>
    public async Task<ServiceResult<(MessageDto Message, bool Created)>> SendAsync(
        Guid conversationId, Guid senderId, SendMessageRequest request, CancellationToken ct)
    {
        if (!MessageContentTypes.Allowed.Contains(request.ContentType))
        {
            return ServiceError.Validation($"Unsupported content type '{request.ContentType}'.");
        }

        if (string.IsNullOrWhiteSpace(request.Body) || request.Body.Length > Limits.MessageBodyMaxLength)
        {
            return ServiceError.Validation($"Message must be 1-{Limits.MessageBodyMaxLength} characters.");
        }

        if (!await conversations.IsParticipantAsync(conversationId, senderId, ct))
        {
            return ServiceError.NotFound("Conversation not found.");
        }

        var duplicate = await FindByClientIdAsync(senderId, request.ClientMessageId, ct);
        if (duplicate is not null)
        {
            return DuplicateResult(duplicate, conversationId);
        }

        if (!limiter.TryAcquireMessage(senderId))
        {
            return ServiceError.RateLimited("You are sending messages too quickly.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = senderId,
            ClientMessageId = request.ClientMessageId,
            ContentType = request.ContentType,
            Body = request.Body,
            CreatedAtUtc = now,
        };

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // Locks the conversation row until commit, so sequences inside one
            // conversation are always committed in order. Clients can then use
            // "after = last seen sequence" without ever skipping a message.
            await db.Conversations
                .Where(c => c.Id == conversationId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.LastActivityAtUtc, now), ct);

            db.Messages.Add(message);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            db.ChangeTracker.Clear();
            var winner = await FindByClientIdAsync(senderId, request.ClientMessageId, ct);
            if (winner is null)
            {
                return ServiceError.Conflict("Please retry.");
            }

            return DuplicateResult(winner, conversationId);
        }

        var dto = message.ToDto();
        var recipients = await ParticipantIdsAsync(conversationId, ct);
        await notifier.MessageReceivedAsync(recipients, dto);
        return (dto, true);
    }

    public async Task<ServiceResult<ReadReceiptDto>> MarkReadAsync(
        Guid conversationId, Guid userId, long lastReadSequence, CancellationToken ct)
    {
        if (lastReadSequence < 0)
        {
            return ServiceError.Validation("lastReadSequence must be non-negative.");
        }

        if (!await conversations.IsParticipantAsync(conversationId, userId, ct))
        {
            return ServiceError.NotFound("Conversation not found.");
        }

        var latest = await db.Messages
            .Where(m => m.ConversationId == conversationId)
            .MaxAsync(m => (long?)m.Sequence, ct) ?? 0;

        var target = Math.Min(lastReadSequence, latest);

        // Read pointers only move forward, even if requests arrive out of order.
        var updated = await db.ConversationParticipants
            .Where(p => p.ConversationId == conversationId && p.UserId == userId && p.LastReadSequence < target)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.LastReadSequence, target), ct);

        var current = await db.ConversationParticipants
            .Where(p => p.ConversationId == conversationId && p.UserId == userId)
            .Select(p => p.LastReadSequence)
            .SingleAsync(ct);

        var receipt = new ReadReceiptDto { ConversationId = conversationId, UserId = userId, LastReadSequence = current };

        if (updated > 0)
        {
            await notifier.ReadReceiptUpdatedAsync(await ParticipantIdsAsync(conversationId, ct), receipt);
        }

        return receipt;
    }

    public async Task<ServiceResult<bool>> TypingAsync(Guid conversationId, Guid userId, CancellationToken ct)
    {
        if (!limiter.TryAcquireTyping(userId))
        {
            return false;
        }

        if (!await conversations.IsParticipantAsync(conversationId, userId, ct))
        {
            return ServiceError.NotFound("Conversation not found.");
        }

        var others = (await ParticipantIdsAsync(conversationId, ct)).Where(id => id != userId).ToList();
        await notifier.UserTypingAsync(others, new TypingDto { ConversationId = conversationId, UserId = userId });
        return true;
    }

    private static ServiceResult<(MessageDto Message, bool Created)> DuplicateResult(Message existing, Guid conversationId)
    {
        if (existing.ConversationId != conversationId)
        {
            return ServiceError.Conflict("ClientMessageId was already used for a different conversation.");
        }

        return (existing.ToDto(), false);
    }

    private Task<Message?> FindByClientIdAsync(Guid senderId, Guid clientMessageId, CancellationToken ct) =>
        db.Messages.AsNoTracking().SingleOrDefaultAsync(m => m.SenderId == senderId && m.ClientMessageId == clientMessageId, ct);

    private Task<List<Guid>> ParticipantIdsAsync(Guid conversationId, CancellationToken ct) =>
        db.ConversationParticipants.Where(p => p.ConversationId == conversationId).Select(p => p.UserId).ToListAsync(ct);
}
