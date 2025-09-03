namespace ScGen.Lib.ImplContracts.Ethereum;

public sealed class EthereumContractDeploy : IEthereumContractDeploy
{
    public Task<Result<DeployContractResponse>> DeployAsync(IFormFile abiFile, IFormFile bytecodeFile, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }
}