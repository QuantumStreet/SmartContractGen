namespace WebAPI.Infrastructure.Options;

public sealed class SolanaOptions
{
    public string RpcUrl { get; set; } = "http://localhost:8899";
    public string KeypairPath { get; set; } = "~/.config/solana/id.json";
    public bool UseLocalValidator { get; set; } = true;
    public bool StopValidatorAfterDeploy { get; set; } = false;
}