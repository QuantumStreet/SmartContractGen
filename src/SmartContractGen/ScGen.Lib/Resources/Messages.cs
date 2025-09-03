namespace ScGen.Lib.Resources;

/// <summary>
/// Centralized accessor for localized resource strings used across the application.
/// 
/// The <c>ResultMessages</c> class provides strongly-typed, localized access to application messages
/// (error messages, notifications, and email templates), simplifying the use of <see cref="ResourceManager"/>.
/// 
/// Each property name maps to a key in a .resx file, and this design:
/// - Reduces hardcoded string usage
/// - Promotes reusability
/// - Enables clean localization and globalization
/// 
/// NOTE: <c>_resources.Get().AsString()</c> is expected to be a custom extension method that resolves the resource
/// string based on the calling property name using reflection or caller info.
/// </summary>
public static class Messages
{
    private static readonly ResourceManager Resources = new(typeof(BaseMessages).FullName!, typeof(BaseMessages).Assembly);
    public static string EmptyJson => Resources.Get().AsString();
    public static string HandlebarTemplateNotFound => Resources.Get().AsString();
    public static string HandlebarTemplateProcessingError => Resources.Get().AsString();

}