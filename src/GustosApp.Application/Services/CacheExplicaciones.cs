using GustosApp.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GustosApp.Application.Services;

/// <summary>Reutiliza explicaciones y coordina una única generación por clave.</summary>
public sealed class CacheExplicaciones(
    IAlmacenExplicaciones almacen,
    IOptions<GeminiSettings> opciones,
    TimeProvider reloj,
    ILogger<CacheExplicaciones> logger)
{
    private static readonly TimeSpan IntervaloConsulta = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan TiempoOperacion = TimeSpan.FromSeconds(1);

    /// <summary>Devuelve un texto exitoso o permite que el llamador use su explicación local.</summary>
    public async Task<string?> ObtenerAsync(string clave, Func<CancellationToken, Task<string?>> generar, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var tiempoGemini = TimeSpan.FromSeconds(Math.Clamp(opciones.Value.TimeoutSegundos, 1, 60));
        using var espera = new CancellationTokenSource(tiempoGemini + TimeSpan.FromSeconds(3), reloj);
        using var solicitud = CancellationTokenSource.CreateLinkedTokenSource(ct, espera.Token);
        try
        {
            var (texto, reservada) = await LeerAsync(clave, solicitud.Token);
            if (!string.IsNullOrWhiteSpace(texto))
                return texto;

            if (!reservada)
            {
                var propietario = Guid.NewGuid().ToString("N");
                using var operacion = LimitarOperacion(solicitud.Token);
                if (await almacen.ReservarAsync(clave, propietario, tiempoGemini + TimeSpan.FromSeconds(5), operacion.Token))
                {
                    // La cancelación de este solicitante termina su espera. La generación
                    // tiene su propio plazo y puede servir a los demás solicitantes.
                    return await GenerarConReservaAsync(clave, propietario, generar, tiempoGemini).WaitAsync(solicitud.Token);
                }
            }

            do
            {
                (texto, reservada) = await LeerAsync(clave, solicitud.Token);
                if (!string.IsNullOrWhiteSpace(texto) || !reservada)
                    return texto;
                await Task.Delay(IntervaloConsulta, reloj, solicitud.Token);
            } while (true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            ct.ThrowIfCancellationRequested();
            RegistrarFallo(ex);
            return null;
        }
    }

    private async Task<string?> GenerarConReservaAsync(string clave, string propietario,
        Func<CancellationToken, Task<string?>> generar, TimeSpan tiempoGemini)
    {
        try
        {
            // Otra instancia pudo publicar entre la primera lectura y nuestra reserva.
            var (existente, _) = await LeerAsync(clave, CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(existente))
                return existente;

            using var limite = new CancellationTokenSource(tiempoGemini + TiempoOperacion, reloj);
            var texto = await generar(limite.Token).WaitAsync(limite.Token);
            if (string.IsNullOrWhiteSpace(texto))
                return null;

            using var operacion = LimitarOperacion(CancellationToken.None);
            var ttl = TimeSpan.FromSeconds(Math.Clamp(opciones.Value.CacheExplicacionesTtlSegundos, 1, 86_400));
            return await almacen.PublicarAsync(clave, propietario, texto.Trim(), ttl, operacion.Token)
                ? texto.Trim() : null;
        }
        catch (Exception ex)
        {
            RegistrarFallo(ex);
            return null;
        }
        finally
        {
            try
            {
                using var operacion = LimitarOperacion(CancellationToken.None);
                await almacen.LiberarAsync(clave, propietario, operacion.Token);
            }
            catch (Exception ex)
            {
                RegistrarFallo(ex);
            }
        }
    }

    private async Task<(string? Texto, bool Reservada)> LeerAsync(string clave, CancellationToken ct)
    {
        using var operacion = LimitarOperacion(ct);
        return await almacen.LeerAsync(clave, operacion.Token);
    }

    private CancellationTokenSource LimitarOperacion(CancellationToken ct)
    {
        var operacion = CancellationTokenSource.CreateLinkedTokenSource(ct);
        operacion.CancelAfter(TiempoOperacion);
        return operacion;
    }

    private void RegistrarFallo(Exception ex) =>
        logger.LogWarning("No se pudo completar la explicación en caché. Tipo: {Tipo}", ex.GetType().Name);
}
