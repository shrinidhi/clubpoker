

using Cysharp.Threading.Tasks;

namespace ClubPoker.Networking
{
    public interface IAuthProvider
    {
        UniTask<bool> RefreshSessionAsync();
        UniTask LogoutAsync(bool callServer = true);

        /// <summary>The server ended this session (CP-337). reason: "superseded"
        /// (logged in on another device), "password_changed", or null when unknown.</summary>
        void HandleSessionRevoked(string reason);
    }
}