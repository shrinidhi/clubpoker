using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Game;
using ClubPoker.Networking.Models;

/// <summary>
/// Host Privilege popup on the game table, opened from the side drawer and only
/// offered to the club table's creator (TableContext.IsHost).
///
///   Auth-Buyin            — reversible. Turning it off asks first; turning it
///                           on is silent. Drives the key button on the table.
///   Auto Open / Auto Ext. — one-way. Turning off asks first, then the switch
///                           locks off for good; the server holds the same rule.
///   Extend Table Duration — opens ExtendTableDurationPanel.
///   Disband               — asks, deletes the club table, leaves to the club.
///
/// The switches are Buttons with an on/off sprite. A tap never flips the sprite;
/// it only changes once the server has accepted the change, so a cancelled Tips
/// or a failed call never shows a setting the table doesn't have.
/// </summary>
public class HostPrivilegePanel : MonoBehaviour
{
    // Sits on the outer container (ClubHostPrivilegePanel), which holds the
    // list, the extend popup and the Tips popup — closing hides all of it.

    [Tooltip("The Host Privilege list itself (MainPopUpPanel). Hidden while the " +
             "extend popup is up.")]
    [SerializeField] private GameObject mainPopup;

    [SerializeField] private Button closeButton;

    [Header("Settings (switch buttons)")]
    [Tooltip("Switches are plain Buttons whose Image flips between the on/off " +
             "sprites below. The Image is the button's own unless set here.")]
    [SerializeField] private Button authBuyInButton;
    [SerializeField] private Image  authBuyInImage;
    [SerializeField] private Button autoOpenButton;
    [SerializeField] private Image  autoOpenImage;
    [SerializeField] private Button autoExtendButton;
    [SerializeField] private Image  autoExtendImage;

    [SerializeField] private Sprite switchOnSprite;
    [SerializeField] private Sprite switchOffSprite;

    [Header("Locked rows")]
    [Tooltip("CanvasGroup on the whole Auto Open / Auto Extension row. Faded to " +
             "lockedAlpha once the setting is off for good.")]
    [SerializeField] private CanvasGroup autoOpenRowGroup;
    [SerializeField] private CanvasGroup autoExtendRowGroup;
    [SerializeField, Range(0f, 1f)] private float lockedAlpha = 0.7f;

    [Tooltip("Row label that carries the remaining extension credits.")]
    [SerializeField] private TextMeshProUGUI autoExtendLabel;

    [Header("Actions")]
    [SerializeField] private Button extendDurationButton;
    [SerializeField] private Button disbandButton;

    [Header("Popups")]
    [SerializeField] private AlertPopup tipsPopup;
    [SerializeField] private ExtendTableDurationPanel extendPanel;

    private const string TipsTitle = "Tips";

    private const string MsgAuthBuyInOff =
        "Confirm to turn off buy-in authorization?";
    private const string MsgAutoOpenOff =
        "Table will not be automatically opened if you turn this off, and this " +
        "setting cannot be turned on again. Confirm to turn off?";
    private const string MsgAutoExtendOff =
        "Table will not be automatically extended if you turn this off, and this " +
        "setting cannot be turned on again. Confirm to turn off?";
    private const string MsgDisband =
        "Are you sure you want to disband the table?";

    private const string AutoExtendLabelFormat = "Auto table extension ({0})";

    private const string ErrUpdate  = "Couldn't update the setting. Please try again.";
    private const string ErrDisband = "Couldn't disband the table. Please try again.";

    // One request at a time — a second tap mid-call would race the first.
    private bool _busy;

    private void Awake()
    {
        if (closeButton != null)          closeButton.onClick.AddListener(Hide);
        if (authBuyInButton != null)      authBuyInButton.onClick.AddListener(OnAuthBuyInTap);
        if (autoOpenButton != null)       autoOpenButton.onClick.AddListener(OnAutoOpenTap);
        if (autoExtendButton != null)     autoExtendButton.onClick.AddListener(OnAutoExtendTap);

        if (authBuyInImage == null)  authBuyInImage  = authBuyInButton  != null ? authBuyInButton.image  : null;
        if (autoOpenImage == null)   autoOpenImage   = autoOpenButton   != null ? autoOpenButton.image   : null;
        if (autoExtendImage == null) autoExtendImage = autoExtendButton != null ? autoExtendButton.image : null;
        if (extendDurationButton != null) extendDurationButton.onClick.AddListener(OnExtendTap);
        if (disbandButton != null)        disbandButton.onClick.AddListener(OnDisbandTap);
    }

    private GameObject MainPopup => mainPopup != null ? mainPopup : gameObject;

    private void OnEnable()
    {
        // Opened from the drawer: always land on the list, even if the container
        // was last closed from some other state.
        if (mainPopup != null) mainPopup.SetActive(true);
        if (extendPanel != null && extendPanel.gameObject != gameObject &&
            extendPanel.transform.IsChildOf(transform))
            extendPanel.gameObject.SetActive(false);

        TableContext.OnClubRowChanged += Render;
        Render();

        // Show the snapshot straight away, then correct it from the server.
        HostTableService.RefreshAsync().Forget();
    }

    private void OnDisable()
    {
        TableContext.OnClubRowChanged -= Render;
    }

    /// <summary>Close the whole Host Privilege container.</summary>
    private void Hide() => gameObject.SetActive(false);

    // ── Render ──────────────────────────────────────────────────────────────

