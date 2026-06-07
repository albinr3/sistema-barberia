# Resumen de Arquitectura (Fase 1)

## Objetivo Tecnico
Aplicación Windows local (WinUI 3) con persistencia en SQLite, diseñada bajo un enfoque offline-first. La operación diaria (kiosco, turnos, autocaja, administración) funciona completamente sin conexión a internet.

## Estructura de Proyectos

* **Barberia.Desktop**: UI, navegación y wiring en WinUI 3. Administra las pantallas (Kiosco, Panel de Barbero, Autocaja, Admin, Pantalla Pública).
* **Barberia.Core**: Dominio puro y lógica de negocio (Motor de asignación, estados, entidades). No tiene dependencias de UI, DB ni hardware.
* **Barberia.Data**: Capa de acceso a datos usando SQLite y EF Core.
* **Barberia.Sync**: Cola de eventos locales para futura sincronización cloud no bloqueante.
* **Barberia.Hardware**: Abstracciones e interfaces para el hardware POS (Impresora, Escáner QR, Cash Drawer).
* **Barberia.ApiClient**: Cliente para la comunicación cloud futura (Supabase).
* **Barberia.Shared**: Contratos compartidos entre proyectos (solo lo estrictamente necesario).

## Flujo de Datos y Dependencias

* `Barberia.Desktop` depende de `Core`, `Data`, `Sync`, `Hardware` y `ApiClient`.
* `Barberia.Data`, `Barberia.Sync` y `Barberia.Hardware` dependen de `Core`.
* `Barberia.Core` **no depende** de infraestructura.

La base de datos local SQLite es la autoridad para la operación en vivo de la Fase 1. La integración cloud posterior usará una cola de eventos (`Barberia.Sync`) para replicación eventual y no bloqueante.
