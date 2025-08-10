namespace WebAPI.Infrastructure.Models.Solidity;

public class SolidityContractDescription : ContractDescription
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;

    [JsonProperty("license")] public string License { get; set; } = "MIT";

    [JsonProperty("pragmaVersion")] public string PragmaVersion { get; set; } = "0.8.0";

    [JsonProperty("imports")] public List<string> Imports { get; set; } = [];

    [JsonProperty("state")] public List<StateVariable> State { get; set; } = [];

    [JsonProperty("events")] public List<EventDescription> Events { get; set; } = [];
    [JsonProperty("functions")] public List<FunctionDescription> Functions { get; set; } = [];
    [JsonProperty("modifiers")] public List<ModifierDescription> Modifiers { get; set; } = [];

    [JsonProperty("constructor")] public ConstructorDescription Constructor { get; set; } = new();
}