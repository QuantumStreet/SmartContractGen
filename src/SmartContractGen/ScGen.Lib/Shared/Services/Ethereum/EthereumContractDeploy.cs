using ValidationResult = FluentValidation.Results.ValidationResult;

namespace ScGen.Lib.ImplContracts.Ethereum;

public sealed partial class EthereumContractDeploy
{
    private static int? _processId;

    private async Task<Result<DeployContractResponse>> ValidationAsync(IFormFile abiFile, IFormFile bytecodeFile,
        CancellationToken token)
    {
        ValidationResult validationResult = await validator.ValidateAsync(_options, token);
        if (!validationResult.IsValid)
        {
            string errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            logger.ValidationFailed(nameof(DeployAsync), errors, httpContext.GetId().ToString());
            return Result<DeployContractResponse>.Failure(ResultPatternError.BadRequest(errors));
        }

        if (!abiFile.IsAbiFile())
        {
            logger.ValidationFailed(nameof(DeployAsync),
                Messages.InvalidAbiFile, httpContext.GetId().ToString());
            return Result<DeployContractResponse>.Failure(ResultPatternError.BadRequest(Messages.InvalidAbiFile));
        }

        if (abiFile.Length == 0)
        {
            logger.ValidationFailed(nameof(DeployAsync),
                Messages.EmptyAbi, httpContext.GetId().ToString());
            return Result<DeployContractResponse>.Failure(ResultPatternError.BadRequest(Messages.EmptyAbi));
        }

        if (!bytecodeFile.IsEthereumBinFile())
        {
            logger.ValidationFailed(nameof(DeployAsync),
                Messages.InvalidBytecodeFile, httpContext.GetId().ToString());
            return Result<DeployContractResponse>.Failure(ResultPatternError.BadRequest(Messages.InvalidBytecodeFile));
        }

        if (bytecodeFile.Length == 0)
        {
            logger.ValidationFailed(nameof(DeployAsync),
                Messages.EmptyBytecode, httpContext.GetId().ToString());
            return Result<DeployContractResponse>.Failure(ResultPatternError.BadRequest(Messages.EmptyBytecode));
        }

        return Result<DeployContractResponse>.Success();
    }


    private async Task<bool> EnsureNodeRunningAsync(CancellationToken cancellationToken = default)
    {
        using (logger.BeginScopedOperation(nameof(EnsureNodeRunningAsync), httpContext.GetId().ToString(),
                   httpContext.GetCorrelationId(), PerformanceThreshold.Normal, true))
        {
            try
            {
                if (await CheckIfRunningAsync(cancellationToken))
                {
                    logger.OperationCompleted(nameof(EnsureNodeRunningAsync), 0, httpContext.GetCorrelationId());
                    return true;
                }

                bool started = await StartNodeAsync(cancellationToken);
                if (!started)
                {
                    logger.OperationFailed(nameof(EnsureNodeRunningAsync), Messages.FailedToStartNode,
                        httpContext.GetId().ToString(), httpContext.GetCorrelationId());
                    return false;
                }

                logger.OperationCompleted(nameof(EnsureNodeRunningAsync), 0, httpContext.GetCorrelationId());
                return true;
            }
            catch (Exception ex)
            {
                logger.OperationFailedWithException(nameof(EnsureNodeRunningAsync), ex.Message,
                    httpContext.GetCorrelationId());
                return false;
            }
        }
    }

    private async Task<Result<DeployContractResponse>> ExecuteContractDeploymentAsync(IFormFile abiFile, IFormFile bytecodeFile,
        CancellationToken token)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        logger.OperationStarted(nameof(ExecuteContractDeploymentAsync),
            httpContext.GetId().ToString(), httpContext.GetCorrelationId());

        string abi;
        string bytecode;

        using (StreamReader reader = new(abiFile.OpenReadStream()))
            abi = await reader.ReadToEndAsync(token);

        using (StreamReader reader = new(bytecodeFile.OpenReadStream()))
            bytecode = await reader.ReadToEndAsync(token);

        logger.LogInformation(Messages.StartDeploymentInEthereum(_options.RpcUrl));

