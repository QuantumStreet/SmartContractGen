namespace WebAPI.Infrastructure.DTOs.Requests;

public record DeployContractRequest(
    [Required] IFormFile AbiFile,
    [Required] IFormFile BytecodeFile,
    [Required] SmartContractLanguage Language);