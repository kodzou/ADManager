namespace ADManager.Helpers;

public static class AppState
{
    public static List<object> LastResults { get; set; } = new();
}

public record BulkUserEntry
{
    public string Domain      { get; init; } = "";   // без .local
    public string DomainFull  { get; init; } = "";   // с .local
    public string Login       { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Position    { get; init; } = "";
    public string Department  { get; init; } = "";
    public string Manager     { get; init; } = "";
    public string DN          { get; init; } = "";
}
