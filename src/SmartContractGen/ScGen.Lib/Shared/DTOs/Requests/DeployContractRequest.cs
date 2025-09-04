namespace ScGen.Lib.Shared.DTOs.Requests;

public record DeployContractRequest(
    [Required] IFormFile Source,
    [Required] SmartContractLanguage Language);