namespace ScGen.Lib.Shared.Extensions;

public static class HandlebarExtension
{
    public static void EthereumRegisterHelpers(this IHandlebars hb)
    {
        hb.RegisterHelper("eq", (writer, _, args) =>
        {
            bool ok = args.Length >= 2 && string.Equals(args[0]?.ToString(), args[1]?.ToString(), StringComparison.Ordinal);
            writer.WriteSafeString(ok ? "true" : "");
        });

        hb.RegisterHelper("ne", (writer, _, args) =>
        {
            bool ok = args.Length >= 2 && !string.Equals(args[0]?.ToString(), args[1]?.ToString(), StringComparison.Ordinal);
            writer.WriteSafeString(ok ? "true" : "");
        });

        hb.RegisterHelper("and", (writer, _, args) =>
        {
            bool ok = args.Length > 0 && args.All(IsTruthy);
            writer.WriteSafeString(ok ? "true" : "");
        });

        hb.RegisterHelper("or", (writer, _, args) =>
        {
            bool ok = args.Length > 0 && args.Any(IsTruthy);
            writer.WriteSafeString(ok ? "true" : "");
        });

        hb.RegisterHelper("not", (writer, _, args) =>
        {
            bool ok = !(args.Length > 0 && IsTruthy(args[0]));
            writer.WriteSafeString(ok ? "true" : "");
        });


        static bool IsTruthy(object? value)
        {
            if (value == null) return false;
            if (value is bool b) return b;
            string s = value.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(s)) return false;
            if (s.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            if (s.Equals("0")) return false;
            return true;
        }
    }
}