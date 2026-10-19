namespace Veil.Contracts;

/// <summary>
/// Single source of truth for size limits. Used by server validation, database
/// column sizes, and client-side validation in the mobile app.
/// </summary>
public static class Limits
{
    public const int UserNameMinLength = 3;
    public const int UserNameMaxLength = 32;
    public const string UserNamePattern = "^[a-zA-Z0-9_.]+$";

    public const int DisplayNameMaxLength = 64;
    public const int EmailMaxLength = 254;

    public const int PasswordMinLength = 10;
    public const int PasswordMaxLength = 128;

    public const int MessageBodyMaxLength = 4000;
    public const int ContentTypeMaxLength = 64;

    public const int SearchQueryMinLength = 3;
    public const int SearchMaxResults = 20;

    public const int ConversationPageMaxSize = 50;
    public const int MessagePageMaxSize = 100;
    public const int SyncPageMaxSize = 500;

    public const int RefreshTokenMaxLength = 512;
    public const int ResetTokenMaxLength = 2048;
}
