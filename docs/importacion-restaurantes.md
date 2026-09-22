# Importación de restaurantes

El archivo histórico `InsertAndUpdateRestaurantDataset.sql` contiene 57 restaurantes,
57 bloques de gustos, 39 asignaciones de restricciones y 228 imágenes. Sus sentencias
son `INSERT` directos: no es seguro ejecutarlo más de una vez sobre la misma base.

Los restaurantes sí incluyen `PlaceId`, por lo que ese valor se usa como identidad
estable para evitar duplicados. Los gustos y las restricciones del archivo fueron
estimados; no deben presentarse como información confirmada por el restaurante.

## Flujo seguro

El endpoint administrativo es `POST /Admin/restaurantes/importar`. Por omisión se
recomienda enviar `confirmar: false`: devuelve qué fichas crearía, actualizaría u
omitiría sin escribir en la base. Después de revisar el resultado, se repite el mismo
lote con `confirmar: true`.

```json
{
  "confirmar": false,
  "restaurantes": [
    {
      "placeId": "ChIJ...",
      "nombre": "Restaurante de ejemplo",
      "direccion": "Dirección 123",
      "latitud": -34.60,
      "longitud": -58.50,
      "horariosJson": "{}",
      "rating": 4.4,
      "cantidadResenas": 120,
      "categoria": "restaurant",
      "fechaDatosUtc": "2026-09-21T12:00:00Z",
      "primaryType": "restaurant",
      "typesJson": "[\"restaurant\"]"
    }
  ]
}
```

La importación:

- exige `PlaceId`, nombre, dirección y coordenadas válidas;
- rechaza lotes de más de 500 fichas;
- detecta `PlaceId` repetidos dentro del mismo lote;
- no reemplaza datos más recientes con datos viejos;
- no sobrescribe automáticamente un restaurante que ya tiene propietario;
- acepta lugares cuyo tipo principal no es gastronómico cuando otra categoría indica
  que ofrecen restaurante, bar, café o comida;
- descarta lugares cuyas categorías sólo indican estación, comercio o entretenimiento;
- conserva gustos, restricciones, imágenes, reseñas y demás relaciones existentes;
- guarda todos los cambios confirmados una sola vez por lote.

Este endpoint importa únicamente datos básicos del catálogo. La asociación de gustos
y restricciones estimados requiere un proceso separado y revisable para no confundir
una inferencia con compatibilidad verificada.

## Descubrir restaurantes cercanos

El endpoint administrativo `POST /Admin/restaurantes/descubrir-cercanos` consulta
Nearby Search de Google Places y siempre devuelve una vista previa comparada con la
base actual. Este endpoint no acepta `confirmar` y nunca escribe en SQL Server.

```json
{
  "latitud": -34.6037,
  "longitud": -58.3816,
  "radioMetros": 1500,
  "cantidadMaxima": 20
}
```

El radio permitido es de 1 a 50.000 metros y Google devuelve como máximo 20 lugares
por búsqueda. La respuesta contiene `restaurantes`, con las fichas completas para una
selección posterior, y `vistaPrevia`, con la comparación frente a SQL Server.

Se solicitan solamente los campos necesarios para identificar y ubicar el
establecimiento; las valoraciones, horarios, fotos, menú, gustos y compatibilidad no
se inventan. Para guardar candidatos se utiliza después el endpoint de importación
con una selección revisada por el administrador.

## Preparar el dataset histórico

Desde PowerShell, en la raíz del backend:

```powershell
.\scripts\catalogo\Convertir-DatasetRestaurantes.ps1 `
  -RutaEntrada "D:\Downloads\InsertAndUpdateRestaurantDataset.sql" `
  -RutaSalida ".\local-data\restaurantes-vista-previa.json"
```

El conversor extrae las 57 fichas básicas y siempre genera el pedido con
`confirmar: false`. No ejecuta el SQL original, no conecta con SQL Server y no importa
los gustos o restricciones estimados. El JSON resultante puede enviarse al endpoint y
revisarse antes de cambiar `confirmar` a `true`.

El filtro considera tanto `primaryType` como `typesJson`. Por ejemplo, una estación
con `coffee_shop` se admite y un salón con `restaurant` también. Una estación que sólo
tenga `gas_station` y `convenience_store` se devuelve como
`DescartarSinOfertaGastronomica` y nunca se guarda, incluso si el lote se confirma.
