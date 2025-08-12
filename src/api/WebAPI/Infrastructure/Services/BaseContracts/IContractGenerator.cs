namespace WebAPI.Infrastructure.Services.BaseContracts;

public interface IContractGenerator
{
    Task<GenerateContractResponse> GenerateAsync(IFormFile jsonFile,CancellationToken token=default);
}