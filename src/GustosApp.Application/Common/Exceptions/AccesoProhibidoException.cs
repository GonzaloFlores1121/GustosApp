namespace GustosApp.Application.Common.Exceptions;

public sealed class AccesoProhibidoException : Exception
{
    public AccesoProhibidoException(string mensaje) : base(mensaje)
    {
    }
}
