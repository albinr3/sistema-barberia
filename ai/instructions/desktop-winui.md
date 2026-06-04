# Instrucciones Desktop WinUI

## Proposito

Plantilla para trabajo futuro en la aplicacion Windows local.

## Alcance Futuro

- C#/.NET/WinUI 3.
- SQLite local.
- Integracion con hardware POS.

## Reglas

- No crear proyectos WinUI hasta que se apruebe el inicio de Fase 1.
- No asumir patrones de arquitectura no confirmados.
- Toda pantalla nueva en `Barberia.Desktop` debe respetar el tema visual aprobado en `docs/diseno/desktop-visual-theme.md`.
- Toda nueva `Window` o `Page` concreta en `Barberia.Desktop` debe crearse como par `.xaml` + `.xaml.cs`, con clase `partial`, llamada a `InitializeComponent()` y layout declarado en XAML.
- No crear pantallas WinUI nuevas con `Content = Build...` ni con arboles visuales principales construidos en C#; el C# queda para code-behind, servicios, eventos y elementos dinamicos dependientes de datos runtime.
- Ejecutar `dotnet test tests/desktop/Barberia.Desktop.Tests/Barberia.Desktop.Tests.csproj --no-restore -v:minimal` cuando se agreguen o renombren ventanas/paginas.
- Reutilizar la shell, navegacion lateral, paleta, espaciado, tarjetas, badges e iconografia existentes antes de crear un estilo nuevo.
- Mantener la UI operacional: moderna, limpia, legible y util para trabajo diario, sin composiciones tipo landing page.
- No duplicar reglas de negocio ni estados de dominio para fines visuales.
- Si un modulo requiere un nuevo patron visual reutilizable, documentarlo o dejarlo encapsulado en `Barberia.Desktop`.

## Tema Visual Actual

- Sidebar oscuro calido.
- Fondo principal gris claro.
- Superficies blancas con borde sutil y radio maximo de 8.
- Acento dorado para marca/seleccion.
- Acento verde sobrio para estado operativo.
- Iconos Segoe Fluent mediante `FontIcon` mientras no exista una libreria de iconos aprobada.
- Header por modulo con titulo y contexto corto.
- Tarjetas solo para bloques funcionales, resumenes o elementos repetidos.

## Referencias Obligatorias Para UI

- `docs/diseno/desktop-visual-theme.md`
- `src/desktop/Barberia.Desktop/App.xaml`
- `src/desktop/Barberia.Desktop/Styles/DesktopTheme.xaml`
- `src/desktop/Barberia.Desktop/MainWindow.xaml`
- `src/desktop/Barberia.Desktop/Views/*.xaml`
