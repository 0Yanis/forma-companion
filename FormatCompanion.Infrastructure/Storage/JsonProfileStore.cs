using System.Text.Json;
using FormatCompanion.Core.Abstractions;
using FormatCompanion.Core.Models;

namespace FormatCompanion.Infrastructure.Storage;

public sealed class JsonProfileStore : IProfileStore
{
    private const string ProfileFileName = "profile.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task SaveAsync(
        string folder,
        ProfileModel profile,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            throw new ArgumentException("Folder path cannot be empty.", nameof(folder));
        }

        if (profile is null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        Directory.CreateDirectory(folder);

        var filePath = Path.Combine(folder, ProfileFileName);

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, profile, JsonOptions, cancellationToken);
    }

    public async Task<ProfileModel> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Profile file was not found.", filePath);
        }

        await using var stream = File.OpenRead(filePath);

        var profile = await JsonSerializer.DeserializeAsync<ProfileModel>(
            stream,
            JsonOptions,
            cancellationToken);

        if (profile is null)
        {
            throw new InvalidOperationException("Profile file could not be deserialized.");
        }

        if (profile.Apps is null)
        {
            profile.Apps = new List<AppEntry>();
        }

        return profile;
    }
}