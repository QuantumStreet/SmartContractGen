namespace WebAPI.Infrastructure.Models.Solidity;

public class ModifierDescription
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;

    [JsonProperty("body")] public List<string> Body { get; set; } = [];
}