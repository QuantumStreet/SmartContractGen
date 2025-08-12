namespace WebAPI.Infrastructure.Services.BaseContracts;

public interface IContractCompiler
{
    Task<CompileContractResponse> CompileAsync(IFormFile sourceCodeFile,CancellationToken token=default);
}