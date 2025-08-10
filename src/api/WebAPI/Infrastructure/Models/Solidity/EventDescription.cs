namespace WebAPI.Infrastructure.Models.Solidity;

public class EventDescription
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;

    [JsonProperty("params")] public List<EventParam> Params { get; set; } = [];
}