using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using Veil.Api.Common;
using Veil.Api.Services;
using Veil.Contracts;
using Veil.Domain.Users;

namespace Veil.Api.Controllers;

[ApiController]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AuthController(
    UserManager<AppUser> users,
    TokenService tokens,
    IVeilEmailSender email,
    TimeProvider clock,
    ILogger<AuthController> logger) : ControllerBase
{
    // Compared against when the login name is unknown so response timing does not reveal which accounts exist.
    private static readonly string DummyPasswordHash =
        new PasswordHasher<AppUser>().HashPassword(new AppUser(), Guid.NewGuid().ToString());

    [HttpPost(ApiRoutes.Register)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.UserName.Trim() : request.DisplayName.Trim(),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };

        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(MapIdentityField(error.Code), error.Description);
            }

            return ValidationProblem(ModelState);
        }

        logger.LogInformation("User {UserId} registered.", user.Id);
        return StatusCode(StatusCodes.Status201Created, await tokens.IssueAsync(user, ct));
    }

    [HttpPost(ApiRoutes.Login)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var login = request.Login.Trim();
        var user = login.Contains('@') ? await users.FindByEmailAsync(login) : await users.FindByNameAsync(login);

        if (user is null)
        {
            new PasswordHasher<AppUser>().VerifyHashedPassword(new AppUser(), DummyPasswordHash, request.Password);
            return InvalidCredentials();
        }

        if (await users.IsLockedOutAsync(user))
        {
            return Problem(detail: "Too many failed attempts. Try again later.", statusCode: StatusCodes.Status423Locked);
        }

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            logger.LogInformation("Failed login for user {UserId}.", user.Id);
            return InvalidCredentials();
        }

        await users.ResetAccessFailedCountAsync(user);
        return Ok(await tokens.IssueAsync(user, ct));
    }

    [HttpPost(ApiRoutes.Refresh)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var result = await tokens.RefreshAsync(request.RefreshToken, ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error!);
    }

    [Authorize]
    [HttpPost(ApiRoutes.Logout)]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken ct)
    {
        await tokens.RevokeAsync(User.GetUserId(), request.RefreshToken, ct);
        return NoContent();
    }

    /// <summary>Always 202 so the endpoint cannot be used to discover registered emails.</summary>
    [HttpPost(ApiRoutes.ForgotPassword)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is not null)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            await email.SendPasswordResetAsync(user.Email!, encoded, ct);
        }

        return Accepted();
    }

    [HttpPost(ApiRoutes.ResetPassword)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return InvalidResetToken();
        }

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch (FormatException)
        {
            return InvalidResetToken();
        }

        var result = await users.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
            {
                return InvalidResetToken();
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(nameof(ResetPasswordRequest.NewPassword), error.Description);
            }

            return ValidationProblem(ModelState);
        }

        // ResetPasswordAsync rotates the security stamp; also sign out every device.
        await tokens.RevokeAllForUserAsync(user.Id, ct);
        await users.ResetAccessFailedCountAsync(user);
        await users.SetLockoutEndDateAsync(user, null);
        logger.LogInformation("Password reset for user {UserId}.", user.Id);
        return NoContent();
    }

    private ObjectResult InvalidCredentials() =>
        Problem(detail: "Invalid username/email or password.", statusCode: StatusCodes.Status401Unauthorized);

    private ObjectResult InvalidResetToken() =>
        Problem(detail: "The reset link is invalid or has expired.", statusCode: StatusCodes.Status400BadRequest);

    private static string MapIdentityField(string code) => code switch
    {
        _ when code.Contains("UserName", StringComparison.Ordinal) => nameof(RegisterRequest.UserName),
        _ when code.Contains("Email", StringComparison.Ordinal) => nameof(RegisterRequest.Email),
        _ when code.StartsWith("Password", StringComparison.Ordinal) => nameof(RegisterRequest.Password),
        _ => string.Empty,
    };
}
