namespace FormatCompanion.Core.Models;

public sealed class ProfileModel
{
    public DateTime CreatedAtUtc { get; set; }

    public string MachineName { get; set; } = "";

    public List<AppEntry> Apps { get; set; } = new();
}