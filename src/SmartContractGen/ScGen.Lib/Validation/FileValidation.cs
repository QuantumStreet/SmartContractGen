namespace ScGen.Lib.Validation;

public static class FileValidation
{
    private const string Json = ".json";
    private const string Cpp = ".cpp";
    private const string Rs = ".rs";
    private const string Sol = ".sol";
    private const string Zip = ".zip";

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

    public static bool IsZipFile(this IFormFile file)
    {
        if (string.IsNullOrWhiteSpace(file.FileName))
            return false;

        return Path.GetExtension(file.FileName)
            .Equals(Zip, StringComparison.OrdinalIgnoreCase);
    }
}