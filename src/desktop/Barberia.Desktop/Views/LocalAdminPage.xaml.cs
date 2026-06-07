using Barberia.Core.Domain;
using Barberia.Data.Models;
using Barberia.Desktop.Services;
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

public sealed partial class LocalAdminPage : Page
{
    private readonly LocalAdminService _service = new();
    private IReadOnlyList<ProfileImageOption> _profileImageOptions = [];
    private Guid? _editingBarberId;
    private Guid? _editingServiceId;
    private int _nextRotationOrder;
    private int _nextStationNumber = 1;
    private int _nextServiceDisplayOrder;

    public LocalAdminPage()
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
        var activeCount = snapshot.Barbers.Count(barber => barber.IsActive);
        var availableCount = snapshot.Barbers.Count(barber => barber.IsActive && barber.State == BarberState.Available);

        _activeTurnsText.Text = snapshot.ActiveTurns.Count.ToString();
        _checkInsText.Text = snapshot.Operations.CheckIns.ToString();
        _availableBarbersText.Text = $"{availableCount}/{activeCount}";
        _cashText.Text = FormatMoney(snapshot.Cash.TotalAmountCents, snapshot.Cash.Currency);
        _lastRefreshText.Text = $"Updated: {snapshot.GeneratedAt:hh:mm tt}";
        _databasePathText.Text = snapshot.DatabasePath;
        _databaseSizeText.Text = FormatBytes(snapshot.DatabaseSizeBytes);
        _messageText.Text = $"Completed services today: {snapshot.Operations.CompletedServices}. Cash payments: {snapshot.Cash.PaymentCount}.";
        _nextRotationOrder = snapshot.Barbers.Count == 0
            ? 0
            : snapshot.Barbers.Max(barber => barber.RotationOrder) + 1;
        _nextStationNumber = GetNextStationNumber(snapshot.Barbers);
        _nextServiceDisplayOrder = snapshot.Services.Count == 0
            ? 0
            : snapshot.Services.Max(service => service.DisplayOrder) + 1;

        SyncProfileImageOptions(snapshot.ProfileImages);
        SyncEditor(snapshot);
        SyncServiceEditor(snapshot);

        ReplaceChildren(
            _barberRows,
            snapshot.Barbers.Select(CreateBarberRow),
            "No barbers registered in the local database.");
        ReplaceChildren(
            _serviceRows,
            snapshot.Services.Select(CreateServiceRow),
            "No services registered in the local database.");
        ReplaceChildren(
            _turnRows,
            snapshot.ActiveTurns.Select(turn => CreateTurnRow(turn, snapshot.Barbers)),
            "No active turns right now.");
        ReplaceChildren(
            _auditRows,
            snapshot.RecentAuditEvents.Select(CreateAuditRow),
            "No audit events recorded yet.");
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

    private UIElement CreateServiceRow(Service service)
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var details = new StackPanel
        {
            Spacing = 3,
            Children =
            {
                new TextBlock
                {
                    Text = service.Name,
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(30, 31, 34),
                    TextWrapping = TextWrapping.WrapWholeWords
                },
                new TextBlock
                {
                    Text = $"Base ${service.Price:0.00} - Order {service.DisplayOrder}",
                    FontSize = 13,
                    Foreground = Brush(101, 108, 116),
                    TextWrapping = TextWrapping.WrapWholeWords
                }
            }
        };
        row.Children.Add(details);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        actions.Children.Add(CreateTextBadge(
            service.IsActive ? "Active" : "Inactive",
            service.IsActive ? Brush(235, 248, 244) : Brush(248, 249, 251),
            service.IsActive ? Brush(17, 105, 88) : Brush(101, 108, 116)));

        var editButton = CreateSmallActionButton("Edit");
        editButton.Click += (_, _) => EditService(service);
        actions.Children.Add(editButton);

        Grid.SetColumn(actions, 1);
        row.Children.Add(actions);

