namespace WebAPI.Infrastructure.Options;

public class RadixOptions
{
    public const string SectionName = "Radix";
    
    public string NetworkUrl { get; set; } = "http://localhost:8080";
    public bool UseLocalSimulator { get; set; } = true;
    public bool StopSimulatorAfterDeploy { get; set; } = false;
    public string DefaultAccountPath { get; set; } = "~/.radix/accounts/default.json";
}