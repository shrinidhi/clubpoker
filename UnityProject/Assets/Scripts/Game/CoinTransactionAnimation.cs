using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ClubPoker.Game
{
    public class CoinTransactionAnimation : MonoBehaviour
    {
        public static CoinTransactionAnimation Instance;

        [Header("References")]
        public RectTransform AnimationParent;
        public RectTransform CoinPrefab;
        public RectTransform PotTarget;

        [Header("Settings")]
        public int coinCount = 6;
        public int maxPotCoins = 6;
        public float moveToPotDuration = 0.45f;
        public float moveToWinnerDuration = 0.6f;
        public float coinDelay = 0.04f;
        private int runningPotAnimations = 0;
        private readonly List<RectTransform> potCoins = new List<RectTransform>();

        private void Awake()
        {
            Instance = this;
        }

        public void PlayToPot(RectTransform from, int amount)
        {
            if (from == null || CoinPrefab == null || AnimationParent == null || PotTarget == null)
                return;

            runningPotAnimations++;
            StartCoroutine(PlayToPotRoutine(from));
        }

        private IEnumerator PlayToPotRoutine(RectTransform from)
        {
            for (int i = 0; i < coinCount; i++)
            {
                RectTransform coin = Instantiate(CoinPrefab, AnimationParent);
                coin.gameObject.SetActive(true);

                Vector3 startOffset = new Vector3(
                    Random.Range(-20f, 20f),
                    Random.Range(-10f, 20f),
                    0
                );

                coin.position = from.position + startOffset;
                coin.localScale = Vector3.one * Random.Range(0.85f, 1.05f);

                Vector3 potOffset = GetPotStackOffset();

                StartCoroutine(MoveCoinToPot(coin, PotTarget.position + potOffset));

                yield return new WaitForSeconds(coinDelay);
            }

            yield return new WaitForSeconds(moveToPotDuration + 0.05f);

            runningPotAnimations--;
        }

        private IEnumerator MoveCoinToPot(RectTransform coin, Vector3 target)
        {
            float t = 0f;
            Vector3 start = coin.position;

            while (t < moveToPotDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / moveToPotDuration);
                float smooth = Mathf.SmoothStep(0f, 1f, p);

                Vector3 pos = Vector3.Lerp(start, target, smooth);
                pos.y += Mathf.Sin(p * Mathf.PI) * 80f;

                coin.position = pos;
                coin.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.7f, smooth);
                coin.Rotate(0, 0, 360f * Time.deltaTime);

                yield return null;
            }

            coin.position = target;
            coin.localScale = Vector3.one * 0.7f;

            AddCoinToPot(coin);
        }

        private void AddCoinToPot(RectTransform coin)
        {
            potCoins.Add(coin);

            while (potCoins.Count > maxPotCoins)
            {
                RectTransform oldCoin = potCoins[0];
                potCoins.RemoveAt(0);

                if (oldCoin != null)
                    Destroy(oldCoin.gameObject);
            }
        }

        private Vector3 GetPotStackOffset()
        {
            return new Vector3(
                Random.Range(-18f, 18f),
                Random.Range(-10f, 12f),
                0
            );
        }

        public void MovePotToWinner(RectTransform winner)
        {
            if (winner == null)
                return;

            StartCoroutine(MovePotToWinnerAfterPotDone(winner));
        }
        private IEnumerator MovePotToWinnerAfterPotDone(RectTransform winner)
        {
            while (runningPotAnimations > 0)
                yield return null;

            yield return new WaitForSeconds(0.8f);

            if (potCoins.Count == 0)
                yield break;

            yield return StartCoroutine(MovePotToWinnerRoutine(winner));
        }
        private IEnumerator MovePotToWinnerRoutine(RectTransform winner)
        {
            List<RectTransform> coinsToMove = new List<RectTransform>(potCoins);
            potCoins.Clear();

            int moveCount = Mathf.Min(coinsToMove.Count, maxPotCoins);

            for (int i = 0; i < coinsToMove.Count; i++)
            {
                RectTransform coin = coinsToMove[i];

                if (coin == null)
                    continue;

                if (i < moveCount)
                {
                    Vector3 winnerOffset = new Vector3(
                        Random.Range(-25f, 25f),
                        Random.Range(-10f, 20f),
                        0
                    );

                    StartCoroutine(MoveCoinToWinner(coin, winner.position + winnerOffset));
                    yield return new WaitForSeconds(coinDelay);
                }
                else
                {
                    Destroy(coin.gameObject);
                }
            }
        }

        private IEnumerator MoveCoinToWinner(RectTransform coin, Vector3 target)
        {
            float t = 0f;
            Vector3 start = coin.position;

            while (t < moveToWinnerDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / moveToWinnerDuration);
                float smooth = Mathf.SmoothStep(0f, 1f, p);

                Vector3 pos = Vector3.Lerp(start, target, smooth);
                pos.y += Mathf.Sin(p * Mathf.PI) * 90f;

                coin.position = pos;
                coin.localScale = Vector3.Lerp(Vector3.one * 0.7f, Vector3.one, smooth);
                coin.Rotate(0, 0, 420f * Time.deltaTime);

                yield return null;
            }

            Destroy(coin.gameObject);
        }

        public void ClearPotCoins()
        {
            foreach (var coin in potCoins)
            {
                if (coin != null)
                    Destroy(coin.gameObject);
            }

            potCoins.Clear();
        }
    }
}