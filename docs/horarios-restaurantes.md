# Horarios habituales

El contrato existente es una lista JSON de entradas con `dia`, `desde`, `hasta` y `cerrado`. Las entradas repetidas de un día representan turnos partidos. `cerrado: true` indica un cierre explícito. `todoElDia: true` o un intervalo de `00:00` a `24:00` indican 24 horas. Dos horas iguales son ambiguas.

El modelo actual carece de una zona horaria por restaurante. Para el alcance local de Argentina, la evaluación usa `America/Argentina/Buenos_Aires` en backend y frontend, independientemente de la zona del servidor o navegador. Agregar restaurantes en otras zonas requerirá registrar su zona explícitamente.

La apertura es inclusiva y el cierre exclusivo. Un turno nocturno pertenece al día de apertura y puede seguir activo al día siguiente. Para afirmar un cierre se necesitan datos interpretables del día actual y del anterior. Una ausencia, un formato inválido o una contradicción conserva el estado desconocido. El origen del restaurante no interviene.

Se evalúa la semana habitual informada. El contrato carece de excepciones de feriados y cierres extraordinarios. La interfaz aclara que el estado corresponde a los horarios informados.

El filtro opcional `abiertoAhora=true` incluye únicamente estados abiertos y se aplica antes de la recomendación y de su límite de resultados. Las tarjetas comparten un reloj local de 30 segundos. El mapa con el filtro activo actualiza la búsqueda una vez por minuto, conservando la zona buscada.

Los mismos casos de regresión se conservan en `test/GustosApp.Domain.Tests/Datos/horarios.json` y en `gusto/__tests__/fixtures/horarios.json`, para ejecutar cada repositorio de manera independiente.
