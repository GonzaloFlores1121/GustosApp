namespace GustosApp.Application.Common.Exceptions;

public sealed class PagoTemporalmenteNoDisponibleException : Exception
{
    public PagoTemporalmenteNoDisponibleException(string message) : base(message) { }
}
