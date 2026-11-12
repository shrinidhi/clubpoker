namespace ClubPoker.Core
{
    /// <summary>
    /// Where the legal pages live. One place for every screen that links to them —
    /// the Login/Register footers (TMPLinkHandler) and the MainMenu drawer rows —
    /// so a changed URL can't end up updated on one screen and stale on another.
    ///
    /// Empty = not published yet: the link does nothing rather than open a dead page.
    /// </summary>
    public static class LegalLinks
    {
        // TODO: set the published pages before release (both stores require a
        // working privacy policy link).
        public const string TermsUrl = "https://poker.anivalegames.com/terms-of-service.html";
        public const string PrivacyUrl = "https://poker.anivalegames.com/privacy-policy.html";
    }
}
