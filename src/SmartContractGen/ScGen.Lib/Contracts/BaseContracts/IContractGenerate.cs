using ScGen.Lib.Shared.DTOs.Responses;

namespace ScGen.Lib.Contracts.BaseContracts;

public interface IContractGenerate
{
    Task<Result<GenerateContractResponse>> GenerateAsync(IFormFile jsonFile, CancellationToken token = default);
}