namespace WebAPI.Infrastructure.Services.BaseContracts;

public interface IContractDeployer
{
    Task<DeployContractResponse> DeployAsync(IFormFile abiFile, IFormFile bytecodeFile,CancellationToken token=default);
}