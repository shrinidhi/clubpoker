using System.Collections.Generic;
using Newtonsoft.Json;

namespace ClubPoker.Core
{
    public class VariantsResponse
    {
        [JsonProperty("variants")]
        public List<PokerVariant> Variants { get; set; }
    }

    public class PokerVariant
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("holeCards")]
        public int HoleCards { get; set; }

        [JsonProperty("communityCards")]
        public int CommunityCards { get; set; }

        [JsonProperty("minPlayers")]
        public int MinPlayers { get; set; }

        [JsonProperty("maxPlayers")]
        public int MaxPlayers { get; set; }

        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("iconKey")]
        public string IconKey { get; set; }
    }
}