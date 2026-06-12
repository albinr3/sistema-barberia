using Barberia.Core.Domain;
using Barberia.Data.Models;
using Barberia.Desktop.Services;
using Barberia.Desktop.Shell;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics.Imaging;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Barberia.Desktop.Views;

public sealed partial class BarbersPage : Page
{
    private readonly LocalAdminService _service = new();
    private Guid? _editingBarberId;
    private int _nextStationNumber = 1;
    private string? _selectedProfileImagePath;
    private IReadOnlyList<Barber> _barbers = [];
    private IReadOnlyDictionary<Guid, DailyRotationEntry> _dailyRotationEntries = new Dictionary<Guid, DailyRotationEntry>();
    private int _currentPage = 1;
    private const int PageSize = 10;

    public event EventHandler? ShellMenuRequested;

    public BarbersPage()
    {
        InitializeComponent();
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs args) => ShellMenuRequested?.Invoke(this, EventArgs.Empty);
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
            SetStatus("", success: true);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void ShowSnapshot(LocalAdminSnapshot snapshot)
    {
        _nextStationNumber = GetNextStationNumber(snapshot.Barbers);
        _dailyRotationEntries = snapshot.DailyRotationEntries.ToDictionary(entry => entry.BarberId);

        SyncEditor(snapshot);

        _barbers = snapshot.Barbers;
        _currentPage = 1;
        UpdatePagination();
    }

    private void UpdatePagination()
    {
        var totalItems = _barbers.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        
        if (_currentPage > totalPages)
        {
            _currentPage = totalPages;
        }

        var pagedBarbers = _barbers.Skip((_currentPage - 1) * PageSize).Take(PageSize).ToList();

        ReplaceChildren(
            _barberRows,
            pagedBarbers.Select(CreateBarberRow),
            "No barbers registered in the local database.");

        var startIndex = totalItems == 0 ? 0 : (_currentPage - 1) * PageSize + 1;
        var endIndex = Math.Min(_currentPage * PageSize, totalItems);
        _barberFooterText.Text = $"Showing {startIndex}-{endIndex} of {totalItems} barbers";

        _paginationPanel.Visibility = totalPages > 1 ? Visibility.Visible : Visibility.Collapsed;
        
        if (totalPages > 1)
        {
            RenderPaginationButtons(totalPages);
        }
    }

    private void RenderPaginationButtons(int totalPages)
    {
        _paginationPanel.Children.Clear();

        var prevButton = new Button
        {
            MinHeight = 34,
            MinWidth = 34,
            Content = "\uE76B",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            IsEnabled = _currentPage > 1
        };
        prevButton.Click += (_, _) => { _currentPage--; UpdatePagination(); };
        _paginationPanel.Children.Add(prevButton);

        for (int i = 1; i <= totalPages; i++)
        {
            var pageNum = i;
            var isCurrent = pageNum == _currentPage;
            var pageButton = new Button
            {
                MinHeight = 34,
                MinWidth = 34,
                Content = pageNum.ToString()
            };
            if (isCurrent)
            {
                pageButton.Background = Brush(223, 224, 255);
                pageButton.BorderBrush = Brush(0, 19, 135);
                pageButton.Foreground = Brush(0, 11, 98);
            }
            pageButton.Click += (_, _) => { _currentPage = pageNum; UpdatePagination(); };
            _paginationPanel.Children.Add(pageButton);
        }

        var nextButton = new Button
        {
            MinHeight = 34,
            MinWidth = 34,
            Content = "\uE76C",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            IsEnabled = _currentPage < totalPages
        };
        nextButton.Click += (_, _) => { _currentPage++; UpdatePagination(); };
        _paginationPanel.Children.Add(nextButton);
    }

    private UIElement CreateBarberRow(Barber barber)
    {
        var canChangeAvailability = barber.State is not (BarberState.Called or BarberState.InService);
        var row = new Grid
        {
            Padding = new Thickness(16, 12, 16, 12),
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(226, 226, 229),
            BorderThickness = new Thickness(0, 0, 0, 1),
            ColumnSpacing = 16
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.3, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });

        var identity = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        identity.Children.Add(CreateProfileAvatar(barber, 40));

