using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.RegularExpressions;
using FormatCompanion.Core.Abstractions;
using FormatCompanion.Core.Models;

namespace FormatCompanion.Infrastructure.Inventory;

[SupportedOSPlatform("windows")]
public sealed class WingetInventoryProvider : IInventoryProvider
{
    public async Task<IReadOnlyList<AppEntry>> GetAppsAsync(CancellationToken cancellationToken = default)
    {
        var tempFile = Path.Combine(
            Path.GetTempPath(),
            $"winget_export_{Guid.NewGuid():N}.json");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = $"export -o \"{tempFile}\" --include-versions --accept-source-agreements",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            var stdErr = await stdErrTask;
            _ = await stdOutTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"winget export failed with exit code {process.ExitCode}. Error: {stdErr}");
            }

            if (!File.Exists(tempFile))
            {
                throw new FileNotFoundException("winget export did not create the expected JSON file.", tempFile);
            }

            var json = await File.ReadAllTextAsync(tempFile, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<AppEntry>();
            }

            using var document = JsonDocument.Parse(json);
            var result = new List<AppEntry>();

            if (!document.RootElement.TryGetProperty("Sources", out var sourcesElement))
            {
                return Array.Empty<AppEntry>();
            }

            foreach (var source in sourcesElement.EnumerateArray())
            {
                if (!source.TryGetProperty("Packages", out var packagesElement))
                {
                    continue;
                }

                foreach (var package in packagesElement.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var packageId = package.TryGetProperty("PackageIdentifier", out var idElement)
                        ? idElement.GetString()
                        : null;

                    var version = package.TryGetProperty("Version", out var versionElement)
                        ? versionElement.GetString()
                        : null;

                    if (string.IsNullOrWhiteSpace(packageId))
                    {
                        continue;
                    }

                    var displayName = HumanizePackageIdentifier(packageId);

                    result.Add(new AppEntry
                    {
                        Selected = true,
                        DisplayName = displayName,
                        NormalizedName = NormalizeName(displayName),
                        WingetId = packageId,
                        Version = version,
                        Publisher = null,
                        Source = "Winget",
                        AppLike = IsProbablyApp(packageId, displayName),
                        Status = "",
                        Notes = ""
                    });
                }
            }

            return result
                .GroupBy(x => x.WingetId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(x => x.DisplayName)
                .ToList();
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                }
            }
        }
    }

    private static string HumanizePackageIdentifier(string packageIdentifier)
    {
        var parts = packageIdentifier
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (parts.Count == 0)
        {
            return packageIdentifier;
        }

        // Παλιά πετάγαμε τυφλά το πρώτο token και για Balena.Etcher βγαίναμε μόνο "Etcher".
        // Τώρα κρατάμε και vendor token όταν φαίνεται ουσιαστικό.
        var usefulParts = new List<string>(parts);

        if (usefulParts.Count >= 2 && IsGenericVendorToken(usefulParts[0]))
        {
            usefulParts.RemoveAt(0);
        }

        var output = new List<string>();

        for (var i = 0; i < usefulParts.Count; i++)
        {
            var token = usefulParts[i];

            if (IsNumericToken(token))
            {
                var versionParts = new List<string> { token };

                while (i + 1 < usefulParts.Count && IsNumericToken(usefulParts[i + 1]))
                {
                    i++;
                    versionParts.Add(usefulParts[i]);
                }

                output.Add(string.Join(".", versionParts));
                continue;
            }

            output.Add(HumanizeToken(token));
        }

        var joined = string.Join(" ", output)
            .Replace(" V C ", " VC ")
            .Trim();

        return NormalizeKnownNames(joined);
    }

    private static bool IsGenericVendorToken(string token)
    {
        var generic = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "microsoft",
            "google",
            "mozilla",
            "python",
            "openjs",
            "observium",
            "dotnet",
            "rarlab",
            "telegram",
            "discord",
            "videolan"
        };

        return generic.Contains(token);
    }

    private static string NormalizeKnownNames(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var v = value.Trim();

        // Known nice display names
        if (string.Equals(v, "Balena Etcher", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v, "Etcher", StringComparison.OrdinalIgnoreCase))
        {
            return "balenaEtcher";
        }

        if (string.Equals(v, "Vlc", StringComparison.OrdinalIgnoreCase))
        {
            return "VLC media player";
        }

        if (string.Equals(v, "Visual Studio Code", StringComparison.OrdinalIgnoreCase))
        {
            return "Microsoft Visual Studio Code";
        }

        if (string.Equals(v, "Telegram Desktop", StringComparison.OrdinalIgnoreCase))
        {
            return "Telegram Desktop";
        }

        return v;
    }

    private static string HumanizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        var value = token.Replace("_", " ").Replace("-", " ");
        value = Regex.Replace(value, @"(?<=[A-Z])(?=[A-Z][a-z])", " ");
        value = Regex.Replace(value, @"(?<=[a-z0-9])(?=[A-Z])", " ");
        value = Regex.Replace(value, @"(?<=[A-Za-z])(?=\d)", " ");
        value = Regex.Replace(value, @"(?<=\d)(?=[A-Za-z])", " ");
        return value.Trim();
    }

    private static bool IsNumericToken(string token)
    {
        return Regex.IsMatch(token, @"^\d+$");
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

    private static bool IsProbablyApp(string packageId, string displayName)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return false;
        }

        var excludedPrefixes = new[]
        {
            "Microsoft.WindowsAppRuntime.",
            "Microsoft.UI.Xaml.",
            "Microsoft.VCLibs.",
            "Microsoft.VCRedist.",
            "Microsoft.DotNet.DesktopRuntime.",
            "Microsoft.DotNet.Runtime.",
            "Microsoft.DotNet.AspNetCore.",
            "Microsoft.AppInstaller",
            "Microsoft.VSTOR",
            "Microsoft.GameInput",
            "Microsoft.DirectX",
            "Python.Launcher"
        };

        foreach (var prefix in excludedPrefixes)
        {
            if (packageId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var excludedNames = new[]
        {
            "Windows App Runtime",
            "App Installer",
            "VC Libs",
            "VC Redist",
            "DirectX",
            "Game Input"
        };

        foreach (var name in excludedNames)
        {
            if (displayName.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}