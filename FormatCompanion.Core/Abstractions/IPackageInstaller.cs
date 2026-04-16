using FormatCompanion.Core.Models;

namespace FormatCompanion.Core.Abstractions;

public interface IPackageInstaller
{
    Task<RestoreResult> InstallAsync(AppEntry app, CancellationToken cancellationToken = default);
}