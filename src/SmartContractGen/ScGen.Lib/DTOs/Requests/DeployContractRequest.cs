namespace ScGen.Lib.DTOs.Requests;

public record DeployContractRequest(
    [Required] IFormFile Source,
    [Required] SmartContractLanguage Language);