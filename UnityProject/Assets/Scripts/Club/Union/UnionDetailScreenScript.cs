using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class UnionDetailScreenScript : MonoBehaviour
{
    public Button CloseButton;
    public Button LeaveButton;

    public Text UnionName;
    public Text UnionCode;
    public Text UnionClubCount;

    public Transform Content;
    public GameObject UnionClubPrefab;

    [Header("Union")]
    public string UnionLookupCode;

    public ClubUnionData UnionData { get; private set; }

    public event Action<LeaveClubUnionResponse> OnUnionLeft;

    private readonly List<GameObject> spawnedRows =
        new List<GameObject>();

    private string loadedClubId;
    private bool loading;
    private bool leaving;
    private bool currentClubIsMember;
    private int viewVersion;

    private void Awake()
    {
        if (CloseButton != null)
            CloseButton.onClick.AddListener(CloseButtonOnTap);

        if (LeaveButton != null)
            LeaveButton.onClick.AddListener(LeaveButtonOnTap);
    }

    private void OnEnable()
    {
        Open(ClubContext.ClubDetail.ActiveUnionCode);
        Reload();
    }

    private void OnDisable()
    {
        ++viewVersion;
    }

    public void Open(string unionCode)
    {
        if (leaving)
            return;

        if (string.IsNullOrWhiteSpace(unionCode))
        {
            Debug.LogError("[Union] Union code is required.");
            return;
        }

        UnionLookupCode = unionCode.Trim();

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        else if (isActiveAndEnabled)
            Reload();
    }

    public void Reload()
    {
        if (!isActiveAndEnabled || leaving)
            return;

        int version = ++viewVersion;

        loadedClubId = ClubContext.ClubId;
        UnionData = null;
        currentClubIsMember = false;
        loading = false;

        ClearRows();

        SetText(UnionName, "-");
        SetText(UnionCode, "-");
        SetText(UnionClubCount, "-");

        
        if (string.IsNullOrWhiteSpace(UnionLookupCode))
        {
            UpdateControls();
            return;
        }

        loading = true;
        UpdateControls();

        LoadUnionAsync(
            version,
            UnionLookupCode.Trim(),
            loadedClubId).Forget();
    }

    private bool IsCurrent(int version)
    {
        return this != null &&
               isActiveAndEnabled &&
               version == viewVersion;
    }

    private async UniTask LoadUnionAsync(
        int version,
        string unionCode,
        string clubId)
    {
        try
        {
            await UniTask.Yield();

            if (!IsCurrent(version))
                return;

            ValidateReferences();

            if (ClubManager.Instance == null)
            {
                throw new InvalidOperationException(
                    "ClubManager is missing.");
            }

            var response =
                await ClubManager.Instance.GetUnionDetailAsync(
                    unionCode);

            if (!IsCurrent(version))
                return;

            var data = response.Union;

            if (!string.Equals(
                    data.UnionCode,
                    unionCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The server returned a different union.");
            }

            UnionData = data;

            SetText(UnionName, data.Name);
            SetText(UnionCode, "UnionCode: " + data.UnionCode);

            var loadedIds = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            if (data.Clubs != null)
            {
                foreach (var club in data.Clubs)
                {
                    if (club == null ||
                        string.IsNullOrWhiteSpace(club.ClubId) ||
                        !loadedIds.Add(club.ClubId))
                    {
                        continue;
                    }

                    if (string.Equals(
                            club.ClubId,
                            clubId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        currentClubIsMember = true;
                    }

                    GameObject instance =
                        Instantiate(UnionClubPrefab, Content);

                    spawnedRows.Add(instance);

                    instance.GetComponent<global::UnionClubPrefab>()
                        .SetData(club);

                    instance.SetActive(true);
                }
            }

            SetText(UnionClubCount, "Member Clubs("+spawnedRows.Count.ToString(CultureInfo.InvariantCulture) + ")");

            

            SetMessage("");
            await RefreshLayoutAsync(version);
        }
        catch (Exception e)
        {
            if (!IsCurrent(version))
                return;

            UnionData = null;
            currentClubIsMember = false;
            ClearRows();

            SetText(UnionName, "-");
            SetText(UnionCode, "-");
            SetText(UnionClubCount, "-");

            Debug.LogError(
                "[Union] Load failed: " + e.Message);

            await RefreshLayoutAsync(version);
        }
        finally
        {
            if (IsCurrent(version))
            {
                loading = false;
                UpdateControls();
            }
        }
    }

    private void ValidateReferences()
    {
        if (Content == null || UnionClubPrefab == null)
        {
            throw new InvalidOperationException(
                "Assign Content and UnionClubPrefab.");
        }

        if (UnionClubPrefab.GetComponent<
                global::UnionClubPrefab>() == null)
        {
            throw new InvalidOperationException(
                "UnionClubPrefab component is missing.");
        }
    }

    private void CloseButtonOnTap()
    {
        if (leaving)
            return;

        gameObject.SetActive(false);
    }

    private void LeaveButtonOnTap()
    {
        if (loading ||
            leaving ||
            UnionData == null ||
            !currentClubIsMember)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(loadedClubId) ||
            !string.Equals(
                loadedClubId,
                ClubContext.ClubId,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        LeaveUnionAsync(
            viewVersion,
            loadedClubId).Forget();
    }

    private async UniTask LeaveUnionAsync(
        int version,
        string clubId)
    {
        leaving = true;
        UpdateControls();
        SetMessage("Leaving union...");

        LeaveClubUnionResponse result = null;

        try
        {
            if (ClubManager.Instance == null)
            {
                throw new InvalidOperationException(
                    "ClubManager is missing.");
            }

            result =
                await ClubManager.Instance.LeaveClubUnionAsync(
                    clubId);
        }
        catch (Exception e)
        {
            if (IsCurrent(version))
                SetMessage(e.Message);

            Debug.LogError(
                "[Union] Leave failed: " + e.Message);
        }
        finally
        {
            if (this != null)
            {
                leaving = false;
                UpdateControls();
            }
        }

        if (this == null || result == null)
            return;

        if (IsCurrent(version))
        {
            UnionData = null;
            currentClubIsMember = false;

            ClearRows();
            UpdateControls();
            gameObject.SetActive(false);
        }
        var detail = await ClubManager.Instance.GetClubDetailAsync(clubId);
        ClubContext.SetClubDetail(detail?.Club);
        OnUnionLeft?.Invoke(result);
    }

    private void UpdateControls()
    {
        if (CloseButton != null)
            CloseButton.interactable = !leaving;

        if (LeaveButton != null)
        {
            LeaveButton.interactable =
                !loading &&
                !leaving &&
                UnionData != null &&
                currentClubIsMember;
        }
    }

    private async UniTask RefreshLayoutAsync(int version)
    {
        await UniTask.NextFrame();

        if (!IsCurrent(version) || Content == null)
            return;

        Canvas.ForceUpdateCanvases();

        RectTransform[] layouts =
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
        foreach (GameObject row in spawnedRows)
        {
            if (row == null)
                continue;

            row.SetActive(false);
            Destroy(row);
        }

        spawnedRows.Clear();
    }

    private static void SetText(Text target, string value)
    {
        if (target == null)
            return;

        target.supportRichText = false;
        target.text = string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private void SetMessage(string message)
    {
        InformationPrefabScript.Instance.ShowMessage(message);
    }

    private void OnDestroy()
    {
        if (CloseButton != null)
            CloseButton.onClick.RemoveListener(CloseButtonOnTap);

        if (LeaveButton != null)
            LeaveButton.onClick.RemoveListener(LeaveButtonOnTap);
    }
}