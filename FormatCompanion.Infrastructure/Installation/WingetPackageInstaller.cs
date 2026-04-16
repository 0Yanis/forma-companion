using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using FormatCompanion.Core.Abstractions;
using FormatCompanion.Core.Models;

namespace FormatCompanion.Infrastructure.Installation;

[SupportedOSPlatform("windows")]
public sealed class WingetPackageInstaller : IPackageInstaller
{
    public async Task<RestoreResult> InstallAsync(AppEntry app, CancellationToken cancellationToken = default)
    {
        if (app is null)
        {
            throw new ArgumentNullException(nameof(app));
        }

        if (string.IsNullOrWhiteSpace(app.WingetId))
        {
            return new RestoreResult
            {
                DisplayName = app.DisplayName,
                WingetId = app.WingetId,
                Result = "ManualRequired",
                Notes = "No Winget ID available. Manual install required."
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "winget",
            Arguments =
                $"install -e --id \"{app.WingetId}\" --accept-source-agreements --accept-package-agreements",
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

        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;

        var combined = (stdOut + Environment.NewLine + stdErr).Trim();

        if (process.ExitCode == 0)
        {
            return new RestoreResult
            {
                DisplayName = app.DisplayName,
                WingetId = app.WingetId,
                Result = "Installed",
                Notes = string.IsNullOrWhiteSpace(combined)
                    ? "Installation completed."
                    : combined
            };
        }

        return new RestoreResult
        {
            DisplayName = app.DisplayName,
            WingetId = app.WingetId,
            Result = "Failed",
            Notes = string.IsNullOrWhiteSpace(combined)
                ? $"winget failed with exit code {process.ExitCode}."
                : combined
        };
    }
}