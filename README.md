# GustosApp

Para aprender a levantar Redis y SQL Server con Docker Compose en desarrollo
local, seguí la [guía del laboratorio](docs/docker-local.md).

GustosApp – Configuración del proyecto
Este proyecto usa Clean Architecture con:
ASP.NET Core 8 (Web API).
Entity Framework Core (Code First) con SQL Server.
Firebase Authentication para autenticación de usuarios.
Swagger para probar endpoints.

 Pasos para levantar el proyecto
1. Requisitos previos
Visual Studio 2022 o superior con soporte para .NET 8.
SQL Server Express (o una instancia de SQL Server local).
Una cuenta en Firebase Console


2. Configuración de la base de datos
Verificá que tengas SQL Server Express corriendo.
Configurá la cadena de conexión en appsettings.Development.json dentro de GustosApp.API:
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=GustosAppDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}

En Visual Studio, abrí la Package Manager Console (PMC):
Menú: Tools → NuGet Package Manager → Package Manager Console.
Ejecutá los siguientes comandos (asegurate de seleccionar como Default Project a GustosApp.Infraestructure):

Update-Database -Project GustosApp.Infraestructure -StartupProject GustosApp.API
 Esto va a crear automáticamente la base de datos GustosAppDb con todas las tablas necesarias.
 

//Ignorar, ya configurado.Solamente hay que crear una carpeta secrets en el proyecto GustosApp.API y meter el firebase-key.json(está en el discord/fijado en #backend) denntro de secrets
3. Configuración de Firebase
En Firebase Console, creá un proyecto (o usá el existente).
Copiá el Project ID (ej: gustosapp-5c3c9).
En Program.cs de GustosApp.API, actualizá la variable:

var firebaseProjectId = "gustosapp-5c3c9"; // tu project ID real


(Opcional) Si usás Firebase Admin SDK para notificaciones o manejo de usuarios:
Descargá la firebase-key.json desde Firebase Console → Service Accounts.
Guardala en la carpeta /secrets dentro del proyecto.
Asegurate de que esté en el .gitignore.

Para habilitar Mercado Pago en producción, configurá `MercadoPago__AccessToken` y `MercadoPago__WebhookSecret` en las variables privadas del servicio. `appsettings.Production.json` conserva estos campos vacíos en el repositorio.

Para habilitar Gemini, configurá `GeminiSettings__ApiKey` en los secretos de la API o usá `GOOGLE_API_KEY`. El modelo se configura con `GeminiSettings__Model`. Si falta la clave o el modelo, la ficha y el análisis de menú usan coincidencias locales. Podés ajustar `GeminiSettings__TimeoutSegundos`, `GeminiSettings__RecomendacionesPorMinuto` y `GeminiSettings__AnalisisPorMinuto`.

ImageSharp requiere una licencia de Six Labors al compilar `GustosApp.Application` y `GustosApp.Infraestructure`. En desarrollo, guardá el archivo `sixlabors.lic` junto a `GustosApp.sln`. Git y Docker excluyen ese archivo. En GitHub Actions, configurá el secreto de repositorio `SIXLABORS_LICENSE_KEY` con el contenido completo del archivo antes de ejecutar CI o desplegar. La imagen de Docker recibe la licencia como secreto de BuildKit durante la compilación. Para construirla localmente con el archivo, usá `docker build --secret id=sixlabors_license,src=sixlabors.lic -t gustosapp:local .`. [Documentación de Six Labors](https://docs.sixlabors.com/articles/imagesharp/)

4. Ejecutar la API

Seleccioná GustosApp.API como Startup Project.
Presioná F5 o Ctrl+F5.
La API se levantará en:
https://localhost:5001/swagger
http://localhost:5000/swagger

5. Probar con Swagger

Primero, registrá o logueá un usuario en Firebase (usando SDK o REST API).
Copiá el idToken que devuelve Firebase.
En Swagger → botón Authorize → pegá:
Bearer <idToken>
Probá los endpoints de /api/usuarios.

📂 Estructura del proyecto
/src
  /GustosApp.Domain
  /GustosApp.Application
  /GustosApp.Infraestructure
  /GustosApp.API
/tests
  /GustosApp.Domain.Tests
  /GustosApp.Application.Tests
  /GustosApp.Infrastructure.Tests
  /GustosApp.API.Tests

## Compatibilidad orientativa en recomendaciones

El perfil y el recomendador utilizan las asociaciones de etiquetas del catálogo. Una opción con una etiqueta crítica conocida no se usa como coincidencia gastronómica adecuada. Si el restaurante también ofrece otras opciones sin conflicto conocido, permanece como candidato con compatibilidad **desconocida**: la ausencia de etiquetas no demuestra que un plato sea seguro ni que se conozcan todos sus ingredientes. Si todas las opciones registradas presentan conflictos conocidos y no existe una declaración sobre esa restricción, se excluye de esa recomendación; el catálogo puede ser parcial y esto no certifica el menú completo. Una declaración desactualizada queda como **desconocida**, no como confirmación.

Una declaración vigente del restaurante sobre una restricción puede producir una evaluación **estimada**, siempre orientativa; las condiciones médicas sin declaración equivalente no se confirman por inferencia. La votación evalúa las preferencias actuales de los participantes fijados al iniciarla y muestra el nivel sin divulgar condiciones individuales. Antes del despliegue, aplicá también la migración `CorregirEtiquetasVegetarianas`, que agrega la etiqueta de mariscos al catálogo vegetariano y vegano existente.

### Pistas de restricciones para propietarios

El dashboard permite pedir sugerencias de declaraciones generales. Se basan solo en nombres inequívocos de gustos seleccionados o de ítems del **menú publicado** que terminan en “sin gluten”, “libre de gluten”, “sin lactosa” o “libre de lactosa”, y solo si existe esa restricción en el catálogo. No se leen descripciones ni borradores de OCR, no se interpreta la ausencia de tags como seguridad y no se usan condiciones de usuarios. La pista muestra su fuente y requiere que el propietario revise ingredientes, preparación y disponibilidad.

Aceptar agrega la restricción al formulario, pero no guarda ni confirma la vigencia. Descartar no cambia datos. Si se retira la fuente antes de guardar, el propietario debe revisar manualmente la declaración o quitarla. El guardado mantiene la regla de actualización parcial y la confirmación integral sigue siendo una acción separada. Este bloque no requiere una migración nueva; la migración previa `SepararVigenciaOfertaYDeclaraciones` sí debe estar aplicada antes de desplegar la vigencia independiente.

## Archivos e imágenes

Las nuevas imágenes se validan (JPG/PNG/WEBP, máximo 5 MB), se orientan y se vuelven a codificar sin metadatos antes de subirlas. Los objetos nuevos usan carpetas ligadas al ID del usuario, restaurante, solicitud u opinión. La eliminación exige URL HTTPS del bucket configurado, nombre generado por la aplicación, carpeta esperada y ausencia de referencias en la base. Las URLs externas y los objetos heredados sin procedencia demostrable se conservan.

Aplicá la migración `RegistrarEliminacionesArchivoPendientes` en el despliegue antes de habilitar las nuevas subidas. Los borrados que fallen y las subidas con persistencia incierta se registran para reintento; el proceso vuelve a verificar referencias antes de eliminar. La frecuencia se configura con `FileCleanup__IntervalMinutes` (10 minutos por defecto, entre 1 y 1440). No se realiza limpieza histórica automática.
