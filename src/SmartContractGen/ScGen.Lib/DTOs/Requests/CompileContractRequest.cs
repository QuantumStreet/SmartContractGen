namespace ScGen.Lib.DTOs.Requests;

public record CompileContractRequest(
    [Required] IFormFile Source,
    [Required] SmartContractLanguage Language);