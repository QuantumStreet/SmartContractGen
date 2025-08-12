namespace WebAPI.Infrastructure.DTOs.Responses;

public class CompileContractResponse
{
    public byte[] AbiFileContent { get; set; } = [];
    public string AbiFileName { get; set; } = string.Empty;

    public byte[] BytecodeFileContent { get; set; } = [];
    public string BytecodeFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";
}