using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;

namespace ClubPoker.UI
{
    /// <summary>
    /// "Enter a nickname" popup shown once on the main menu right after sign-up,
    /// pre-filled with the server's default nickname. Put this on the popup root.
    /// Confirm saves it as the nickname; Cancel / X
    /// skips — the player can still set it later from the profile screen.
    ///
    /// The pending flag lives in PlayerPrefs, keyed by player id, so killing the
    /// app between Register and MainMenu still shows it on next launch, and a
    /// different account on the same device never sees someone else's prompt.
    /// </summary>
    public class NicknamePromptPanel : MonoBehaviour
    {
        [SerializeField] private TMP_InputField nicknameInput;
        [SerializeField] private Button         confirmButton;
        [SerializeField] private Button         cancelButton;
        [SerializeField] private Button         closeButton;

        private const string PENDING_KEY_PREFIX = "NicknamePrompt.Pending.";

        private bool _saving;

        #region Pending flag

        /// <summary>Called by RegisterView on a successful sign-up.</summary>
        public static void MarkPending(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            PlayerPrefs.SetInt(PENDING_KEY_PREFIX + playerId, 1);
            PlayerPrefs.Save();
        }

        private static bool IsPending(string playerId) =>
            !string.IsNullOrEmpty(playerId) &&
            PlayerPrefs.GetInt(PENDING_KEY_PREFIX + playerId, 0) == 1;

        private static void ClearPending(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            PlayerPrefs.DeleteKey(PENDING_KEY_PREFIX + playerId);
            PlayerPrefs.Save();
        }

        #endregion

        // Sits on the popup root, which starts inactive: Awake runs when TryShow
        // first activates it, so the listeners are in place before any tap.
        private void Awake()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
            if (cancelButton != null)  cancelButton.onClick.AddListener(Skip);
            if (closeButton != null)   closeButton.onClick.AddListener(Skip);
        }

        /// <summary>Show the prompt if this player just signed up. Guests never see it.</summary>
        public void TryShow()
        {
            var session = AuthManager.Instance != null ? AuthManager.Instance.Session : null;
            if (session == null || session.IsGuest || !IsPending(session.Id))
                return;

            if (nicknameInput != null)
            {
                nicknameInput.characterLimit = NicknameRules.MaxLength;
                // Server's default nickname ("CP" + player code); username only
                // if the server didn't send one.
                nicknameInput.text = string.IsNullOrEmpty(session.Nickname)
                    ? session.Username
                    : session.Nickname;
            }

            gameObject.SetActive(true);
        }

        private void Skip()
        {
            if (_saving) return;

            var session = AuthManager.Instance != null ? AuthManager.Instance.Session : null;
            if (session != null) ClearPending(session.Id);

            gameObject.SetActive(false);
        }

        private async void OnConfirm()
        {
            if (_saving || nicknameInput == null) return;

            string nickname = nicknameInput.text.Trim();

            string error = NicknameRules.Validate(nickname);
            if (error != null)
            {
                InformationPrefabScript.Instance.ShowMessage(error);
                return;
            }

            var session = AuthManager.Instance.Session;

            _saving = true;

            try
            {
                UpdateProfileData result =
                    await AuthManager.Instance.UpdateNicknameAsync(nickname);

                // Only count it saved if the server echoes it back; otherwise the
                // player can retry or skip.
                if (result != null && result.Nickname == nickname)
                {
                    ClearPending(session.Id);
                    gameObject.SetActive(false);
                    InformationPrefabScript.Instance.ShowMessage("Nickname saved.");
                }
                else
                {
                    InformationPrefabScript.Instance.ShowMessage(
                        "Nickname could not be saved. Please try again later.");
                }
            }
            catch (Exception ex)
            {
                InformationPrefabScript.Instance.ShowMessage(ex.Message);
            }
            finally
            {
                _saving = false;
            }
        }
    }
}
