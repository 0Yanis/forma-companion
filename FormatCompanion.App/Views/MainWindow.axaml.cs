using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FormatCompanion.Core.Models;
using FormatCompanion.Core.Services;
using FormatCompanion.Infrastructure.Installation;
using FormatCompanion.Infrastructure.Inventory;
using FormatCompanion.Infrastructure.Storage;

namespace FormatCompanion.App.Views;

[SupportedOSPlatform("windows")]
public partial class MainWindow : Window
{
    private readonly RegistryInventoryProvider _registryInventoryProvider = new();
    private readonly WingetInventoryProvider _wingetInventoryProvider = new();
    private readonly JsonProfileStore _profileStore = new();
    private readonly InventoryMergeService _inventoryMergeService = new();
    private readonly WingetPackageInstaller _packageInstaller = new();

    private string _sessionLogFilePath = string.Empty;
    private List<AppEntry> _currentMergedApps = new();

    private TextBox? _storageFolderTextBox;
    private TextBox? _logTextBox;
    private TextBlock? _summaryTextBlock;
    private TextBlock? _activeProfileTextBlock;
    private DataGrid? _foundProgramsDataGrid;
    private DataGrid? _selectedProgramsDataGrid;

    public MainWindow()
    {
        InitializeComponent();

        _storageFolderTextBox = this.FindControl<TextBox>("StorageFolderTextBox");
        _logTextBox = this.FindControl<TextBox>("LogTextBox");
        _summaryTextBlock = this.FindControl<TextBlock>("SummaryTextBlock");
        _activeProfileTextBlock = this.FindControl<TextBlock>("ActiveProfileTextBlock");
        _foundProgramsDataGrid = this.FindControl<DataGrid>("FoundProgramsDataGrid");
        _selectedProgramsDataGrid = this.FindControl<DataGrid>("SelectedProgramsDataGrid");

        var defaultFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "FormatCompanionData");

        if (_storageFolderTextBox is not null)
        {
            _storageFolderTextBox.Text = defaultFolder;
        }

        EnsureSessionLogFilePath();
        UpdateActiveProfileLabel();

