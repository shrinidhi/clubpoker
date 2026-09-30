using System;
using System.Collections;
using System.Collections.Generic;
using ClubPoker.Game;
using ClubPoker.Networking;
using ClubPoker.Networking.Models;
using UnityEngine;
using UnityEngine.UI;

// Keep this component on an active host; only the child popup is hidden.
public class RunIt_MultipleTimesHandler : MonoBehaviour
{
    public static RunIt_MultipleTimesHandler Instance { get; private set; }
    public GameObject RunItMultipleTimeScreen;
    public Button CloseButton;
    public Button OnceTimesButton;
    public Button TwoTimesButton;
    public Button ThreeTimesButton;
    public Button FourTimesButton;
    public Text DecideTimerText;

    [Header("Board result display, unrelated to action time")]
    public float BoardResultDisplaySeconds = 1f;

    private string promptTableId;
    private int maxRuns;
    private double deadline;
    private bool promptOpen;
    private bool pickSent;
    private bool promptSeen;
    private int runCount;
    private Coroutine presentation;
    private readonly Queue<RunItBoardResult> boards = new Queue<RunItBoardResult>();
    private readonly HashSet<int> seenRuns = new HashSet<int>();
    private RoundEndPayload finalResult;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (CloseButton != null) CloseButton.onClick.AddListener(DismissPrompt);
        if (OnceTimesButton != null) OnceTimesButton.onClick.AddListener(PickOnce);
        if (TwoTimesButton != null) TwoTimesButton.onClick.AddListener(PickTwice);
        if (ThreeTimesButton != null) ThreeTimesButton.onClick.AddListener(PickThree);
        if (FourTimesButton != null) FourTimesButton.onClick.AddListener(PickFour);
        HidePopup();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        if (CloseButton != null) CloseButton.onClick.RemoveListener(DismissPrompt);
        if (OnceTimesButton != null) OnceTimesButton.onClick.RemoveListener(PickOnce);
        if (TwoTimesButton != null) TwoTimesButton.onClick.RemoveListener(PickTwice);
        if (ThreeTimesButton != null) ThreeTimesButton.onClick.RemoveListener(PickThree);
        if (FourTimesButton != null) FourTimesButton.onClick.RemoveListener(PickFour);
    }

    private bool IsCurrentTable(string id) => !string.IsNullOrEmpty(id) &&
        SocketManager.Instance != null && id == SocketManager.Instance.CurrentTableId;

    private void Update()
    {
        if (!promptOpen) return;
        if (!IsCurrentTable(promptTableId)) { HidePopup(); return; }
        double seconds = Math.Max(0d, deadline - Time.realtimeSinceStartupAsDouble);
        if (DecideTimerText != null) DecideTimerText.text = Mathf.CeilToInt((float)seconds) + "s to decide";
        if (seconds <= 0d) HidePopup(); // Server resolves an unanswered prompt.
    }

    public void OnPrompt(RunItPromptPayload payload)
    {
        if (payload == null || !IsCurrentTable(payload.TableId) || payload.TimeAllowedMs <= 0) return;
        // Duplicate delivery must not restart the clock or permit a second pick.
        if (promptTableId == payload.TableId && promptSeen) return;
        promptTableId = payload.TableId;
        promptSeen = true;
        maxRuns = Mathf.Clamp(payload.MaxRuns, 1, 4);
        deadline = Time.realtimeSinceStartupAsDouble + payload.TimeAllowedMs / 1000d;
        pickSent = false;
        SetChoice(OnceTimesButton, 1); SetChoice(TwoTimesButton, 2);
        SetChoice(ThreeTimesButton, 3); SetChoice(FourTimesButton, 4);
        if (RunItMultipleTimeScreen != null) RunItMultipleTimeScreen.SetActive(true);
        promptOpen = true;
        if (DecideTimerText != null)
            DecideTimerText.text = Mathf.CeilToInt(payload.TimeAllowedMs / 1000f) + "s to decide";
    }

    private void SetChoice(Button button, int count)
    {
        if (button == null) return;
        button.gameObject.SetActive(count <= maxRuns);
        button.interactable = count <= maxRuns;
    }
    private void PickOnce() => Pick(1);
    private void PickTwice() => Pick(2);
    private void PickThree() => Pick(3);
    private void PickFour() => Pick(4);

    private void Pick(int count)
    {
        if (!promptOpen || pickSent || count < 1 || count > maxRuns ||
            Time.realtimeSinceStartupAsDouble >= deadline || !IsCurrentTable(promptTableId)) return;
        if (!SocketManager.Instance.IsConnected) return;
        try
        {
            // Selected count is a request. Resolved runCount belongs to the server.
            SocketManager.Instance.Emit("player:run_it_pick", new Dictionary<string, object>
            {
                { "tableId", promptTableId }, { "count", count }
            });
            pickSent = true;
            HidePopup();
        }
        catch (Exception e) { Debug.LogError($"[RunIt] Pick could not be sent: {e}"); }
    }

    private void DismissPrompt() => HidePopup(); // No invented count on Close.
    private void HidePopup()
    {
        promptOpen = false;
        if (RunItMultipleTimeScreen != null) RunItMultipleTimeScreen.SetActive(false);
    }

    public void OnWaiting(RunItWaitingPayload payload)
    {
        if (payload == null || !IsCurrentTable(payload.TableId)) return;
        // Informational broadcast. Only the private prompt opens choice UI.
    }

    public void OnResolved(RunItResolvedPayload payload)
    {
        if (payload == null || !IsCurrentTable(payload.TableId)) return;
        HidePopup();
        runCount = Mathf.Max(1, payload.RunCount);
    }

    public void OnRunoutStart(RunItStartPayload payload)
    {
        if (payload == null || !IsCurrentTable(payload.TableId)) return;
        HidePopup();
        runCount = Mathf.Max(1, payload.RunCount);
        if (CommunityCardsUI.Instance != null)
            CommunityCardsUI.Instance.BeginRunIt(payload.FixedCommunity);
        if (PokerTableUI.Instance != null)
            PokerTableUI.Instance.ShowRunItStatus($"Running it {runCount} times!");
    }

    public void OnRunoutBoard(RunItBoardPayload payload)
    {
        if (payload == null || !IsCurrentTable(payload.TableId)) return;
        runCount = Mathf.Max(runCount, payload.RunCount);
        AddBoard(payload);
        StartPresentation();
    }

    private void AddBoard(RunItBoardResult board)
    {
        if (board == null || board.RunNumber < 1 || board.CommunityCards == null ||
            board.CommunityCards.Count == 0 || !seenRuns.Add(board.RunNumber)) return;
        runCount = Mathf.Max(runCount, board.RunNumber);
        boards.Enqueue(board);
    }

    public bool HandleRoundEnd(RoundEndPayload result)
    {
        if (result == null || !result.multiRunout || result.boards == null || result.boards.Count == 0)
            return false;
        HidePopup();
        finalResult = result;
        var sorted = new List<RunItBoardResult>(result.boards);
        sorted.RemoveAll(b => b == null);
        sorted.Sort((a, b) => a.RunNumber.CompareTo(b.RunNumber));
        // Restore only missed boards; do not replay already presented ones.
        foreach (var board in sorted) AddBoard(board);
        StartPresentation();
        return true;
    }

    private void StartPresentation()
    {
        if (presentation == null) presentation = StartCoroutine(PresentBoards());
    }

    private IEnumerator PresentBoards()
    {
        // Avoid a completed coroutine leaving a stale handle on a synchronous path.
        yield return null;
        while (boards.Count > 0)
        {
            var board = boards.Dequeue();
            if (CommunityCardsUI.Instance != null)
                yield return CommunityCardsUI.Instance.RenderRunItBoard(board.CommunityCards);
            if (PokerTableUI.Instance != null)
                PokerTableUI.Instance.ShowRunItBoardResult(board.RunNumber, runCount,
                    board.Winner?.username, board.HandName);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, BoardResultDisplaySeconds));
        }
        if (finalResult != null && PokerTableUI.Instance != null)
        {
            if (finalResult.winner != null)
                PokerTableUI.Instance.ShowWinner(finalResult.winner.username,
                    finalResult.potWon, finalResult.hand?.name);
            finalResult = null;
        }
        presentation = null;
    }

    public void ResetForNewHand()
    {
        HidePopup();
        if (presentation != null) StopCoroutine(presentation);
        presentation = null;
        promptTableId = null;
        promptSeen = false;
        pickSent = false;
        runCount = 0;
        boards.Clear(); seenRuns.Clear(); finalResult = null;
        if (PokerTableUI.Instance != null) PokerTableUI.Instance.ClearRunItStatus();
    }
}
