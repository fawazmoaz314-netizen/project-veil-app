namespace Veil.Contracts;

/// <summary>
/// HTTP routes and SignalR hub contract shared by the server and the mobile
/// client, so the two cannot silently drift apart.
/// </summary>
public static class ApiRoutes
{
    public const string Register = "api/auth/register";
    public const string Login = "api/auth/login";
    public const string Refresh = "api/auth/refresh";
    public const string Logout = "api/auth/logout";
    public const string ForgotPassword = "api/auth/forgot-password";
    public const string ResetPassword = "api/auth/reset-password";

    public const string Me = "api/users/me";
    public const string UserSearch = "api/users/search";

    public const string Conversations = "api/conversations";
    public const string DirectConversations = "api/conversations/direct";

    public static string Conversation(Guid id) => $"api/conversations/{id}";
    public static string Messages(Guid conversationId) => $"api/conversations/{conversationId}/messages";
    public static string MarkRead(Guid conversationId) => $"api/conversations/{conversationId}/read";

    public const string SyncMessages = "api/sync/messages";

    public const string HealthLive = "health/live";
    public const string HealthReady = "health/ready";
}

public static class ChatHubContract
{
    public const string Path = "/hubs/chat";

    /// <summary>Methods the client invokes on the server.</summary>
    public static class Server
    {
        /// <summary>(Guid conversationId, SendMessageRequest request) returns MessageDto.</summary>
        public const string SendMessage = "SendMessage";

        /// <summary>(Guid conversationId, long lastReadSequence).</summary>
        public const string MarkRead = "MarkRead";

        /// <summary>(Guid conversationId).</summary>
        public const string Typing = "Typing";
    }

    /// <summary>Events the server pushes to clients.</summary>
    public static class Client
    {
        /// <summary>Payload: MessageDto.</summary>
        public const string MessageReceived = "MessageReceived";

        /// <summary>Payload: ReadReceiptDto.</summary>
        public const string ReadReceiptUpdated = "ReadReceiptUpdated";

        /// <summary>Payload: TypingDto.</summary>
        public const string UserTyping = "UserTyping";

        /// <summary>Payload: ConversationDto (from the recipient's point of view).</summary>
        public const string ConversationCreated = "ConversationCreated";
    }
}
