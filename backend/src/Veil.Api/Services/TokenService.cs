using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Veil.Api.Common;
using Veil.Api.Options;
using Veil.Contracts;
using Veil.Domain.Auth;
using Veil.Domain.Users;
using Veil.Infrastructure.Data;
using JwtClaims = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames;

namespace Veil.Api.Services;

public sealed class TokenService(
    VeilDbContext db,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider clock,
    ILogger<TokenService> logger)
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public static SymmetricSecurityKey CreateSigningKey(string signingKey) => new(Encoding.UTF8.GetBytes(signingKey));

    /// <summary>Issues a new access token and a new refresh token family (a fresh login).</summary>
    public async Task<AuthResponse> IssueAsync(AppUser user, CancellationToken ct)
    {
        var refresh = CreateRefreshToken(user.Id, Guid.NewGuid());
        db.RefreshTokens.Add(refresh.Entity);
        await db.SaveChangesAsync(ct);
        return BuildResponse(user, refresh.RawValue, refresh.Entity.ExpiresAtUtc);
    }

    /// <summary>
    /// Rotates a refresh token: the presented token is revoked and replaced.
    /// Reusing an already-rotated token revokes the entire family, because
    /// that only happens when a token has been copied by someone else.
    /// </summary>
    public async Task<ServiceResult<AuthResponse>> RefreshAsync(string rawToken, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var hash = Hash(rawToken);

        var existing = await db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (existing is null)
        {
            return ServiceError.Unauthorized("Invalid refresh token.");
        }

        if (existing.RevokedAtUtc is not null)
        {
            logger.LogWarning("Refresh token reuse detected for user {UserId}; revoking token family {FamilyId}.",
                existing.UserId, existing.FamilyId);
            await RevokeFamilyAsync(existing.FamilyId, now, ct);
            return ServiceError.Unauthorized("Invalid refresh token.");
        }

        if (existing.ExpiresAtUtc <= now)
        {
            return ServiceError.Unauthorized("Refresh token has expired.");
        }

        if (existing.User.LockoutEnd is { } lockoutEnd && lockoutEnd > clock.GetUtcNow())
        {
            return ServiceError.Unauthorized("Account is locked.");
        }

        var replacement = CreateRefreshToken(existing.UserId, existing.FamilyId);

        // Conditional update: if two refreshes race with the same token, exactly one wins.
        var revoked = await db.RefreshTokens
            .Where(t => t.Id == existing.Id && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAtUtc, now)
                .SetProperty(t => t.ReplacedByTokenId, replacement.Entity.Id), ct);

        if (revoked == 0)
        {
            return ServiceError.Unauthorized("Invalid refresh token.");
        }

        db.RefreshTokens.Add(replacement.Entity);
        await db.SaveChangesAsync(ct);

        return BuildResponse(existing.User, replacement.RawValue, replacement.Entity.ExpiresAtUtc);
    }

    /// <summary>Revokes the presented token's family (logout from this device). Unknown tokens are ignored.</summary>
    public async Task RevokeAsync(Guid userId, string rawToken, CancellationToken ct)
    {
        var hash = Hash(rawToken);
        var familyId = await db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.UserId == userId)
            .Select(t => (Guid?)t.FamilyId)
            .SingleOrDefaultAsync(ct);

        if (familyId is not null)
        {
            await RevokeFamilyAsync(familyId.Value, clock.GetUtcNow().UtcDateTime, ct);
        }
    }

    /// <summary>Logs the user out everywhere (used after a password reset).</summary>
    public Task RevokeAllForUserAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);
    }

    private Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken ct) =>
        db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);

    private AuthResponse BuildResponse(AppUser user, string rawRefreshToken, DateTime refreshExpiresAtUtc)
    {
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(_jwt.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(CreateSigningKey(_jwt.SigningKey), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtClaims.Sub, user.Id.ToString()),
                new Claim(JwtClaims.UniqueName, user.UserName ?? string.Empty),
                new Claim(JwtClaims.Jti, Guid.NewGuid().ToString()),
            ]),
        };

        return new AuthResponse
        {
            AccessToken = new JsonWebTokenHandler().CreateToken(descriptor),
            AccessTokenExpiresAt = expires,
            RefreshToken = rawRefreshToken,
            RefreshTokenExpiresAt = refreshExpiresAtUtc.AsUtc(),
            User = user.ToProfileDto(),
        };
    }

    private (RefreshToken Entity, string RawValue) CreateRefreshToken(Guid userId, Guid familyId)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48));

        return (new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = Hash(raw),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(_jwt.RefreshTokenDays),
        }, raw);
    }

    private static string Hash(string raw) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
