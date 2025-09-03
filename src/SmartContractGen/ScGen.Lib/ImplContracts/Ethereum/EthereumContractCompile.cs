namespace ScGen.Lib.ImplContracts.Ethereum;

public sealed class EthereumContractCompile : IEthereumContractCompile
{
    public Task<Result<EthereumCompileContractResponse>> CompileAsync(IFormFile sourceCodeFile, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }
}