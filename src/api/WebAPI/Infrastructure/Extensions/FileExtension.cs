namespace WebAPI.Infrastructure.Extensions;

public static class FileExtension
{
    public const string Json = ".json";
    public const string Cpp = ".cpp";
    public const string Rs = ".rs";
    public const string Sol = ".sol";
    public const string Zip = ".zip";

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

    public static bool IsContractFile(this IFormFile file)
    {
        if (string.IsNullOrWhiteSpace(file.FileName))
            return false;

        string ext = Path.GetExtension(file.FileName);
        return ContractExtensions.Contains(ext);
    }
}