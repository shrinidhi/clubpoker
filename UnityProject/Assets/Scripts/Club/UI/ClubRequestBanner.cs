
using System.Collections.Generic;
using ClubPoker.Auth;
using ClubPoker.Core;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ClubRequestBanner : MonoBehaviour
{
    // Scene_Club = GameSceneManager ka loading key
    // ClubScene = Unity mein actual loaded scene ka naam

    private const string SCENE_LOAD_KEY = "Scene_Club";
    private const string SCENE_RUNTIME_NAME = "ClubScene";

    private const string PREFAB_PATH =
        "UI/ClubRequestBanner_Prefab";

    private const string MEMBER_SCREEN_NAME =
        "MemberManagment_Screen";

    private const float SlideSeconds = 0.3f;
    private const float ShownY = -66f;

    private const int CountdownSeconds = 5;
    private const int MAX_WAIT_FRAMES = 600;

    private static ClubRequestBanner _instance;

    private RectTransform _safeArea;
    private RectTransform _panel;

    private TMP_Text _text;

    private Button _closeButton;
    private Button _gotoButton;

    private float _hiddenY;

    private Sequence _sequence;

    private string _clubId;
    private ClubListData _club;

    private int _showVersion;

    private static string _pendingClubId;

    private Rect _lastSafeArea;

    // --------------------------------------------------
    // SHOW BANNER
    // --------------------------------------------------

    public static void Show(ClubNewApplicationPayload payload)
    {
        if (payload == null ||
            string.IsNullOrEmpty(payload.ClubId))
            return;

        string scene =
            SceneManager.GetActiveScene().name;

        if (scene.Contains("Login") ||
            scene.Contains("Register") ||
            scene.Contains("Splash") ||
            scene.Contains("Bootstrap"))
        {
            return;
        }

        ClubRequestBanner banner = Ensure();

        if (banner != null)
            banner.ShowAsync(payload).Forget();
    }

    // --------------------------------------------------
    // CREATE BANNER
    // --------------------------------------------------

    private static ClubRequestBanner Ensure()
    {
        if (_instance != null)
            return _instance;

        GameObject root =
            new GameObject("ClubRequestBanner");

        DontDestroyOnLoad(root);

        Canvas canvas = root.AddComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.sortingOrder = short.MaxValue - 2;

        CanvasScaler scaler =
            root.AddComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1080, 1920);

        scaler.matchWidthOrHeight = 0f;

        root.AddComponent<GraphicRaycaster>();

        ClubRequestBanner banner =
            root.AddComponent<ClubRequestBanner>();

        banner._safeArea =
            NewRect("SafeArea", root.transform);

        GameObject prefab =
            Resources.Load<GameObject>(PREFAB_PATH);

        if (prefab == null)
        {
            Debug.LogError(
                "[ClubRequestBanner] Prefab missing: Resources/" +
                PREFAB_PATH
            );

            Destroy(root);
            return null;
        }

        GameObject panelObject =
            Instantiate(prefab, banner._safeArea, false);

        banner._panel =
            panelObject.GetComponent<RectTransform>();

        if (banner._panel == null)
        {
            Debug.LogError(
                "[ClubRequestBanner] Prefab RectTransform missing!"
            );

            Destroy(root);
            return null;
        }

        banner._hiddenY =
            banner._panel.anchoredPosition.y;

        banner._text =
            FindChild(banner._panel, "requestText")
            ?.GetComponent<TMP_Text>();

        banner._closeButton =
            FindChild(banner._panel, "closeBtn")
            ?.GetComponent<Button>();

        banner._gotoButton =
            FindChild(banner._panel, "Accept_Button")
            ?.GetComponent<Button>();

        if (banner._gotoButton != null)
        {
            banner._gotoButton.onClick.AddListener(
                banner.OnTap
            );
        }

        if (banner._closeButton != null)
        {
            banner._closeButton.onClick.AddListener(
                banner.Hide
            );
        }

        banner._panel.gameObject.SetActive(false);

        _instance = banner;

        banner.ApplySafeArea();

        SceneManager.sceneLoaded += banner.OnSceneLoaded;

        Debug.Log(
            "[ClubRequestBanner] Initialized successfully!"
        );

        return banner;
    }

    private static Transform FindChild(
        Transform root,
        string childName)
    {
        foreach (Transform t in
                 root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName)
                return t;
        }

        Debug.LogWarning(
            "[ClubRequestBanner] Child missing: " + childName
        );

        return null;
    }

    private static RectTransform NewRect(
        string name,
        Transform parent)
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform)
        );

        go.transform.SetParent(parent, false);

        RectTransform rect =
            go.GetComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;

        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return rect;
    }

    // --------------------------------------------------
    // SAFE AREA
    // --------------------------------------------------

    private void Update()
    {
        if (Screen.safeArea != _lastSafeArea)
            ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        if (_safeArea == null ||
            Screen.width <= 0 ||
            Screen.height <= 0)
            return;

        _lastSafeArea = Screen.safeArea;

        Rect safe = Screen.safeArea;

        _safeArea.anchorMin = new Vector2(
            safe.xMin / Screen.width,
            safe.yMin / Screen.height
        );

        _safeArea.anchorMax = new Vector2(
            safe.xMax / Screen.width,
            safe.yMax / Screen.height
        );
    }

    // --------------------------------------------------
    // SHOW BANNER
    // --------------------------------------------------

    private async UniTaskVoid ShowAsync(
        ClubNewApplicationPayload payload)
    {
        _clubId = payload.ClubId;

        _club = await FindClubAsync(payload.ClubId);

        if (this == null ||
            _clubId != payload.ClubId)
            return;

        if (_club == null)
        {
            Debug.LogWarning(
                "[ClubRequestBanner] Club data not found!"
            );

            return;
        }

        ClubRole role =
            ClubContext.ParseRole(_club.Role);

        if (role != ClubRole.Creator &&
            role != ClubRole.Manager)
            return;

        if (_text != null)
        {
            _text.text =
                "A new club join request has been received.";
        }

        int version = ++_showVersion;

        _sequence?.Kill();

        _panel.gameObject.SetActive(true);

        _panel.anchoredPosition = new Vector2(
            _panel.anchoredPosition.x,
            _hiddenY
        );

        _sequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(
                _panel.DOAnchorPosY(
                    ShownY,
                    SlideSeconds
                ).SetEase(Ease.OutCubic)
            );

        for (int i = CountdownSeconds; i > 0; i--)
        {
            if (this == null ||
                version != _showVersion)
                return;

            if (_text != null)
            {
                _text.text =
                    $"A new club join request has been received. ({i}s)";
            }

            await UniTask.Delay(
                1000,
                ignoreTimeScale: true
            );
        }

        if (this == null ||
            version != _showVersion)
            return;

        Hide();
    }

    // --------------------------------------------------
    // HIDE BANNER
    // --------------------------------------------------

    private void Hide()
    {
        _showVersion++;

        _sequence?.Kill();

        if (_panel != null)
            _panel.gameObject.SetActive(false);
    }

    // --------------------------------------------------
    // BANNER BUTTON CLICK
    // --------------------------------------------------

    private void OnTap()
    {
        string clubId = _clubId;
        ClubListData club = _club;

        Hide();

        if (string.IsNullOrEmpty(clubId))
            return;

        Scene activeScene =
            SceneManager.GetActiveScene();

        // Already inside ClubScene
        if (activeScene.name == SCENE_RUNTIME_NAME)
        {
            if (ClubContext.SelectedClub != null &&
                ClubContext.SelectedClub.ClubId == clubId)
            {
                Debug.Log(
                    "[ClubRequestBanner] Same club already open. Opening Applicant Panel."
                );

                OpenApplicantPanelAsync().Forget();
                return;
            }
        }

        // Open another club
        OpenClubAsync(clubId, club).Forget();
    }

    // --------------------------------------------------
    // FIND OBJECT IN SCENE
    // --------------------------------------------------

    private static GameObject FindSceneObject(
        Scene scene,
        string objectName)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        GameObject[] roots =
            scene.GetRootGameObjects();

        foreach (GameObject root in roots)
        {
            Transform[] children =
                root.GetComponentsInChildren<Transform>(true);

            foreach (Transform child in children)
            {
                if (child.name == objectName)
                    return child.gameObject;
            }
        }

        return null;
    }

    private static MemberManagmentScreenScript
        FindMemberManagementScript(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        GameObject[] roots =
            scene.GetRootGameObjects();

        foreach (GameObject root in roots)
        {
            MemberManagmentScreenScript screen =
                root.GetComponentInChildren
                    <MemberManagmentScreenScript>(true);

            if (screen != null)
                return screen;
        }

        return null;
    }

    // --------------------------------------------------
    // ACTIVATE OBJECT + ALL PARENTS
    // --------------------------------------------------

    private static void ActivateHierarchy(
        GameObject target)
    {
        if (target == null)
            return;

        List<Transform> hierarchy =
            new List<Transform>();

        Transform current = target.transform;

        while (current != null)
        {
            hierarchy.Add(current);
            current = current.parent;
        }

        // Parent first, then child
        for (int i = hierarchy.Count - 1; i >= 0; i--)
        {
            GameObject obj =
                hierarchy[i].gameObject;

            if (!obj.activeSelf)
            {
                obj.SetActive(true);

                Debug.Log(
                    "[ClubRequestBanner] Activated: " +
                    obj.name
                );
            }
        }
    }

    // --------------------------------------------------
    // OPEN APPLICANT PANEL - SAME SCENE
    // --------------------------------------------------

    private async UniTaskVoid OpenApplicantPanelAsync()
    {
        Debug.Log(
            "[ClubRequestBanner] Waiting for Member Management screen..."
        );

        Scene scene =
            SceneManager.GetActiveScene();

        for (int i = 0; i < MAX_WAIT_FRAMES; i++)
        {
            await UniTask.DelayFrame(1);

            if (this == null)
                return;

            MemberManagmentScreenScript screen =
                FindMemberManagementScript(scene);

            if (screen == null)
                continue;

            GameObject memberScreen =
                FindSceneObject(
                    scene,
                    MEMBER_SCREEN_NAME
                );

            if (memberScreen != null)
            {
                ActivateHierarchy(memberScreen);
            }

            ActivateHierarchy(screen.gameObject);

            await UniTask.DelayFrame(2);

            if (this == null)
                return;

            screen =
                FindMemberManagementScript(scene);

            if (screen == null)
                continue;

            screen.gameObject.SetActive(true);

            screen.Applicant_ButtonOnTap();

            Debug.Log(
                "[ClubRequestBanner] Applicant Panel opened in existing scene!"
            );

            return;
        }

        Debug.LogError(
            "[ClubRequestBanner] Member Management script not found in active scene!"
        );

        PrintSceneHierarchy(scene);
    }

    // --------------------------------------------------
    // OPEN CLUB
    // --------------------------------------------------

    private static async UniTaskVoid OpenClubAsync(
        string clubId,
        ClubListData club)
    {
        if (club == null)
            club = await FindClubAsync(clubId);

        if (club == null)
        {
            Debug.LogError(
                "[ClubRequestBanner] Club data not found!"
            );

            return;
        }

        if (GameSceneManager.Instance == null)
        {
            Debug.LogError(
                "[ClubRequestBanner] GameSceneManager is null!"
            );

            return;
        }

        _pendingClubId = clubId;

        if (ClubSocketHandler.Instance != null)
        {
            ClubSocketHandler.Instance.JoinClubPage(clubId);
        }

        ClubContext.SelectClub(club);

        Debug.Log(
            "[ClubRequestBanner] Loading Scene_Club..."
        );

        // IMPORTANT: Load scene only ONCE
        GameSceneManager.Instance.LoadScene(
            SCENE_LOAD_KEY
        );
    }

    // --------------------------------------------------
    // SCENE LOADED EVENT
    // --------------------------------------------------

    private void OnSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        Debug.Log(
            "[ClubRequestBanner] Scene Loaded: " +
            scene.name
        );

        // Actual Unity scene name check
        if (scene.name != SCENE_RUNTIME_NAME)
            return;

        if (string.IsNullOrEmpty(_pendingClubId))
        {
            Debug.LogWarning(
                "[ClubRequestBanner] Pending Club ID is empty!"
            );

            return;
        }

        Debug.Log(
            "[ClubRequestBanner] Starting Member Management activation..."
        );

        OpenApplicantAfterLoad(
            _pendingClubId
        ).Forget();
    }

    // --------------------------------------------------
    // OPEN APPLICANT AFTER SCENE LOAD
    // --------------------------------------------------

    private async UniTaskVoid OpenApplicantAfterLoad(
        string clubId)
    {
        Debug.Log(
            "[ClubRequestBanner] Waiting for Club Scene initialization..."
        );

        Scene clubScene =
            SceneManager.GetSceneByName(
                SCENE_RUNTIME_NAME
            );

        for (int i = 0; i < MAX_WAIT_FRAMES; i++)
        {
            await UniTask.DelayFrame(1);

            if (this == null ||
                _pendingClubId != clubId)
                return;

            // Check actual runtime scene
            clubScene =
                SceneManager.GetSceneByName(
                    SCENE_RUNTIME_NAME
                );

            if (!clubScene.IsValid() ||
                !clubScene.isLoaded)
                continue;

            // Find MemberManagment_Screen
            GameObject memberScreen =
                FindSceneObject(
                    clubScene,
                    MEMBER_SCREEN_NAME
                );

            // Find MemberManagmentScreenScript
            MemberManagmentScreenScript screen = null;

            if (memberScreen != null)
            {
                screen =
                    memberScreen.GetComponentInChildren
                        <MemberManagmentScreenScript>(true);
            }

            // Fallback: Search complete scene
            if (screen == null)
            {
                screen =
                    FindMemberManagementScript(clubScene);
            }

            if (screen == null)
                continue;

            Debug.Log(
                "[ClubRequestBanner] Member Management script found!"
            );

            // Activate named screen
            if (memberScreen != null)
            {
                ActivateHierarchy(memberScreen);
            }
            else
            {
                Debug.LogWarning(
                    "[ClubRequestBanner] Named MemberManagment_Screen not found. Activating script hierarchy."
                );

                ActivateHierarchy(screen.gameObject);
            }

            // Activate script object
            ActivateHierarchy(screen.gameObject);

            await UniTask.DelayFrame(2);

            if (this == null ||
                _pendingClubId != clubId)
                return;

            // Search again after activation
            screen =
                FindMemberManagementScript(clubScene);

            if (screen == null)
            {
                Debug.LogWarning(
                    "[ClubRequestBanner] Script disappeared after activation!"
                );

                continue;
            }

            screen.gameObject.SetActive(true);

            await UniTask.DelayFrame(2);

            if (this == null ||
                _pendingClubId != clubId)
                return;

            // Open Applicant Panel
            screen.Applicant_ButtonOnTap();

            _pendingClubId = null;

            Debug.Log(
                "[ClubRequestBanner] MemberManagment_Screen activated!"
            );

            Debug.Log(
                "[ClubRequestBanner] Applicant Panel opened!"
            );

            return;
        }

        Debug.LogError(
            "[ClubRequestBanner] Member Management screen not found in ClubScene!"
        );

        // Print all objects to identify actual hierarchy
        PrintSceneHierarchy(clubScene);
    }

    // --------------------------------------------------
    // PRINT SCENE HIERARCHY FOR DEBUGGING
    // --------------------------------------------------

    private static void PrintSceneHierarchy(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError(
                "[ClubRequestBanner] Cannot print hierarchy. Scene is not loaded!"
            );

            return;
        }

        Debug.Log(
            "========== CLUB SCENE HIERARCHY START =========="
        );

        GameObject[] roots =
            scene.GetRootGameObjects();

        foreach (GameObject root in roots)
        {
            Debug.Log(
                "[Club Scene ROOT] " + root.name
            );

            Transform[] children =
                root.GetComponentsInChildren<Transform>(true);

            foreach (Transform child in children)
            {
                Debug.Log(
                    "[Club Scene OBJECT] " +
                    child.name +
                    " | Active: " +
                    child.gameObject.activeSelf +
                    " | ActiveInHierarchy: " +
                    child.gameObject.activeInHierarchy
                );
            }
        }

        Debug.Log(
            "========== CLUB SCENE HIERARCHY END =========="
        );
    }

    // --------------------------------------------------
    // CLUB LOOKUP
    // --------------------------------------------------

    private static async UniTask<ClubListData> FindClubAsync(
        string clubId)
    {
        if (ClubContext.SelectedClub != null &&
            ClubContext.SelectedClub.ClubId == clubId)
        {
            return ClubContext.SelectedClub;
        }

        if (AuthManager.Instance == null)
            return null;

        List<ClubListData> clubs =
            await AuthManager.Instance.GetClubsAsync();

        return clubs?.Find(
            c => c != null && c.ClubId == clubId
        );
    }

    // --------------------------------------------------
    // DESTROY
    // --------------------------------------------------

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        _sequence?.Kill();

        if (_instance == this)
            _instance = null;
    }
}
