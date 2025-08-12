namespace WebAPI.Infrastructure.DTOs.Responses;

public readonly record struct DeployContractResponse(
    string ContractAddress,
    bool Success,
    string TransactionHash);