        var details = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = barber.DisplayName,
                    FontFamily = new FontFamily("Inter"),
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(26, 28, 30),
                    TextWrapping = TextWrapping.WrapWholeWords
                },
                new TextBlock
                {
                    Text = barber.StationCode ?? "Unassigned",
                    FontFamily = new FontFamily("Inter"),
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(68, 70, 85),
                    TextWrapping = TextWrapping.WrapWholeWords
                }
            }
        };
        identity.Children.Add(details);
        Grid.SetColumn(identity, 1);
        row.Children.Add(identity);

        var stateBadge = CreateTextBadge(
            barber.IsActive ? FormatBarberState(barber.State) : "Inactive",
            barber.IsActive ? GetStateBackground(barber.State) : Brush(248, 249, 251),
            barber.IsActive ? GetStateForeground(barber.State) : Brush(101, 108, 116));
        stateBadge.HorizontalAlignment = HorizontalAlignment.Left;
        stateBadge.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(stateBadge, 2);
        row.Children.Add(stateBadge);

        var stationBadge = CreateTextBadge(
            barber.StationCode ?? "Unassigned",
            barber.StationCode is null ? Brush(238, 238, 240) : Brush(226, 226, 229),
            barber.StationCode is null ? Brush(68, 70, 85) : Brush(26, 28, 30));
        stationBadge.HorizontalAlignment = HorizontalAlignment.Center;
        stationBadge.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(stationBadge, 3);
        row.Children.Add(stationBadge);

        var rotation = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = FormatDailyRotationText(barber),
                    FontFamily = new FontFamily("Inter"),
                    FontSize = 14,
                    Foreground = Brush(26, 28, 30)
                },
                new TextBlock
                {
                    Text = $"{barber.ClientsServedToday} served today",
                    FontFamily = new FontFamily("Inter"),
                    FontSize = 12,
                    Foreground = Brush(68, 70, 85)
                }
            }
        };
        Grid.SetColumn(rotation, 4);
        row.Children.Add(rotation);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var editButton = CreateIconActionButton("\uE70F", "Edit");
        editButton.Click += (_, _) => EditBarber(barber);
        actions.Children.Add(editButton);

        var deleteButton = CreateIconActionButton("\uE74D", "Delete");
        deleteButton.Click += (_, _) =>
        {
            _editingBarberId = barber.Id;
            OnDeleteBarberClick(deleteButton, new RoutedEventArgs());
        };
        actions.Children.Add(deleteButton);

        Grid.SetColumn(actions, 5);
        row.Children.Add(actions);

        return row;
    }

    private void OnNewBarberClick(object sender, RoutedEventArgs args)
    {
        _editingBarberId = null;
        ClearBarberEditor();
        OpenBarberModal("Add New Barber");
        SetStatus("New barber", success: true);
    }

    private void OnSaveBarberClick(object sender, RoutedEventArgs args)
    {
        var success = ExecuteAdminAction(
            () =>
            {
                _service.SaveBarber(
                    _editingBarberId,
                    _barberNameInput.Text,
                    ParseStationNumber(_showInKioskCheckBox.IsChecked == true),
                    GetSelectedProfileImagePath(),
                    _showInKioskCheckBox.IsChecked == true,
                    ParseCommissionPercentage());
                _editingBarberId = null;
            },
            "Saved");

        if (success)
        {
            ClearBarberEditor();
            CloseBarberModal();
        }
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
            SelectProfileImage(importedPath);
            _messageText.Text = "Profile image imported. Save the barber to apply it.";
            SetModalMessage("Profile image imported. Save the barber to apply it.", success: true);
            RefreshModalAvatar();
            SetStatus("Image loaded", success: true);
        }
        catch (Exception exception)
        {
            _messageText.Text = exception.Message;
            SetModalMessage(exception.Message, success: false);
            SetStatus("Image blocked", success: false);
        }
    }

    private void OnDeleteBarberClick(object sender, RoutedEventArgs args)
    {
        var success = ExecuteAdminAction(
            () =>
            {
                if (_editingBarberId is not Guid barberId)
                {
                    throw new InvalidOperationException("Select a barber before deleting.");
                }

                _service.DeleteBarber(barberId);
                _editingBarberId = null;
            },
            "Deleted");

        if (success)
        {
            ClearBarberEditor();
            CloseBarberModal();
        }
    }

    private bool ExecuteAdminAction(Action action, string successStatus = "Updated")
    {
        try
        {
            action();
            LoadAdmin();
            SetModalMessage("", success: true);
            SetStatus(successStatus, success: true);
            return true;
        }
        catch (Exception exception)
        {
            _messageText.Text = exception.Message;
            SetModalMessage(exception.Message, success: false);
            SetStatus("Action blocked", success: false);
            return false;
        }
    }

    private void ShowError(string message)
    {
        _messageText.Text = message;
        _barberRows.Children.Clear();
        _deleteBarberButton.IsEnabled = false;
        _barberRows.Children.Add(CreateEmptyState("Could not read barbers from the local database."));
        _barberFooterText.Text = "Showing 0 of 0 barbers";
        _paginationPanel.Visibility = Visibility.Collapsed;
        SetStatus("Error", success: false);
    }

    private void SetStatus(string text, bool success)
    {
        _messageText.Text = text;
        _messageText.Foreground = success ? Brush(17, 105, 88) : Brush(154, 58, 47);
    }

    private void SetModalMessage(string text, bool success)
    {
        if (_barberModalMessageText is null)
        {
            return;
        }

        _barberModalMessageText.Text = text;
        _barberModalMessageText.Foreground = success ? Brush(17, 105, 88) : Brush(154, 58, 47);
    }

    private void OnCancelBarberClick(object sender, RoutedEventArgs args)
    {
        _editingBarberId = null;
        ClearBarberEditor();
        CloseBarberModal();
    }

    private static Button CreateIconActionButton(string glyph, string toolTip)
    {
        var button = new Button
        {
            MinHeight = 34,
            MinWidth = 34,
            Padding = new Thickness(6),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderBrush = new SolidColorBrush(Colors.Transparent),
            Content = new FontIcon
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 16,
                Glyph = glyph
            }
        };
        ToolTipService.SetToolTip(button, toolTip);
        return button;
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
        OpenBarberModal("Edit Barber");
        SetStatus("Editing", success: true);
    }

    private void LoadBarberIntoEditor(Barber barber)
    {
        _barberNameInput.Text = barber.DisplayName;
        _stationCodeInput.Text = barber.StationCode ?? string.Empty;
        _commissionPercentageInput.Text = barber.CommissionPercentage.ToString();
        SelectProfileImage(ProfileImageCatalog.ResolveImageUri(barber.ProfileImagePath) is null
            ? null
            : barber.ProfileImagePath);
        _showInKioskCheckBox.IsChecked = barber.IsActive;
        _deleteBarberButton.IsEnabled = true;
        RefreshModalAvatar();
    }

    private void ClearBarberEditor()
    {
        _barberNameInput.Text = string.Empty;
        _stationCodeInput.Text = FormatStationCode(_nextStationNumber);
        _commissionPercentageInput.Text = Barber.DefaultCommissionPercentage.ToString();
        SelectProfileImage(null);
        _showInKioskCheckBox.IsChecked = true;
        _deleteBarberButton.IsEnabled = false;
        RefreshModalAvatar();
    }

    private void OpenBarberModal(string title)
    {
        _barberModalTitle.Text = title;
        _barberModalOverlay.Visibility = Visibility.Visible;
        SetModalMessage("", success: true);
        RefreshModalAvatar();
    }

    private void CloseBarberModal()
    {
        SetModalMessage("", success: true);
        _barberModalOverlay.Visibility = Visibility.Collapsed;
    }

    private int? ParseStationNumber(bool isActive)
    {
        var text = _stationCodeInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            if (!isActive)
            {
                return null;
            }

            throw new InvalidOperationException("Write a station number before activating this barber.");
        }

        if (!text.StartsWith("B-", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(text[2..], out var stationNumber)
            || stationNumber <= 0)
        {
            throw new InvalidOperationException("Station code must use the B-1 format.");
        }

        return stationNumber;
    }

    private int ParseCommissionPercentage()
    {
        var text = _commissionPercentageInput.Text.Trim();
        if (!int.TryParse(text, out var commissionPercentage) || commissionPercentage is < 0 or > 100)
        {
            throw new InvalidOperationException("Commission percentage must be between 0 and 100.");
        }

        return commissionPercentage;
    }

    private string? GetSelectedProfileImagePath()
    {
        return _selectedProfileImagePath;
    }

    private void SelectProfileImage(string? relativePath)
    {
        _selectedProfileImagePath = relativePath;
        RefreshModalAvatar();
    }

    private void RefreshModalAvatar()
    {
        if (_modalAvatarHost is null)
        {
            return;
        }

        _modalAvatarHost.Children.Clear();
        _modalAvatarHost.Children.Add(CreateProfileAvatar(
            _barberNameInput.Text,
            GetSelectedProfileImagePath(),
            64));
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

    private string FormatDailyRotationText(Barber barber)
    {
        return _dailyRotationEntries.TryGetValue(barber.Id, out var entry)
            ? $"Arrival #{entry.QueuePosition + 1} - {entry.ArrivedAt:hh:mm tt}"
            : "Not checked in today";
    }

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
        return CreateProfileAvatar(barber.DisplayName, barber.ProfileImagePath, size);
    }

    private static Grid CreateProfileAvatar(string displayName, string? relativeImagePath, double size)
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

        avatar.Children.Add(new TextBlock
        {
            Text = GetInitials(displayName),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = size >= 50 ? 18 : 15,
            FontWeight = FontWeights.Bold,
            Foreground = Brush(0, 19, 135)
        });

        var imagePath = ProfileImageCatalog.ResolveImagePath(relativeImagePath);
        if (imagePath is not null)
        {
            var imageBrush = new ImageBrush
            {
                Stretch = Stretch.UniformToFill
            };
            var imageCircle = new Ellipse
            {
                Fill = imageBrush,
                Stroke = Brush(255, 255, 255),
                StrokeThickness = 1,
                Visibility = Visibility.Collapsed
            };
            avatar.Children.Add(imageCircle);
            _ = LoadProfileImageAsync(imageBrush, imageCircle, imagePath);
        }

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

    private static async Task LoadProfileImageAsync(ImageBrush imageBrush, UIElement imageElement, string fullPath)
    {
        try
        {
            await using var fileStream = File.OpenRead(fullPath);
            using var imageStream = fileStream.AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(imageStream);
            var bitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);
            var source = new SoftwareBitmapSource();
            await source.SetBitmapAsync(bitmap);
            imageBrush.ImageSource = source;
            imageElement.Visibility = Visibility.Visible;
        }
        catch
        {
            imageBrush.ImageSource = null;
            imageElement.Visibility = Visibility.Collapsed;
        }
    }

}
