using GustosApp.Application.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GustosApp.Infraestructure.Services;

public sealed class BloqueoResolucionSolicitudSql(GustosDbContext db) : IBloqueoResolucionSolicitud
{
    public Task<bool> IntentarAsync(Guid solicitudId, Func<CancellationToken, Task> trabajo, CancellationToken ct) =>
        BloqueoExclusivoSql.IntentarAsync(db, $"gustos:resolucion-solicitud:{solicitudId:N}", trabajo, ct);
}

internal static class BloqueoExclusivoSql
{
    internal static async Task<bool> IntentarAsync(GustosDbContext db, string recurso, Func<CancellationToken, Task> trabajo, CancellationToken ct)
    {
        // El contexto de escritura queda separado de la transacción que sólo posee el bloqueo.
        if (!db.Database.IsRelational())
        {
            await trabajo(ct);
            return true;
        }

        await using var conexion = new SqlConnection(db.Database.GetConnectionString());
        await conexion.OpenAsync(ct);
        await using var transaccion = (SqlTransaction)await conexion.BeginTransactionAsync(ct);
        await using var comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText = """
            DECLARE @resultado int;
            EXEC @resultado = sys.sp_getapplock @Resource=@recurso, @LockMode='Exclusive',
                @LockOwner='Transaction', @LockTimeout=0, @DbPrincipal='public';
            SELECT @resultado;
            """;
        comando.Parameters.AddWithValue("@recurso", recurso);
        var resultado = (int)(await comando.ExecuteScalarAsync(ct))!;
        if (resultado == -1) return false;
        if (resultado < 0) throw new InvalidOperationException("No se pudo obtener el bloqueo exclusivo.");
        await trabajo(ct);
        await transaccion.CommitAsync(ct);
        return true;
    }
}
