# Laboratorio Docker local: Redis y SQL Server

Este ejercicio agrega SQL Server al Redis que ya usa el backend. El archivo
`docker-compose.yml` sigue levantando solo Redis; al sumar
`docker-compose.sqlserver.yml`, Docker Compose levanta ambos servicios.
La API se ejecuta localmente con .NET, para ver con claridad qué parte corre
en un contenedor y qué parte corre en Windows.

## 1. Preparar Docker y la contraseña

Iniciá Docker Desktop y esperá a que indique que el motor está funcionando.
Usá contenedores Linux. SQL Server requiere al menos 2 GB de memoria disponible
para arrancar. Desde PowerShell, en la raíz del repositorio backend:

```powershell
$claveSql = Read-Host "Contraseña local para el usuario sa" -AsSecureString
$env:MSSQL_SA_PASSWORD = [System.Net.NetworkCredential]::new("", $claveSql).Password
docker compose -f docker-compose.yml -f docker-compose.sqlserver.yml config --quiet
```

Elegí una contraseña de al menos ocho caracteres, con caracteres de tres
categorías entre mayúsculas, minúsculas, números y símbolos. Queda en esta
sesión de PowerShell y no se guarda en Git. Guardala en tu gestor de
contraseñas: cuando cierres la terminal, volvé a usar la misma contraseña
para conectar con la base ya creada.

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

## 3. Conectar el backend

En la misma terminal, configurá la conexión para el proceso local de .NET:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost,14330;Database=GustosAppDb;User Id=sa;Password=$env:MSSQL_SA_PASSWORD;TrustServerCertificate=True"
dotnet ef database update --project src/GustosApp.Infraestructure/GustosApp.Infraestructure.csproj --startup-project src/GustosApp.API/GustosApp.API.csproj --context GustosDbContext
dotnet run --project src/GustosApp.API/GustosApp.API.csproj --launch-profile http
```

Las dos rayas bajas en `ConnectionStrings__DefaultConnection` representan
los dos puntos de `ConnectionStrings:DefaultConnection` en la configuración
de ASP.NET Core. Así se reemplaza la conexión local a SQLEXPRESS sin editar
`appsettings.Development.json`. Las credenciales locales de Firebase que
ya requiere el proyecto siguen siendo necesarias para ejecutar la API.
Con el perfil `http`, Swagger queda en `http://localhost:5174/swagger`.
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
