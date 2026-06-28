using Barberia.Desktop.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Security.Cryptography;
using System.Text;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Barberia.Desktop.Views;

public sealed partial class BackupsPage : Page
{
    private const string DummyPassword = "********";
    private DesktopBackupSettings _currentSettings = new DesktopBackupSettings(true, "21:00", null, 7, 7);

    public event EventHandler? ShellMenuRequested;

    public BackupsPage()
    {
        InitializeComponent();
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs args)
    {
        ShellMenuRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        LoadSettings();
        LoadLocalFiles();
    }

    private void LoadSettings()
    {
        try
        {
            _currentSettings = DesktopBackupSettings.Load();
            
            _enabledSwitch.IsOn = _currentSettings.Enabled;
            
            if (TimeOnly.TryParse(_currentSettings.TimeOfDay, out var time))
            {
                _timePicker.Time = time.ToTimeSpan();
            }
            else
            {
                _timePicker.Time = new TimeSpan(21, 0, 0);
            }

            if (!string.IsNullOrEmpty(_currentSettings.EncryptedPassword))
            {
                _passwordBox.Password = DummyPassword;
            }
            else
            {
                _passwordBox.Password = string.Empty;
            }

            _localRetentionBox.Value = _currentSettings.LocalRetentionDays;
            _cloudRetentionBox.Value = _currentSettings.CloudRetentionDays;
        }
        catch (Exception ex)
        {
            ShowStatus($"Error loading settings: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs args)
    {
        try
        {
            _currentSettings = _currentSettings with
            {
                Enabled = _enabledSwitch.IsOn,
                TimeOfDay = TimeOnly.FromTimeSpan(_timePicker.Time).ToString("HH:mm"),
                LocalRetentionDays = (int)_localRetentionBox.Value,
                CloudRetentionDays = (int)_cloudRetentionBox.Value
            };

            var inputPassword = _passwordBox.Password;
            if (inputPassword == DummyPassword)
            {
                // Kept existing password, do nothing to _currentSettings.EncryptedPassword
            }
            else if (string.IsNullOrWhiteSpace(inputPassword))
            {
                _currentSettings = _currentSettings with { EncryptedPassword = null };
            }
            else
            {
                var plainBytes = Encoding.UTF8.GetBytes(inputPassword);
                var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.LocalMachine);
                _currentSettings = _currentSettings with { EncryptedPassword = Convert.ToBase64String(encryptedBytes) };
                // After saving, reset to dummy
                _passwordBox.Password = DummyPassword;
            }

            _currentSettings.Save();
            ShowStatus("Settings saved successfully.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"Error saving settings: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void OnRunManualBackupClick(object sender, RoutedEventArgs args)
    {
        SetBusy(true);
        try
        {
            ShowStatus("Running backup...", InfoBarSeverity.Informational);
            
            if (App.BackupServiceInstance != null)
            {
                await App.BackupServiceInstance.RunManualBackupAsync();
                ShowStatus("Manual backup completed.", InfoBarSeverity.Success);
                LoadLocalFiles(); // Refresh files list
            }
            else
            {
                ShowStatus("Backup service is not initialized.", InfoBarSeverity.Error);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Backup error: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnRestoreFromFileClick(object sender, RoutedEventArgs args)
    {
        try
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".zip");

            if (App.MainWindowInstance is not null)
            {
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindowInstance));
            }

            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            await RestoreBackupAsync(file.Path);
        }
        catch (Exception ex)
        {
            ShowStatus($"Restore error: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void OnRestoreLocalBackupClick(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: string path })
        {
            return;
        }

        await RestoreBackupAsync(path);
    }

    private async Task RestoreBackupAsync(string zipPath)
    {
        if (App.BackupServiceInstance is null)
        {
            ShowStatus("Backup service is not initialized.", InfoBarSeverity.Error);
            return;
        }

        var fileInfo = new FileInfo(zipPath);
        if (!fileInfo.Exists)
        {
            ShowStatus("Backup file was not found.", InfoBarSeverity.Error);
            return;
        }

        var confirmed = await ConfirmRestoreAsync(fileInfo);
        if (!confirmed)
        {
            return;
        }

        SetBusy(true);
        try
        {
            ShowStatus("Validating backup before restore...", InfoBarSeverity.Informational);
            var result = await App.BackupServiceInstance.RestoreBackupAsync(zipPath);
            ShowRestoreSuccess(result);
            LoadLocalFiles();
        }
        catch (DesktopBackupPasswordRequiredException)
        {
            SetBusy(false);
            var password = await PromptForBackupPasswordAsync();
            if (password is null)
            {
                ShowStatus("Restore cancelled. Backup password was not entered.", InfoBarSeverity.Warning);
                return;
            }

            SetBusy(true);
            try
            {
                ShowStatus("Restoring password-protected backup...", InfoBarSeverity.Informational);
                var result = await App.BackupServiceInstance.RestoreBackupAsync(zipPath, password);
                ShowRestoreSuccess(result);
                LoadLocalFiles();
            }
            catch (Exception ex)
            {
                ShowStatus($"Restore error: {ex.Message}", InfoBarSeverity.Error);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Restore error: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<bool> ConfirmRestoreAsync(FileInfo file)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Restore backup?",
            Content = $"This will validate and restore {file.Name} ({file.Length / 1024.0 / 1024.0:F2} MB).\n\nA safety backup of the current database will be created first. Restore is blocked if Desktop still has pending sync events. After restore, close and reopen Barberia Desktop before operating.",
            PrimaryButtonText = "Restore",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<string?> PromptForBackupPasswordAsync()
    {
        var passwordBox = new PasswordBox
        {
            Header = "Backup password",
            PlaceholderText = "Enter backup password"
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Password required",
            Content = passwordBox,
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(passwordBox.Password)
            ? passwordBox.Password
            : null;
    }

    private void ShowRestoreSuccess(DesktopBackupRestoreResult result)
    {
        ShowStatus(
            $"Backup restored. Restore id: {result.RestoreId}. Safety backup created at {result.SafetyBackupPath}. Close and reopen Barberia Desktop before operating; Web will adopt this restored state after sync and keep later records as reverted audit.",
            InfoBarSeverity.Success);
    }

    private void LoadLocalFiles()
    {
        _localFilesList.Children.Clear();
        
        try
        {
            var directory = Path.Combine(LocalAppPaths.RootDirectory, "backups");
            if (!Directory.Exists(directory))
            {
                _localFilesList.Children.Add(CreateEmptyState("No local backups found."));
                return;
            }

            var files = new DirectoryInfo(directory).GetFiles("*.zip")
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            if (files.Count == 0)
            {
                _localFilesList.Children.Add(CreateEmptyState("No local backups found."));
                return;
            }

            foreach (var file in files)
            {
                var row = new Grid { Padding = new Thickness(12, 8, 12, 8) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                row.Children.Add(new TextBlock { Text = file.Name, VerticalAlignment = VerticalAlignment.Center });
                
                var dateText = new TextBlock { Text = file.CreationTime.ToString("g"), VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray) };
                Grid.SetColumn(dateText, 1);
                row.Children.Add(dateText);

                var sizeText = new TextBlock { Text = $"{file.Length / 1024.0 / 1024.0:F2} MB", VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(sizeText, 2);
                row.Children.Add(sizeText);

                var restoreButton = new Button
                {
                    Content = "Restore",
                    Tag = file.FullName,
                    MinHeight = 32,
                    Padding = new Thickness(12, 4, 12, 4),
                    VerticalAlignment = VerticalAlignment.Center
                };
                restoreButton.Click += OnRestoreLocalBackupClick;
                Grid.SetColumn(restoreButton, 3);
                row.Children.Add(restoreButton);

                var border = new Border
                {
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 238, 238, 240)),
                    Child = row
                };

                _localFilesList.Children.Add(border);
            }
        }
        catch (Exception ex)
        {
            _localFilesList.Children.Add(CreateEmptyState($"Could not load files: {ex.Message}"));
        }
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        _statusInfoBar.Message = message;
        _statusInfoBar.Severity = severity;
        _statusInfoBar.IsOpen = true;
    }

    private void SetBusy(bool isBusy)
    {
        _saveButton.IsEnabled = !isBusy;
        _runBackupButton.IsEnabled = !isBusy;
        _restoreFromFileButton.IsEnabled = !isBusy;
        foreach (var child in _localFilesList.Children)
        {
            if (child is Border { Child: Grid row })
            {
                foreach (var rowChild in row.Children)
                {
                    if (rowChild is Button button)
                    {
                        button.IsEnabled = !isBusy;
                    }
                }
            }
        }
    }

    private static UIElement CreateEmptyState(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 14,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            Margin = new Thickness(12)
        };
    }
}
