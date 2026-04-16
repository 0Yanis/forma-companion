using FormatCompanion.Core.Models;

namespace FormatCompanion.Core.Abstractions;

public interface IProfileStore
{
    Task SaveAsync(string folder, ProfileModel profile, CancellationToken cancellationToken = default);

    Task<ProfileModel> LoadAsync(string filePath, CancellationToken cancellationToken = default);
}