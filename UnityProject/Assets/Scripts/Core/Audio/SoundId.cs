namespace ClubPoker.Core
{
    /// <summary>
    /// Every sound the game can play. Values are serialized into SoundLibrary.asset,
    /// so never renumber or reuse one — append new ids with a fresh value.
    /// </summary>
    public enum SoundId
    {
        None = 0,

        // UI
        ButtonClick   = 1,
        Notification  = 2,   // toast
        Coins         = 3,   // purchase / reward

        // Cards
        CardDeal      = 10,  // hole cards
        CardFlip      = 11,  // flop / turn / river

        // Player actions
        Check         = 20,
        Fold          = 21,
        Call          = 22,
        Raise         = 23,  // bet or raise
        AllIn         = 24,

        // Chips / pot
        ChipsToPot    = 30,  // end-of-street collect
        PotWin        = 31,  // pot slides to the winner

        // Turn
        YourTurn      = 40,
        TimerWarning  = 41,  // one tick per second, last few seconds of your turn
        TimeBank      = 42,

        // Table
        DealerButton  = 50,  // new hand
        PlayerJoin    = 51,
        PlayerLeave   = 52,
        ChatMessage   = 53,

        // Results
        HandWon       = 60,  // local player won the hand
        Busted        = 61,  // local player eliminated
        Jackpot       = 62,

        // Music (looping, played through GameAudio.PlayMusic / scene music)
        MusicMenu     = 70,  // main menu, lobby, club, profile
        MusicTable    = 71,  // at a table
    }
}
