namespace WebAPI.Infrastructure.Services.Solidity;

public sealed class SolidityContractDeployer:ISolidityContractDeployer
{
    public Task<DeployContractResponse> DeployAsync(IFormFile abiFile, IFormFile bytecodeFile,CancellationToken token=default)
    {
        throw new NotImplementedException();
    }
}