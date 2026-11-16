# Etapa 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiamos toda la solución
COPY . .

# Restauramos dependencias del proyecto principal
RUN dotnet restore "src/GustosApp.API/GustosApp.API.csproj"

# Publicamos el proyecto principal
RUN --mount=type=secret,id=sixlabors_license,required=true dotnet publish "src/GustosApp.API/GustosApp.API.csproj" -c Release -o /app/publish /p:UseAppHost=false /p:SixLaborsLicenseFile=/run/secrets/sixlabors_license

# ===========================
# Etapa 2: Runtime
# ===========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copiamos los archivos publicados desde la etapa build
COPY --from=build /app/publish .

# Configuramos la API
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "GustosApp.API.dll"]
