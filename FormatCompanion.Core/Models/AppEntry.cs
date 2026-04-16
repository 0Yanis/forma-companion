namespace FormatCompanion.Core.Models;

public sealed class AppEntry
{
    public bool Selected { get; set; } = true;

    public string DisplayName { get; set; } = "";

    public string NormalizedName { get; set; } = "";

    public string? WingetId { get; set; }

    public string? Version { get; set; }

    public string? Publisher { get; set; }

    public string Source { get; set; } = "";

    public bool AppLike { get; set; }

    public string Status { get; set; } = "";

    public string Notes { get; set; } = "";

    public string InstallStatus { get; set; } = "Pending";

    public string InstallMessage { get; set; } = "";

    public string StatusColorHex =>
        InstallStatus switch
        {
            "Installed" => "#DCFCE7",
            "ManualRequired" => "#FEF3C7",
            "Failed" => "#FEE2E2",
            "Installing" => "#DBEAFE",
            _ => "#00000000"
        };

    public string StatusTextColorHex =>
        InstallStatus switch
        {
            "Installed" => "#000000",
            "ManualRequired" => "#000000",
            "Failed" => "#000000",
            "Installing" => "#000000",
            _ => "#EDEDED"
        };
}