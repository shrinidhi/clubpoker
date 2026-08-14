using ClubPoker.Networking.Models;
using UnityEngine;

/// <summary>Where the player entered the current table from. Drives which in-game
/// menu options are offered and where "Back"/"Exit" navigate to.</summary>
public enum TableOrigin
{
    Lobby = 0,
    Club  = 1
}

/// <summary>
/// The subset of table metadata the in-game screens need, filled from whichever
/// model the entry point had (ClubTableData for club tables, TableData for lobby
/// tables). Lets RealTimeResult / HandHistory read one shape in both contexts.
/// </summary>
public class TableInfo
{
    public string TableId;
    public string Name;
    public string Variant;
    public int    SmallBlind;
    public int    BigBlind;
    public int    BuyInMin;
    public int    BuyInMax;

    /// Club tables only — lobby tables have no creator, so creator-only controls
    /// (kick, observer panel) stay hidden there.
    public string CreatedById;

    public string ClubId;
}

/// <summary>
/// Table-scoped state that outlives the scene load into Scene_GameTable. Written
/// once by whichever screen starts the join, read by the in-game screens.
/// </summary>
public static class TableContext
{
    private const string PrefOrigin    = "table_origin";
    private const string PrefBackScene = "table_back_scene";
    private const string PrefClubId    = "table_club_id";

    private const string SceneMainMenu = "Scene_MainMenu";

    /// <summary>Table metadata for the table we're seated at / watching.</summary>
    public static TableInfo Info { get; private set; }

    public static TableOrigin Origin { get; private set; } = TableOrigin.Lobby;

    /// <summary>Addressable scene key to return to on Back / Exit.</summary>
    public static string BackScene { get; private set; } = SceneMainMenu;

    public static string ClubId => Info?.ClubId;

    public static bool IsClub => Origin == TableOrigin.Club;

    /// <summary>Live table id. Kept as a field of its own because the join flow
    /// learns it before the metadata in some paths (join-by-code).</summary>
    public static string TableId { get; private set; }

    // ── Entry points ────────────────────────────────────────────────────────

    /// <summary>Lobby / main-menu / friend-table join. <paramref name="data"/> may be
    /// null when the entry point only has a table id (join by code).</summary>
    public static void EnterFromLobby(string tableId, TableData data = null,
                                      string backScene = null)
    {
        Origin    = TableOrigin.Lobby;
        BackScene = string.IsNullOrEmpty(backScene) ? CurrentSceneKey() : backScene;
        TableId   = tableId;

        Info = data == null
            ? new TableInfo { TableId = tableId }
            : new TableInfo
            {
                TableId    = tableId,
                Name       = data.Name,
                Variant    = data.Variant,
                SmallBlind = data.SmallBlind,
                BigBlind   = data.BigBlind,
                BuyInMin   = data.MinBuyIn,
                BuyInMax   = data.MaxBuyIn
            };

        Save();
    }

    /// <summary>Club table join. <paramref name="tableId"/> is the real lobby table
    /// id, which differs from the club table row id.</summary>
    public static void EnterFromClub(ClubTableData table, string tableId,
                                     string backScene = null)
    {
        Origin    = TableOrigin.Club;
        BackScene = string.IsNullOrEmpty(backScene) ? CurrentSceneKey() : backScene;
        TableId   = tableId;

        Info = table == null
            ? new TableInfo { TableId = tableId }
            : new TableInfo
            {
                TableId     = tableId,
                Name        = table.Name,
                Variant     = table.Variant,
                SmallBlind  = table.SmallBlind,
                BigBlind    = table.BigBlind,
                BuyInMin    = table.BuyInMin,
                BuyInMax    = table.BuyInMax,
                CreatedById = table.CreatedById,
                ClubId      = table.ClubId
            };

        Save();
    }

    /// <summary>Full leave — the seat is gone, so drop everything.</summary>
    public static void Clear()
    {
        Origin    = TableOrigin.Lobby;
        BackScene = SceneMainMenu;
        TableId   = null;
        Info      = null;

        PlayerPrefs.DeleteKey(PrefOrigin);
        PlayerPrefs.DeleteKey(PrefBackScene);
        PlayerPrefs.DeleteKey(PrefClubId);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Addressable key of the scene the join was started from — that's where Back
    /// and Exit return to. Club tables can be opened from the club screen inside
    /// ClubScene *or* from its copy inside MainMenu, so the origin enum alone
    /// doesn't pin the destination down.
    /// GameSceneManager loads by Addressables address, which isn't the scene's own
    /// name, hence the map.
    /// </summary>
    private static string CurrentSceneKey()
    {
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        switch (scene)
        {
            case "ClubScene":   return "Scene_Club";
            case "LobbyScene":  return "Scene_Lobby";
            case "MainMenu":    return SceneMainMenu;
            default:            return SceneMainMenu;
        }
    }

    // ── Cold-start survival ─────────────────────────────────────────────────
    // Statics die when the process is killed. A reconnect after that drops the
    // player straight into the table with no entry screen having run, so the
    // origin has to come back from disk or Back/Exit would misroute to the lobby.

    private static void Save()
    {
        PlayerPrefs.SetInt(PrefOrigin, (int)Origin);
        PlayerPrefs.SetString(PrefBackScene, BackScene);
        PlayerPrefs.SetString(PrefClubId, Info?.ClubId ?? "");
        PlayerPrefs.Save();
    }

    /// <summary>Restore origin + back destination after a cold start. Only fills
    /// what's missing — an entry point that already ran wins.</summary>
    public static void Restore()
    {
        if (Info != null) return;

        Origin    = (TableOrigin)PlayerPrefs.GetInt(PrefOrigin, (int)TableOrigin.Lobby);
        BackScene = PlayerPrefs.GetString(PrefBackScene, SceneMainMenu);

        string clubId = PlayerPrefs.GetString(PrefClubId, "");
        if (!string.IsNullOrEmpty(clubId))
            Info = new TableInfo { TableId = TableId, ClubId = clubId };
    }
}
