

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using ClubPoker.Auth;
using ClubPoker.Core;

namespace ClubPoker.UI
{
    public class RegisterView : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Input Fields")]
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private TMP_InputField passwordInput;

        [Header("Error Display")]
        [SerializeField] private TextMeshProUGUI usernameErrorText;
        [SerializeField] private TextMeshProUGUI emailErrorText;
        [SerializeField] private TextMeshProUGUI passwordErrorText;

        [Header("Buttons")]
        [SerializeField] private Button registerButton;
        [SerializeField] private Button loginButton;

        [Header("Bonus")]
        [SerializeField] private TextMeshProUGUI bonusText;

        [Header("Loading")]
        [SerializeField] private GameObject loadingOverlay;

        #endregion

        #region Constants

        private const float ERROR_SHAKE_DURATION  = 0.4f;
        private const float ERROR_SHAKE_STRENGTH  = 12f;
        private const int   ERROR_SHAKE_VIBRATO   = 20;
        private const float BONUS_ANIMATION_DELAY = 0.5f;
        private const float BONUS_DISPLAY_SECONDS = 2f;
        private const float LOBBY_NAVIGATE_DELAY  = 1.5f;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            ResetView();
            BindButtons();
        }

        #endregion

        #region Setup

        private void BindButtons()
        {
            registerButton.onClick.AddListener(OnRegisterClicked);
            loginButton.onClick.AddListener(OnLoginClicked);

            // Clear field errors as the user types
            usernameInput.onValueChanged.AddListener(_ => ClearFieldError(usernameErrorText));
            emailInput.onValueChanged.AddListener(_ => ClearFieldError(emailErrorText));
            passwordInput.onValueChanged.AddListener(_ => ClearFieldError(passwordErrorText));
        }

        private void ResetView()
        {
            ClearAllErrors();
            SetLoading(false);
            bonusText.gameObject.SetActive(false);
            usernameInput.text = string.Empty;
            emailInput.text    = string.Empty;
            passwordInput.text = string.Empty;
        }

        #endregion

        #region Button Handlers

        private async void OnRegisterClicked()
        {
            if (!ValidateInputs()) return;

            SetLoading(true);
            ClearAllErrors();

            RegisterResult result = await AuthManager.Instance.RegisterAsync(
                usernameInput.text.Trim(),
                emailInput.text.Trim(),
                passwordInput.text);

            SetLoading(false);

            if (result.Success)
            {
                OnRegisterSuccess();
                return;
            }

            HandleRegisterError(result);
        }

        private void OnLoginClicked()
        {
            GameSceneManager.Instance.LoadScene("Scene_Login");
        }

        #endregion

        #region Success

        private void OnRegisterSuccess()
        {
            // Disable inputs so user can't tap again
            SetButtonsInteractable(false);

            // Show bonus chip animation then navigate to Lobby
            StartCoroutine(ShowBonusAndNavigate());
        }

        private IEnumerator ShowBonusAndNavigate()
        {
            yield return new WaitForSeconds(BONUS_ANIMATION_DELAY);

            // Show bonus text with punch scale animation
            int chips = AuthManager.Instance.Session.WalletChips;
            bonusText.text = $"+{chips} chips bonus!";
            bonusText.gameObject.SetActive(true);
            bonusText.transform
                .DOPunchScale(Vector3.one * 0.3f, 0.5f, 5, 0.5f)
                .SetEase(Ease.OutBack);

            yield return new WaitForSeconds(LOBBY_NAVIGATE_DELAY);

            GameSceneManager.Instance.LoadScene("Scene_Lobby");
        }

        #endregion

        #region Error Handling

        private void HandleRegisterError(RegisterResult result)
        {
            switch (result.ErrorCode)
            {
                case "U001":
                    // Duplicate username — highlight username field
                    ShowFieldError(usernameErrorText, result.ErrorMessage);
                    ShakeField(usernameInput.GetComponent<RectTransform>());
                    break;

                case "U002":
                    // Duplicate email — highlight email field
                    ShowFieldError(emailErrorText, result.ErrorMessage);
                    ShakeField(emailInput.GetComponent<RectTransform>());
                    break;

                case "V001":
                    // Validation error e.g. weak password
                    ShowFieldError(passwordErrorText, result.ErrorMessage);
                    ShakeField(passwordInput.GetComponent<RectTransform>());
                    break;

                case "A007":
                    // Rate limited
                    ShowFieldError(passwordErrorText, result.ErrorMessage);
                    break;

                default:
                    ShowFieldError(passwordErrorText, result.ErrorMessage);
                    break;
            }
        }

        private void ShowFieldError(TextMeshProUGUI errorText, string message)
        {
            errorText.text = message;
            errorText.gameObject.SetActive(true);
        }

        private void ClearFieldError(TextMeshProUGUI errorText)
        {
            errorText.gameObject.SetActive(false);
            errorText.text = string.Empty;
        }

        private void ClearAllErrors()
        {
            ClearFieldError(usernameErrorText);
            ClearFieldError(emailErrorText);
            ClearFieldError(passwordErrorText);
        }

        #endregion

        #region Validation

        private bool ValidateInputs()
        {
            bool valid = true;

            if (string.IsNullOrWhiteSpace(usernameInput.text))
            {
                ShakeField(usernameInput.GetComponent<RectTransform>());
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(emailInput.text))
            {
                ShakeField(emailInput.GetComponent<RectTransform>());
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(passwordInput.text))
            {
                ShakeField(passwordInput.GetComponent<RectTransform>());
                valid = false;
            }

            return valid;
        }

        #endregion

        #region UI Helpers

        private void SetLoading(bool isLoading)
        {
            if (loadingOverlay != null)
                loadingOverlay.SetActive(isLoading);

            SetButtonsInteractable(!isLoading);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            registerButton.interactable = interactable;
            loginButton.interactable    = interactable;
        }

        private void ShakeField(RectTransform rectTransform)
        {
            rectTransform.DOShakeAnchorPos(
                ERROR_SHAKE_DURATION,
                ERROR_SHAKE_STRENGTH,
                ERROR_SHAKE_VIBRATO,
                randomness: 0f,
                snapping: false,
                fadeOut: true);
        }

        #endregion
    }
}