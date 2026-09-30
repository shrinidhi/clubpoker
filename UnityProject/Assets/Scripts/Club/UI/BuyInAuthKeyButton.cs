using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

/// <summary>
/// The key button on the table (top-left) that the host uses to review buy-in
/// requests. Visible only while the host has Auth-Buyin on.
///
/// Put this on an ALWAYS-ACTIVE object, not on the key button itself — the
/// button is what gets hidden, and a hidden object's Start never runs.
/// </summary>
public class BuyInAuthKeyButton : MonoBehaviour
{
    [SerializeField] private Button keyButton;

    [Tooltip("Buy-in authorization request list. TODO: needs backend request " +
             "list / approve / reject endpoints before it can show anything.")]
    [SerializeField] private GameObject authListPanel;

    private void Start()
    {
        if (keyButton != null)
            keyButton.onClick.AddListener(OnKeyTap);

        // Always start closed, whatever state the scene was saved in — an
        // already-active panel would swallow the first tap (SetActive(true) on
        // an active object runs no OnEnable, so nothing slides in or loads).
        if (authListPanel != null)
            authListPanel.SetActive(false);

        TableContext.OnClubRowChanged += Apply;
        Apply();

        // The join snapshot may be stale or, after a cold start, missing the
        // settings entirely — read the row once on arrival.
        if (TableContext.IsClub)
            HostTableService.RefreshAsync().Forget();
    }

    private void OnDestroy()
    {
        TableContext.OnClubRowChanged -= Apply;
    }

    private void Apply()
    {
        bool show = TableContext.IsHost && TableContext.Info != null &&
                    TableContext.Info.AuthBuyIn;

        if (keyButton != null)
            keyButton.gameObject.SetActive(show);

        // Auth-Buyin turned off while the list was open — nothing left to review.
        if (!show && authListPanel != null)
            authListPanel.SetActive(false);
    }

    private void OnKeyTap()
    {
        Debug.Log($"[BuyInAuthKey] Tapped — panel " +
                  $"{(authListPanel == null ? "NOT ASSIGNED" : authListPanel.name)}");

        if (authListPanel == null) return;

        // Re-open from scratch if it's somehow still up, so it slides and reloads.
        if (authListPanel.activeSelf)
            authListPanel.SetActive(false);

        authListPanel.SetActive(true);
    }
}
