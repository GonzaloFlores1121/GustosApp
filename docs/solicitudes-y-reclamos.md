# Solicitudes y reclamos de restaurantes

La cuenta siempre representa a una persona. Crear o reclamar un restaurante genera una solicitud pendiente sin cambiar su rol ni impedirle usar GustosApp. Al aprobarla, el usuario obtiene el rol `DuenoRestaurante`, que agrega acceso administrativo a su restaurante y conserva el acceso normal a la aplicación.

Solo puede existir una solicitud pendiente por usuario. La regla se valida en Application y mediante un índice único filtrado en SQL Server para cubrir solicitudes simultáneas. `PendienteRestaurante` queda como valor histórico para migrar datos anteriores, no como estado nuevo de una cuenta.

El alta nueva continúa mediante `POST /api/Restaurantes` con el formulario existente.
El reclamo usa `POST /api/Restaurantes/{id}/reclamo`, autenticado y con multipart de datos y comprobante privado. Ver [comprobantes de reclamos](comprobantes-reclamos.md).
Devuelve el identificador de la solicitud pendiente. Repetirlo para el mismo
usuario y restaurante devuelve la misma solicitud.
Los reclamos aparecen en el listado administrativo existente; no envían el correo
de alta nueva. La evidencia de propiedad se revisa por el administrador antes de aprobar.

## Aprobación

`POST /Admin/solicitudes/aprobar/{id}` conserva ambos comportamientos:

- `RestauranteExistenteId` nulo: crea una ficha nueva.
- `RestauranteExistenteId` informado: asigna el propietario a esa ficha, conservando
  su Id, PlaceId, menú, gustos, restricciones, imágenes, opiniones e historial.
  El propietario puede editar sus datos posteriormente desde su panel.

Los DTOs administrativos exponen `RestauranteExistenteId`, `RestauranteAprobadoId`,
`RolFirebaseSincronizado` y `CorreoAprobacionEnviado`.
Una ficha con dueño (incluido el campo legado PropietarioUid) no puede reclamarse.
Se mantiene la regla actual de un alta/reclamo pendiente por usuario y un restaurante
afiliado por propietario. Diferentes usuarios pueden presentar solicitudes sobre
la misma ficha: sólo uno podrá obtener su propiedad.

La ficha, el estado y el rol SQL se guardan juntos. Los tokens de concurrencia de
Estado, DuenoId, PropietarioUid y Rol detectan lecturas obsoletas. SQL Server guarda
los cambios de cada SaveChanges en una transacción; un conflicto cancela esa escritura.
Los servicios externos se ejecutan después de confirmar la aprobación. Repetir una
aprobación retoma Firebase/correo pendientes sin crear otra ficha ni procesar otra vez
el menú. Un error externo puede devolver error HTTP aunque SQL ya esté aprobado:
consultar los indicadores administrativos y reintentar el mismo identificador.

La entrega de correo no es exactamente una vez: si se envía pero falla el guardado
del indicador, un reintento puede reenviarlo. No hay worker de reintentos automáticos.
La recuperación de efectos externos del rechazo queda fuera de este bloque.

## Menú y valoraciones

El alta nueva conserva el OCR existente después de confirmar la aprobación.
El administrador puede ejecutar `POST /Admin/solicitudes/{id}/reprocesar-menu`
para volver a procesar la imagen de una solicitud aprobada y vinculada. No cambia
la aprobación ni reenvía el correo. Si OCR falla, se conserva MenuProcesado=false
y MenuError; consultar el restaurante después de ejecutar el endpoint.
No hay procesamiento en segundo plano en este bloque.

Las altas nuevas tienen Rating nulo. Un mínimo de estrellas cero permite candidatos
sin opiniones; un mínimo positivo exige una calificación que lo alcance. El resto
de las reglas de gustos, ubicación y compatibilidad sigue aplicándose.
No se borran calificaciones anteriores porque no puede determinarse cuáles eran artificiales.
La interfaz debe representar la ausencia de valoración explícitamente; los contratos
legados que convierten nulo a cero se mantienen para una adaptación frontend posterior.

## Migración y puesta en marcha

Aplicar `PermitirReclamosRestaurantes` a la base elegida antes de usar estos endpoints:

```powershell
dotnet ef database update --project src/GustosApp.Infraestructure --startup-project src/GustosApp.API --context GustosDbContext
```

Verificar antes el entorno y la conexión seleccionados. La implementación no ejecuta
esta actualización automáticamente. Las solicitudes históricas aprobadas quedan sin
RestauranteAprobadoId: reaprobarlas devuelve conflicto y nunca genera un duplicado.
No se vinculan por nombre ni se deduce un propietario por similitud.
Las referencias nuevas usan claves foráneas restrictivas para conservar la relación
de la solicitud con su ficha; no debe eliminarse físicamente una ficha referenciada
sin resolver previamente esas referencias.

Pruebas: casos de uso con xUnit/Moq; API y concurrencia con EF InMemory. Estas últimas
comprueban contratos, persistencia y tokens, pero no sustituyen una validación de
migración y rollback transaccional en una instancia SQL Server de prueba.

`GET /api/Restaurantes/mi-solicitud` devuelve la última solicitud del usuario
autenticado, su estado, el motivo de rechazo y el restaurante aprobado. El frontend
usa esta consulta para impedir cargas duplicadas y ofrecer el acceso al dashboard.
