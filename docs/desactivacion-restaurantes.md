# Desactivación reversible y archivo de solicitudes

Solo un administrador vigente puede desactivar o reactivar un restaurante. Debe indicar un motivo de hasta 1000 caracteres. Cada cambio efectivo guarda actor Firebase, fecha UTC, acción y motivo en `RegistrosAdministracionRestaurantes`. La versión de disponibilidad evita que una pestaña anterior revierta otro cambio; repetir el estado actual no agrega registros. No se transfieren permisos ni se borra la ficha.

El panel incluye búsqueda por nombre, dirección, PlaceId o ID y páginas de hasta 100 restaurantes. Permite consultar activos, inactivos o todos, incluidos los importados y los creados sin una solicitud asociada. Desde una solicitud aprobada se utiliza `RestauranteAprobadoId`, nunca el identificador de la solicitud como identificador de restaurante.

Los inactivos no participan de búsquedas públicas, recomendaciones individuales/con amigo/de grupo, ni nuevas candidaturas. El filtro se aplica antes de límites y ranking. No existe un filtro global de EF: detalle, propietario, favoritos, visitados, votos, candidatos y ganadores históricos mantienen sus relaciones y muestran su disponibilidad actual. Las búsquedas de identidad para importación y detección de duplicados también ven los inactivos. PlaceId mantiene su unicidad; importar, actualizar desde Google o reclamar una ficha no la reactiva.

La ruta histórica `DELETE /api/Restaurantes/{id}` responde 410 y el servicio de eliminación física rechaza la operación. Los propietarios conservan sus permisos de edición; la disponibilidad se cambia exclusivamente mediante la acción administrativa.

## Votaciones y concurrencia

Se conserva el total original de votos. No se recalcula un ganador alternativo al desactivar un restaurante: si la baja afecta al resultado, se informa que el administrador del grupo puede cancelar la votación e iniciar otra. El cierre automático, el cierre explícito y la ruleta no pueden confirmar un inactivo. Una elección confirmada antes de la baja conserva su ganador histórico.

Al persistir candidaturas, votos o ganadores se consulta nuevamente la disponibilidad desde la base, sin usar la entidad seguida anteriormente. La comprobación y el guardado comparten una transacción serializable con la desactivación. Si un voto ya está confirmándose, la baja espera: ese voto queda registrado antes de la baja, y los siguientes son rechazados. Los conflictos de concurrencia se responden como 409 para recargar y reintentar.

No se cachean listas de disponibilidad. La explicación de Gemini comprueba el estado antes de generar/usar caché y antes de entregar la respuesta. El mapa revalida los marcadores fijados cuando vuelve a consultar. Una pantalla ya abierta puede mostrar el estado anterior hasta que se actualiza; toda mutación vuelve a validar en el servidor.

## Archivo independiente

`PUT /Admin/solicitudes/{id}/archivo` solo admite rechazadas. Archivar o restaurar cambia únicamente la organización del panel, con versión, fecha y registro de actor/acción. No cambia el rechazo, su motivo, propietario ni roles. El listado normal excluye las archivadas; `archivadas=true` las permite consultar y restaurar. El detalle y la consulta de la solicitud del usuario se conservan.

## Migración y verificación

La migración `20261120040257_DesactivacionReversibleYArchivoSolicitudes` agrega columnas y la tabla de registros. Las filas existentes quedan activas y las solicitudes no archivadas. No elimina ni recrea restaurantes.

Debe aplicarse antes de ejecutar esta versión sobre una base existente. Desde el repositorio backend:

```powershell
dotnet ef database update --project src/GustosApp.Infraestructure --startup-project src/GustosApp.API
```

La implementación no aplica la migración a la base habitual. Las pruebas SQL usan bases con nombre aleatorio `GustosDisponibilidadPruebas_*` que se eliminan al terminar. La prueba de migración reconstruye el esquema previo quitando solo las adiciones de esta migración y ejecuta sus operaciones Up reales sobre filas existentes. No sustituye una prueba de toda la cadena histórica de migraciones.

```powershell
$env:GUSTOS_EF_SQL_DISPONIBILIDAD = '1'
dotnet test test/GustosApp.API.Tests --configuration Release --filter FullyQualifiedName~DisponibilidadSqlPruebas
```

Incluye regresiones de permisos, reintentos, versiones anteriores, conservación de relaciones, límites/paginación, importación/Google, archivo/restauración, votos y ganadores concurrentes e historial. La interfaz cubre mensajes dentro del modal, motivo obligatorio, bloqueo de envíos repetidos, búsqueda/paginación, disponibilidad e historial. Se verificó además el recorrido administrativo en navegador headless con API y datos aislados, en escritorio y móvil.
