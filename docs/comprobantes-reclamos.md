# Comprobantes privados de reclamos

`POST /api/Restaurantes/{id}/reclamo` ahora recibe multipart: NombreSolicitante, RelacionRestaurante, TelefonoContacto, DeclaraAutorizacion y Comprobante. Este cambio deliberado de contrato requiere desplegar backend y frontend juntos. La aprobación continúa siendo manual.

El archivo se limita a 2 MB, con validación de extensión y firma de PDF, PNG o JPEG. No es una comprobación de autenticidad del documento ni un análisis antivirus. Se guarda en SQL, junto a la solicitud, sin subirlo al almacenamiento público de imágenes. Para un volumen mayor convendrá un almacenamiento privado de objetos.

`GET /api/solicitudes-restaurantes/{id}/comprobante` permite descargar únicamente al solicitante o a quien cumpla la política Admin. Responde como adjunto con no-store y nosniff. El detalle administrativo expone el tipo, pero nunca el contenido binario ni una URL pública.

La migración AgregarComprobantePrivadoReclamo agrega columnas opcionales para conservar solicitudes anteriores. El panel identifica reclamos anteriores sin comprobante; no se inventa evidencia ni se cambia su estado. Los reintentos de una solicitud pendiente conservan la primera evidencia guardada.

Las pruebas existentes se actualizaron por el requisito deliberado de multipart con evidencia. Se prueban datos incompletos, archivo vacío, límite, firma incompatible, persistencia, reintento, descarga del solicitante, rechazo a otro usuario y acceso administrativo.
