using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;

/// <summary>
/// Admin ▸ Club Badge & Name. A ClubScene copy of the create-club popup's badge grid + name
/// field, but it edits the current club: prefills the current name/badge and saves via
/// PUT /api/clubs/{clubId} { name, badge }. Reuses ClubBadgePrefabScript + ClubBadgeSO.
/// Backend is LIVE.
/// </summary>
public class AdminClubBadgeNamePopupScript : MonoBehaviour
{
    private const int NameMax = 20;

    [Header("Header")]
    public Button Close_Button;

    [Header("Name")]
    public TMP_InputField ClubName_InputField;

    [Header("Badges")]
    public GameObject ClubBadge_Prefab;
    public Transform ClubBadge_Content;
    public ClubBadgeSO ClubBadgeSO;

    [Header("Submit")]
    public Button Save_Button;

    private readonly List<ClubBadgePrefabScript> _badgeItems = new List<ClubBadgePrefabScript>();
    private string _selectedBadge = "";
    private string _currentName = "";
    private string _currentBadge = "";
    private bool _built;
    private bool _busy;

    private void Start()
    {
        if (Close_Button != null) Close_Button.onClick.AddListener(Close);
        if (Save_Button  != null) Save_Button.onClick.AddListener(OnSaveTap);

        if (ClubName_InputField != null)
        {
            ClubName_InputField.characterLimit = NameMax;
            ClubName_InputField.onValueChanged.AddListener(_ => RefreshSaveState());
        }

        GenerateBadges();
        _built = true;
        Prefill();
    }

    private void OnEnable()
    {
        if (_built) Prefill();   // refresh from the latest cached detail on each open
    }

    private void GenerateBadges()
    {
        _badgeItems.Clear();
        if (ClubBadge_Content == null || ClubBadge_Prefab == null || ClubBadgeSO == null) return;

        for (int i = ClubBadge_Content.childCount - 1; i >= 0; i--)
            Destroy(ClubBadge_Content.GetChild(i).gameObject);

        foreach (ClubBadgeData data in ClubBadgeSO.ClubBadges)
        {
            var obj = Instantiate(ClubBadge_Prefab, ClubBadge_Content);
            var badge = obj.GetComponent<ClubBadgePrefabScript>();
            badge.Setup(data, SelectBadge);
            _badgeItems.Add(badge);
        }
    }

    // Show the current name as the placeholder (field starts empty) + select current badge.
    private void Prefill()
    {
        var d = ClubContext.ClubDetail;
        _currentName  = d?.Name ?? ClubContext.ClubName ?? "";
        _currentBadge = d?.Badge?.ToLower() ?? "";

        if (ClubName_InputField != null)
        {
            ClubName_InputField.text = "";
            if (ClubName_InputField.placeholder is TMP_Text ph)
                ph.text = string.IsNullOrEmpty(_currentName) ? "Club name" : _currentName;
        }

        ClubBadgePrefabScript match = null;
        foreach (var item in _badgeItems)
            if (item.BadgeKey == _currentBadge) { match = item; break; }

        if (match == null && _badgeItems.Count > 0) match = _badgeItems[0];
        if (match != null) SelectBadge(match, match.BadgeKey);
    }

    public void SelectBadge(ClubBadgePrefabScript selectedItem, string badgeKey)
    {
        _selectedBadge = badgeKey;
        foreach (var item in _badgeItems)
            item.SetSelected(item == selectedItem);
        RefreshSaveState();
    }

    // Name changed (non-empty + different) OR badge changed.
    private bool HasChanges()
    {
        string typed = ClubName_InputField != null ? ClubName_InputField.text.Trim() : "";
        bool nameChanged = !string.IsNullOrEmpty(typed) &&
                           !string.Equals(typed, _currentName, System.StringComparison.Ordinal);
        bool badgeChanged = !string.Equals(_selectedBadge, _currentBadge,
                           System.StringComparison.OrdinalIgnoreCase);
        return nameChanged || badgeChanged;
    }

    private void RefreshSaveState()
    {
        if (Save_Button != null) Save_Button.interactable = !_busy && HasChanges();
    }

    private void OnSaveTap()
    {
        if (_busy) return;

        // Empty field = keep the current name (placeholder shows it).
        string typed = ClubName_InputField != null ? ClubName_InputField.text.Trim() : "";
        string name  = string.IsNullOrEmpty(typed) ? _currentName : typed;

        if (string.IsNullOrEmpty(_selectedBadge))
        {
            ShowToast("Please select a badge");
            return;
        }

        // Send only what actually changed.
        var fields = new Dictionary<string, object>();
        if (!string.Equals(name, _currentName, System.StringComparison.Ordinal))
            fields["name"] = name;
        if (!string.Equals(_selectedBadge, _currentBadge, System.StringComparison.OrdinalIgnoreCase))
            fields["badge"] = _selectedBadge;

        if (fields.Count == 0)
        {
            ShowToast("No changes");
            return;
        }

        Save(fields).Forget();
    }

    private async UniTaskVoid Save(Dictionary<string, object> fields)
    {
        _busy = true;
        if (Save_Button != null) Save_Button.interactable = false;
        try
        {
            var res = await ClubManager.Instance.UpdateClubAsync(ClubContext.ClubId, fields);

            // Response returns the full club → cache it. SetClubDetail fires
            // OnClubDetailChanged, which the home header listens to and repaints itself.
            if (res?.Club != null) ClubContext.SetClubDetail(res.Club);

            ShowToast("Club updated");
            Close();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AdminClubBadgeNamePopupScript] save error: {e.Message}");
            ShowToast(string.IsNullOrEmpty(e.Message) ? "Failed to save" : e.Message);
        }
        finally
        {
            _busy = false;
            RefreshSaveState();
        }
    }

    private void Close()
    {
        gameObject.SetActive(false);
    }

    private void ShowToast(string message)
    {
        if (InformationPrefabScript.Instance != null)
            InformationPrefabScript.Instance.ShowMessage(message);
    }
}
