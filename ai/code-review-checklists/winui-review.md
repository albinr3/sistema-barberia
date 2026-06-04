# Checklist WinUI

## Revisar

- El cambio pertenece al alcance desktop aprobado.
- La interfaz respeta los flujos confirmados.
- No hay dependencias innecesarias.
- Toda nueva `Window` o `Page` concreta tiene `.xaml` + `.xaml.cs`, clase `partial` e `InitializeComponent()`.
- El layout principal usa XAML y recursos de `Styles/DesktopTheme.xaml`; C# queda para code-behind, eventos y elementos dinamicos por datos runtime.
- Se mantiene separacion entre UI, servicios, datos, hardware y reglas de negocio.
- Pasa `dotnet test tests/desktop/Barberia.Desktop.Tests/Barberia.Desktop.Tests.csproj --no-restore -v:minimal` cuando se cambia la estructura de vistas.
