namespace WebAPI.Infrastructure.Services.Solidity;

public sealed class SolidityContractDeployer(
    IOptions<EthereumOptions> options,
    ILogger<SolidityContractDeployer> logger ) : ISolidityContractDeployer
{
    private readonly EthereumOptions _options = options.Value;

    public async Task<DeployContractResponse> DeployAsync(
        IFormFile abiFile,
        IFormFile bytecodeFile,
        CancellationToken token = default)
    {
        string abi;
        string bytecode;

        using (StreamReader reader = new(abiFile.OpenReadStream()))
            abi = await reader.ReadToEndAsync(token);

        using (StreamReader reader = new(bytecodeFile.OpenReadStream()))
            bytecode = await reader.ReadToEndAsync(token);

        logger.LogInformation("Starting contract deployment to {RpcUrl}...", _options.RpcUrl);

        Account account = new(_options.PrivateKey);
        Web3 web3 = new(account, _options.RpcUrl);

        try
        {
            TransactionReceipt receipt = await web3.Eth.DeployContract
                .SendRequestAndWaitForReceiptAsync(
                    abi, bytecode, account.Address,
                    new Nethereum.Hex.HexTypes.HexBigInteger(3000000)
                );

            bool success = receipt.Status.Value == 1;

            if (receipt.Status.Value == 1)
                logger.LogInformation("Contract successfully deployed at address: {ContractAddress}", receipt.ContractAddress);
            else
                logger.LogError("Contract deployment failed. Transaction status: {Status}", receipt.Status.Value);


            return new()
            {
                ContractAddress = receipt.ContractAddress,
                TransactionHash = receipt.TransactionHash,
                Success = success
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred during contract deployment");
            throw;
        }
    }
}