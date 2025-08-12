namespace WebAPI.Infrastructure.Extensions.DI;

public static class RegisterLocalServices
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<ISolidityContractGenerator, SolidityContractGenerator>();
        services.AddScoped<IContractGenerator>(provider => provider.GetRequiredService<ISolidityContractGenerator>());

        services.AddScoped<ISolidityContractCompiler, SolidityContractCompiler>();
        services.AddScoped<IContractCompiler>(provider => provider.GetRequiredService<ISolidityContractCompiler>());

        services.AddScoped<ISolidityContractDeployer, SolidityContractDeployer>();
        services.AddScoped<IContractDeployer>(provider => provider.GetRequiredService<ISolidityContractDeployer>());

        services.AddScoped<IContractServiceFactory, ContractServiceFactory>();
        services.AddSingleton<IHandlebars>(Handlebars.Create());
        return services;
    }
}