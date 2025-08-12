namespace WebAPI.Infrastructure.Services.Solidity;

public sealed class SolidityContractCompiler:ISolidityContractCompiler
{
    public Task<CompileContractResponse> CompileAsync(IFormFile sourceCodeFile,CancellationToken token=default)
    {
        throw new NotImplementedException();
    }
}