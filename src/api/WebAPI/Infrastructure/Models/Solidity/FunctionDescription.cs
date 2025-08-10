namespace WebAPI.Infrastructure.Models.Solidity;

public class FunctionDescription
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;

    [JsonProperty("params")] public List<FunctionParam> Params { get; set; } = [];

    [JsonProperty("visibility")] public string Visibility { get; set; } = "public";

    [JsonProperty("modifiers")] public List<string> Modifiers { get; set; } = [];

    [JsonProperty("returns")] public string Returns { get; set; } = string.Empty;

    [JsonProperty("body")] public List<string> Body { get; set; } = [];
}