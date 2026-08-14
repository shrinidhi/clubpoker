using ClubPoker.Core;

namespace ClubPoker.Game
{
    /// <summary>
    /// Single owner of "where do we go when we leave the table". Two different
    /// exits share it:
    ///
    ///   Back  — keep the seat (sit out), leave the screen. Re-entering resumes.
    ///   Exit  — release the seat (leave table), then leave the screen.
    ///
    /// Both land on the screen the player came from: the club for a club table,
    /// the main menu for a lobby / friend table.
    /// </summary>
    public static class TableExitRouter
    {
        private const string SceneMainMenu = "Scene_MainMenu";

        /// <summary>Addressable scene key to return to. Falls back to the main menu
        /// when the context was never populated (e.g. a cold-start reconnect that
        /// found nothing on disk).</summary>
        public static string BackDestination =>
            string.IsNullOrEmpty(TableContext.BackScene)
                ? SceneMainMenu
                : TableContext.BackScene;

        /// <summary>Label for the "leave the screen but keep the seat" option.</summary>
        public static string BackLabel =>
            TableContext.IsClub ? "Back to Club" : "Back to Home";

        /// <summary>Navigate back without touching the seat.</summary>
        public static void GoBack()
        {
            GameSceneManager.Instance.LoadScene(BackDestination);
        }

        /// <summary>Navigate back and drop the table context — the seat is gone.
        /// Reads the destination before clearing, since clearing resets it.</summary>
        public static void GoBackAndClear()
        {
            string destination = BackDestination;
            TableContext.Clear();
            GameSceneManager.Instance.LoadScene(destination);
        }
    }
}
