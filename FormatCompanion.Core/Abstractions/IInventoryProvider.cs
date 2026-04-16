using FormatCompanion.Core.Models;

namespace FormatCompanion.Core.Abstractions;

public interface IInventoryProvider
{
    Task<IReadOnlyList<AppEntry>> GetAppsAsync(CancellationToken cancellationToken = default);
}