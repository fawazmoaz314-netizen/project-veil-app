using Microsoft.AspNetCore.SignalR;
using Veil.Api.Hubs;
using Veil.Contracts;

namespace Veil.Api.Services;

public interface IRealtimeNotifier
{
    Task MessageReceivedAsync(IReadOnlyCollection<Guid> userIds, MessageDto message);
    Task ReadReceiptUpdatedAsync(IReadOnlyCollection<Guid> userIds, ReadReceiptDto receipt);
    Task UserTypingAsync(IReadOnlyCollection<Guid> userIds, TypingDto typing);
    Task ConversationCreatedAsync(Guid userId, ConversationDto conversation);
}

/// <summary>
/// Pushes events to every connection of the given users. Delivery is
/// best-effort: the database is the source of truth and clients catch up via
/// the REST "after" cursor on reconnect, so a failed push is logged, not thrown.
/// With multiple API instances add a SignalR backplane (Redis or Azure SignalR).
/// </summary>
public sealed class RealtimeNotifier(IHubContext<ChatHub> hub, ILogger<RealtimeNotifier> logger) : IRealtimeNotifier
{
    public Task MessageReceivedAsync(IReadOnlyCollection<Guid> userIds, MessageDto message) =>
        Send(userIds, ChatHubContract.Client.MessageReceived, message);

    public Task ReadReceiptUpdatedAsync(IReadOnlyCollection<Guid> userIds, ReadReceiptDto receipt) =>
        Send(userIds, ChatHubContract.Client.ReadReceiptUpdated, receipt);

    public Task UserTypingAsync(IReadOnlyCollection<Guid> userIds, TypingDto typing) =>
        Send(userIds, ChatHubContract.Client.UserTyping, typing);

    public Task ConversationCreatedAsync(Guid userId, ConversationDto conversation) =>
        Send([userId], ChatHubContract.Client.ConversationCreated, conversation);

    private async Task Send(IReadOnlyCollection<Guid> userIds, string method, object payload)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        try
        {
            await hub.Clients.Users(userIds.Select(id => id.ToString())).SendAsync(method, payload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Realtime push {Method} failed; clients will catch up on reconnect.", method);
        }
    }
}
