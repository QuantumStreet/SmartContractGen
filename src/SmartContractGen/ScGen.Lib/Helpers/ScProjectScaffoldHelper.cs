namespace ScGen.Lib.Helpers;
public static class ScProjectScaffoldHelper
{
    public static void CreateEthereumProjectTemplate(string projectPath, ILogger logger)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        logger.OperationStarted(nameof(CreateEthereumProjectTemplate));

        if (!Directory.Exists(projectPath))
            Directory.CreateDirectory(projectPath);

        string contractsPath = Path.Combine(projectPath, KeyNames.Contracts);
        if (!File.Exists(Path.Combine(contractsPath)))
            File.Create(contractsPath);

        stopwatch.Stop();
        logger.OperationCompleted(nameof(CreateEthereumProjectTemplate), stopwatch.ElapsedMilliseconds);
    }
}