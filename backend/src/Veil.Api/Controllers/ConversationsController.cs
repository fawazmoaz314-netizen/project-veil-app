using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Veil.Api.Common;
using Veil.Api.Services;
using Veil.Contracts;

namespace Veil.Api.Controllers;

[ApiController]
[Authorize]
public sealed class ConversationsController(ConversationService conversations, MessageService messages) : ControllerBase
{
    [HttpGet(ApiRoutes.Conversations)]
    public async Task<PagedResult<ConversationDto>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await conversations.ListAsync(User.GetUserId(), page, pageSize, ct);

    [HttpPost(ApiRoutes.DirectConversations)]
    public async Task<IActionResult> CreateDirect(CreateDirectConversationRequest request, CancellationToken ct)
    {
        var result = await conversations.GetOrCreateDirectAsync(User.GetUserId(), request.UserId, ct);
        if (!result.IsSuccess)
        {
            return this.ToProblem(result.Error!);
        }

        var (conversation, created) = result.Value;
        return created
            ? CreatedAtAction(nameof(Get), new { id = conversation.Id }, conversation)
            : Ok(conversation);
    }

    [HttpGet("api/conversations/{id:guid}")]
    public async Task<ActionResult<ConversationDto>> Get(Guid id, CancellationToken ct)
    {
        var conversation = await conversations.GetAsync(id, User.GetUserId(), ct);
        return conversation is null ? Problem(detail: "Conversation not found.", statusCode: 404) : conversation;
    }

    [HttpGet("api/conversations/{id:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid id,
        [FromQuery] long? before,
        [FromQuery] long? after,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var result = await messages.GetPageAsync(id, User.GetUserId(), before, after, limit, ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error!);
    }

    [HttpPost("api/conversations/{id:guid}/messages")]
    public async Task<IActionResult> SendMessage(Guid id, SendMessageRequest request, CancellationToken ct)
    {
        var result = await messages.SendAsync(id, User.GetUserId(), request, ct);
        if (!result.IsSuccess)
        {
            return this.ToProblem(result.Error!);
        }

        var (message, created) = result.Value;
        return created ? StatusCode(StatusCodes.Status201Created, message) : Ok(message);
    }

    [HttpPost("api/conversations/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, MarkReadRequest request, CancellationToken ct)
    {
        var result = await messages.MarkReadAsync(id, User.GetUserId(), request.LastReadSequence, ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error!);
    }
}
