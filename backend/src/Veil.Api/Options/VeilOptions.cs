using System.ComponentModel.DataAnnotations;

namespace Veil.Api.Options;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    [Required]
    public string Issuer { get; set; } = "veil-api";

    [Required]
    public string Audience { get; set; } = "veil-mobile";

    /// <summary>HMAC-SHA256 key. Must be at least 32 bytes. Supply via Jwt__SigningKey; never commit it.</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 60)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 30;
}

public sealed class RateLimitOptions
{
    public const string Section = "RateLimiting";

    /// <summary>Login, register, refresh, password reset: per client IP.</summary>
    [Range(1, 10_000)]
    public int AuthPermitsPerMinute { get; set; } = 10;

    /// <summary>All other HTTP requests: per user (or per IP when anonymous).</summary>
    [Range(1, 100_000)]
    public int ApiPermitsPerMinute { get; set; } = 300;

    /// <summary>Message sends over HTTP or SignalR: per user.</summary>
    [Range(1, 10_000)]
    public int MessagesPerMinute { get; set; } = 60;

    /// <summary>Typing indicators over SignalR: per user.</summary>
    [Range(1, 10_000)]
    public int TypingEventsPerMinute { get; set; } = 30;
}

public sealed class CorsSettings
{
    public const string Section = "Cors";

    /// <summary>Only needed for browser clients. The native mobile app does not use CORS.</summary>
    public string[] AllowedOrigins { get; set; } = [];
}

public sealed class ReverseProxyOptions
{
    public const string Section = "ReverseProxy";

    /// <summary>
    /// Trust X-Forwarded-For / X-Forwarded-Proto. Enable ONLY when the API is
    /// reachable exclusively through your hosting provider's proxy, otherwise
    /// clients can spoof their IP and bypass per-IP rate limits.
    /// </summary>
    public bool Enabled { get; set; }
}
