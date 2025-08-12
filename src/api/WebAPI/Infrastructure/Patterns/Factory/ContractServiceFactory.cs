namespace WebAPI.Infrastructure.Patterns.Factory;

public class ContractServiceFactory(IServiceProvider serviceProvider) : IContractServiceFactory
{
    public IContractGenerator GetGenerator(SmartContractLanguage language) => language switch
    {
        SmartContractLanguage.Solidity => serviceProvider.GetRequiredService<ISolidityContractGenerator>(),
        _ => throw new NotSupportedException($"Generator for language {language} is not supported.")
    };

    public IContractCompiler GetCompiler(SmartContractLanguage language) => language switch
    {
        SmartContractLanguage.Solidity => serviceProvider.GetRequiredService<ISolidityContractCompiler>(),
        _ => throw new NotSupportedException($"Compiler for language {language} is not supported.")
    };

    public IContractDeployer GetDeployer(SmartContractLanguage language) => language switch
    {
        SmartContractLanguage.Solidity => serviceProvider.GetRequiredService<ISolidityContractDeployer>(),
        _ => throw new NotSupportedException($"Deployer for language {language} is not supported.")
    };
}