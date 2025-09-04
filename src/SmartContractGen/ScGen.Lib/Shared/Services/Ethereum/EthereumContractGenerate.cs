namespace ScGen.Lib.Shared.Services.Ethereum
{ }

namespace ScGen.Lib.ImplContracts.Ethereum
{
    public sealed partial class EthereumContractGenerate
    {
        private Result<GenerateContractResponse> Validation(IFormFile file)
        {
            if (!file.IsJsonFile())
            {
                _logger.ValidationFailed(nameof(GenerateAsync),
                    Messages.InvalidJsonFile, _httpContextAccessor.GetId().ToString());
                return Result<GenerateContractResponse>.Failure(ResultPatternError.BadRequest(Messages.InvalidJsonFile));
            }

            if (file.Length == 0)
            {
                _logger.ValidationFailed(nameof(GenerateAsync),
                    Messages.EmptyJson, _httpContextAccessor.GetId().ToString());
                return Result<GenerateContractResponse>.Failure(ResultPatternError.BadRequest(Messages.EmptyJson));
            }

            return Result<GenerateContractResponse>.Success();
        }
    }
}