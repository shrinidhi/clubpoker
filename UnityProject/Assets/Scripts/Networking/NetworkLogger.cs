using UnityEngine;

namespace ClubPoker.Networking
{
    public static class NetworkLogger
    {
        #region Constants

        private const string LOG_TAG = "[NetworkLogger]";

        #endregion

        #region Public Methods

        public static void LogRequest(string method, string url, object body = null)
        {
            if (!IsDevelopmentBuild()) return;

            Debug.Log($"{LOG_TAG} → {method} {url}");

            if (body != null)
            {
                Debug.Log($"{LOG_TAG} → Request Body: {Newtonsoft.Json.JsonConvert.SerializeObject(body)}");
            }
        }

        public static void LogResponse(string method, string url, long responseCode, string responseBody)
        {
            if (!IsDevelopmentBuild()) return;

            Debug.Log($"{LOG_TAG} ← {method} {url} [{responseCode}]");
            Debug.Log($"{LOG_TAG} ← Response Body: {responseBody}");
        }

        public static void LogError(string method, string url, string error)
        {
            if (!IsDevelopmentBuild()) return;

            Debug.LogError($"{LOG_TAG} ✗ {method} {url} - Error: {error}");
        }

        public static void LogTokenRefresh()
        {
            if (!IsDevelopmentBuild()) return;

            Debug.Log($"{LOG_TAG} 🔄 Token refresh triggered");
        }

        public static void LogTokenRefreshSuccess()
        {
            if (!IsDevelopmentBuild()) return;
            Debug.Log($"{LOG_TAG} ✓ Token refresh successful - retrying request");
        }

        public static void LogTokenRefreshFailed()
        {
            if (!IsDevelopmentBuild()) return;

            Debug.LogError($"{LOG_TAG} ✗ Token refresh failed - forcing logout");
        }

        #endregion

        #region Private Methods

        private static bool IsDevelopmentBuild()
        {
            return Debug.isDebugBuild || Application.isEditor;
        }

        #endregion
    }
}