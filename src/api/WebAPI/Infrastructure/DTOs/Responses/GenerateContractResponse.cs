namespace WebAPI.Infrastructure.DTOs.Responses;

public class GenerateContractResponse
{
    public byte[] ContractFileContent { get; set; } = [];
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/plain"; 
}