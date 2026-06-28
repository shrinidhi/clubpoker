using System.Collections.Generic;

namespace ClubPoker.Game
{
    [System.Serializable]
    public class HandHistoryRecord
    {
        public int RoundNumber;

        public string WinningHand;

        public int PotAmount;

        public List<string> BoardCards =
            new List<string>();

        public List<HandHistoryPlayer> Players =
            new List<HandHistoryPlayer>();

        public List<HandActionRecord> Actions =
            new List<HandActionRecord>();

        public List<StreetBoardRecord> StreetBoards =
    new List<StreetBoardRecord>();
    }

    [System.Serializable]
    public class HandHistoryPlayer
    {
        public string PlayerId;

        public string Username;

        public string HandName;

        public bool IsWinner;

        public int ChipDifference;

        public List<string> HoleCards =
            new List<string>();
    }

    [System.Serializable]
    public class HandActionRecord
    {
        public string Street;

        public string PlayerId;

        public string Username;

        public string Action;

        public int Amount;

        public int PotAfter;

        public int ChipsAfter;
    }



    public class StreetBoardRecord
    {
        public string Street;

        public List<string> Cards = new List<string>();
    }
}