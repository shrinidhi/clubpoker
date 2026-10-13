using System.Collections;
using System.Globalization;
using ClubPoker.Networking.Models;
using UnityEngine;
using UnityEngine.UI;

public class Jackpotinformation : MonoBehaviour
{
    public static Jackpotinformation Instance;

    public Text WinnerUserName;
    public Text WinnerPayout;

    public GameObject JackpotinformationScreen;

    private Coroutine closeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if (JackpotinformationScreen != null)
            JackpotinformationScreen.SetActive(false);
    }

    public void ShowInformation(JackpotTriggeredPayload data)
    {
        if (data == null)
            return;

        if (JackpotinformationScreen == null)
        {
            Debug.LogError(
                "[Jackpot] Information screen is not assigned.");
            return;
        }

        if (closeRoutine != null)
        {
            StopCoroutine(closeRoutine);
            closeRoutine = null;
        }

        if (WinnerUserName != null)
        {
            WinnerUserName.supportRichText = false;
            WinnerUserName.text =
                !string.IsNullOrWhiteSpace(data.Winner?.Username)
                    ? data.Winner.Username
                    : "-";
        }

        if (WinnerPayout != null)
        {
            WinnerPayout.supportRichText = false;
            WinnerPayout.text = data.WinnerPayout.ToString(
                "#,0.##", CultureInfo.InvariantCulture);
        }

        JackpotinformationScreen.SetActive(true);
        closeRoutine = StartCoroutine(CloseAfterDelay());
    }

    private IEnumerator CloseAfterDelay()
    {
        yield return new WaitForSecondsRealtime(1f);

        closeRoutine = null;

        if (JackpotinformationScreen != null)
            JackpotinformationScreen.SetActive(false);
    }

    private void OnDisable()
    {
        if (closeRoutine != null)
        {
            StopCoroutine(closeRoutine);
            closeRoutine = null;
        }

        if (JackpotinformationScreen != null)
            JackpotinformationScreen.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}