namespace WebAPI.Infrastructure.DTOs.Requests;

public record CompileContractRequest(
    [Required] IFormFile SourceCodeFile,
    [Required] SmartContractLanguage Language);