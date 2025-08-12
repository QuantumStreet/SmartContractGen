namespace WebAPI.Infrastructure.Controllers.V1;

[ApiController]
[Route($"{ApiAddresses.Base}/contracts")]
public sealed class ContractGeneratorController(IContractServiceFactory factory) : BaseController
{
    [HttpPost("generate")]
    public async Task<IActionResult> ContractGenerateAsync([FromForm]GenerateContractRequest request)
    {
        if (!request.JsonFile.IsJsonFile())
            return BadRequest("Invalid JSON file.");

        IContractGenerator generator = factory.GetGenerator(request.Language);
        GenerateContractResponse result = await generator.GenerateAsync(request.JsonFile);

        return File(result.ContractFileContent, result.ContentType, result.FileName);
    }

    [HttpPost("compile")]
    public async Task<IActionResult> ContractCompileAsync([FromForm]CompileContractRequest request)
    {
        if (!request.SourceCodeFile.IsContractFile())
            return BadRequest("Invalid source code file.");

        var compiler = factory.GetCompiler(request.Language);
        var compileResult = await compiler.CompileAsync(request.SourceCodeFile);

        return Ok(compileResult);
    }

    [HttpPost("deploy")]
    public async Task<IActionResult> ContractDeployAsync([FromForm]DeployContractRequest request)
    {
        var deployer = factory.GetDeployer(request.Language);
        var deployResult = await deployer.DeployAsync(request.AbiFile, request.BytecodeFile);

        return Ok(deployResult);
    }
}