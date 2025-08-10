namespace WebAPI.Infrastructure.Services;

public interface IContractGenerator
{
    (string Code, string Extension) Generate(string contract);
}

public interface IContractCompiler
{
  
    Task<(string Abi, string Bytecode)> CompileAsync(string code, string language);
}

public interface IContractDeployer
{
   
    Task<string> DeployAsync(string abi, string bytecode);
}
