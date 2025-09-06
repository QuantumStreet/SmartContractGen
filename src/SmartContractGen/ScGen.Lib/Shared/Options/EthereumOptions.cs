namespace ScGen.Lib.Shared.Options;

public sealed class EthereumOptions
{
    public string RpcUrl { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public BigInteger GasLimit { get; set; } = 3000000;
}