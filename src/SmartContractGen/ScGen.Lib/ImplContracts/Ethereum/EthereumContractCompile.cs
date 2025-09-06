namespace ScGen.Lib.ImplContracts.Ethereum;

public sealed partial class EthereumContractCompile(
    ILogger<EthereumContractCompile> logger,
    IHttpContextAccessor httpContext) : IEthereumContractCompile
{
    private const long MaxFileSize = 100 * 1024 * 1024;

    public async Task<Result<EthereumCompileContractResponse>> CompileAsync(IFormFile sourceCodeFile,
        CancellationToken token = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        logger.OperationStarted(nameof(CompileAsync),
            httpContext.GetId().ToString(), httpContext.GetCorrelationId());

        Result<EthereumCompileContractResponse> validation = Validation(sourceCodeFile);
        if (!validation.IsSuccess) return validation;

        (string tempDir, string sourceFilePath) = await SaveSourceFileAsync(sourceCodeFile, token);
        try
        {
            ProcessExecutionResult result = await ProcessExtensions
                .RunSolcAsync(sourceFilePath, tempDir,logger, token);
            if (!result.IsSuccess)
                return Result<EthereumCompileContractResponse>.Failure(
                    ResultPatternError.InternalServerError(result.StandardError + result.GetErrorMessage()));

            string contractName = Path.GetFileNameWithoutExtension(sourceFilePath);

            return Result<EthereumCompileContractResponse>.Success(await CreateResponseAsync(tempDir, contractName, token));
        }
        catch (Exception ex)
        {
            logger.OperationFailed(nameof(CompileAsync), ex.Message,
                httpContext.GetId().ToString(), httpContext.GetCorrelationId());
            return Result<EthereumCompileContractResponse>.Failure(ResultPatternError.InternalServerError(ex.Message));
        }
        finally
        {
            await CleanupTempDirectoryAsync(tempDir);
            stopwatch.Stop();
            logger.OperationCompleted(nameof(CompileAsync),
                stopwatch.ElapsedMilliseconds, httpContext.GetCorrelationId());
        }
    }
}