using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using Veil.Api.Options;

namespace Veil.Api.Services;

/// <summary>
/// Per-user limits shared by the REST and SignalR send paths, so a client
/// cannot bypass the HTTP rate limiter by switching to the hub.
/// In-memory: with several API instances, each instance enforces its own
/// window. Move to a distributed limiter (e.g. Redis) when you scale out.
/// </summary>
public sealed class UserActionLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<Guid> _messages;
    private readonly PartitionedRateLimiter<Guid> _typing;

    public UserActionLimiter(IOptions<RateLimitOptions> options)
    {
        var o = options.Value;
        _messages = Create(o.MessagesPerMinute);
        _typing = Create(o.TypingEventsPerMinute);
    }

    public bool TryAcquireMessage(Guid userId) => Acquire(_messages, userId);

    public bool TryAcquireTyping(Guid userId) => Acquire(_typing, userId);

    private static bool Acquire(PartitionedRateLimiter<Guid> limiter, Guid userId)
    {
        using var lease = limiter.AttemptAcquire(userId);
        return lease.IsAcquired;
    }

    private static PartitionedRateLimiter<Guid> Create(int permitsPerMinute) =>
        PartitionedRateLimiter.Create<Guid, Guid>(userId =>
            RateLimitPartition.GetSlidingWindowLimiter(userId, _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
            }));

    public void Dispose()
    {
        _messages.Dispose();
        _typing.Dispose();
    }
}
