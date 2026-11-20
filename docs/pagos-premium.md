# Pagos Premium

Premium es una compra única, sin vencimiento ni renovación. Los beneficios vigentes son únicamente grupos y restaurantes favoritos sin límite.

## Configuración requerida

Los cobros permanecen deshabilitados mientras `MercadoPago:Premium:HabilitarCobros` sea `false` o falte un precio positivo. El precio definitivo sigue pendiente y no se incluye un valor comercial predeterminado.

Antes de habilitar cobros se deben configurar por secretos o variables de entorno:

- `MercadoPago__Premium__Precio` y `MercadoPago__Premium__Moneda`;
- `MercadoPago__AccessToken`, `MercadoPago__WebhookSecret` y `MercadoPago__CollectorId`;
- las URL HTTPS de webhook, éxito, fallo y pendiente;
- `MercadoPago__Produccion`, coherente con las credenciales;
- finalmente, `MercadoPago__Premium__HabilitarCobros=true`.

La integración usa la API de Orders y una clave de idempotencia persistida. Una creación remota con resultado incierto queda en revisión y se reutiliza; no se crea otra orden a ciegas.

## Migración y operación

La migración `ComprasPremiumAuditables` conserva los usuarios Plus existentes mediante `AccesoPlusLegado`. Esto preserva el acceso sin fabricar pagos aprobados y permite su revisión posterior.

Los pagos distintos aprobados para una misma cuenta se conservan y se marcan como posible cobro duplicado. No se ejecutan devoluciones automáticas: la resolución administrativa continúa pendiente de definición.

Un reembolso total o contracargo invalida solamente ese pago. Si no queda otro pago aprobado ni acceso heredado, el usuario vuelve a Free conservando grupos y favoritos existentes. Los reembolsos parciales quedan para revisión y no retiran el beneficio automáticamente.
