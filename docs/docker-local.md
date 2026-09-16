# Laboratorio Docker local: Redis y SQL Server

Este ejercicio agrega SQL Server al Redis que ya usa el backend. El archivo
`docker-compose.yml` sigue levantando solo Redis; al sumar
`docker-compose.sqlserver.yml`, Docker Compose levanta ambos servicios.
La API se ejecuta localmente con .NET, para ver con claridad qué parte corre
en un contenedor y qué parte corre en Windows.

## 1. Preparar Docker y los archivos locales

Iniciá Docker Desktop y esperá a que indique que el motor está funcionando.
Usá contenedores Linux. SQL Server requiere al menos 2 GB de memoria disponible
para arrancar.

Desde PowerShell, copiá `.env.example` como `.env` en la raíz del backend:

```powershell
Copy-Item .env.example .env
```

En `.env`, escribí
la contraseña que ya elegiste para el usuario `sa`:

```env
MSSQL_SA_PASSWORD=tu_misma_contrasena_de_sql_server
```

Si todavía no creaste SQL Server, elegí una contraseña de al menos ocho
caracteres, con caracteres de tres categorías entre mayúsculas, minúsculas,
números y símbolos. `.env` está ignorado por Git; no lo compartas ni lo
subas al repositorio.

Conservá también tu `appsettings.Development.json`: sigue siendo el perfil
para SQL Server Express. Creá `src/GustosApp.API/appsettings.Docker.json`,
que también está ignorado por Git:

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379",
    "DefaultConnection": "Server=localhost,14330;Database=GustosAppDb;User Id=sa;Password=tu_misma_contrasena_de_sql_server;TrustServerCertificate=True"
  }
}
```

ASP.NET Core carga `appsettings.Docker.json` al elegir el perfil
`Docker local` en Visual Studio.

## 2. Levantar y observar

```powershell
docker compose -f docker-compose.yml -f docker-compose.sqlserver.yml up -d
docker compose -f docker-compose.yml -f docker-compose.sqlserver.yml ps
docker compose -f docker-compose.yml -f docker-compose.sqlserver.yml logs sqlserver
```

La primera ejecución descarga la imagen de SQL Server y puede tardar. En los
logs esperá el mensaje que indica que SQL Server está listo para conexiones.
Redis conserva el puerto local 6379. SQL Server escucha en el puerto 14330
de tu computadora, conectado al 1433 del contenedor. Ese puerto local evita
interferir con una instancia de SQL Server Express existente.

Si solamente necesitás Redis, el comando de siempre sigue funcionando:

```powershell
docker compose up -d
```

## 3. Crear la base y ejecutar el backend

La primera vez, aplicá las migraciones:

```powershell
dotnet ef database update --project src/GustosApp.Infraestructure/GustosApp.Infraestructure.csproj --startup-project src/GustosApp.API/GustosApp.API.csproj --context GustosDbContext
```

En Visual Studio, elegí `Docker local` en el selector junto al botón de
inicio y presioná Play. La API local usará SQL Server Docker y quedará en
`http://localhost:5174/swagger`. Para volver a SQL Server Express, elegí
el perfil `http` o `https`.

Las credenciales locales de Firebase que ya requiere el proyecto siguen
siendo necesarias para ejecutar la API.
El frontend puede ejecutarse en otra terminal; este laboratorio no modifica
su repositorio.

## 4. Comprobar la persistencia

Después de aplicar las migraciones, verificá con tu cliente SQL que
`GustosAppDb` y sus tablas existen en `localhost,14330`, usuario `sa`.
Luego detené y recreá los contenedores:

```powershell
docker compose -f docker-compose.yml -f docker-compose.sqlserver.yml down
docker compose -f docker-compose.yml -f docker-compose.sqlserver.yml up -d
```

Volvé a consultar la base: debe seguir ahí. El volumen
`gustos_sqlserver` (Docker le agrega el nombre del proyecto como prefijo)
guarda los archivos bajo `/var/opt/mssql` aunque
se elimine el contenedor. `down` conserva el volumen; `down -v` lo borra
y perderías los datos del laboratorio. No uses `down -v` si querés
conservarlos.

Este Compose es solo para desarrollo local. No modifica el CI ni el
despliegue existentes.
