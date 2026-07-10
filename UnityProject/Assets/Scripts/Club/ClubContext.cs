using System;
using ClubPoker.Networking.Models;

public enum ClubRole { Member, Agent, Manager, Creator }

public static class ClubContext
{
    /// <summary>
    /// Full club detail from GET /api/clubs/{clubId}, fetched once when the club opens.
    /// Carries createdAt, feeAllocPercent, scrollMessage, chipPool… Refresh it via
    /// <see cref="SetClubDetail"/> after any PUT that changes these fields.
    /// </summary>
    public static ClubDetailData ClubDetail { get; private set; }

    // Club creation date, parsed from ClubDetail. Bounds how far back the date pickers
    // and the Data/Career range arrows may go.
    public static DateTime? ClubCreatedAt { get; private set; }

    /// Hard cap so an old club doesn't render dozens of month blocks.
    private const int MaxMonthsBack = 12;

    /// <summary>
    /// Earliest selectable day: the first of the club's creation month. Falls back to
    /// the previous month while creation date is unknown. Single source of truth —
    /// DateRangePopupView and the range arrows both read this.
    /// </summary>
    public static DateTime MinSelectableDate
    {
        get
        {
            DateTime today = DateTime.Today;
            DateTime currentMonth = new DateTime(today.Year, today.Month, 1);

            if (!ClubCreatedAt.HasValue)
                return currentMonth.AddMonths(-1);   // unknown → previous + current

            DateTime created = ClubCreatedAt.Value.ToLocalTime();
            DateTime createdMonth = new DateTime(created.Year, created.Month, 1);

            if (createdMonth > currentMonth) return currentMonth;   // created today

            DateTime floor = currentMonth.AddMonths(-MaxMonthsBack);
            return createdMonth < floor ? floor : createdMonth;
        }
    }

    /// <summary>Cache the club detail and derive the values other screens read.</summary>
    public static void SetClubDetail(ClubDetailData detail)
    {
        ClubDetail = detail;
        if (detail == null)
        {
            ClubCreatedAt = null;
            return;
        }

        ClubCreatedAt = DateTime.TryParse(detail.CreatedAt, null,
            System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime dt)
            ? dt
            : (DateTime?)null;
    }

    /// <summary>Current fee allocation %, 0 while the detail hasn't loaded.</summary>
    public static int FeeAllocPercent => ClubDetail?.FeeAllocPercent ?? 0;

    public static string   ClubId       { get; private set; }
    public static string   ClubName     { get; private set; }
    public static long     PoolChips    { get; private set; }
    public static long     MembersChips { get; private set; }
    public static long     AgentsCredit { get; private set; }
    public static ClubRole UserRole     { get; private set; }

    // Full club selected in the carousel — survives the MainMenu → ClubScene load.
    public static ClubListData SelectedClub { get; private set; }

    public static bool IsAdmin      => UserRole == ClubRole.Creator;
    public static bool AutoReject   { get; set; }
    public static int  PendingCount { get; set; }

    public static void SelectClub(ClubListData club)
    {
        SelectedClub = club;
        if (club != null)
            Set(club.ClubId, club.Name, ParseRole(club.Role), 0, 0, 0);
    }

    public static void Set(string clubId, string clubName, ClubRole role,
                           long poolChips, long membersChips, long agentsCredit)
    {
        ClubId       = clubId;
        ClubName     = clubName;
        UserRole     = role;
        PoolChips    = poolChips;
        MembersChips = membersChips;
        AgentsCredit = agentsCredit;
    }

    public static void UpdatePoolChips(long pool, long members, long agents)
    {
        PoolChips    = pool;
        MembersChips = members;
        AgentsCredit = agents;
    }

    public static ClubRole ParseRole(string role)
    {
        return role?.ToUpper() switch
        {
            "CREATOR" => ClubRole.Creator,
            "MANAGER" => ClubRole.Manager,
            "AGENT"   => ClubRole.Agent,
            _         => ClubRole.Member,
        };
    }

    public static void Clear()
    {
        ClubId = null;
        ClubName = null;
        PoolChips = MembersChips = AgentsCredit = 0;
        UserRole = ClubRole.Member;
        SelectedClub = null;
        ClubDetail = null;
        ClubCreatedAt = null;
    }
}
