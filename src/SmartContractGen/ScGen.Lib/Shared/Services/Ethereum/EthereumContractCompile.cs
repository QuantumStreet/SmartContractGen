using ScGen.Lib.Shared.Validation;

namespace ScGen.Lib.Shared.Services.Ethereum
{
}

namespace ScGen.Lib.ImplContracts.Ethereum
{
    public sealed partial class EthereumContractCompile
    {
        private Result<EthereumCompileContractResponse> Validation(IFormFile file)
        {
            if (!file.IsSolidityFile())
            {
                logger.ValidationFailed(nameof(CompileAsync),
                    Messages.InvalidSolidityFile, httpContext.GetId().ToString());
                return Result<EthereumCompileContractResponse>.Failure(
                    ResultPatternError.BadRequest(Messages.InvalidSolidityFile));
            }

            if (file.Length == 0)
            {
                logger.ValidationFailed(nameof(CompileAsync),
                    Messages.EmptyFile, httpContext.GetId().ToString());
                return Result<EthereumCompileContractResponse>.Failure(ResultPatternError.BadRequest(Messages.EmptyFile));
            }

            if (file.Length > MaxFileSize)
            {
                logger.ValidationFailed(nameof(CompileAsync),
                    Messages.FileTooLarge, httpContext.GetId().ToString());
                return Result<EthereumCompileContractResponse>.Failure(ResultPatternError.BadRequest(Messages.FileTooLarge));
            }

            return Result<EthereumCompileContractResponse>.Success();
        }

        private async Task<(string, string)> SaveSourceFileAsync(IFormFile sourceCodeFile, CancellationToken token)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            string sourceFilePath = Path.Combine(tempDir, sourceCodeFile.GetSoliditySafeFileName());

            await using FileStream fs = new(sourceFilePath, FileMode.CreateNew);
            await sourceCodeFile.CopyToAsync(fs, token);
            return (tempDir, sourceFilePath);
        }

        private async Task CleanupTempDirectoryAsync(string tempDir)
        {
            if (string.IsNullOrEmpty(tempDir) || !Directory.Exists(tempDir))
                return;
            try
            {
                await Task.Delay(100);

                Directory.Delete(tempDir, recursive: true);
                logger.LogInformation("Successfully cleaned up temporary directory: {TempDir}", tempDir);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to cleanup temporary directory: {TempDir}", tempDir);
            }
        }

        private async Task<EthereumCompileContractResponse> CreateResponseAsync(
            string outputDir, string contractName, CancellationToken token)
        {
            string abiPath = Path.Combine(outputDir, $"{contractName}.abi");
            string binPath = Path.Combine(outputDir, $"{contractName}.bin");

            if (!File.Exists(abiPath))
                throw new FileNotFoundException($"ABI file not found: {abiPath}");

            if (!File.Exists(binPath))
                throw new FileNotFoundException($"Bytecode file not found: {binPath}");

            Task<byte[]> abiTask = File.ReadAllBytesAsync(abiPath, token);
            Task<byte[]> binTask = File.ReadAllBytesAsync(binPath, token);

            byte[] abiBytes = await abiTask;
            byte[] binBytes = await binTask;

            logger.LogInformation(
                "Successfully compiled contract '{ContractName}'. ABI size: {AbiSize} bytes, Bytecode size: {BytecodeSize} bytes",
                contractName, abiBytes.Length, binBytes.Length);

            return new EthereumCompileContractResponse()
            {
                Abi = abiBytes,
                AbiFileName = $"{contractName}.abi",
                CompiledCode = binBytes,
                CompiledCodeFileName = $"{contractName}.bin",
                ContentType = "application/octet-stream"
            };
        }
    }
}