namespace WebAPI.Infrastructure.Services.Rust;

public interface IRustContractGenerator : IContractGenerator;

public class RustContractGenerator : IRustContractGenerator
{
    public Task<GenerateContractResponse> GenerateAsync(IFormFile jsonFile, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }
}