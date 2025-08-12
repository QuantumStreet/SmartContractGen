namespace WebAPI.Infrastructure.Options;

public sealed class EthereumOptions
{
    public string RpcUrl { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
}