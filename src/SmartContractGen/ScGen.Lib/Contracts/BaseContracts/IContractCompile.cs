using ScGen.Lib.Shared.DTOs.Responses;

namespace ScGen.Lib.Contracts.BaseContracts;

public interface IContractCompile<T> where T : BaseCompileContractResponse
{
    Task<Result<T>> CompileAsync(IFormFile sourceCodeFile, CancellationToken token = default);
}