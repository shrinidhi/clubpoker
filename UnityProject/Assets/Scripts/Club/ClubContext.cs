using UnityEngine;

namespace ClubPoker.Club
{
    public enum ClubRole { Member, Agent, Manager, Creator }

    /// <summary>
    /// Runtime contract between club scene and chip economy panels.
    /// Set this when navigating to Club Home. All club panels read from here.
    /// </summary>
    public class ClubContext : MonoBehaviour
    {
        public static ClubContext Instance { get; private set; }

        public string   ClubId       { get; private set; }
        public string   ClubName     { get; private set; }
        public long     PoolChips    { get; private set; }
        public long     MembersChips { get; private set; }
        public long     AgentsCredit { get; private set; }
        public ClubRole UserRole     { get; private set; }

        public bool IsAdmin => UserRole == ClubRole.Creator || UserRole == ClubRole.Manager;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Set(string clubId, string clubName, ClubRole role,
                        long poolChips, long membersChips, long agentsCredit)
        {
            ClubId       = clubId;
            ClubName     = clubName;
            UserRole     = role;
            PoolChips    = poolChips;
            MembersChips = membersChips;
            AgentsCredit = agentsCredit;
        }

        public void UpdatePoolChips(long pool, long members, long agents)
        {
            PoolChips    = pool;
            MembersChips = members;
            AgentsCredit = agents;
        }

        public void Clear()
        {
            ClubId = null;
            ClubName = null;
            PoolChips = MembersChips = AgentsCredit = 0;
            UserRole = ClubRole.Member;
        }
    }
}