        return WrapRow(row, service.IsActive ? Brush(255, 255, 255) : Brush(248, 249, 251));
    }

    private void OnNewServiceClick(object sender, RoutedEventArgs args)
    {
        _editingServiceId = null;
        ClearServiceEditor();
        SetStatus("New service", success: true);
    }

    private void OnSaveServiceClick(object sender, RoutedEventArgs args)
    {
        ExecuteAdminAction(
            () =>
            {
                _service.SaveService(
                    _editingServiceId,
                    _serviceNameInput.Text,
                    ParseServicePrice(),
                    _serviceActiveCheckBox.IsChecked == true,
                    ParseServiceDisplayOrder());
                _editingServiceId = null;
                ClearServiceEditor();
            },
            "Saved");
    }

    private void OnDeleteServiceClick(object sender, RoutedEventArgs args)
    {
        ExecuteAdminAction(
            () =>
            {
                if (_editingServiceId is not Guid serviceId)
                {
                    throw new InvalidOperationException("Select a service before deleting.");
                }

                _service.DeleteService(serviceId);
                _editingServiceId = null;
                ClearServiceEditor();
            },
            "Deleted");
    }

    private UIElement CreateTurnRow(Turn turn, IReadOnlyList<Barber> barbers)
    {
        var barberName = turn.AssignedBarberId is null
            ? "Unassigned"
            : barbers.FirstOrDefault(barber => barber.Id == turn.AssignedBarberId)?.DisplayNameWithStation ?? "Local barber";
        var customerName = string.IsNullOrWhiteSpace(turn.CustomerName) ? "Walk-in customer" : turn.CustomerName;

        var row = new Grid
        {
            ColumnSpacing = 12,
            Children =
            {
                new StackPanel
                {
                    Spacing = 3,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"{turn.DisplayTicketNumber} - {customerName}",
                            FontSize = 17,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = Brush(30, 31, 34),
                            TextWrapping = TextWrapping.WrapWholeWords
                        },
                        new TextBlock
                        {
                            Text = $"{FormatTurnSource(turn.Source)} - {barberName} - {turn.CheckedInAt:hh:mm tt} - Interno {turn.TicketNumber}",
                            FontSize = 13,
                            Foreground = Brush(101, 108, 116),
                            TextWrapping = TextWrapping.WrapWholeWords
                        }
                    }
                }
            }
        };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        actions.Children.Add(CreateTextBadge(
            FormatTurnState(turn.State),
            GetTurnBackground(turn.State),
            GetTurnForeground(turn.State)));

        var cancelButton = CreateSmallActionButton("Cancel");
        cancelButton.IsEnabled = turn.State is TurnState.Waiting or TurnState.Called or TurnState.InService;
        cancelButton.Click += (_, _) => ExecuteAdminAction(() => _service.CancelTurn(turn.Id), "Cancelled");
        actions.Children.Add(cancelButton);

        Grid.SetColumn(actions, 1);
        row.Children.Add(actions);

        return WrapRow(row, Brush(255, 255, 255));
    }

    private static UIElement CreateAuditRow(AuditEvent auditEvent)
    {
        return WrapRow(
            new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = auditEvent.EventType,
                        FontSize = 16,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brush(30, 31, 34),
                        TextWrapping = TextWrapping.WrapWholeWords
                    },
                    new TextBlock
                    {
                        Text = $"{auditEvent.OccurredAt:hh:mm tt} - {auditEvent.AggregateType} - {auditEvent.DeviceId ?? "local device"}",
                        FontSize = 13,
                        Foreground = Brush(101, 108, 116),
                        TextWrapping = TextWrapping.WrapWholeWords
                    }
                }
            },
            Brush(255, 255, 255));
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
        _activeTurnsText.Text = "0";
        _checkInsText.Text = "0";
        _availableBarbersText.Text = "0/0";
        _cashText.Text = "USD 0.00";
        _lastRefreshText.Text = "No local snapshot loaded";
        _databasePathText.Text = LocalAppPaths.DatabasePath;
        _databaseSizeText.Text = "0 B";
        _messageText.Text = message;
        _barberRows.Children.Clear();
        _turnRows.Children.Clear();
        _auditRows.Children.Clear();
        _serviceRows.Children.Clear();
        _deleteBarberButton.IsEnabled = false;
        _deleteServiceButton.IsEnabled = false;
        _barberRows.Children.Add(CreateEmptyState("Could not read barbers from the local database."));
        _serviceRows.Children.Add(CreateEmptyState("Could not read services from the local database."));
        _turnRows.Children.Add(CreateEmptyState("Could not read active turns."));
        _auditRows.Children.Add(CreateEmptyState(message));
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

    private void SyncServiceEditor(LocalAdminSnapshot snapshot)
    {
        if (_editingServiceId is Guid serviceId)
        {
            var service = snapshot.Services.FirstOrDefault(candidate => candidate.Id == serviceId);
            if (service is not null)
            {
                LoadServiceIntoEditor(service);
                return;
            }

            _editingServiceId = null;
        }

        if (string.IsNullOrWhiteSpace(_serviceDisplayOrderInput.Text))
        {
            _serviceDisplayOrderInput.Text = _nextServiceDisplayOrder.ToString();
        }

        _deleteServiceButton.IsEnabled = false;
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

    private void EditService(Service service)
    {
        _editingServiceId = service.Id;
        LoadServiceIntoEditor(service);
        SetStatus("Editing", success: true);
    }

    private void LoadServiceIntoEditor(Service service)
    {
        _serviceNameInput.Text = service.Name;
        _servicePriceInput.Text = $"{service.Price:0.00}";
        _serviceDisplayOrderInput.Text = service.DisplayOrder.ToString();
        _serviceActiveCheckBox.IsChecked = service.IsActive;
        _deleteServiceButton.IsEnabled = true;
    }

    private void ClearServiceEditor()
    {
        _serviceNameInput.Text = string.Empty;
        _servicePriceInput.Text = string.Empty;
        _serviceDisplayOrderInput.Text = _nextServiceDisplayOrder.ToString();
        _serviceActiveCheckBox.IsChecked = true;
        _deleteServiceButton.IsEnabled = false;
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

    private decimal ParseServicePrice()
    {
        var text = _servicePriceInput.Text.Trim();
        if (!decimal.TryParse(text, out var price) || price <= 0)
        {
            throw new InvalidOperationException("Service price must be greater than zero.");
        }

        return price;
    }

    private int ParseServiceDisplayOrder()
    {
        var text = _serviceDisplayOrderInput.Text.Trim();
        if (!int.TryParse(text, out var displayOrder) || displayOrder < 0)
        {
            throw new InvalidOperationException("Service display order must be zero or greater.");
        }

        return displayOrder;
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

    private static string FormatStationCode(int stationNumber)
    {
        return $"B-{stationNumber}";
    }

    private static void ReplaceChildren(StackPanel panel, IEnumerable<UIElement> children, string emptyText)
    {
        panel.Children.Clear();
        var added = false;

        foreach (var child in children)
        {
            panel.Children.Add(child);
            added = true;
        }

        if (!added)
        {
            panel.Children.Add(CreateEmptyState(emptyText));
        }
    }

    private static UIElement WrapRow(UIElement child, Brush background)
    {
        return new Border
        {
            Background = background,
            BorderBrush = Brush(226, 230, 235),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Child = child
        };
    }

    private static UIElement CreateEmptyState(string text)
    {
        return WrapRow(
            new TextBlock
            {
                Text = text,
                FontSize = 14,
                Foreground = Brush(101, 108, 116),
                TextWrapping = TextWrapping.Wrap
            },
            Brush(248, 249, 251));
    }

    private static Border CreateTextBadge(string text, Brush background, Brush foreground)
    {
        return new Border
        {
            Background = background,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6, 10, 6),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = foreground
            }
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

    private static string FormatBarberState(BarberState state)
    {
        return state switch
        {
            BarberState.NotCheckedIn => "Not checked in",
            BarberState.Available => "Available",
            BarberState.Called => "Called",
            BarberState.InService => "In service",
            BarberState.Offline => "Offline",
            _ => state.ToString()
        };
    }

    private static string FormatTurnState(TurnState state)
    {
        return state switch
        {
            TurnState.Waiting => "Waiting",
            TurnState.Called => "Called",
            TurnState.InService => "In service",
            TurnState.Completed => "Completed",
            TurnState.Cancelled => "Cancelled",
            TurnState.NoShow => "No show",
            TurnState.Voided => "Voided",
            _ => state.ToString()
        };
    }

    private static string FormatTurnSource(TurnSource source)
    {
        return source == TurnSource.Appointment ? "Appointment" : "Walk-in";
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

    private static Brush GetStateBackground(BarberState state)
    {
        return state switch
        {
            BarberState.Available => Brush(235, 248, 244),
            BarberState.Called => Brush(255, 247, 232),
            BarberState.InService => Brush(240, 244, 250),
            BarberState.Offline => Brush(255, 240, 238),
            _ => Brush(248, 249, 251)
        };
    }

    private static Brush GetStateForeground(BarberState state)
    {
        return state switch
        {
            BarberState.Available => Brush(17, 105, 88),
            BarberState.Called => Brush(122, 82, 21),
            BarberState.InService => Brush(63, 78, 97),
            BarberState.Offline => Brush(154, 58, 47),
            _ => Brush(101, 108, 116)
        };
    }

    private static Brush GetTurnBackground(TurnState state)
    {
        return state switch
        {
            TurnState.Waiting => Brush(255, 247, 232),
            TurnState.Called => Brush(235, 248, 244),
            TurnState.InService => Brush(240, 244, 250),
            _ => Brush(248, 249, 251)
        };
    }

    private static Brush GetTurnForeground(TurnState state)
    {
        return state switch
        {
            TurnState.Waiting => Brush(122, 82, 21),
            TurnState.Called => Brush(17, 105, 88),
            TurnState.InService => Brush(63, 78, 97),
            _ => Brush(101, 108, 116)
        };
    }

    private static string FormatMoney(long cents, string currency)
    {
        return $"{currency} {Money.FromCents(cents):0.00}";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var kilobytes = bytes / 1024m;
        if (kilobytes < 1024)
        {
            return $"{kilobytes:0.0} KB";
        }

        return $"{kilobytes / 1024m:0.0} MB";
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }
}