    private void Render()
    {
        TableInfo info = TableContext.Info;
        if (info == null) return;

        RenderSwitch(authBuyInButton, authBuyInImage, info.AuthBuyIn, !_busy);

        // Once off, off for good — the switch stays visible but dead.
        RenderSwitch(autoOpenButton,   autoOpenImage,   info.AutoOpen,   !_busy && info.AutoOpen);
        RenderSwitch(autoExtendButton, autoExtendImage, info.AutoExtend, !_busy && info.AutoExtend);

        // Fade on the locked state only, not on _busy — a request in flight
        // shouldn't make the rows flicker.
        SetRowAlpha(autoOpenRowGroup,   info.AutoOpen);
        SetRowAlpha(autoExtendRowGroup, info.AutoExtend);

        if (autoExtendLabel != null)
            autoExtendLabel.text = string.Format(AutoExtendLabelFormat, info.ExtensionCredits);

        if (extendDurationButton != null) extendDurationButton.interactable = !_busy;
        if (disbandButton != null)        disbandButton.interactable = !_busy;
    }

    private void SetRowAlpha(CanvasGroup group, bool isOn)
    {
        if (group != null)
            group.alpha = isOn ? 1f : lockedAlpha;
    }

    private void RenderSwitch(Button button, Image image, bool isOn, bool interactable)
    {
        if (image != null)
        {
            Sprite sprite = isOn ? switchOnSprite : switchOffSprite;
            if (sprite != null) image.sprite = sprite;
        }

        if (button != null)
            button.interactable = interactable;
    }

    // ── Switches ────────────────────────────────────────────────────────────
    // A tap never flips the sprite itself — Render does, once the server has
    // accepted the change, so Cancel or a failed call leaves it untouched.

    private void OnAuthBuyInTap()
    {
        TableInfo info = TableContext.Info;
        if (_busy || info == null) return;

        if (!info.AuthBuyIn)
        {
            ApplyAsync(AuthManager.Instance.SetTableAuthBuyInAsync, true).Forget();
            return;
        }

        tipsPopup.Show(TipsTitle, MsgAuthBuyInOff, true, () =>
            ApplyAsync(AuthManager.Instance.SetTableAuthBuyInAsync, false).Forget());
    }

    private void OnAutoOpenTap()
    {
        TableInfo info = TableContext.Info;

        // Can't be turned back on — the button is locked once off, so this only
        // guards a stray tap.
        if (_busy || info == null || !info.AutoOpen) return;

        tipsPopup.Show(TipsTitle, MsgAutoOpenOff, true, () =>
            ApplyAsync(AuthManager.Instance.SetTableAutoOpenAsync, false).Forget());
    }

    private void OnAutoExtendTap()
    {
        TableInfo info = TableContext.Info;
        if (_busy || info == null || !info.AutoExtend) return;

        tipsPopup.Show(TipsTitle, MsgAutoExtendOff, true, () =>
            ApplyAsync(AuthManager.Instance.SetTableAutoExtendAsync, false).Forget());
    }

    /// <summary>Call one of the AuthManager setting endpoints and take the new
    /// state from what the server echoes back.</summary>
    private async UniTaskVoid ApplyAsync(
        Func<string, string, bool, UniTask<ClubTableSettingResponse>> setSetting,
        bool enabled)
    {
        TableInfo info = TableContext.Info;
        if (_busy || info == null || string.IsNullOrEmpty(info.ClubTableRowId))
            return;

        _busy = true;
        Render();

        try
        {
            ClubTableSettingResponse response =
                await setSetting(info.ClubId, info.ClubTableRowId, enabled);

            if (response != null)
            {
                if (response.AuthBuyIn.HasValue)        info.AuthBuyIn        = response.AuthBuyIn.Value;
                if (response.AutoOpen.HasValue)         info.AutoOpen         = response.AutoOpen.Value;
                if (response.AutoExtend.HasValue)       info.AutoExtend       = response.AutoExtend.Value;
                if (response.ExtensionCredits.HasValue) info.ExtensionCredits = response.ExtensionCredits.Value;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[HostPrivilege] Settings update failed: {e.Message}");
            ToastEvents.Show(ErrUpdate);
        }
        finally
        {
            _busy = false;

            // Re-renders this panel (if still open) and moves the key button.
            TableContext.NotifyClubRowChanged();
        }
    }

    // ── Rows ────────────────────────────────────────────────────────────────

    private void OnExtendTap()
    {
        if (extendPanel == null) return;

        // Swap, don't stack: hide the list while the extend popup is up. Closing
        // the extend popup (X or a successful Confirm) closes Host Privilege
        // entirely — OnEnable puts the list back for the next open.
        MainPopup.SetActive(false);
        extendPanel.Open(onClosed: () =>
        {
            if (this != null) Hide();
        });
    }

    private void OnDisbandTap()
    {
        tipsPopup.Show(TipsTitle, MsgDisband, true, () => DisbandAsync().Forget());
    }

    private async UniTaskVoid DisbandAsync()
    {
        TableInfo info = TableContext.Info;
        if (_busy || info == null || string.IsNullOrEmpty(info.ClubTableRowId))
            return;

        _busy = true;
        Render();

        try
        {
            await AuthManager.Instance.DeleteClubTableAsync(info.ClubId, info.ClubTableRowId);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[HostPrivilege] Disband failed: {e.Message}");
            ToastEvents.Show(ErrDisband);

            _busy = false;
            if (this != null) Render();
            return;
        }

        _busy = false;
        Hide();

        // The table is gone server-side — drop the socket and table state and go
        // back to the club, the same way a bust-out leaves.
        if (LeaveTableHandler.Instance != null)
            LeaveTableHandler.Instance.ExitAfterDisband();
        else
            TableExitRouter.GoBackAndClear();
    }
}
