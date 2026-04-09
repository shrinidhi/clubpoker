using System;
using Newtonsoft.Json;

namespace ClubPoker.Networking
{
    [Serializable]
    public class ApiResponse<T>
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("data")]
        public T Data { get; set; }

        [JsonProperty("error")]
        public ApiError Error { get; set; }

        public bool IsSuccess => Status == "ok" || Status == "success";
    }

    [Serializable]
    public class ApiError
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        // Some errors have extra fields like lockoutRemainingSeconds
        [JsonProperty("lockoutRemainingSeconds")]
        public int? LockoutRemainingSeconds { get; set; }
    }
}