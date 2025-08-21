namespace WebAPI.Infrastructure.Services.Rust;

public interface IRustContractDeployer : IContractDeployer;

public class RustContractDeployer : IRustContractDeployer
{
    public Task<DeployContractResponse> DeployAsync(IFormFile abiFile, IFormFile bytecodeFile, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }
}