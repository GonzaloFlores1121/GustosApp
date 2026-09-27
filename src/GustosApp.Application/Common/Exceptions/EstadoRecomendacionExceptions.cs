namespace GustosApp.Application.Common.Exceptions;

public sealed class SinPreferenciasRecomendacionException : Exception
{
    public SinPreferenciasRecomendacionException()
        : base("Completá tus gustos para recibir recomendaciones.")
    {
    }
}

public sealed class FiltroGustosInvalidoException : ArgumentException
{
    public FiltroGustosInvalidoException(IEnumerable<string> nombresInvalidos)
        : base($"Los siguientes gustos no existen: {string.Join(", ", nombresInvalidos)}.")
    {
    }
}
