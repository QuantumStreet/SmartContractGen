namespace ScGen.Lib.DTOs.Responses;

public sealed class RadixCompileContractResponse : BaseCompileContractResponse
{
    public RadixCompileContractResponse()
    {
        ContentType = MediaTypeNames.Application.Wasm;
    }
}