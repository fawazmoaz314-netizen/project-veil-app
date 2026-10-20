using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Veil.Api.Common;
using Veil.Contracts;
using Veil.Domain.Users;
using Veil.Infrastructure.Data;

namespace Veil.Api.Controllers;

[ApiController]
[Authorize]
public sealed class UsersController(
    UserManager<AppUser> users,
    VeilDbContext db,
    ILogger<UsersController> logger) : ControllerBase
{
    [HttpGet(ApiRoutes.Me)]
    public async Task<ActionResult<UserProfileDto>> GetMe()
    {
        var user = await users.FindByIdAsync(User.GetUserId().ToString());
        return user is null ? Unauthorized() : user.ToProfileDto();
    }

    [HttpPut(ApiRoutes.Me)]
    public async Task<ActionResult<UserProfileDto>> UpdateMe(UpdateProfileRequest request)
    {
        var user = await users.FindByIdAsync(User.GetUserId().ToString());
        if (user is null)
        {
            return Unauthorized();
        }

        user.DisplayName = request.DisplayName.Trim();
        await users.UpdateAsync(user);
        return user.ToProfileDto();
    }

    /// <summary>
    /// Permanently deletes the account, its refresh tokens and every message it
    /// sent. Conversations left with no participants are deleted too.
    /// </summary>
    [HttpDelete(ApiRoutes.Me)]
    public async Task<IActionResult> DeleteMe(DeleteAccountRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(User.GetUserId().ToString());
        if (user is null)
        {
            return Unauthorized();
        }

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            return Problem(detail: "Password is incorrect.", statusCode: StatusCodes.Status400BadRequest);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await users.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return Problem(detail: "Account could not be deleted.", statusCode: StatusCodes.Status500InternalServerError);
        }

        await db.Conversations.Where(c => !c.Participants.Any()).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation("User {UserId} deleted their account.", user.Id);
        return NoContent();
    }

    /// <summary>Username prefix search. Email addresses are never searchable or returned.</summary>
    [HttpGet(ApiRoutes.UserSearch)]
    public async Task<ActionResult<IReadOnlyList<UserSummaryDto>>> Search([FromQuery] string? q, CancellationToken ct)
    {
        var query = q?.Trim() ?? string.Empty;
        if (query.Length < Limits.SearchQueryMinLength || query.Length > Limits.UserNameMaxLength)
        {
            return Problem(
                detail: $"Search query must be {Limits.SearchQueryMinLength}-{Limits.UserNameMaxLength} characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var me = User.GetUserId();
        var normalized = users.NormalizeName(query);
        var pattern = EscapeLike(normalized) + "%";

        var results = await db.Users
            .AsNoTracking()
            .Where(u => u.Id != me && EF.Functions.Like(u.NormalizedUserName!, pattern, "\\"))
            .OrderBy(u => u.NormalizedUserName)
            .Take(Limits.SearchMaxResults)
            .Select(u => new UserSummaryDto { Id = u.Id, UserName = u.UserName!, DisplayName = u.DisplayName })
            .ToListAsync(ct);

        return results;
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
