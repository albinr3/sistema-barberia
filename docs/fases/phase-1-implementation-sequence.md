# Secuencia De Implementacion Futura De Fase 1

## Proposito

Ordenar el trabajo futuro de Fase 1 sin implementar todavia funcionalidades. Esta secuencia debe guiar issues, agentes y revisiones cuando se apruebe iniciar la implementacion.

## Secuencia Recomendada

1. Crear solucion .NET y proyectos vacios segun `docs/arquitectura/phase-1-solution-structure.md`.
2. Definir modelos de dominio iniciales en `Barberia.Core`.
3. Escribir pruebas del motor de asignacion antes de implementar el motor.
4. Implementar motor de asignacion de turnos en `Barberia.Core`.
5. Crear capa SQLite local en `Barberia.Data`.
6. Crear shell WinUI 3 en `Barberia.Desktop`.
7. Implementar modulo de kiosco local.
8. Implementar pantalla publica.
9. Implementar panel de barbero.
10. Implementar autocaja local.
11. Implementar abstracciones de hardware POS.
12. Implementar foundation de sincronizacion no bloqueante.
13. Implementar reportes administrativos locales.
14. Preparar packaging y actualizaciones.

## Reglas De Orden

- Las pruebas del motor de turnos deben escribirse antes de implementar el motor.
- La UI no debe decidir reglas de asignacion.
- La persistencia no debe definir reglas de negocio.
- Hardware debe entrar detras de interfaces y simuladores.
- Sync debe llegar como cola no bloqueante, no como dependencia de operacion.
- Booking web y app movil quedan fuera de esta secuencia.

## Criterios De Avance

Cada paso debe cerrarse con:

- Archivos dentro de las carpetas permitidas por su tarea.
- Pruebas correspondientes cuando aplique.
- Documentacion actualizada si aparece una decision tecnica nueva.
- Confirmacion de que no se implementaron funcionalidades de Fase 2 ni Fase 3.

