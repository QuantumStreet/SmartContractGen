namespace ScGen.Lib.Shared.Validation;

public static class FileValidation
{
    private const string Json = ".json";
    private const string Cpp = ".cpp";
    private const string Rs = ".rs";
    private const string Sol = ".sol";
    private const string Zip = ".zip";
    private const string Abi = ".abi";
    private const string Bin = ".bin";

    private static readonly HashSet<string> ContractExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        Sol,
        Rs,
        Cpp,
        Zip
    };

    public static bool IsJsonFile(this IFormFile file)
    {
        if (string.IsNullOrWhiteSpace(file.FileName))
            return false;

        return Path.GetExtension(file.FileName)
            .Equals(Json, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAbiFile(this IFormFile file)
    {
        if (string.IsNullOrWhiteSpace(file.FileName))
            return false;

        return Path.GetExtension(file.FileName)
            .Equals(Abi, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsEthereumBinFile(this IFormFile file)
    {
        if (string.IsNullOrWhiteSpace(file.FileName))
            return false;

        return Path.GetExtension(file.FileName)
            .Equals(Bin, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSolidityFile(this IFormFile file)
    {
        if (string.IsNullOrWhiteSpace(file.FileName))
            return false;

        return Path.GetExtension(file.FileName)
            .Equals(Sol, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsRustFile(this IFormFile file)
    {
        if (string.IsNullOrWhiteSpace(file.FileName))
            return false;

        return Path.GetExtension(file.FileName)
            .Equals(Rs, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCppFile(this IFormFile file)
    {
        if (string.IsNullOrWhiteSpace(file.FileName))
            return false;

        return Path.GetExtension(file.FileName)
            .Equals(Cpp, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsContractFile(this IFormFile file)
    {
        if (string.IsNullOrWhiteSpace(file.FileName))
            return false;

        return ContractExtensions.Contains(Path.GetExtension(file.FileName));
    }

    public static bool IsZipFile(this IFormFile file)
    {
        if (string.IsNullOrWhiteSpace(file.FileName))
            return false;

        return Path.GetExtension(file.FileName)
            .Equals(Zip, StringComparison.OrdinalIgnoreCase);
    }

    public static string GetSoliditySafeFileName(this IFormFile file)
    {
        if (string.IsNullOrWhiteSpace(file.FileName))
            return $"contract_{Guid.NewGuid().ToString("N")[..8]}.sol";

        string safeFileName = Path.GetFileName(file.FileName);

        char[] invalidChars = Path.GetInvalidFileNameChars();
        if (safeFileName.IndexOfAny(invalidChars) >= 0)
        {
            safeFileName = $"contract_{Guid.NewGuid().ToString("N")[..8]}.sol";
        }

        return safeFileName;
    }
}