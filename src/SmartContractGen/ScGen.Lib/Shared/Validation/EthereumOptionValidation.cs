namespace ScGen.Lib.Shared.Validation;

public sealed class EthereumOptionValidation : AbstractValidator<EthereumOptions>
{
    public EthereumOptionValidation()
    {
        RuleFor(x => x.RpcUrl)
            .NotEmpty().WithMessage(Messages.RpcUrlIsRequired)
            .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _))
            .WithMessage(Messages.RpcUrlMustBeValid);

        RuleFor(x => x.PrivateKey)
            .NotEmpty().WithMessage(Messages.PrivateKeyIsRequired)
            .MinimumLength(64).WithMessage(Messages.PrivateKeyMustBeValid)
            .Matches(@"^(0x)?[a-fA-F0-9]{64}$").WithMessage(Messages.PrivateKeyMustBeValidHex);

        RuleFor(x => x.GasLimit)
            .GreaterThanOrEqualTo(21000).WithMessage(Messages.GasLimitMustBeValid);
    }
}