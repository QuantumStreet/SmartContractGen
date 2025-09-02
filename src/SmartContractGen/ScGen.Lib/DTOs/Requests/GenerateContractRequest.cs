namespace ScGen.Lib.DTOs.Requests;

public record GenerateContractRequest(
    [Required] IFormFile JsonFile,
    [Required] SmartContractLanguage Language);