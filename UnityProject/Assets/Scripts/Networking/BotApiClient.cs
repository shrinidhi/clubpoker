using System;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using ClubPoker.Core;
using ClubPoker.Networking.Models;
using ClubPoker.Networking;

public static class BotApiClient
    {
        public static async UniTask<T> Post<T>(string endpoint, object body, string token = null)
        {
            string url = ConfigManager.Instance.Config.apiBaseUrl + endpoint;

            using UnityWebRequest req = new UnityWebRequest(url, "POST");

            string json = JsonConvert.SerializeObject(body);
            byte[] raw = Encoding.UTF8.GetBytes(json);

            req.uploadHandler = new UploadHandlerRaw(raw);
            req.downloadHandler = new DownloadHandlerBuffer();

            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/json");

            if (!string.IsNullOrEmpty(token))
                req.SetRequestHeader("Authorization", "Bearer " + token);

            req.timeout = 10;

            await req.SendWebRequest();

            Debug.Log($"[BotApi] POST {url} [{req.responseCode}] {req.downloadHandler.text}");

            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception(req.downloadHandler.text);

            var response = JsonConvert.DeserializeObject<ApiResponse<T>>(req.downloadHandler.text);

            if (response == null || !response.IsSuccess)
                throw new Exception(response?.Error?.Message ?? "Bot API failed");

            return response.Data;
        }
    }
