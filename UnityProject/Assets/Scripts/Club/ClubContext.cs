public enum ClubRole { Member, Agent, Manager, Creator }

public static class ClubContext
{
    public static string   ClubId       { get; private set; }
    public static string   ClubName     { get; private set; }
    public static long     PoolChips    { get; private set; }
    public static long     MembersChips { get; private set; }
    public static long     AgentsCredit { get; private set; }
    public static ClubRole UserRole     { get; private set; }

    public static bool IsAdmin      => UserRole == ClubRole.Creator;
    public static bool AutoReject   { get; set; }
    public static int  PendingCount { get; set; }

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
    }
}
