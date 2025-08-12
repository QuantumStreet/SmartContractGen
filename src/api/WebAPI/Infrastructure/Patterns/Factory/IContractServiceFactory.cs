namespace WebAPI.Infrastructure.Patterns.Factory;

public interface IContractServiceFactory
{
    IContractGenerator GetGenerator(SmartContractLanguage language);
    IContractCompiler GetCompiler(SmartContractLanguage language);
    IContractDeployer GetDeployer(SmartContractLanguage language);
}