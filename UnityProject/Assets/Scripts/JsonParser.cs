using System.Collections.Generic;
using Newtonsoft.Json;

[System.Serializable]
public class LobbyVariantResponse
{
    [JsonProperty("lobbyvariants")]
    public List<LobbyVariantData> LobbyVariants { get; set; }
}

[System.Serializable]
public class LobbyVariantData
{
    [JsonProperty("variantKey")]
    public string VariantKey { get; set; }

    [JsonProperty("variantName")]
    public string VariantName { get; set; }

    [JsonProperty("isLocked")]
    public bool IsLocked { get; set; }
}