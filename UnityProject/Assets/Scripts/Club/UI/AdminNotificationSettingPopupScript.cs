using System;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

/// <summary>
/// Admin ▸ Notification Setting. Three independent flags, each saved the moment it flips
/// (PUT /notification-settings with only the changed key). Optimistic: the switch sprite
/// changes immediately and reverts if the call fails.
/// Switches are Button + Image sprite swap (no Unity Toggle).
/// Backend is LIVE for this feature.
/// </summary>
public class AdminNotificationSettingPopupScript : MonoBehaviour
{
    private const string KeyClubApplicants = "clubApplicants";
    private const string KeyMemberLeave    = "memberLeave";
    private const string KeyChipsRequest   = "chipsRequest";

    [Serializable]
    public class SettingSwitch
    {
        public Button Button;    // the tappable switch
        public Image  Image;     // graphic whose sprite swaps on/off

        [NonSerialized] public bool IsOn;
    }

    [Header("Header")]
    public Button Close_Button;

    [Header("Switch Sprites")]
    public Sprite On_Sprite;
    public Sprite Off_Sprite;

    [Header("Switches")]
    public SettingSwitch ClubApplicants;
    public SettingSwitch MemberLeave;
    public SettingSwitch ChipsRequest;

    private void Start()
    {
        if (Close_Button != null) Close_Button.onClick.AddListener(Close);

        Bind(ClubApplicants, KeyClubApplicants);
        Bind(MemberLeave,    KeyMemberLeave);
        Bind(ChipsRequest,   KeyChipsRequest);
    }

    private void Bind(SettingSwitch sw, string key)
    {
        if (sw == null || sw.Button == null) return;
        sw.Button.onClick.RemoveAllListeners();
        sw.Button.onClick.AddListener(() => Save(sw, key, !sw.IsOn).Forget());
    }

    private void OnEnable()
    {
        SetInteractable(false);
        Load().Forget();
    }

    private async UniTaskVoid Load()
    {
        try
        {
            var s = await ClubManager.Instance
                .GetNotificationSettingsAsync(ClubContext.ClubId);
            Apply(s);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdminNotificationSettingPopupScript] load error: {e.Message}");
            ShowToast("Failed to load notification settings");
        }
        finally
        {
            SetInteractable(true);
        }
    }

    private void Apply(NotificationSettingsData s)
    {
        if (s == null) return;
        SetState(ClubApplicants, s.ClubApplicants);
        SetState(MemberLeave,    s.MemberLeave);
        SetState(ChipsRequest,   s.ChipsRequest);
    }

    // Single place that owns "state → sprite".
    private void SetState(SettingSwitch sw, bool isOn)
    {
        if (sw == null) return;
        sw.IsOn = isOn;
        if (sw.Image != null && On_Sprite != null && Off_Sprite != null)
            sw.Image.sprite = isOn ? On_Sprite : Off_Sprite;
    }

    private async UniTaskVoid Save(SettingSwitch sw, string key, bool value)
    {
        bool previous = sw.IsOn;

        SetState(sw, value);                 // optimistic
        if (sw.Button != null) sw.Button.interactable = false;

        try
        {
            var s = await ClubManager.Instance
                .UpdateNotificationSettingAsync(ClubContext.ClubId, key, value);

            // Server echoes the full row — resync all three in case of coupled rules.
            Apply(s);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdminNotificationSettingPopupScript] save {key} error: {e.Message}");
            SetState(sw, previous);          // revert
            ShowToast(string.IsNullOrEmpty(e.Message) ? "Failed to save setting" : e.Message);
        }
        finally
        {
            if (sw.Button != null) sw.Button.interactable = true;
        }
    }

    private void SetInteractable(bool on)
    {
        if (ClubApplicants?.Button != null) ClubApplicants.Button.interactable = on;
        if (MemberLeave?.Button    != null) MemberLeave.Button.interactable    = on;
        if (ChipsRequest?.Button   != null) ChipsRequest.Button.interactable   = on;
    }

    private void Close()
    {
        gameObject.SetActive(false);
    }

    // Same toast used across the club screens (Data/Export).
    private void ShowToast(string message)
    {
        if (InformationPrefabScript.Instance != null)
            InformationPrefabScript.Instance.ShowMessage(message);
    }
}
