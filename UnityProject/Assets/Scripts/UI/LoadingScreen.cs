using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ClubPoker.Core;

namespace ClubPoker.UI
{
    public class LoadingScreen : MonoBehaviour
    {
        [SerializeField] private Slider progressBar;
        [SerializeField] private Transform spinner;
        [SerializeField] private TextMeshProUGUI tipText;
        [SerializeField] private float spinnerSpeed = 60f;
        [SerializeField] private float minimumDisplayTime = 0.5f;

        [SerializeField] private string[] tips = new string[]
        {
            "Shuffling the deck...",
            "Dealing cards...",
            "Placing bets...",
            "Reading your poker face...",
            "Counting chips..."
        };

        private float _displayTimer = 0f;
        private bool _loadComplete = false;

        private void OnEnable()
        {
            _displayTimer = 0f;
            _loadComplete = false;
            progressBar.value = 0f;
            ShowRandomTip();

            // Subscribe to GameSceneManager events
            GameSceneManager.Instance.OnLoadingProgress += UpdateProgress;
            GameSceneManager.Instance.OnLoadingComplete += OnLoadComplete;
        }

        private void OnDisable()
        {
            // Unsubscribe to prevent memory leaks
            if (GameSceneManager.Instance != null)
            {
                GameSceneManager.Instance.OnLoadingProgress -= UpdateProgress;
                GameSceneManager.Instance.OnLoadingComplete -= OnLoadComplete;
            }
        }

        private void Update()
        {
            // Rotate spinner
            spinner.Rotate(0f, 0f, -spinnerSpeed * Time.deltaTime);

            // Track display time
            _displayTimer += Time.deltaTime;

            // Hide only after minimum display time AND load complete
            if (_loadComplete && _displayTimer >= minimumDisplayTime)
            {
                gameObject.SetActive(false);
            }
        }

        private void UpdateProgress(float progress)
        {
            progressBar.value = progress;
        }

        private void OnLoadComplete()
        {
            _loadComplete = true;
        }

        private void ShowRandomTip()
        {
            if (tips.Length > 0)
            {
                tipText.text = tips[Random.Range(0, tips.Length)];
            }
        }
    }
}