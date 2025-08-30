namespace WebAPI.Infrastructure.Patterns.Factory;

public class ContractServiceFactory(IServiceProvider serviceProvider) : IContractServiceFactory
{
    public IContractGenerator GetGenerator(SmartContractLanguage language) => language switch
    {
        SmartContractLanguage.Solidity => serviceProvider.GetRequiredService<ISolidityContractGenerator>(),
        SmartContractLanguage.Rust => serviceProvider.GetRequiredService<IRustContractGenerator>(),
        SmartContractLanguage.Scrypto => serviceProvider.GetRequiredService<IScryptoContractGenerator>(),
        _ => throw new NotSupportedException($"Generator for language {language} is not supported.")
    };

    public IContractCompiler GetCompiler(SmartContractLanguage language) => language switch
    {
        SmartContractLanguage.Solidity => serviceProvider.GetRequiredService<ISolidityContractCompiler>(),
        SmartContractLanguage.Rust => serviceProvider.GetRequiredService<IRustContractCompiler>(),
        SmartContractLanguage.Scrypto => serviceProvider.GetRequiredService<IScryptoContractCompiler>(),
        _ => throw new NotSupportedException($"Compiler for language {language} is not supported.")
    };

    public IContractDeployer GetDeployer(SmartContractLanguage language) => language switch
    {
        SmartContractLanguage.Solidity => serviceProvider.GetRequiredService<ISolidityContractDeployer>(),
        SmartContractLanguage.Rust => serviceProvider.GetRequiredService<IRustContractDeployer>(),
        SmartContractLanguage.Scrypto => serviceProvider.GetRequiredService<IScryptoContractDeployer>(),
        _ => throw new NotSupportedException($"Deployer for language {language} is not supported.")
    };
}