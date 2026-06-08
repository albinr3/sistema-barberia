using Barberia.Core.Domain;
using Barberia.Data.Models;
using Barberia.Desktop.Services;
using Barberia.Desktop.Shell;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Barberia.Desktop.Views;

public sealed partial class ServicesPage : Page
{
    private readonly LocalAdminService _service = new();
    private Guid? _editingServiceId;
    private int _nextServiceDisplayOrder;

    public ServicesPage()
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
        _nextServiceDisplayOrder = snapshot.Services.Count == 0
            ? 0
            : snapshot.Services.Max(service => service.DisplayOrder) + 1;

        SyncServiceEditor(snapshot);

        ReplaceChildren(
            _serviceRows,
            snapshot.Services.Select(CreateServiceRow),
            "No services registered in the local database.");
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
        _serviceRows.Children.Clear();
        _deleteServiceButton.IsEnabled = false;
        _serviceRows.Children.Add(CreateEmptyState("Could not read services from the local database."));
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
}
