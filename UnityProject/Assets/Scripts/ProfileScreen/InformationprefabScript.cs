using System.Collections;
using UnityEngine;
using TMPro;

public class InformationPrefabScript : MonoBehaviour
{
    public static InformationPrefabScript Instance;

    [Header("References")]
    public TextMeshProUGUI InformationText;
    public RectTransform InformationRect;

    [Header("Animation Settings")]
    private float moveDuration = 0.4f;
    private float stayDuration = 1.5f;
    private float bottomOffset = 200f;

    private Vector2 showPosition;
    private Vector2 hidePosition;

    Coroutine currentRoutine;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }


    void Start()
    {
        showPosition = InformationRect.anchoredPosition;
        hidePosition = showPosition - new Vector2(0, bottomOffset);

        InformationRect.anchoredPosition = hidePosition;
        gameObject.SetActive(false);
    }

    public void ShowMessage(string message)
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
        }

        gameObject.SetActive(true);
        InformationText.text = message;

        currentRoutine = StartCoroutine(AnimatePopup());
    }

    IEnumerator AnimatePopup()
    {
        InformationRect.anchoredPosition = hidePosition;

        float time = 0f;
        while (time < moveDuration)
        {
            time += Time.deltaTime;

            InformationRect.anchoredPosition =
                Vector2.Lerp(hidePosition, showPosition, time / moveDuration);

            yield return null;
        }

        yield return new WaitForSeconds(stayDuration);

        time = 0f;
        while (time < moveDuration)
        {
            time += Time.deltaTime;

            InformationRect.anchoredPosition =
                Vector2.Lerp(showPosition, hidePosition, time / moveDuration);

            yield return null;
        }

        gameObject.SetActive(false);
    }
}