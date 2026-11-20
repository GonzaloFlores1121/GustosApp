using GustosApp.Application.Interfaces;

namespace GustosApp.Application.Tests;

internal sealed class RelojExplicacionesPrueba(DateTimeOffset ahora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => ahora;

    public void Avanzar(TimeSpan tiempo) => ahora += tiempo;
}

internal sealed class AlmacenExplicacionesPrueba(TimeProvider reloj) : IAlmacenExplicaciones
{
    private readonly object _sync = new();
    private readonly Dictionary<string, (string Valor, DateTimeOffset Expira)> _reservas = new();
    private readonly Dictionary<string, (string Valor, DateTimeOffset Expira)> _textos = new();
    public bool Caido { get; set; }
    public List<string> ClavesLeidas { get; } = [];

    public Task<(string? Texto, bool Reservada)> LeerAsync(string clave, CancellationToken ct)
    {
        if (Caido) throw new InvalidOperationException("Redis no disponible");
        lock (_sync)
        {
            ClavesLeidas.Add(clave);
            return Task.FromResult((Vigente(_textos, clave), Vigente(_reservas, clave) is not null));
        }
    }

    public Task<bool> ReservarAsync(string clave, string propietario, TimeSpan duracion, CancellationToken ct)
    {
        lock (_sync)
        {
            if (Vigente(_reservas, clave) is not null) return Task.FromResult(false);
            _reservas[clave] = (propietario, reloj.GetUtcNow() + duracion);
            return Task.FromResult(true);
        }
    }

    public Task<bool> PublicarAsync(string clave, string propietario, string texto, TimeSpan ttl, CancellationToken ct)
    {
        lock (_sync)
        {
            if (Vigente(_reservas, clave) != propietario) return Task.FromResult(false);
            _textos[clave] = (texto, reloj.GetUtcNow() + ttl);
            _reservas.Remove(clave);
            return Task.FromResult(true);
        }
    }

    public Task LiberarAsync(string clave, string propietario, CancellationToken ct)
    {
        lock (_sync)
        {
            if (Vigente(_reservas, clave) == propietario) _reservas.Remove(clave);
            return Task.CompletedTask;
        }
    }

    private string? Vigente(Dictionary<string, (string Valor, DateTimeOffset Expira)> entradas, string clave) =>
        entradas.TryGetValue(clave, out var entrada) && entrada.Expira > reloj.GetUtcNow() ? entrada.Valor : null;
}
