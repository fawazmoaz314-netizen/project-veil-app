using Veil.Contracts;
using Veil.Domain.Messages;
using Veil.Domain.Users;

namespace Veil.Api.Common;

public static class Mapping
{
    public static DateTimeOffset AsUtc(this DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public static UserProfileDto ToProfileDto(this AppUser user) => new()
    {
        Id = user.Id,
        UserName = user.UserName ?? string.Empty,
        DisplayName = user.DisplayName,
        Email = user.Email ?? string.Empty,
        CreatedAt = user.CreatedAtUtc.AsUtc(),
    };

    public static MessageDto ToDto(this Message message) => new()
    {
        Id = message.Id,
        Sequence = message.Sequence,
        ConversationId = message.ConversationId,
        SenderId = message.SenderId,
        ClientMessageId = message.ClientMessageId,
        ContentType = message.ContentType,
        Body = message.Body,
        CreatedAt = message.CreatedAtUtc.AsUtc(),
    };
}