        AppendLog("Window initialized.");
        AppendLog($"Session log file: {_sessionLogFilePath}");
    }

    private async void OnBrowseStorageFolderClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select storage folder",
                AllowMultiple = false
            });

            var folder = folders.FirstOrDefault();
            if (folder is null)
            {
                return;
            }

            var localPath = folder.Path.LocalPath;
            if (string.IsNullOrWhiteSpace(localPath))
            {
                return;
            }

            if (_storageFolderTextBox is not null)
            {
                _storageFolderTextBox.Text = localPath;
            }

            _sessionLogFilePath = string.Empty;
            EnsureSessionLogFilePath();
            UpdateActiveProfileLabel();

            AppendLog($"Storage folder selected: {localPath}");
        }
        catch (Exception ex)
        {
            AppendLog("Browse storage folder failed: " + ex.Message);
            SetSummary("Browse storage folder failed. See log.");
        }
    }

    private string GetStorageFolder()
    {
        var folder = _storageFolderTextBox?.Text?.Trim();

        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FormatCompanionData");
        }

        Directory.CreateDirectory(folder);
        return folder;
    }

    private string GetProfileFilePath()
    {
        return Path.Combine(GetStorageFolder(), "profile.json");
    }

    private void EnsureSessionLogFilePath()
    {
        var storageFolder = GetStorageFolder();
        var logsFolder = Path.Combine(storageFolder, "logs");
        Directory.CreateDirectory(logsFolder);

        if (string.IsNullOrWhiteSpace(_sessionLogFilePath))
        {
            _sessionLogFilePath = Path.Combine(
                logsFolder,
                $"session_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        }
    }

    private void UpdateActiveProfileLabel()
    {
        if (_activeProfileTextBlock is not null)
        {
            _activeProfileTextBlock.Text = $"Active profile: {GetProfileFilePath()}";
        }
    }

    private async void OnScanCurrentClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            EnsureSessionLogFilePath();
            UpdateActiveProfileLabel();

            AppendLog("Starting current system scan...");

            var mergedApps = await ScanAndMergeCurrentInventoryAsync();

            _currentMergedApps = mergedApps
                .OrderBy(x => x.DisplayName)
                .ToList();

            foreach (var app in _currentMergedApps)
            {
                app.Selected = false;
                app.InstallStatus = "Pending";
                app.InstallMessage = string.Empty;
            }

            RefreshProgramLists();
            UpdateSelectionSummary();

            AppendLog($"Registry + WinGet merge produced {_currentMergedApps.Count} entries.");
            AppendLog("Scan loaded all programs into Programs found.");
        }
        catch (Exception ex)
        {
            AppendLog("Scan failed: " + ex.Message);
            SetSummary("Scan failed. See log.");
        }
    }

    private void OnAutoPickLikelyAppsClicked(object? sender, RoutedEventArgs e)
    {
        foreach (var app in _currentMergedApps)
        {
            app.Selected = app.AppLike;

            if (!app.Selected)
            {
                app.InstallStatus = "Pending";
                app.InstallMessage = string.Empty;
            }
        }

        RefreshProgramLists();
        UpdateSelectionSummary();
        AppendLog("Auto-picked likely apps.");
    }

    private void OnMoveRightClicked(object? sender, RoutedEventArgs e)
    {
        if (_foundProgramsDataGrid?.SelectedItem is AppEntry app)
        {
            app.Selected = true;

            if (string.IsNullOrWhiteSpace(app.InstallStatus))
            {
                app.InstallStatus = "Pending";
            }

            RefreshProgramLists();
            UpdateSelectionSummary();
            AppendLog($"Moved to install set: {app.DisplayName}");
        }
    }

    private void OnMoveLeftClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedProgramsDataGrid?.SelectedItem is AppEntry app)
        {
            app.Selected = false;
            app.InstallStatus = "Pending";
            app.InstallMessage = string.Empty;

            RefreshProgramLists();
            UpdateSelectionSummary();
            AppendLog($"Returned to found list: {app.DisplayName}");
        }
    }

    private void OnTransferAllClicked(object? sender, RoutedEventArgs e)
    {
        if (_currentMergedApps.Count == 0)
        {
            AppendLog("Transfer all ignored: no scanned programs loaded.");
            return;
        }

        var foundCount = _currentMergedApps.Count(x => !x.Selected);
        var selectedCount = _currentMergedApps.Count(x => x.Selected);

        if (foundCount > 0)
        {
            foreach (var app in _currentMergedApps.Where(x => !x.Selected))
            {
                app.Selected = true;

                if (string.IsNullOrWhiteSpace(app.InstallStatus))
                {
                    app.InstallStatus = "Pending";
                }
            }

            RefreshProgramLists();
            UpdateSelectionSummary();
            AppendLog($"Transferred all {foundCount} programs to Programs to install.");
            return;
        }

        if (selectedCount > 0)
        {
            foreach (var app in _currentMergedApps.Where(x => x.Selected))
            {
                app.Selected = false;
                app.InstallStatus = "Pending";
                app.InstallMessage = string.Empty;
            }

            RefreshProgramLists();
            UpdateSelectionSummary();
            AppendLog($"Transferred all {selectedCount} programs back to Programs found.");
            return;
        }

        AppendLog("Transfer all had nothing to do.");
    }

    private async void OnSaveProfileClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            EnsureSessionLogFilePath();
            UpdateActiveProfileLabel();

            if (_currentMergedApps.Count == 0)
            {
                AppendLog("No current inventory cached yet. Running scan first...");
                _currentMergedApps = (await ScanAndMergeCurrentInventoryAsync()).ToList();

                foreach (var app in _currentMergedApps)
                {
                    app.Selected = false;
                    app.InstallStatus = "Pending";
                    app.InstallMessage = string.Empty;
                }

                RefreshProgramLists();
            }

            var folder = GetStorageFolder();

            var selectedApps = _currentMergedApps
                .Where(x => x.Selected)
                .OrderBy(x => x.DisplayName)
                .Select(CloneForProfileSave)
                .ToList();

            if (selectedApps.Count == 0)
            {
                throw new InvalidOperationException("No programs are currently in 'Programs to install'.");
            }

            var profile = new ProfileModel
            {
                CreatedAtUtc = DateTime.UtcNow,
                MachineName = Environment.MachineName,
                Apps = selectedApps
            };

            await _profileStore.SaveAsync(folder, profile);

            AppendLog($"Profile saved: {GetProfileFilePath()}");
            AppendLog($"Saved {selectedApps.Count} selected entries.");
            SetSummary($"Profile saved successfully with {selectedApps.Count} selected entries.");
        }
        catch (Exception ex)
        {
            AppendLog("Save failed: " + ex.Message);
            SetSummary("Save failed. See log.");
        }
    }

    private async void OnLoadProfileClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            EnsureSessionLogFilePath();
            UpdateActiveProfileLabel();

            var filePath = GetProfileFilePath();

            var loadedProfile = await _profileStore.LoadAsync(filePath);

            _currentMergedApps = loadedProfile.Apps
                .Select(CloneLoadedProfileApp)
                .OrderBy(x => x.DisplayName)
                .ToList();

            RefreshProgramLists();
            UpdateSelectionSummary();

            AppendLog($"Profile loaded from: {filePath}");
            AppendLog($"Loaded profile apps into Programs to install: {loadedProfile.Apps.Count}");

            SetSummary(
                $"Profile loaded. Machine: {loadedProfile.MachineName}. " +
                $"Programs to install: {loadedProfile.Apps.Count}.");
        }
        catch (Exception ex)
        {
            AppendLog("Load failed: " + ex.Message);
            SetSummary("Load failed. See log.");
        }
    }

    private async void OnInstallSelectedClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var queue = _currentMergedApps
                .Where(x => x.Selected)
                .OrderBy(x => x.DisplayName)
                .ToList();

            if (queue.Count == 0)
            {
                throw new InvalidOperationException("There are no programs in 'Programs to install'.");
            }

            AppendLog($"Starting install pass for {queue.Count} programs...");

            foreach (var app in queue)
            {
                if (app.InstallStatus == "Installed")
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(app.WingetId))
                {
                    app.InstallStatus = "ManualRequired";
                    app.InstallMessage = "No Winget ID available. Manual install required.";
                    AppendLog($"Manual required: {app.DisplayName}");
                    RefreshProgramLists();
                    UpdateSelectionSummary();
                    continue;
                }

                app.InstallStatus = "Installing";
                app.InstallMessage = string.Empty;
                RefreshProgramLists();
                UpdateSelectionSummary();

                AppendLog($"Installing: {app.DisplayName} [{app.WingetId}]");

                var result = await _packageInstaller.InstallAsync(app);

                app.InstallStatus = result.Result;
                app.InstallMessage = result.Notes;

                AppendLog($"{result.Result}: {app.DisplayName}");

                RefreshProgramLists();
                UpdateSelectionSummary();
            }

            AppendLog("Install pass complete.");
        }
        catch (Exception ex)
        {
            AppendLog("Install selected failed: " + ex.Message);
            SetSummary("Install selected failed. See log.");
        }
    }

    private async void OnRetryFailedClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var retryQueue = _currentMergedApps
                .Where(x => x.Selected && x.InstallStatus == "Failed")
                .OrderBy(x => x.DisplayName)
                .ToList();

            if (retryQueue.Count == 0)
            {
                AppendLog("Retry failed ignored: no failed programs in Programs to install.");
                return;
            }

            AppendLog($"Retrying {retryQueue.Count} failed programs...");

            foreach (var app in retryQueue)
            {
                if (string.IsNullOrWhiteSpace(app.WingetId))
                {
                    app.InstallStatus = "ManualRequired";
                    app.InstallMessage = "No Winget ID available. Manual install required.";
                    AppendLog($"Manual required: {app.DisplayName}");
                    RefreshProgramLists();
                    UpdateSelectionSummary();
                    continue;
                }

                app.InstallStatus = "Installing";
                app.InstallMessage = string.Empty;
                RefreshProgramLists();
                UpdateSelectionSummary();

                AppendLog($"Retrying: {app.DisplayName} [{app.WingetId}]");

                var result = await _packageInstaller.InstallAsync(app);

                app.InstallStatus = result.Result;
                app.InstallMessage = result.Notes;

                AppendLog($"{result.Result}: {app.DisplayName}");

                RefreshProgramLists();
                UpdateSelectionSummary();
            }

            AppendLog("Retry failed pass complete.");
        }
        catch (Exception ex)
        {
            AppendLog("Retry failed errored: " + ex.Message);
            SetSummary("Retry failed errored. See log.");
        }
    }

    private async Task<IReadOnlyList<AppEntry>> ScanAndMergeCurrentInventoryAsync()
    {
        var registryApps = await _registryInventoryProvider.GetAppsAsync();
        var wingetApps = await _wingetInventoryProvider.GetAppsAsync();

        AppendLog($"Registry apps: {registryApps.Count}");
        AppendLog($"WinGet apps: {wingetApps.Count}");

        return _inventoryMergeService.Merge(registryApps, wingetApps);
    }

    private void RefreshProgramLists()
    {
        if (_foundProgramsDataGrid is not null)
        {
            _foundProgramsDataGrid.ItemsSource = null;
            _foundProgramsDataGrid.ItemsSource = _currentMergedApps
                .Where(x => !x.Selected)
                .OrderBy(x => x.DisplayName)
                .ToList();
        }

        if (_selectedProgramsDataGrid is not null)
        {
            _selectedProgramsDataGrid.ItemsSource = null;
            _selectedProgramsDataGrid.ItemsSource = _currentMergedApps
                .Where(x => x.Selected)
                .OrderBy(x => x.DisplayName)
                .ToList();
        }
    }

    private void UpdateSelectionSummary()
    {
        var foundCount = _currentMergedApps.Count(x => !x.Selected);
        var selectedCount = _currentMergedApps.Count(x => x.Selected);
        var installedCount = _currentMergedApps.Count(x => x.Selected && x.InstallStatus == "Installed");
        var manualCount = _currentMergedApps.Count(x => x.Selected && x.InstallStatus == "ManualRequired");
        var failedCount = _currentMergedApps.Count(x => x.Selected && x.InstallStatus == "Failed");
        var pendingCount = _currentMergedApps.Count(x => x.Selected && x.InstallStatus == "Pending");
        var installingCount = _currentMergedApps.Count(x => x.Selected && x.InstallStatus == "Installing");

        SetSummary(
            $"Programs found: {foundCount}. " +
            $"Programs to install: {selectedCount}. " +
            $"Pending: {pendingCount}. Installing: {installingCount}. Installed: {installedCount}. Manual: {manualCount}. Failed: {failedCount}.");
    }

    private static AppEntry CloneLoadedProfileApp(AppEntry source)
    {
        return new AppEntry
        {
            Selected = true,
            DisplayName = source.DisplayName,
            NormalizedName = source.NormalizedName,
            WingetId = source.WingetId,
            Version = source.Version,
            Publisher = source.Publisher,
            Source = source.Source,
            AppLike = source.AppLike,
            Status = source.Status,
            Notes = source.Notes,
            InstallStatus = "Pending",
            InstallMessage = string.Empty
        };
    }

    private static AppEntry CloneForProfileSave(AppEntry source)
    {
        return new AppEntry
        {
            Selected = true,
            DisplayName = source.DisplayName,
            NormalizedName = source.NormalizedName,
            WingetId = source.WingetId,
            Version = source.Version,
            Publisher = source.Publisher,
            Source = source.Source,
            AppLike = source.AppLike,
            Status = source.Status,
            Notes = source.Notes,
            InstallStatus = "Pending",
            InstallMessage = string.Empty
        };
    }

    private void AppendLog(string message)
    {
        EnsureSessionLogFilePath();

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var line = $"[{timestamp}] {message}";

        if (_logTextBox is not null)
        {
            _logTextBox.Text += line + Environment.NewLine;
        }

        try
        {
            File.AppendAllText(_sessionLogFilePath, line + Environment.NewLine);
        }
        catch
        {
        }
    }

    private void SetSummary(string message)
    {
        if (_summaryTextBlock is not null)
        {
            _summaryTextBlock.Text = message;
        }
    }
}