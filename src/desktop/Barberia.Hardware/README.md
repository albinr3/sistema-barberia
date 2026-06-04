# Barberia.Hardware

Libreria de abstraccion de hardware POS futuro.

Responsabilidades:

- Impresora de tickets o constancias.
- Escaner QR.
- Cash drawer.
- Interfaces y adaptadores simulados.

Restricciones actuales:

- Depende de `Barberia.Core`.
- No implementa hardware real.
- No habla con drivers ni protocolos de dispositivos.
- Los simuladores permiten validar exito y fallos sin dispositivos fisicos.
