using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Veil.Api.Common;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The authenticated user's id from the validated JWT "sub" claim. This is
    /// the only source of identity used by the API; ids in request bodies are
    /// never trusted for "who is acting".
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        TryGetUserId(principal) ?? throw new InvalidOperationException("Authenticated principal has no valid 'sub' claim.");

    public static Guid? TryGetUserId(this ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}

/// <summary>Makes SignalR's Clients.User(id) use the same "sub" claim.</summary>
public sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User.TryGetUserId()?.ToString();
}
