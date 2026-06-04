# Notas De Hardware POS

## Proposito

Registrar informacion futura sobre hardware POS, impresoras, lectores y cash drawer.

## Pendiente

- TODO: confirmar modelos de hardware.
- TODO: confirmar protocolos de comunicacion.
- TODO: confirmar manejo de errores.

## Fase 1

- La app debe consumir impresora, escaner QR y cash drawer mediante interfaces en `Barberia.Hardware`.
- Los adaptadores reales quedan pendientes hasta confirmar modelos fisicos y protocolos.
- Los simuladores actuales cubren exito y fallos principales sin requerir dispositivos reales.
- La autocaja usa constancia impresa y cash drawer simulados; el escaner QR queda como contrato disponible para integrar cuando se conecte el flujo de lectura fisica.
- El escenario fisico mas probable para cash drawer es conexion a la impresora POS mediante RJ11/RJ12, con apertura por pulso enviado desde la impresora usando ESC/POS u otro protocolo equivalente.
- Mantener `ICashDrawer` aunque la apertura real dependa de la impresora; una implementacion futura puede ser `PrinterDrivenCashDrawer` sin cambiar la logica de autocaja.
