using System;

namespace ClubPoker.Networking
{
    // Base exception for all API errors
    public class ApiException : Exception
    {
        public string Code { get; }
        public int? LockoutRemainingSeconds { get; }

        public ApiException(string code, string message, int? lockoutSeconds = null)
            : base(message)
        {
            Code = code;
            LockoutRemainingSeconds = lockoutSeconds;
        }
    }

    // A001-A007 Auth errors
    public class AuthException : ApiException
    {
        public AuthException(string code, string message)
            : base(code, message) { }
    }

    // G001-G015 Game errors
    public class GameException : ApiException
    {
        public GameException(string code, string message, int? lockoutSeconds = null)
            : base(code, message, lockoutSeconds) { }
    }

    // V001-V010 Validation errors
    public class ValidationException : ApiException
    {
        public ValidationException(string code, string message)
            : base(code, message) { }
    }

    // E001-E005 Economy errors
    public class EconomyException : ApiException
    {
        public EconomyException(string code, string message)
            : base(code, message) { }
    }

    // L001-L003 Lobby errors
    public class LobbyException : ApiException
    {
        public LobbyException(string code, string message)
            : base(code, message) { }
    }

    // S001-S004 System errors
    public class SystemApiException : ApiException
    {
        public SystemApiException(string code, string message)
            : base(code, message) { }
    }

    // Unknown/Network errors
    public class NetworkException : ApiException
    {
        public NetworkException(string code, string message)
            : base(code, message) { }
    }
}