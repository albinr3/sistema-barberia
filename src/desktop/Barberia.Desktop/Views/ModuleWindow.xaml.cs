using Barberia.Desktop.Shell;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace Barberia.Desktop.Views;

public sealed partial class ModuleWindow : Window
{
    public ModuleWindow(ShellModuleKey moduleKey)
    {
        InitializeComponent();

        var module = ShellModuleCatalog.Modules.First(definition => definition.Key == moduleKey);
        AppWindow.Title = module.Title;
        _contentHost.Content = ShellPageFactory.Create(module, () => { });

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }
    }
}
