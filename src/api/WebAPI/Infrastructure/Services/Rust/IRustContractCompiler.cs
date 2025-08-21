namespace WebAPI.Infrastructure.Services.Rust;

public interface IRustContractCompiler : IContractCompiler;

public class RustContractCompiler : IRustContractCompiler
{
    public Task<CompileContractResponse> CompileAsync(IFormFile sourceCodeFile, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }
}