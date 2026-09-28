using System;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.Game
{
    /// <summary>
    /// What an unoccupied seat shows. Which one depends on the viewer, not on the
    /// seat: a seated player can't take a second seat, so every empty seat is just
    /// "this table has room" (Open). Only someone without a seat — stood up, sat
    /// out past the limit, busted, or watching — gets the tappable "+".
    /// </summary>
    public enum EmptySeatState
    {
        Hidden,  // someone is sitting here
        Open,    // empty, but I already hold a seat
        Plus     // empty and I have no seat — tap to buy in
    }

    /// <summary>
    /// One empty-seat marker, parented to the same slot Transform as the player
    /// card that would sit there. Lives for as long as the layout does and toggles
    /// its two children — spawning and destroying it per state_update would fight
    /// the seat diff in PokerTableUI.
    /// </summary>
    public class EmptySeatView : MonoBehaviour
    {
        [Header("States")]
        // The "+" — tappable. Starts the buy-in.
        public GameObject plusRoot;
        // "OPEN" — a label, not a button. There is nothing to tap: the server picks
        // the seat, so tapping a particular one would promise something we can't keep.
        public GameObject openRoot;

        [Header("Refs")]
        public Button plusButton;

        /// Absolute seat number, as the server numbers seats — not an index into
        /// players[]. A two-handed table can be sitting in seats 0 and 5.
        public int Seat { get; private set; }

        private Action<int> _onTake;

        public void Init(int seat, Action<int> onTake)
        {
            Seat = seat;
            _onTake = onTake;

            if (plusButton != null)
            {
                plusButton.onClick.RemoveAllListeners();
                plusButton.onClick.AddListener(() => _onTake?.Invoke(Seat));
            }

            SetState(EmptySeatState.Hidden);
        }

        public void SetState(EmptySeatState state)
        {
            if (plusRoot != null)
                plusRoot.SetActive(state == EmptySeatState.Plus);

            if (openRoot != null)
                openRoot.SetActive(state == EmptySeatState.Open);
        }

        /// Held down while a buy-in is in flight so a second tap can't start a
        /// second one — on this seat or any other.
        public void SetInteractable(bool interactable)
        {
            if (plusButton != null)
                plusButton.interactable = interactable;
        }
    }
}
