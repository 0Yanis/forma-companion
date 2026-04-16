using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using FormatCompanion.Core.Abstractions;
using FormatCompanion.Core.Models;

namespace FormatCompanion.Infrastructure.Inventory;

[SupportedOSPlatform("windows")]
public sealed class RegistryInventoryProvider : IInventoryProvider
{
    private static readonly string[] LocalMachinePaths =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    };

    private const string CurrentUserPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    private static readonly string[] ExcludePatterns =
    {
        @"Microsoft Visual C\+\+",
        @"Redistributable",
        @"Targeting Pack",
        @"AppHost Pack",
        @"Host FX Resolver",
        @"Shared Framework",
        @"WindowsDesktop Runtime",
        @"ASP\.NET Core .* Shared Framework",
        @"Extension SDK",
        @"Windows SDK",
        @"ClickOnce",
        @"VCLibs",
        @"Windows App Runtime",
        @"Workload\.",
        @"Manifest",
        @"Templates",
        @"Toolset",
        @"Setup WMI Provider",
        @"Setup Configuration",
        @"CoreEditorFonts",
        @"^vs_",
        @"filehandler",
        @"filetracker",
        @"minshell",
        @"protocolselector",
        @"githubprotocol",
        @"AURA",
        @"\bHAL\b",
        @"Framework Service",
        @"Update Helper",
        @"Live Service",
        @"OMNI RECEIVER",
        @"ROGFontInstaller",
        @"Armoury Crate Service",
        @"NVIDIA .* Container",
        @"NVIDIA MessageBus",
        @"NVIDIA Telemetry Client",
        @"NVIDIA Install Application",
        @"NvCpl",
        @"NvDLISR",
        @"Virtual Audio",
        @"LocalSystem Container",
        @"User Container",
        @"Backend",
        @"Session Container",
        @"AIUser Container",
        @"Watchdog Plugin",
        @"Add to Path",
        @"Core Interpreter",
        @"Development Libraries",
        @"Documentation",
        @"Executables",
        @"pip Bootstrap",
        @"Standard Library",
        @"Tcl/Tk Support",
        @"Test Suite",
        @"Apple Application Support",
        @"Maintenance Service"
    };

    private static readonly Regex ExcludeRegex =
        new(string.Join("|", ExcludePatterns), RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task<IReadOnlyList<AppEntry>> GetAppsAsync(CancellationToken cancellationToken = default)
    {
        var apps = new List<AppEntry>();

        foreach (var path in LocalMachinePaths)
        {
            ReadRegistryPath(Registry.LocalMachine, path, apps, cancellationToken);
        }

        ReadRegistryPath(Registry.CurrentUser, CurrentUserPath, apps, cancellationToken);

        var deduped = apps
            .GroupBy(x => $"{x.DisplayName}||{x.Publisher}")
            .Select(g => g
                .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.Version))
                .First())
            .OrderBy(x => x.DisplayName)
            .ToList();

        return Task.FromResult<IReadOnlyList<AppEntry>>(deduped);
    }

    private static void ReadRegistryPath(
        RegistryKey root,
        string subKeyPath,
        List<AppEntry> apps,
        CancellationToken cancellationToken)
    {
        using var uninstallKey = root.OpenSubKey(subKeyPath);
        if (uninstallKey is null)
        {
            return;
        }

        foreach (var subKeyName in uninstallKey.GetSubKeyNames())
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var appKey = uninstallKey.OpenSubKey(subKeyName);
            if (appKey is null)
            {
                continue;
            }

            var displayName = appKey.GetValue("DisplayName") as string;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                continue;
            }

            var version = appKey.GetValue("DisplayVersion") as string;
            var publisher = appKey.GetValue("Publisher") as string;
            var installDate = appKey.GetValue("InstallDate") as string;

            apps.Add(new AppEntry
            {
                Selected = true,
                DisplayName = displayName.Trim(),
                NormalizedName = NormalizeName(displayName),
                WingetId = null,
                Version = version,
                Publisher = publisher,
                Source = "Registry",
                AppLike = IsProbablyApp(displayName, publisher),
                Status = "",
                Notes = installDate ?? ""
            });
        }
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var normalized = name.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"\(x64.*?\)|\(x86.*?\)|64-bit|32-bit", "");
        normalized = Regex.Replace(normalized, @"[®™©]", "");
        normalized = Regex.Replace(normalized, @"\s+", " ");

        return normalized.Trim();
    }

    private static bool IsProbablyApp(string displayName, string? publisher)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        if (ExcludeRegex.IsMatch(displayName))
        {
            return false;
        }

        var normalized = NormalizeName(displayName);

        if (normalized.StartsWith("microsoft .net host", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("microsoft .net apphost", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("microsoft .net toolset", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("microsoft .net workload", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("microsoft asp.net core", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalized.StartsWith("python ") &&
            (normalized.Contains("core interpreter") ||
             normalized.Contains("development libraries") ||
             normalized.Contains("documentation") ||
             normalized.Contains("executables") ||
             normalized.Contains("pip bootstrap") ||
             normalized.Contains("standard library") ||
             normalized.Contains("tcl/tk support") ||
             normalized.Contains("test suite") ||
             normalized.Contains("add to path")))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(publisher) &&
            publisher.Contains("ASUSTek", StringComparison.OrdinalIgnoreCase) &&
            (normalized.Contains("service") ||
             normalized.Contains("hal") ||
             normalized.Contains("helper") ||
             normalized.Contains("receiver")))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(publisher) &&
            publisher.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) &&
            (normalized.Contains("container") ||
             normalized.Contains("messagebus") ||
             normalized.Contains("telemetry") ||
             normalized.Contains("watchdog")))
        {
            return false;
        }

        return true;
    }
}