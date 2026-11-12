namespace GustosApp.Application.Common.Exceptions;

public sealed class CorreoNoIniciadoException() : Exception("No se pudo preparar el correo.");

public sealed class ResultadoCorreoInciertoException() : Exception("No se pudo confirmar el resultado del envío SMTP.");
