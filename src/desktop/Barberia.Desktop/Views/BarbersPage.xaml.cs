using Barberia.Core.Domain;
using Barberia.Data.Models;
using Barberia.Desktop.Services;
using Barberia.Desktop.Shell;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Barberia.Desktop.Views;

public sealed partial class BarbersPage : Page
{
    private readonly LocalAdminService _service = new();
    private IReadOnlyList<ProfileImageOption> _profileImageOptions = [];
    private Guid? _editingBarberId;
    private int _nextRotationOrder;
    private int _nextStationNumber = 1;

    public BarbersPage()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        LoadAdmin();
    }

    private void OnRefreshClick(object sender, RoutedEventArgs args)
    {
        LoadAdmin();
    }

    private void LoadAdmin()
    {
        try
        {
            var snapshot = _service.Load();
            ShowSnapshot(snapshot);
            SetStatus("Local", success: true);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void ShowSnapshot(LocalAdminSnapshot snapshot)
    {
        _nextRotationOrder = snapshot.Barbers.Count == 0
            ? 0
            : snapshot.Barbers.Max(barber => barber.RotationOrder) + 1;
        _nextStationNumber = GetNextStationNumber(snapshot.Barbers);

        SyncProfileImageOptions(snapshot.ProfileImages);
        SyncEditor(snapshot);

        ReplaceChildren(
            _barberRows,
            snapshot.Barbers.Select(CreateBarberRow),
            "No barbers registered in the local database.");
    }

    private UIElement CreateBarberRow(Barber barber)
    {
        var canChangeAvailability = barber.State is not (BarberState.Called or BarberState.InService);
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var avatar = CreateProfileAvatar(barber, 52);
        Grid.SetColumn(avatar, 0);
        row.Children.Add(avatar);

        var details = new StackPanel
        {
            Spacing = 3,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = barber.DisplayNameWithStation,
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(30, 31, 34),
                    TextWrapping = TextWrapping.WrapWholeWords
                },
                new TextBlock
                {
                    Text = $"Rotation {barber.RotationOrder} - {barber.ClientsServedToday} served today",
                    FontSize = 13,
                    Foreground = Brush(101, 108, 116),
                    TextWrapping = TextWrapping.WrapWholeWords
                }
            }
        };
        Grid.SetColumn(details, 1);
        row.Children.Add(details);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var statusBadge = CreateTextBadge(
            FormatBarberState(barber.State),
            GetStateBackground(barber.State),
            GetStateForeground(barber.State));
        actions.Children.Add(statusBadge);

        actions.Children.Add(CreateTextBadge(
            barber.IsActive ? "Active" : "Inactive",
            barber.IsActive ? Brush(235, 248, 244) : Brush(248, 249, 251),
            barber.IsActive ? Brush(17, 105, 88) : Brush(101, 108, 116)));

        var editButton = CreateSmallActionButton("Edit");
        editButton.Click += (_, _) => EditBarber(barber);
        actions.Children.Add(editButton);

        var availableButton = CreateSmallActionButton("Available");
        availableButton.IsEnabled = barber.IsActive && canChangeAvailability && barber.State != BarberState.Available;
        availableButton.Click += (_, _) => ExecuteAdminAction(() => _service.MarkBarberAvailable(barber.Id));
        actions.Children.Add(availableButton);

        var offlineButton = CreateSmallActionButton("Offline");
        offlineButton.IsEnabled = barber.IsActive && canChangeAvailability && barber.State != BarberState.Offline;
        offlineButton.Click += (_, _) => ExecuteAdminAction(() => _service.MarkBarberOffline(barber.Id));
        actions.Children.Add(offlineButton);

        var activeButton = CreateSmallActionButton(barber.IsActive ? "Deactivate" : "Activate");
        activeButton.IsEnabled = canChangeAvailability;
        activeButton.Click += (_, _) =>
        {
            if (barber.IsActive)
            {
                ExecuteAdminAction(() => _service.DeactivateBarber(barber.Id));
                return;
            }

            EditBarber(barber);
            _showInKioskCheckBox.IsChecked = true;
            _stationCodeInput.Text = string.Empty;
            _messageText.Text = "Assign an available station code like B-1, then save.";
            SetStatus("Assign station", success: true);
        };
        actions.Children.Add(activeButton);

        Grid.SetColumn(actions, 2);
        row.Children.Add(actions);

        return WrapRow(row, barber.IsActive && canChangeAvailability ? Brush(255, 255, 255) : Brush(248, 249, 251));
    }

    private void OnNewBarberClick(object sender, RoutedEventArgs args)
    {
        _editingBarberId = null;
        ClearBarberEditor();
        SetStatus("New barber", success: true);
    }

    private void OnSaveBarberClick(object sender, RoutedEventArgs args)
    {
        ExecuteAdminAction(
            () =>
            {
                _service.SaveBarber(
                    _editingBarberId,
                    _barberNameInput.Text,
                    ParseRotationOrder(),
                    ParseStationNumber(_showInKioskCheckBox.IsChecked == true),
                    GetSelectedProfileImagePath(),
                    _showInKioskCheckBox.IsChecked == true);
                _editingBarberId = null;
                ClearBarberEditor();
            },
            "Saved");
    }

    private async void OnBrowseProfileImageClick(object sender, RoutedEventArgs args)
    {
        try
        {
            var window = App.MainWindowInstance
                ?? throw new InvalidOperationException("Main window is not ready for file selection.");
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                ViewMode = PickerViewMode.Thumbnail
            };

            foreach (var extension in ProfileImageCatalog.FilePickerExtensions)
            {
                picker.FileTypeFilter.Add(extension);
            }

            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            var importedPath = _service.ImportProfileImage(file.Path);
            SyncProfileImageOptions(_service.LoadProfileImages());
            SelectProfileImage(importedPath);
            _messageText.Text = "Profile image imported. Save the barber to apply it.";
            SetStatus("Image loaded", success: true);
        }
        catch (Exception exception)
        {
            _messageText.Text = exception.Message;
            SetStatus("Image blocked", success: false);
        }
    }

    private void OnDeleteBarberClick(object sender, RoutedEventArgs args)
    {
        ExecuteAdminAction(
            () =>
            {
                if (_editingBarberId is not Guid barberId)
                {
                    throw new InvalidOperationException("Select a barber before deleting.");
                }

                _service.DeleteBarber(barberId);
                _editingBarberId = null;
                ClearBarberEditor();
            },
            "Deleted");
    }

    private void ExecuteAdminAction(Action action, string successStatus = "Updated")
    {
        try
        {
            action();
            LoadAdmin();
            SetStatus(successStatus, success: true);
        }
        catch (Exception exception)
        {
            _messageText.Text = exception.Message;
            SetStatus("Action blocked", success: false);
        }
    }

    private void ShowError(string message)
    {
        _messageText.Text = message;
        _barberRows.Children.Clear();
        _deleteBarberButton.IsEnabled = false;
        _barberRows.Children.Add(CreateEmptyState("Could not read barbers from the local database."));
        SetStatus("Error", success: false);
    }

    private void SetStatus(string text, bool success)
    {
        _statusBadgeText.Text = text;
        _statusBadge.Background = success ? Brush(235, 248, 244) : Brush(255, 240, 238);
        _statusBadge.BorderBrush = success ? Brush(181, 224, 211) : Brush(231, 170, 162);
        _statusBadgeText.Foreground = success ? Brush(17, 105, 88) : Brush(154, 58, 47);
    }

    private static Button CreateSmallActionButton(string text)
    {
        return new Button
        {
            Content = text,
            MinHeight = 36,
            MinWidth = 96,
            Padding = new Thickness(12, 6, 12, 6),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private void SyncProfileImageOptions(IReadOnlyList<ProfileImageOption> profileImages)
    {
        var selectedPath = GetSelectedProfileImagePath();
        _profileImageOptions =
        [
            new ProfileImageOption("No image - use initials", null),
            ..profileImages
        ];
        _profileImageSelector.ItemsSource = _profileImageOptions;
        SelectProfileImage(selectedPath);
    }

    private void SyncEditor(LocalAdminSnapshot snapshot)
    {
        if (_editingBarberId is Guid barberId)
        {
            var barber = snapshot.Barbers.FirstOrDefault(candidate => candidate.Id == barberId);
            if (barber is not null)
            {
                LoadBarberIntoEditor(barber);
                return;
            }

            _editingBarberId = null;
        }

        if (string.IsNullOrWhiteSpace(_rotationOrderInput.Text))
        {
            _rotationOrderInput.Text = _nextRotationOrder.ToString();
        }

        if (_showInKioskCheckBox.IsChecked == true && string.IsNullOrWhiteSpace(_stationCodeInput.Text))
        {
            _stationCodeInput.Text = FormatStationCode(_nextStationNumber);
        }

        _deleteBarberButton.IsEnabled = false;
    }

    private void EditBarber(Barber barber)
    {
        _editingBarberId = barber.Id;
        LoadBarberIntoEditor(barber);
        SetStatus("Editing", success: true);
    }

    private void LoadBarberIntoEditor(Barber barber)
    {
        _barberNameInput.Text = barber.DisplayName;
        _stationCodeInput.Text = barber.StationCode ?? string.Empty;
        _rotationOrderInput.Text = barber.RotationOrder.ToString();
        SelectProfileImage(barber.ProfileImagePath);
        _showInKioskCheckBox.IsChecked = barber.IsActive;
        _deleteBarberButton.IsEnabled = true;
    }

    private void ClearBarberEditor()
    {
        _barberNameInput.Text = string.Empty;
        _stationCodeInput.Text = FormatStationCode(_nextStationNumber);
        _rotationOrderInput.Text = _nextRotationOrder.ToString();
        SelectProfileImage(null);
        _showInKioskCheckBox.IsChecked = true;
        _deleteBarberButton.IsEnabled = false;
    }

    private int ParseRotationOrder()
    {
        var text = _rotationOrderInput.Text.Trim();
        if (!int.TryParse(text, out var rotationOrder) || rotationOrder < 0)
        {
            throw new InvalidOperationException("Rotation order must be zero or greater.");
        }

        return rotationOrder;
    }

    private int? ParseStationNumber(bool isActive)
    {
        if (!isActive)
        {
            return null;
        }

        var text = _stationCodeInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Station code is required for active barbers.");
        }

        if (!text.StartsWith("B-", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(text[2..], out var stationNumber)
            || stationNumber <= 0)
        {
            throw new InvalidOperationException("Station code must use the B-1 format.");
        }

        return stationNumber;
    }

    private string? GetSelectedProfileImagePath()
    {
        return _profileImageSelector.SelectedItem is ProfileImageOption option
            ? option.RelativePath
            : null;
    }

    private void SelectProfileImage(string? relativePath)
    {
        if (_profileImageOptions.Count == 0)
        {
            return;
        }

        var option = _profileImageOptions.FirstOrDefault(candidate =>
            string.Equals(candidate.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));
        _profileImageSelector.SelectedItem = option ?? _profileImageOptions[0];
    }

    private static int GetNextStationNumber(IEnumerable<Barber> barbers)
    {
        var usedStations = barbers
            .Where(barber => barber.IsActive)
            .Select(barber => barber.StationNumber)
            .OfType<int>()
            .ToHashSet();
        var nextStation = 1;
        while (usedStations.Contains(nextStation))
        {
            nextStation++;
        }
        return nextStation;
    }

    private static string FormatStationCode(int stationNumber) => $"B-{stationNumber}";

    private static string FormatBarberState(BarberState state) => state switch
    {
        BarberState.Offline => "Offline",
        BarberState.Available => "Available",
        BarberState.Called => "Called",
        BarberState.InService => "In Service",
        _ => "Unknown"
    };

    private static SolidColorBrush GetStateBackground(BarberState state) => state switch
    {
        BarberState.Offline => Brush(248, 249, 251),
        BarberState.Available => Brush(235, 248, 244),
        BarberState.Called => Brush(255, 248, 230),
        BarberState.InService => Brush(240, 244, 255),
        _ => Brush(248, 249, 251)
    };

    private static SolidColorBrush GetStateForeground(BarberState state) => state switch
    {
        BarberState.Offline => Brush(101, 108, 116),
        BarberState.Available => Brush(17, 105, 88),
        BarberState.Called => Brush(140, 96, 16),
        BarberState.InService => Brush(36, 84, 204),
        _ => Brush(101, 108, 116)
    };

    private static SolidColorBrush Brush(byte r, byte g, byte b)
    {
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
    }

    private static Border CreateTextBadge(string text, SolidColorBrush background, SolidColorBrush foreground)
    {
        return new Border
        {
            Background = background,
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = foreground,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    private static void ReplaceChildren(StackPanel container, IEnumerable<UIElement> elements, string emptyMessage)
    {
        container.Children.Clear();
        var hasElements = false;
        foreach (var element in elements)
        {
            container.Children.Add(element);
            hasElements = true;
        }

        if (!hasElements)
        {
            container.Children.Add(CreateEmptyState(emptyMessage));
        }
    }

    private static Border CreateEmptyState(string message)
    {
        return new Border
        {
            Padding = new Thickness(24),
            Background = Brush(248, 249, 251),
            CornerRadius = new CornerRadius(8),
            Child = new TextBlock
            {
                Text = message,
                FontSize = 14,
                Foreground = Brush(101, 108, 116),
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
    }

    private static Border WrapRow(UIElement content, SolidColorBrush background)
    {
        return new Border
        {
            Background = background,
            BorderBrush = Brush(226, 232, 240),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Child = content
        };
    }

    private static Grid CreateProfileAvatar(Barber barber, double size)
    {
        var avatar = new Grid
        {
            Width = size,
            Height = size,
            VerticalAlignment = VerticalAlignment.Center
        };

        avatar.Children.Add(new Ellipse
        {
            Fill = Brush(243, 243, 246),
            Stroke = Brush(226, 230, 235),
            StrokeThickness = 1
        });

        var imageUri = ProfileImageCatalog.ResolveImageUri(barber.ProfileImagePath);
        if (imageUri is not null)
        {
            avatar.Children.Add(new Ellipse
            {
                Fill = new ImageBrush
                {
                    ImageSource = new BitmapImage(imageUri),
                    Stretch = Stretch.UniformToFill
                },
                Stroke = Brush(255, 255, 255),
                StrokeThickness = 1
            });
            return avatar;
        }

        avatar.Children.Add(new TextBlock
        {
            Text = GetInitials(barber.DisplayName),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = size >= 50 ? 18 : 15,
            FontWeight = FontWeights.Bold,
            Foreground = Brush(0, 19, 135)
        });

        return avatar;
    }

    private static string GetInitials(string displayName)
    {
        var parts = displayName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0]));

        var initials = string.Concat(parts);
        return string.IsNullOrWhiteSpace(initials) ? "?" : initials;
    }
}
