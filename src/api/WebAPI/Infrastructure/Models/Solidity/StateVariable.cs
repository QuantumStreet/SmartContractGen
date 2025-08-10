namespace WebAPI.Infrastructure.Models.Solidity;

public class StateVariable
{
    [JsonProperty("visibility")] public string Visibility { get; set; } = "public";

    [JsonProperty("type")] public string Type { get; set; } = string.Empty;

    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
}