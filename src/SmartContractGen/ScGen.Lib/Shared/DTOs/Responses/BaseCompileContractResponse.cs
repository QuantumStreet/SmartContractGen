namespace ScGen.Lib.Shared.DTOs.Responses;

public abstract class BaseCompileContractResponse
{
    public byte[] CompiledCode { get; set; } = [];
    public string CompiledCodeFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = MediaTypeNames.Application.Octet;
}