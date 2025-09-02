using ScGen.Lib.DTOs.Responses;

namespace ScGen.Lib.Contracts.BaseContracts;

public interface IContractDeploy
{
    Task<Result<DeployContractResponse>> DeployAsync(IFormFile abiFile, IFormFile bytecodeFile,CancellationToken token=default);
}