using Microsoft.AspNetCore.Identity;
using Veil.Domain.Auth;
using Veil.Domain.Conversations;
using Veil.Domain.Messages;

namespace Veil.Domain.Users;

/// <summary>
/// A VEIL account. Inherits password hash, lockout, security stamp, email and
/// username handling from ASP.NET Core Identity so we never write our own
/// password storage code.
/// </summary>
public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<ConversationParticipant> Participations { get; set; } = new List<ConversationParticipant>();

    public ICollection<Message> SentMessages { get; set; } = new List<Message>();

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
