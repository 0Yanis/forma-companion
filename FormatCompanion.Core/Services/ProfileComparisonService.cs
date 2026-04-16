using FormatCompanion.Core.Models;

namespace FormatCompanion.Core.Services;

public sealed class ProfileComparisonService
{
    public IReadOnlyList<AppEntry> Compare(
        ProfileModel profile,
        IReadOnlyList<AppEntry> currentApps)
    {
        if (profile is null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        if (currentApps is null)
        {
            throw new ArgumentNullException(nameof(currentApps));
        }

        var currentWingetIds = currentApps
            .Where(x => !string.IsNullOrWhiteSpace(x.WingetId))
            .Select(x => x.WingetId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var currentNormalizedNames = currentApps
            .Where(x => !string.IsNullOrWhiteSpace(x.NormalizedName))
            .Select(x => x.NormalizedName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var results = new List<AppEntry>();

        foreach (var profileApp in profile.Apps)
        {
            var installed = false;

            if (!string.IsNullOrWhiteSpace(profileApp.WingetId) &&
                currentWingetIds.Contains(profileApp.WingetId))
            {
                installed = true;
            }
            else if (!string.IsNullOrWhiteSpace(profileApp.NormalizedName) &&
                     currentNormalizedNames.Contains(profileApp.NormalizedName))
            {
                installed = true;
            }

            var app = Clone(profileApp);

            app.Selected = !installed;

            if (installed)
            {
                app.Status = "Installed";
                app.Notes = "";
            }
            else if (string.IsNullOrWhiteSpace(app.WingetId))
            {
                app.Status = "ManualOnly";
                app.Notes = "No exact winget id";
            }
            else
            {
                app.Status = "Missing";
                app.Notes = "";
            }

            results.Add(app);
        }

        return results
            .OrderBy(x => x.DisplayName)
            .ToList();
    }

    private static AppEntry Clone(AppEntry source)
    {
        return new AppEntry
        {
            Selected = source.Selected,
            DisplayName = source.DisplayName,
            NormalizedName = source.NormalizedName,
            WingetId = source.WingetId,
            Version = source.Version,
            Publisher = source.Publisher,
            Source = source.Source,
            AppLike = source.AppLike,
            Status = source.Status,
            Notes = source.Notes
        };
    }
}