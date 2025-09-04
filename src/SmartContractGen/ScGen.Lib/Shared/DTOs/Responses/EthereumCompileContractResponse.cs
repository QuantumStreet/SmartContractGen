namespace ScGen.Lib.Shared.DTOs.Responses;

public class EthereumCompileContractResponse : BaseCompileContractResponse
{
    public byte[] Abi { get; set; } = [];

    public string AbiFileName { get; set; } = string.Empty;
    public string AbiContentType { get; set; } = MediaTypeNames.Application.Json;
}