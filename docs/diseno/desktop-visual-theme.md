# Tema Visual Desktop WinUI

## Proposito

Definir el tema visual base que debe mantenerse en la aplicacion Windows local de Fase 1.

Este documento aplica a toda pantalla nueva en `Barberia.Desktop`: kiosco, pantalla publica, panel de barbero, autocaja, administracion local, reportes y cualquier vista auxiliar futura.

## Fuente Actual

El tema aprobado parte de la shell y recursos XAML actuales:

- `src/desktop/Barberia.Desktop/App.xaml`
- `src/desktop/Barberia.Desktop/Styles/DesktopTheme.xaml`
- `src/desktop/Barberia.Desktop/MainWindow.xaml`
- `src/desktop/Barberia.Desktop/Shell/`
- `src/desktop/Barberia.Desktop/Views/*.xaml`

Toda nueva pantalla real debe declararse en XAML y reutilizar los recursos compartidos antes de introducir estilos locales.

## Direccion Visual

- Moderno, limpio y operacional.
- Interfaz sobria para uso diario en una barberia, no una landing page.
- Alta legibilidad a distancia razonable y en pantallas touch.
- Navegacion clara, con iconos y labels consistentes.
- Secciones ordenadas por flujo operativo, no por decoracion.

## Paleta Base

- Fondo principal: gris claro casi blanco.
- Sidebar: oscuro calido.
- Superficies: blanco.
- Acento primario: dorado calido.
- Acento operativo: verde sobrio.
- Texto principal: casi negro.
- Texto secundario: gris medio.
- Bordes: gris claro.

No convertir la app en una paleta de un solo color. Mantener contraste suficiente y evitar gradientes decorativos innecesarios.

## Layout

- Mantener la navegacion lateral global de la shell en modulos administrativos y operativos generales.
- Kiosco, pantalla publica y panel de barbero pueden ocultar navegacion lateral y header superior cuando funcionen como superficies dedicadas de atencion.
- Kiosco debe ocupar todo el viewport disponible cuando la shell oculta el chrome global, con padding adaptable y grilla responsive de barberos para tablets, pantallas pequenas y monitores grandes; en monitor 1920x1080 debe mostrar el check-in completo con hasta 12 barberos sin scroll vertical.
- Mantener header superior con titulo de modulo y contexto operativo en modulos que usan la shell completa.
- Usar tarjetas solo para bloques funcionales, elementos repetidos o resumenes concretos.
- No anidar tarjetas dentro de tarjetas.
- Usar espaciado amplio, pero no convertir pantallas operativas en paginas de marketing.
- Preparar controles touch con areas comodas para kiosco, autocaja y panel de barbero.

## Componentes

- Botones de navegacion: icono + titulo + subtitulo corto.
- Acciones principales: botones claros, visibles y con jerarquia.
- Estados: badges o indicadores compactos, no textos largos flotantes.
- Formularios: labels claros, inputs grandes cuando sean touch, validacion visible.
- Reportes: tablas o listas densas pero legibles, con resumenes superiores.
- Pantalla publica: tipografia mas grande y composicion optimizada para lectura a distancia.

## Reglas Para Agentes

- Antes de crear o modificar una vista WinUI, revisar este documento y `ai/instructions/desktop-winui.md`.
- Reutilizar `Styles/DesktopTheme.xaml`, `MainWindow.xaml`, `Shell` y las vistas XAML existentes antes de inventar un estilo nuevo.
- Crear toda nueva `Window` o `Page` concreta como `.xaml` + `.xaml.cs`; el layout principal no debe construirse programaticamente en C#.
- Si un modulo necesita un patron nuevo, debe verse compatible con la shell y documentarse si se vuelve reutilizable.
- No implementar reglas de negocio solo para mostrar datos visuales.
- No duplicar estados de dominio como strings locales para pintar la UI.
- Si una decision visual cambia el tema global, registrar la decision en `docs/decisiones/decision-log.md`.

