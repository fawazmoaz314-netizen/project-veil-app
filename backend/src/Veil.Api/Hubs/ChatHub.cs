using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Veil.Api.Common;
using Veil.Api.Services;
using Veil.Contracts;

namespace Veil.Api.Hubs;

/// <summary>
/// Realtime channel. Identity comes only from the validated JWT. Every method
/// re-checks conversation membership in <see cref="MessageService"/>.
/// </summary>
[Authorize]
public sealed class ChatHub(MessageService messages) : Hub
{
    [HubMethodName(ChatHubContract.Server.SendMessage)]
    public async Task<MessageDto> SendMessage(Guid conversationId, SendMessageRequest request)
    {
        Validate(request);
        var result = await messages.SendAsync(conversationId, UserId, request, Context.ConnectionAborted);
        return result.IsSuccess ? result.Value.Message : throw ToHubException(result.Error!);
    }

    [HubMethodName(ChatHubContract.Server.MarkRead)]
    public async Task MarkRead(Guid conversationId, long lastReadSequence)
    {
        var result = await messages.MarkReadAsync(conversationId, UserId, lastReadSequence, Context.ConnectionAborted);
        if (!result.IsSuccess)
        {
            throw ToHubException(result.Error!);
        }
    }

    [HubMethodName(ChatHubContract.Server.Typing)]
    public async Task Typing(Guid conversationId)
    {
        // Typing is cosmetic: silently drop rate-limited events instead of erroring.
        var result = await messages.TypingAsync(conversationId, UserId, Context.ConnectionAborted);
        if (!result.IsSuccess)
        {
            throw ToHubException(result.Error!);
        }
    }

    private Guid UserId => Context.User.TryGetUserId() ?? throw new HubException("Unauthorized.");

    private static void Validate(SendMessageRequest? request)
    {
        if (request is null)
        {
            throw new HubException("Request is required.");
        }

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true))
        {
            throw new HubException(string.Join(" ", results.Select(r => r.ErrorMessage)));
        }
    }

    // HubException messages are sent to the client; other exceptions are not (EnableDetailedErrors stays off).
    private static HubException ToHubException(ServiceError error) => new($"{error.Kind}: {error.Message}");
}
