namespace WebAPI.Infrastructure.Services.Solidity;

public sealed class SolidityContractCompiler : ISolidityContractCompiler
{
    public async Task<CompileContractResponse> CompileAsync(IFormFile sourceCodeFile, CancellationToken token = default)
    {
        if (sourceCodeFile == null || sourceCodeFile.Length == 0)
            throw new ArgumentException("Source code file is empty.", nameof(sourceCodeFile));

        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        string sourceFilePath = Path.Combine(tempDir, sourceCodeFile.FileName);

        await using FileStream fs = new (sourceFilePath, FileMode.CreateNew);
        await sourceCodeFile.CopyToAsync(fs, token);
        await fs.FlushAsync(token);

        string outputDir = tempDir;

        ProcessStartInfo processStartInfo = new()
        {
            FileName = "solc",
            Arguments = $"--abi --bin --optimize -o \"{outputDir}\" \"{sourceFilePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process process = new ();
        process.StartInfo = processStartInfo;
        process.Start();

        Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync(token);
        Task<string> stdErrTask = process.StandardError.ReadToEndAsync(token);

        await Task.WhenAll(stdOutTask, stdErrTask);

        string stdError = stdErrTask.Result;

        await process.WaitForExitAsync(token);

        if (process.ExitCode != 0)
            throw new InvalidOperationException("Solidity compiler error: " + stdError);

        string contractName = Path.GetFileNameWithoutExtension(sourceCodeFile.FileName);

        string abiPath = Path.Combine(outputDir, contractName + ".abi");
        string binPath = Path.Combine(outputDir, contractName + ".bin");

        if (!File.Exists(abiPath) || !File.Exists(binPath))
            throw new FileNotFoundException("Compiled contract files not found.");

        byte[] abiBytes = await File.ReadAllBytesAsync(abiPath, token);
        byte[] binBytes = await File.ReadAllBytesAsync(binPath, token);

        try
        {
            Directory.Delete(tempDir, true);
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to delete temporary directory.", ex);
        }

        CompileContractResponse response = new CompileContractResponse
        {
            AbiFileContent = abiBytes,
            AbiFileName = contractName + ".abi",
            BytecodeFileContent = binBytes,
            BytecodeFileName = contractName + ".bin",
            ContentType = "application/octet-stream"
        };

        return response;
    }
}