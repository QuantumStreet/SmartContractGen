namespace WebAPI.Infrastructure.Models.Solidity;

public class FunctionParam
{
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;

    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
}