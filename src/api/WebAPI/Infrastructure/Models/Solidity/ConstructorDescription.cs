namespace WebAPI.Infrastructure.Models.Solidity;

public class ConstructorDescription
{
    [JsonProperty("params")] public List<FunctionParam> Params { get; set; } = [];

    [JsonProperty("body")] public List<string> Body { get; set; } = [];
}