        Nethereum.Web3.Accounts.Account account = new(_options.PrivateKey);
        Web3 web3 = ethereumFactory.CreateWeb3WithAccount(_options.RpcUrl, _options.PrivateKey);

        try
        {
            TransactionReceipt receipt = await web3.Eth.DeployContract
                .SendRequestAndWaitForReceiptAsync(
                    abi, bytecode, account.Address,
                    new Nethereum.Hex.HexTypes.HexBigInteger(_options.GasLimit)
                );

            bool success = receipt.Status.Value == 1;

            if (success)
                logger.LogInformation(Messages.ContractSuccessfullyDeployed(receipt.ContractAddress));
            else
                logger.LogError(Messages.ContractFailedDeployed(receipt.Status.Value));

            return Result<DeployContractResponse>.Success(new()
            {
                ContractAddress = receipt.ContractAddress,
                TransactionHash = receipt.TransactionHash,
                Success = success
            });
        }
        catch (Exception ex)
        {
            logger.OperationFailedWithException(nameof(ExecuteContractDeploymentAsync), ex.Message,
                httpContext.GetCorrelationId());
            return Result<DeployContractResponse>.Failure(
                ResultPatternError.InternalServerError(ex.Message));
        }
        finally
        {
            stopwatch.Stop();
            logger.OperationCompleted(nameof(ExecuteContractDeploymentAsync),
                stopwatch.ElapsedMilliseconds, httpContext.GetCorrelationId());
        }
    }

    public async Task<bool> CheckIfRunningAsync(CancellationToken cancellationToken = default)
    {
        using (logger.BeginScopedOperation(nameof(CheckIfRunningAsync), httpContext.GetId().ToString(),
                   httpContext.GetCorrelationId(), PerformanceThreshold.Fast, true))
        {
            try
            {
                await ethereumFactory.CreateWeb3(_options.RpcUrl)
                    .Eth.Blocks.GetBlockNumber.SendRequestAsync();
                return true;
            }
            catch (Exception ex)
            {
                logger.OperationFailedWithException(nameof(CheckIfRunningAsync),
                    ex.Message, httpContext.GetCorrelationId());
                return false;
            }
        }
    }

    public async Task<bool> StartNodeAsync(CancellationToken cancellationToken = default)
    {
        using (logger.BeginScopedOperation(nameof(StartNodeAsync), httpContext.GetId().ToString(),
                   httpContext.GetCorrelationId(), PerformanceThreshold.Normal, true))
        {
            try
            {
                using Process process = new();
                if (await CheckIfRunningAsync(cancellationToken)) return true;

                ProcessExecutionResult result =
                    await process.RunGanacheAsync(logger, _options.RpcUrl.GetPort(), cancellationToken: cancellationToken);
                if (!result.IsSuccess)
                {
                    logger.OperationFailed(nameof(StartNodeAsync), result.GetErrorMessage() + result.StandardError,
                        httpContext.GetId().ToString(), httpContext.GetCorrelationId());
                    return false;
                }

                _processId = result.ProcessId;

                return true;
            }
            catch (Exception e)
            {
                logger.OperationFailedWithException(nameof(StartNodeAsync),
                    e.Message, httpContext.GetCorrelationId());
                return false;
            }
        }
    }

    public bool StopNode(CancellationToken cancellationToken = default)
    {
        using (logger.BeginScopedOperation(nameof(StopNode), httpContext.GetId().ToString(),
                   httpContext.GetCorrelationId(), PerformanceThreshold.Normal, true))
        {
            if (_processId == null)
            {
                logger.OperationFailed(nameof(StopNode), Messages.NodeProcessNotRun,
                    httpContext.GetId().ToString(), httpContext.GetCorrelationId());
                return true;
            }

            try
            {
                using Process process = Process.GetProcessById(_processId.Value);
                if (process.HasExited)
                {
                    _processId = null;
                }
                else
                {
                    process.Kill(true);
                    _processId = null;
                }

                return true;
            }
            catch (Exception e)
            {
                logger.OperationFailedWithException(nameof(StopNode),
                    e.Message, httpContext.GetCorrelationId());
                return false;
            }
        }
    }
}