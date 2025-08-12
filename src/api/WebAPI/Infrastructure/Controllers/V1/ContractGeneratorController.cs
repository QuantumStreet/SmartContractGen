namespace WebAPI.Infrastructure.Controllers.V1;

[ApiController]
[Route($"{ApiAddresses.Base}/contracts")]
public sealed class ContractGeneratorController(IContractServiceFactory factory) : BaseController
{
    [HttpPost("generate")]
    public async Task<IActionResult> ContractGenerateAsync([FromForm] GenerateContractRequest request)
    {
        if (!request.JsonFile.IsJsonFile())
            return BadRequest("Invalid JSON file.");

        IContractGenerator generator = factory.GetGenerator(request.Language);
        GenerateContractResponse result = await generator.GenerateAsync(request.JsonFile);

        return File(result.ContractFileContent, result.ContentType, result.FileName);
    }

    [HttpPost("compile")]
    public async Task<IActionResult> ContractCompileAsync([FromForm] CompileContractRequest request)
    {
        if (!request.SourceCodeFile.IsContractFile())
            return BadRequest("Invalid source code file.");

        IContractCompiler compiler = factory.GetCompiler(request.Language);
        CompileContractResponse compileResult = await compiler.CompileAsync(request.SourceCodeFile);

        using MemoryStream zipStream = new ();
        using (ZipArchive archive = new (zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry abiEntry = archive.CreateEntry(compileResult.AbiFileName);
            await using (Stream entryStream = abiEntry.Open())
                await entryStream.WriteAsync(compileResult.AbiFileContent);

            ZipArchiveEntry binEntry = archive.CreateEntry(compileResult.BytecodeFileName);
            await using (Stream entryStream = binEntry.Open()) await entryStream.WriteAsync(compileResult.BytecodeFileContent);
        }

        zipStream.Position = 0;
        string zipFileName = "contract_artifacts.zip";

        return File(zipStream.ToArray(), "application/zip", zipFileName);
    }


    [HttpPost("deploy")]
    public async Task<IActionResult> ContractDeployAsync([FromForm] DeployContractRequest request)
    {
        var deployer = factory.GetDeployer(request.Language);
        var deployResult = await deployer.DeployAsync(request.AbiFile, request.BytecodeFile);

        return Ok(deployResult);
    }
}