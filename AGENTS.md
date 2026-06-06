## Tareas ambiguas, riesgosas o dependientes

No ejecutes a ciegas si una instrucción que yo te de parece incorrecta, incompleta, contradictoria o inviable.

Detente y avisa si detectas que la tarea:
- No tiene sentido con el estado actual del proyecto.
- Requiere crear algo que aún no existe (funcionalidad, estructura, dependencia, etc.).
- Podría romper comportamiento existente o desincronizar el sistema.
- Podría causar problemas futuros (acoplamiento, duplicación, migraciones parciales, efectos secundarios).

El aviso debe ser breve: problema + riesgo + pregunta concreta o dos opciones.

No hagas cambios grandes, destructivos o estructurales sin confirmación explícita.

Si el problema es menor y resoluble sin cambiar el alcance, continúa dejando constancia breve del ajuste.

## Documentación de cambios

Todo cambio mediano o grande debe documentarse en los archivos pertinentes del proyecto: documentación técnica, instrucciones de uso, notas de arquitectura, decisiones, flujos, dependencias, configuración o comportamiento.

No dejes documentación obsoleta. Si un cambio altera funcionamiento, configuración, uso o mantenimiento, actualiza la documentación como parte del mismo trabajo.

Si no existe dónde registrar el cambio, avisa y pregunta si crear nueva documentación.
