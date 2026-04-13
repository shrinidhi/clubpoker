

using Cysharp.Threading.Tasks;

namespace ClubPoker.Networking
{
    public interface IAuthProvider
    {
        UniTask<bool> RefreshSessionAsync();
        UniTask LogoutAsync(bool callServer = true);
    }
}