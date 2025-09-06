namespace ScGen.Lib.ImplContracts.Ethereum;

public sealed partial class EthereumContractDeploy(
    ILogger<EthereumContractDeploy> logger,
    IHttpContextAccessor httpContext,
    IOptions<EthereumOptions> options,
    IValidator<EthereumOptions> validator,
    IEthereumWeb3Factory ethereumFactory) : IEthereumContractDeploy
{
    private readonly EthereumOptions _options = options.Value;

    public async Task<Result<DeployContractResponse>> DeployAsync(IFormFile abiFile, IFormFile bytecodeFile,
        CancellationToken token = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        logger.OperationStarted(nameof(DeployAsync),
            httpContext.GetId().ToString(), httpContext.GetCorrelationId());
        try
        {
            Result<DeployContractResponse> validation = await ValidationAsync(abiFile, bytecodeFile, token);
            if (!validation.IsSuccess) return validation;

            if (!await EnsureNodeRunningAsync(token))
                return Result<DeployContractResponse>.Failure(
                    ResultPatternError.InternalServerError(Messages.EthereumNodeNotRunning));

            return await ExecuteContractDeploymentAsync(abiFile, bytecodeFile, token);
        }
        catch (Exception ex)
        {
            logger.OperationFailed(nameof(DeployAsync), ex.Message,
                httpContext.GetId().ToString(), httpContext.GetCorrelationId());
            return Result<DeployContractResponse>.Failure(ResultPatternError.InternalServerError(ex.Message));
        }
        finally
        {
            stopwatch.Stop();
            logger.OperationCompleted(nameof(DeployAsync),
                stopwatch.ElapsedMilliseconds, httpContext.GetCorrelationId());
        }
    }
}