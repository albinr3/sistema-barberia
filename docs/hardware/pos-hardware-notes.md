# Notas De Hardware POS

## Proposito

Registrar informacion futura sobre hardware POS, impresoras, lectores y cash drawer.

## Pendiente

- TODO: confirmar modelos de hardware.
- TODO: confirmar protocolos de comunicacion.
- TODO: confirmar manejo de errores.

## Fase 1

- La app debe consumir impresora, escaner QR y cash drawer mediante interfaces en `Barberia.Hardware`.
- El kiosko imprime tickets reales mediante `WindowsGraphicsKioskTicketPrinter`, que dibuja una pagina Windows/GDI en la impresora predeterminada.
- El ticket de kiosko imprime el numero visible, el codigo QR real basado en el `ticket_number` interno y el codigo interno como texto para entrada manual.
- No se envian comandos ESC/POS desde el ticket de kiosko; el modelo/driver del piloto puede imprimir esos bytes como letras/numeros o quedarse procesando durante varios minutos.
- El equipo de kiosko debe tener una impresora predeterminada configurada en Windows; si no existe o el driver rechaza trabajos graficos, el boton de imprimir muestra error y no simula exito.
- Los simuladores actuales siguen disponibles para pruebas automatizadas y para cubrir exito/fallos principales sin requerir dispositivos reales.
- La autocaja usa constancia impresa y cash drawer simulados; el escaner QR queda como contrato disponible para integrar cuando se conecte el flujo de lectura fisica.
- El escenario fisico mas probable para cash drawer es conexion a la impresora POS mediante RJ11/RJ12, con apertura por pulso enviado desde la impresora usando ESC/POS u otro protocolo equivalente.
- Mantener `ICashDrawer` aunque la apertura real dependa de la impresora; una implementacion futura puede ser `PrinterDrivenCashDrawer` sin cambiar la logica de autocaja.
