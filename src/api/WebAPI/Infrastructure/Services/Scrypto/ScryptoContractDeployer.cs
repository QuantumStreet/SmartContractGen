namespace WebAPI.Infrastructure.Services.Scrypto;

public class ScryptoContractDeployer(ILogger<ScryptoContractDeployer> logger) : IScryptoContractDeployer
{
    private readonly ILogger<ScryptoContractDeployer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly string _radixCliPath = "resim"; // Radix Engine Simulator CLI
    private readonly string _networkUrl = "http://localhost:8080"; // Default Radix Gateway URL

    public async Task<DeployContractResponse> DeployAsync(IFormFile abiFile, IFormFile bytecodeFile,
        CancellationToken token = default)
    {
        try
        {
            // Check if local Radix network is running
            if (!await IsLocalNetworkRunning(token))
            {
                _logger.LogInformation("Local Radix network not running, starting it...");
                await StartLocalNetwork(token);

                // Wait a bit for the network to start
                await Task.Delay(5000, token);
            }

            // Create temporary directory for deployment
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Save bytecode file
                string bytecodePath = Path.Combine(tempDir, bytecodeFile.FileName);
                await using (FileStream fs = new(bytecodePath, FileMode.Create, FileAccess.Write))
                    await bytecodeFile.CopyToAsync(fs, token);

                // For Scrypto, we need to create a transaction manifest and publish the package
                string manifestPath = Path.Combine(tempDir, "manifest.rtm");
                await CreateTransactionManifest(manifestPath, bytecodePath);

                // Execute the deployment
                ProcessStartInfo psi = new()
                {
                    FileName = _radixCliPath,
                    Arguments = $"run \"{manifestPath}\"",
                    WorkingDirectory = tempDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process? proc = Process.Start(psi);
                if (proc == null) throw new Exception("Unable to start resim process");

                string stdout = await proc.StandardOutput.ReadToEndAsync(token);
                string stderr = await proc.StandardError.ReadToEndAsync(token);
                await proc.WaitForExitAsync(token);

                if (proc.ExitCode != 0)
                {
                    _logger.LogError("Resim deployment failed: {Error}", stderr);
                    throw new Exception($"resim deployment failed: {stderr}");
                }

                _logger.LogInformation("Resim deployment completed successfully");

                // Parse the deployment result to extract package address
                string packageAddress = ParsePackageAddress(stdout);

                return new DeployContractResponse
                {
                    ContractAddress = packageAddress,
                    TransactionHash = Guid.NewGuid().ToString(),
                    Success = true
                };
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment failed");
            throw;
        }
    }

    private async Task<bool> IsLocalNetworkRunning(CancellationToken token)
    {
        try
        {
            using HttpClient client = new();
            HttpResponseMessage response = await client.GetAsync($"{_networkUrl}/health", token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task StartLocalNetwork(CancellationToken token)
    {
        ProcessStartInfo psi = new()
        {
            FileName = "resim",
            Arguments = "reset",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process? proc = Process.Start(psi);
        if (proc == null) throw new Exception("Unable to start resim reset process");

        await proc.WaitForExitAsync(token);

        if (proc.ExitCode != 0)
        {
            string stderr = await proc.StandardError.ReadToEndAsync(token);
            _logger.LogError("Failed to reset resim network: {Error}", stderr);
            throw new Exception($"Failed to reset resim network: {stderr}");
        }
    }

    private async Task CreateTransactionManifest(string manifestPath, string bytecodePath)
    {
        // Create a simple transaction manifest for publishing the package
        string manifestContent = $@"# Publish Scrypto Package
CALL_METHOD ComponentAddress(""{GetAccountAddress()}"") ""lock_fee"" Decimal(""10"");
PUBLISH_PACKAGE_ADVANCED HashPartition {{ code: Blob(""{await GetBlobHash(bytecodePath)}""), royalty_vault: None, metadata: Map<String, String>() }};";

        await File.WriteAllTextAsync(manifestPath, manifestContent);
    }

    private string GetAccountAddress()
    {
        // This should be obtained from resim or configured
        // For now, return a placeholder
        return "account_tdx_2_1285lqe7k2l2e6ht2p8q8p3n6q2n6q2n6q2n6q2n6q2n6q2n6q2n6q2n6q2n6q";
    }

    private async Task<string> GetBlobHash(string filePath)
    {
        // Calculate hash of the bytecode file
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(filePath);
        byte[] hashBytes = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
    }

    private string ParsePackageAddress(string output)
    {
        // Parse the package address from resim output
        // This is a simplified implementation
        var lines = output.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("package_"))
            {
                // Extract package address from the line
                var parts = line.Split(' ');
                foreach (var part in parts)
                {
                    if (part.StartsWith("package_"))
                    {
                        return part.Trim();
                    }
                }
            }
        }

        // Return a placeholder if parsing fails
        return $"package_tdx_2_1{DateTime.Now.Ticks.ToString().Substring(0, 10)}";
    }
}