namespace Veil.Api.Services;

/// <summary>
/// Delivers password reset tokens. Plug a real provider (SMTP, SES, Resend,
/// SendGrid...) in by implementing this interface and registering it.
/// </summary>
public interface IVeilEmailSender
{
    Task SendPasswordResetAsync(string email, string resetToken, CancellationToken ct);
}

/// <summary>
/// Development only: writes the reset token to the log so the flow can be
/// tested locally without an email provider. Never registered in Production.
/// </summary>
public sealed class DevelopmentLogEmailSender(ILogger<DevelopmentLogEmailSender> logger) : IVeilEmailSender
{
    public Task SendPasswordResetAsync(string email, string resetToken, CancellationToken ct)
    {
        logger.LogWarning("[DEVELOPMENT ONLY] Password reset token for {Email}: {Token}", email, resetToken);
        return Task.CompletedTask;
    }
}

/// <summary>Production default until a provider is configured. Never logs the token.</summary>
public sealed class NotConfiguredEmailSender(ILogger<NotConfiguredEmailSender> logger) : IVeilEmailSender
{
    public Task SendPasswordResetAsync(string email, string resetToken, CancellationToken ct)
    {
        logger.LogError("A password reset was requested but no email provider is configured. Implement IVeilEmailSender.");
        return Task.CompletedTask;
    }
}
