namespace WebAPI.Infrastructure.Models.Solidity;

public class EventParam
{
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;

    [JsonProperty("name")] public string Name { get; set; } = string.Empty;

    [JsonProperty("indexed")] public bool Indexed { get; set; }
}