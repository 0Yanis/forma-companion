using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FormatCompanion.Core.Models;

namespace FormatCompanion.Core.Services;

public sealed class InventoryMergeService
{
    private static readonly HashSet<string> NoiseTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "x64", "x86", "64", "32", "64bit", "32bit",
        "bit", "stable", "preview", "user", "machine",
        "release", "community", "edition", "exe"
    };

    private static readonly HashSet<string> WeakSingleTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "app", "setup", "installer", "install", "launcher", "service",
        "runtime", "sdk", "driver", "helper", "support", "desktop",
        "tools", "tool", "update", "plugin", "component", "manager"
    };

    public IReadOnlyList<AppEntry> Merge(
        IReadOnlyList<AppEntry> registryApps,
        IReadOnlyList<AppEntry> wingetApps)
    {
        if (registryApps is null)
        {
            throw new ArgumentNullException(nameof(registryApps));
        }

        if (wingetApps is null)
        {
            throw new ArgumentNullException(nameof(wingetApps));
        }

        var merged = registryApps
            .Select(Clone)
            .ToList();

        foreach (var wingetApp in wingetApps.Select(Clone))
        {
            var existing = FindSafeMatch(merged, wingetApp);

            if (existing is null)
            {
                merged.Add(wingetApp);
                continue;
            }

            MergeInto(existing, wingetApp);
        }

        return merged
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void MergeInto(AppEntry existing, AppEntry candidate)
    {
        if (string.IsNullOrWhiteSpace(existing.DisplayName) &&
            !string.IsNullOrWhiteSpace(candidate.DisplayName))
        {
            existing.DisplayName = candidate.DisplayName;
        }

        if (string.IsNullOrWhiteSpace(existing.NormalizedName) &&
            !string.IsNullOrWhiteSpace(candidate.NormalizedName))
        {
            existing.NormalizedName = candidate.NormalizedName;
        }

        if (string.IsNullOrWhiteSpace(existing.WingetId) &&
            !string.IsNullOrWhiteSpace(candidate.WingetId))
        {
            existing.WingetId = candidate.WingetId;
        }

        if (string.IsNullOrWhiteSpace(existing.Version) &&
            !string.IsNullOrWhiteSpace(candidate.Version))
        {
            existing.Version = candidate.Version;
        }

        if (string.IsNullOrWhiteSpace(existing.Publisher) &&
            !string.IsNullOrWhiteSpace(candidate.Publisher))
        {
            existing.Publisher = candidate.Publisher;
        }

        existing.Source = CombineSource(existing.Source, candidate.Source);
        existing.AppLike = existing.AppLike || candidate.AppLike;
    }

    private static string CombineSource(string left, string right)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(left))
        {
            parts.AddRange(left.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        if (!string.IsNullOrWhiteSpace(right))
        {
            parts.AddRange(right.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return string.Join("+",
            parts
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
    }

    private static AppEntry? FindSafeMatch(List<AppEntry> current, AppEntry candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate.WingetId))
        {
            var exactWingetId = current.FirstOrDefault(x =>
                string.Equals(x.WingetId, candidate.WingetId, StringComparison.OrdinalIgnoreCase));

            if (exactWingetId is not null)
            {
                return exactWingetId;
            }
        }

        var candidateTokens = CanonicalTokens(candidate.DisplayName);
        if (candidateTokens.Count == 0)
        {
            return null;
        }

        foreach (var item in current)
        {
            var itemTokens = CanonicalTokens(item.DisplayName);
            if (itemTokens.Count == 0)
            {
                continue;
            }

            if (AreSafelyEquivalent(itemTokens, candidateTokens))
            {
                return item;
            }
        }

        return null;
    }

    private static bool AreSafelyEquivalent(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return false;
        }

        if (SequenceEquals(left, right))
        {
            return true;
        }

        var shorter = left.Count <= right.Count ? left : right;
        var longer = left.Count <= right.Count ? right : left;

        // Safe prefix/suffix matching only.
        if (shorter.Count >= 2)
        {
            if (EndsWithTokens(longer, shorter) || StartsWithTokens(longer, shorter))
            {
                return true;
            }
        }

        // Allow strong 1-token product match only if the token is not weak/generic.
        if (shorter.Count == 1)
        {
            var token = shorter[0];

            if (token.Length >= 6 &&
                !WeakSingleTokens.Contains(token) &&
                (EndsWithTokens(longer, shorter) || StartsWithTokens(longer, shorter)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SequenceEquals(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool StartsWithTokens(IReadOnlyList<string> longer, IReadOnlyList<string> shorter)
    {
        if (shorter.Count > longer.Count)
        {
            return false;
        }

        for (var i = 0; i < shorter.Count; i++)
        {
            if (!string.Equals(longer[i], shorter[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool EndsWithTokens(IReadOnlyList<string> longer, IReadOnlyList<string> shorter)
    {
        if (shorter.Count > longer.Count)
        {
            return false;
        }

        var offset = longer.Count - shorter.Count;

        for (var i = 0; i < shorter.Count; i++)
        {
            if (!string.Equals(longer[offset + i], shorter[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static List<string> CanonicalTokens(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        var text = value.Trim();

        // split camel / Pascal
        text = Regex.Replace(text, @"(?<=[a-z0-9])(?=[A-Z])", " ");
        text = Regex.Replace(text, @"(?<=[A-Z])(?=[A-Z][a-z])", " ");

        // remove parenthesized content that is usually arch/version noise
        text = Regex.Replace(text, @"\((.*?)\)", " ");

        // normalize punctuation
        text = Regex.Replace(text, @"[®™©]", "");
        text = text.Replace("_", " ").Replace("-", " ").Replace(".", " ");
        text = Regex.Replace(text, @"[^A-Za-z0-9\s]+", " ");

        // remove version-like tokens
        text = Regex.Replace(text, @"\b\d+(\.\d+){1,4}\b", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim().ToLowerInvariant();

        var rawTokens = text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !NoiseTokens.Contains(t))
            .ToList();

        // remove duplicate tokens while preserving order
        var deduped = new List<string>();
        foreach (var token in rawTokens)
        {
            if (!deduped.Contains(token, StringComparer.OrdinalIgnoreCase))
            {
                deduped.Add(token);
            }
        }

        return deduped;
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
            Notes = source.Notes,
            InstallStatus = source.InstallStatus,
            InstallMessage = source.InstallMessage
        };
    }
}