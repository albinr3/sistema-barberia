using Barberia.Core.Domain;
using Barberia.Data.Models;
using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Globalization;

namespace Barberia.Desktop.Views;

public sealed partial class ServicesPage : Page
{
    private const int PageSize = 10;

    private readonly LocalAdminService _service = new();
    private Guid? _editingServiceId;
    private int _nextServiceDisplayOrder;
    private bool _resetDisplayOrderToDefault = true;
    private IReadOnlyList<Service> _services = [];
    private int _currentPage = 1;

    public event EventHandler? ShellMenuRequested;

    public ServicesPage()
    {
        InitializeComponent();
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs args) => ShellMenuRequested?.Invoke(this, EventArgs.Empty);

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        LoadAdmin();
    }

    private void LoadAdmin()
    {
        try
        {
            var snapshot = _service.Load();
            ShowSnapshot(snapshot);
            _messageText.Text = string.Empty;
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

        _services = snapshot.Services
            .OrderBy(service => service.DisplayOrder)
            .ThenBy(service => service.Name)
            .ToList();
        _currentPage = 1;
        UpdatePagination();
    }

    private void UpdatePagination()
    {
        var totalItems = _services.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));

        if (_currentPage > totalPages)
        {
            _currentPage = totalPages;
        }

        var pagedServices = _services.Skip((_currentPage - 1) * PageSize).Take(PageSize).ToList();

        ReplaceChildren(
            _serviceRows,
            pagedServices.Select(CreateServiceRow),
            "No services registered in the local database.");

        var startIndex = totalItems == 0 ? 0 : (_currentPage - 1) * PageSize + 1;
        var endIndex = Math.Min(_currentPage * PageSize, totalItems);
        _serviceFooterText.Text = $"Showing {startIndex} to {endIndex} of {totalItems} entries";

        _paginationPanel.Visibility = totalPages > 1 ? Visibility.Visible : Visibility.Collapsed;

        if (totalPages > 1)
        {
            RenderPaginationButtons(totalPages);
        }
        else
        {
            _paginationPanel.Children.Clear();
        }
    }

    private void RenderPaginationButtons(int totalPages)
    {
        _paginationPanel.Children.Clear();

        var prevButton = CreatePaginationButton("Prev", isCurrent: false);
        prevButton.IsEnabled = _currentPage > 1;
        prevButton.Click += (_, _) =>
        {
            _currentPage--;
            UpdatePagination();
        };
        _paginationPanel.Children.Add(prevButton);

        for (var i = 1; i <= totalPages; i++)
        {
            var pageNumber = i;
            var pageButton = CreatePaginationButton(pageNumber.ToString(), pageNumber == _currentPage);
            pageButton.Click += (_, _) =>
            {
                _currentPage = pageNumber;
                UpdatePagination();
            };
            _paginationPanel.Children.Add(pageButton);
        }

        var nextButton = CreatePaginationButton("Next", isCurrent: false);
        nextButton.IsEnabled = _currentPage < totalPages;
        nextButton.Click += (_, _) =>
        {
            _currentPage++;
            UpdatePagination();
        };
        _paginationPanel.Children.Add(nextButton);
    }

    private UIElement CreateServiceRow(Service service)
    {
        var row = new Grid
        {
            Padding = new Thickness(24, 18, 24, 18),
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(197, 197, 216),
            BorderThickness = new Thickness(0, 0, 0, 1),
            ColumnSpacing = 16
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        row.Children.Add(new TextBlock
        {
            Text = service.Name,
            FontFamily = new FontFamily("Inter"),
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush(26, 28, 30),
            TextWrapping = TextWrapping.WrapWholeWords,
            VerticalAlignment = VerticalAlignment.Center
        });

        var priceText = new TextBlock
        {
            Text = $"${service.Price:0.00}",
            FontFamily = new FontFamily("Inter"),
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush(26, 28, 30),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(priceText, 1);
        row.Children.Add(priceText);

        var statusBadge = CreateTextBadge(
            service.IsActive ? "Active" : "Archived",
            service.IsActive ? Brush(220, 252, 231) : Brush(232, 232, 234),
            service.IsActive ? Brush(21, 128, 61) : Brush(68, 70, 85));
        statusBadge.HorizontalAlignment = HorizontalAlignment.Center;
        statusBadge.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(statusBadge, 2);
        row.Children.Add(statusBadge);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var editButton = CreateIconActionButton("\uE70F", "Edit");
        editButton.Click += (_, _) => EditService(service);
        actions.Children.Add(editButton);

        var deleteButton = CreateIconActionButton("\uE74D", "Delete");
        deleteButton.Click += (_, _) =>
        {
            _editingServiceId = service.Id;
            OnDeleteServiceClick(deleteButton, new RoutedEventArgs());
        };
        actions.Children.Add(deleteButton);

        Grid.SetColumn(actions, 3);
        row.Children.Add(actions);

        return row;
    }

    private void OnNewServiceClick(object sender, RoutedEventArgs args)
    {
        _editingServiceId = null;
        _resetDisplayOrderToDefault = true;
        ClearServiceEditor();
        _serviceModalTitle.Text = "Add New Service";
        _serviceModalOverlay.Visibility = Visibility.Visible;
        _messageText.Text = string.Empty;
    }

    private void OnCancelServiceClick(object sender, RoutedEventArgs args)
    {
        _serviceModalOverlay.Visibility = Visibility.Collapsed;
        _editingServiceId = null;
        _resetDisplayOrderToDefault = true;
        ClearServiceEditor();
    }

    private void OnSaveServiceClick(object sender, RoutedEventArgs args)
    {
        var isEditing = _editingServiceId is not null;
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
                _resetDisplayOrderToDefault = true;
                _serviceModalOverlay.Visibility = Visibility.Collapsed;
                ClearServiceEditor();
            },
            isEditing ? "Service updated." : "Service saved.");
    }

    private async void OnDeleteServiceClick(object sender, RoutedEventArgs args)
    {
        var dialog = new ContentDialog
        {
            Title = "Confirm Deletion",
            Content = "Are you sure you want to delete this service?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        ExecuteAdminAction(
            () =>
            {
                if (_editingServiceId is not Guid serviceId)
                {
                    throw new InvalidOperationException("Select a service before deleting.");
                }

                _service.DeleteService(serviceId);
                _editingServiceId = null;
                _resetDisplayOrderToDefault = true;
                _serviceModalOverlay.Visibility = Visibility.Collapsed;
                ClearServiceEditor();
            },
            "Service deleted.");
    }

    private void ExecuteAdminAction(Action action, string successMessage)
    {
        try
        {
            action();
            LoadAdmin();
            _messageText.Text = successMessage;
        }
        catch (Exception exception)
        {
            _messageText.Text = exception.Message;
        }
    }

    private void ShowError(string message)
    {
        _messageText.Text = message;
        _services = [];
        _serviceRows.Children.Clear();
        _deleteServiceButton.IsEnabled = false;
        _serviceRows.Children.Add(CreateEmptyState("Could not read services from the local database."));
        _serviceFooterText.Text = "Showing 0 to 0 of 0 entries";
        _paginationPanel.Visibility = Visibility.Collapsed;
        _paginationPanel.Children.Clear();
    }

    private void SyncServiceEditor(LocalAdminSnapshot snapshot)
    {
        if (_editingServiceId is Guid serviceId)
        {
            var service = snapshot.Services.FirstOrDefault(candidate => candidate.Id == serviceId);
            if (service is not null)
            {
                LoadServiceIntoEditor(service);
                SetEditorModeText(service.Name);
                return;
            }

            _editingServiceId = null;
        }

        if (_resetDisplayOrderToDefault || string.IsNullOrWhiteSpace(_serviceDisplayOrderInput.Text))
        {
            _serviceDisplayOrderInput.Text = _nextServiceDisplayOrder.ToString();
            _resetDisplayOrderToDefault = false;
        }

        SetEditorModeText(null);
        _deleteServiceButton.IsEnabled = false;
    }

    private void EditService(Service service)
    {
        _editingServiceId = service.Id;
        _resetDisplayOrderToDefault = false;
        LoadServiceIntoEditor(service);
        SetEditorModeText(service.Name);
        _serviceModalTitle.Text = "Edit Service";
        _serviceModalOverlay.Visibility = Visibility.Visible;
        _messageText.Text = string.Empty;
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
        SetEditorModeText(null);
    }

    private void SetEditorModeText(string? serviceName)
    {
        _editorModeText.Text = serviceName is null
            ? "Creating a new service"
            : $"Editing: {serviceName}";
    }

    private decimal ParseServicePrice()
    {
        var text = _servicePriceInput.Text.Trim().Replace("$", string.Empty).Trim();
        if ((!decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var price)
                && !decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out price))
            || price <= 0)
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

    private static Button CreateIconActionButton(string glyph, string tooltip)
    {
        var button = new Button
        {
            MinHeight = 36,
            MinWidth = 36,
            Padding = new Thickness(6),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderBrush = new SolidColorBrush(Colors.Transparent),
            Foreground = Brush(68, 70, 85),
            Content = new FontIcon
            {
                Glyph = glyph,
                FontSize = 18
            }
        };

        ToolTipService.SetToolTip(button, tooltip);
        return button;
    }

    private static Button CreatePaginationButton(string text, bool isCurrent)
    {
        var button = new Button
        {
            MinHeight = 32,
            MinWidth = text.Length == 1 ? 34 : 64,
            Padding = new Thickness(12, 4, 12, 4),
            Content = text,
            CornerRadius = new CornerRadius(4)
        };

        if (isCurrent)
        {
            button.Background = Brush(0, 19, 135);
            button.BorderBrush = Brush(0, 19, 135);
            button.Foreground = Brush(255, 255, 255);
        }
        else
        {
            button.Background = Brush(255, 255, 255);
            button.BorderBrush = Brush(197, 197, 216);
            button.Foreground = Brush(68, 70, 85);
        }

        return button;
    }

    private static Border CreateTextBadge(string text, SolidColorBrush background, SolidColorBrush foreground)
    {
        return new Border
        {
            Background = background,
            Padding = new Thickness(12, 4, 12, 4),
            CornerRadius = new CornerRadius(999),
            Child = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Inter"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
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
            Background = Brush(255, 255, 255),
            Child = new TextBlock
            {
                Text = message,
                FontFamily = new FontFamily("Inter"),
                FontSize = 14,
                Foreground = Brush(68, 70, 85),
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }
}
