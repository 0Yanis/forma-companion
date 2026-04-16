namespace FormatCompanion.Core.Models;

public sealed class RestoreResult
{
    public string DisplayName { get; set; } = "";

    public string? WingetId { get; set; }

    public string Result { get; set; } = "";

    public string Notes { get; set; } = "";
}