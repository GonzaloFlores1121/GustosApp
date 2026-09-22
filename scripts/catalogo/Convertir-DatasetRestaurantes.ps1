[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $RutaEntrada,

    [Parameter(Mandatory = $true)]
    [string] $RutaSalida,

    [switch] $Sobrescribir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Separar-ValoresSql {
    param([Parameter(Mandatory = $true)][string] $Texto)

    $valores = [System.Collections.Generic.List[string]]::new()
    $actual = [System.Text.StringBuilder]::new()
    $dentroDeTexto = $false
    $profundidad = 0

    for ($indice = 0; $indice -lt $Texto.Length; $indice++) {
        $caracter = $Texto[$indice]

        if ($caracter -eq "'") {
            if ($dentroDeTexto -and $indice + 1 -lt $Texto.Length -and $Texto[$indice + 1] -eq "'") {
                [void] $actual.Append("''")
                $indice++
                continue
            }

            $dentroDeTexto = -not $dentroDeTexto
            [void] $actual.Append($caracter)
            continue
        }

        if (-not $dentroDeTexto) {
            if ($caracter -eq '(') {
                $profundidad++
            }
            elseif ($caracter -eq ')') {
                $profundidad--
            }
            elseif ($caracter -eq ',' -and $profundidad -eq 0) {
                $valores.Add($actual.ToString().Trim())
                [void] $actual.Clear()
                continue
            }
        }

        [void] $actual.Append($caracter)
    }

    if ($dentroDeTexto -or $profundidad -ne 0) {
        throw 'Se encontró una expresión SQL sin cerrar.'
    }

    $valores.Add($actual.ToString().Trim())
    return $valores
}

function Convertir-ValorSql {
    param([Parameter(Mandatory = $true)][string] $Valor)

    if ($Valor -eq 'NULL') {
        return $null
    }

    if ($Valor -match "^CAST\(N'(?<fecha>.*)' AS DateTime2\)$") {
        $fecha = [DateTime]::Parse(
            $Matches.fecha,
            [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::AssumeUniversal)
        return $fecha.ToUniversalTime().ToString('o')
    }

    if ($Valor -match "^N?'(?<texto>.*)'$") {
        return $Matches.texto.Replace("''", "'")
    }

    $entero = 0
    if ([int]::TryParse(
            $Valor,
            [Globalization.NumberStyles]::Integer,
            [Globalization.CultureInfo]::InvariantCulture,
            [ref] $entero)) {
        return $entero
    }

    $decimal = 0.0
    if ([double]::TryParse(
            $Valor,
            [Globalization.NumberStyles]::Float,
            [Globalization.CultureInfo]::InvariantCulture,
            [ref] $decimal)) {
        return $decimal
    }

    throw "No se pudo interpretar el valor SQL: $Valor"
}

$rutaEntradaResuelta = (Resolve-Path -LiteralPath $RutaEntrada).Path
$rutaSalidaCompleta = [IO.Path]::GetFullPath($RutaSalida)

if ((Test-Path -LiteralPath $rutaSalidaCompleta) -and -not $Sobrescribir) {
    throw "El archivo de salida ya existe. Usá -Sobrescribir para reemplazarlo: $rutaSalidaCompleta"
}

$restaurantes = [System.Collections.Generic.List[object]]::new()
$patron = '^INSERT \[dbo\]\.\[Restaurantes\] \((?<columnas>.+)\) VALUES \((?<valores>.+)\)$'

foreach ($linea in Get-Content -LiteralPath $rutaEntradaResuelta) {
    if ($linea -notmatch $patron) {
        continue
    }

    $columnas = $Matches.columnas.Split(',') |
        ForEach-Object { $_.Trim().TrimStart('[').TrimEnd(']') }
    $valores = @(Separar-ValoresSql -Texto $Matches.valores)

    if ($columnas.Count -ne $valores.Count) {
        throw "La cantidad de columnas y valores no coincide en: $linea"
    }

    $fila = @{}
    for ($indice = 0; $indice -lt $columnas.Count; $indice++) {
        $fila[$columnas[$indice]] = Convertir-ValorSql -Valor $valores[$indice]
    }

    $restaurantes.Add([ordered]@{
        placeId = $fila.PlaceId
        nombre = $fila.Nombre
        direccion = $fila.Direccion
        latitud = $fila.Latitud
        longitud = $fila.Longitud
        horariosJson = $fila.HorariosJson
        rating = $fila.Rating
        cantidadResenas = $fila.CantidadResenas
        categoria = $fila.Categoria
        fechaDatosUtc = $fila.UltimaActualizacion
        webUrl = $fila.WebUrl
        primaryType = $fila.PrimaryType
        typesJson = $fila.TypesJson
        imagenUrl = $fila.ImagenUrl
    })
}

if ($restaurantes.Count -eq 0) {
    throw 'No se encontraron INSERT de restaurantes en el archivo.'
}

$placeIdsInvalidos = @($restaurantes | Where-Object { [string]::IsNullOrWhiteSpace($_.placeId) })
if ($placeIdsInvalidos.Count -gt 0) {
    throw "Hay $($placeIdsInvalidos.Count) restaurantes sin PlaceId."
}

$placeIdsDuplicados = @($restaurantes |
    Group-Object { $_.placeId.ToLowerInvariant() } |
    Where-Object Count -gt 1)
if ($placeIdsDuplicados.Count -gt 0) {
    throw "Hay PlaceId duplicados en el dataset: $($placeIdsDuplicados.Name -join ', ')"
}

$solicitud = [ordered]@{
    confirmar = $false
    restaurantes = $restaurantes
}

$directorioSalida = [IO.Path]::GetDirectoryName($rutaSalidaCompleta)
if (-not [string]::IsNullOrWhiteSpace($directorioSalida) -and -not (Test-Path -LiteralPath $directorioSalida)) {
    [void] (New-Item -ItemType Directory -Path $directorioSalida)
}

$json = $solicitud | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText($rutaSalidaCompleta, $json, [Text.UTF8Encoding]::new($false))

Write-Host "Se prepararon $($restaurantes.Count) restaurantes."
Write-Host "Vista previa lista en: $rutaSalidaCompleta"
Write-Host 'El archivo conserva confirmar=false y todavía no modifica la base de datos.'
