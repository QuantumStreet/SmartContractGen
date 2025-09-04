namespace ScGen.Lib.Shared.DTOs.Responses;

public sealed class RadixCompileContractResponse : BaseCompileContractResponse
{
    public RadixCompileContractResponse()
    {
        ContentType = MediaTypeNames.Application.Wasm;
    }
}