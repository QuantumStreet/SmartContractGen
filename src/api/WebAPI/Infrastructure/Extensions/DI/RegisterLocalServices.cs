namespace WebAPI.Infrastructure.Extensions.DI;

public static class RegisterLocalServices
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<ISolidityContractGenerator, SolidityContractGenerator>();
        services.AddScoped<IContractGenerator>(provider => provider.GetRequiredService<ISolidityContractGenerator>());

        services.AddScoped<IRustContractGenerator, RustContractGenerator>();
        services.AddScoped<IContractGenerator>(provider => provider.GetRequiredService<IRustContractGenerator>());

        services.AddScoped<ISolidityContractCompiler, SolidityContractCompiler>();
        services.AddScoped<IContractCompiler>(provider => provider.GetRequiredService<ISolidityContractCompiler>());

        services.AddScoped<IRustContractCompiler, RustContractCompiler>();
        services.AddScoped<IContractCompiler>(provider => provider.GetRequiredService<IRustContractCompiler>());
        
        services.AddScoped<ISolidityContractDeployer, SolidityContractDeployer>();
        services.AddScoped<IContractDeployer>(provider => provider.GetRequiredService<ISolidityContractDeployer>());

        services.AddScoped<IRustContractDeployer, RustContractDeployer>();
        services.AddScoped<IContractDeployer>(provider => provider.GetRequiredService<IRustContractDeployer>());
        
        services.AddScoped<IContractServiceFactory, ContractServiceFactory>();
        services.AddSingleton(Handlebars.Create());

        return services;
    }
}