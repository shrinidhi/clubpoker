namespace ClubPoker.Core
{
    public enum ShopTab { Gold, Diamond, Item }

    /// <summary>
    /// Which tab the shop opens on. Set by a caller just before activating the
    /// shop screen (e.g. the Lobby's top-bar diamond / gold panels); the shop
    /// consumes it in OnEnable and falls back to Gold when nothing was asked for.
    /// Lives in Core so assemblies that can't see the shop script can still ask.
    /// </summary>
    public static class ShopNavigation
    {
        private static ShopTab? _requested;

        public static void Request(ShopTab tab) => _requested = tab;

        public static ShopTab Consume()
        {
            ShopTab tab = _requested ?? ShopTab.Gold;
            _requested = null;
            return tab;
        }
    }
}
