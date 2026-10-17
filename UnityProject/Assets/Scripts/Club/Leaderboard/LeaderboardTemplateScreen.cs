using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardTemplateScreen : MonoBehaviour
{
    public Button CloseButton;
    public Transform Content;
    public GameObject TempletePrefab;

    [Header("Optional")]
    public Text MessageText;
    public GameObject NoDataAvailable;

    public ClubLeaderboardData SelectedTemplate { get; private set; }

    public event Action<ClubLeaderboardData> OnTemplateSelected;

    private readonly List<LeaderboardTempletePrefab> spawnedRows =
        new List<LeaderboardTempletePrefab>();

    private ToggleGroup selectionGroup;
    private int viewVersion;

    public Button ConfirmButton;

    public TempleteDateSelect DateSelectPopup;
    private string loadedClubId;

    private void Awake()
    {
        if (CloseButton != null)
            CloseButton.onClick.AddListener(CloseButtonOnTap);

        if (ConfirmButton != null)
        {
            ConfirmButton.onClick.AddListener(ConfirmButtonOnTap);
            ConfirmButton.interactable = false;
        }
    }

    private void OnEnable()
    {
        Reload();
    }

    private void OnDisable()
    {
        ++viewVersion;
    }

    public void Reload()
    {
        if (!isActiveAndEnabled)
            return;

        int version = ++viewVersion;
       
        ClearRows();

        if (NoDataAvailable != null)
            NoDataAvailable.SetActive(false);

        SetMessage("Loading...");

        loadedClubId = ClubContext.ClubId;
        LoadTemplatesAsync(version,ClubContext.ClubId).Forget();
    }

    private bool IsCurrent(int version)
    {
        return this != null &&
               isActiveAndEnabled &&
               version == viewVersion;
    }

    private async UniTask LoadTemplatesAsync(
        int version,
        string clubId)
    {
        try
        {
            await UniTask.Yield();

            if (!IsCurrent(version))
                return;

            ValidateReferences();

            if (ClubManager.Instance == null)
                throw new InvalidOperationException(
                    "ClubManager is missing.");

            var response =
                await ClubManager.Instance
                    .GetClubLeaderboardTemplatesAsync(clubId);

            if (!IsCurrent(version))
                return;

            if (response == null)
                throw new InvalidOperationException(
                    "Templates response was empty.");

            var loadedIds = new HashSet<string>();

            if (response.Templates != null)
            {
                foreach (var template in response.Templates)
                {
                    if (template == null ||
                        string.IsNullOrWhiteSpace(template.Id) ||
                        !loadedIds.Add(template.Id))
                    {
                        continue;
                    }

                    GameObject instance =
                        Instantiate(TempletePrefab, Content);

                    var row =
                        instance.GetComponent<LeaderboardTempletePrefab>();

                    spawnedRows.Add(row);

                    row.SetData(
                        template,
                        selectionGroup,
                        TemplateSelectionChanged);

                    instance.SetActive(true);
                }
            }

            bool empty = spawnedRows.Count == 0;

            if (NoDataAvailable != null)
                NoDataAvailable.SetActive(empty);

            SetMessage(
                empty && NoDataAvailable == null
                    ? "No templates found."
                    : "");

            await RefreshLayoutAsync(version);
        }
        catch (Exception e)
        {
            if (!IsCurrent(version))
                return;

            ClearRows();

            if (NoDataAvailable != null)
                NoDataAvailable.SetActive(false);

            SetMessage("Failed to load templates.");

            Debug.LogError(
                "[Leaderboard Templates] Load failed: " + e.Message);

            await RefreshLayoutAsync(version);
        }
    }

    private void ValidateReferences()
    {
        if (Content == null || TempletePrefab == null)
            throw new InvalidOperationException(
                "Assign Content and TempletePrefab.");

        var prefab =
            TempletePrefab.GetComponent<LeaderboardTempletePrefab>();

        if (prefab == null)
            throw new InvalidOperationException(
                "LeaderboardTempletePrefab component is missing.");

        if (prefab.SelectToggle == null)
            throw new InvalidOperationException(
                "Assign SelectToggle on the template prefab.");

        selectionGroup = Content.GetComponent<ToggleGroup>();

        if (selectionGroup == null)
            selectionGroup = Content.gameObject.AddComponent<ToggleGroup>();

        // No initial selection is required, but multiple selections are blocked.
        selectionGroup.allowSwitchOff = true;
    }

    private void TemplateSelectionChanged(
     LeaderboardTempletePrefab row,
     bool selected)
    {
        if (row == null)
            return;

        if (selected)
        {
            SelectedTemplate = row.Data;
            OnTemplateSelected?.Invoke(SelectedTemplate);
        }
        else if (SelectedTemplate == row.Data)
        {
            SelectedTemplate = null;
            OnTemplateSelected?.Invoke(null);
        }

        if (ConfirmButton != null)
            ConfirmButton.interactable = SelectedTemplate != null;
    }

    private void ConfirmButtonOnTap()
    {
        if (SelectedTemplate == null)
        {
            SetMessage("Select a template first.");
            return;
        }

        if (DateSelectPopup == null)
        {
            SetMessage("Assign the template date selection popup.");
            return;
        }

        DateSelectPopup.Open(
            loadedClubId,
            SelectedTemplate,
            this);
    }

    private async UniTask RefreshLayoutAsync(int version)
    {
        await UniTask.NextFrame();

        if (!IsCurrent(version) || Content == null)
            return;

        Canvas.ForceUpdateCanvases();

        var layouts =
            Content.GetComponentsInChildren<RectTransform>(false);

        for (int i = layouts.Length - 1; i >= 0; i--)
            LayoutRebuilder.ForceRebuildLayoutImmediate(layouts[i]);

        Transform current = Content.parent;

        while (current != null)
        {
            if (current is RectTransform rect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

            if (current.GetComponent<Canvas>() != null)
                break;

            current = current.parent;
        }

        Canvas.ForceUpdateCanvases();
    }

    private void ClearRows()
    {
        bool hadSelection = SelectedTemplate != null;
        SelectedTemplate = null;
        if (ConfirmButton != null)
            ConfirmButton.interactable = false;
        foreach (var row in spawnedRows)
        {
            if (row == null)
                continue;

            row.SetSelected(false, false);
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }

        spawnedRows.Clear();

        if (hadSelection)
            OnTemplateSelected?.Invoke(null);
    }

    private void SetMessage(string message)
    {
        if (MessageText == null)
            return;

        MessageText.supportRichText = false;
        MessageText.text = message;
        MessageText.gameObject.SetActive(
            !string.IsNullOrEmpty(message));
    }

    private void CloseButtonOnTap()
    {
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (CloseButton != null)
            CloseButton.onClick.RemoveListener(CloseButtonOnTap);

        if (ConfirmButton != null)
            ConfirmButton.onClick.RemoveListener(ConfirmButtonOnTap);
    }
}