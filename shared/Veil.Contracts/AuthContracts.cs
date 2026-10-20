using System.ComponentModel.DataAnnotations;

namespace Veil.Contracts;

public sealed record RegisterRequest
{
    [Required]
    [StringLength(Limits.UserNameMaxLength, MinimumLength = Limits.UserNameMinLength)]
    [RegularExpression(Limits.UserNamePattern, ErrorMessage = "Username may only contain letters, numbers, '_' and '.'.")]
    public string UserName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(Limits.EmailMaxLength)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(Limits.PasswordMaxLength, MinimumLength = Limits.PasswordMinLength)]
    public string Password { get; init; } = string.Empty;

    [StringLength(Limits.DisplayNameMaxLength)]
    public string? DisplayName { get; init; }
}

public sealed record LoginRequest
{
    /// <summary>Username or email address.</summary>
    [Required]
    [StringLength(Limits.EmailMaxLength)]
    public string Login { get; init; } = string.Empty;

    [Required]
    [StringLength(Limits.PasswordMaxLength)]
    public string Password { get; init; } = string.Empty;
}

public sealed record RefreshRequest
{
    [Required]
    [StringLength(Limits.RefreshTokenMaxLength)]
    public string RefreshToken { get; init; } = string.Empty;
}

public sealed record LogoutRequest
{
    [Required]
    [StringLength(Limits.RefreshTokenMaxLength)]
    public string RefreshToken { get; init; } = string.Empty;
}

public sealed record ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    [StringLength(Limits.EmailMaxLength)]
    public string Email { get; init; } = string.Empty;
}

public sealed record ResetPasswordRequest
{
    [Required]
    [EmailAddress]
    [StringLength(Limits.EmailMaxLength)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(Limits.ResetTokenMaxLength)]
    public string Token { get; init; } = string.Empty;

    [Required]
    [StringLength(Limits.PasswordMaxLength, MinimumLength = Limits.PasswordMinLength)]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed record AuthResponse
{
    public required string AccessToken { get; init; }
    public required DateTimeOffset AccessTokenExpiresAt { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTimeOffset RefreshTokenExpiresAt { get; init; }
    public required UserProfileDto User { get; init; }
}

public sealed record UserProfileDto
{
    public required Guid Id { get; init; }
    public required string UserName { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Public view of another user. Deliberately excludes email.</summary>
public sealed record UserSummaryDto
{
    public required Guid Id { get; init; }
    public required string UserName { get; init; }
    public required string DisplayName { get; init; }
}

public sealed record UpdateProfileRequest
{
    [Required]
    [StringLength(Limits.DisplayNameMaxLength, MinimumLength = 1)]
    public string DisplayName { get; init; } = string.Empty;
}

public sealed record DeleteAccountRequest
{
    [Required]
    [StringLength(Limits.PasswordMaxLength)]
    public string Password { get; init; } = string.Empty;
}
