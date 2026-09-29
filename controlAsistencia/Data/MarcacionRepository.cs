using System.Data;
using Microsoft.Data.SqlClient;

namespace controlAsistencia.Data;

/// <summary>
/// Repositorio para consultas relacionadas con marcaciones (solo lectura/sobre existencia).
/// </summary>
public class MarcacionRepository
{
    private readonly string _connectionString;

    public MarcacionRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Comprueba si ya existe una marcación en la base de datos con la misma combinación
    /// TrabajadorId + FechaHora + Tipo. Origen NO participa en la comparación.
    /// </summary>
    /// <param name="trabajadorId">Identificador del trabajador</param>
    /// <param name="fechaHora">Fecha y hora de la marcación</param>
    /// <param name="tipo">Tipo de marcación (E o S)</param>
    public async Task<bool> ExistsAsync(int trabajadorId, DateTime fechaHora, string tipo)
    {
        const string sql = @"SELECT COUNT(1) FROM dbo.Marcacion
WHERE TrabajadorId = @TrabajadorId
  AND FechaHora = @FechaHora
  AND Tipo = @Tipo";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandType = CommandType.Text;

        cmd.Parameters.Add(new SqlParameter("@TrabajadorId", SqlDbType.Int) { Value = trabajadorId });
        cmd.Parameters.Add(new SqlParameter("@FechaHora", SqlDbType.DateTime2) { Value = fechaHora });
        cmd.Parameters.Add(new SqlParameter("@Tipo", SqlDbType.Char, 1) { Value = tipo });

        var result = await cmd.ExecuteScalarAsync();
        if (result == null || result == DBNull.Value) return false;

        var count = Convert.ToInt32(result);
        return count > 0;
    }

    /// <summary>
    /// Inserta una marcación en la tabla dbo.Marcacion.
    /// Si se pasan una conexión y transacción existentes, se usan; si no, se crea una conexión propia.
    /// </summary>
    public async Task InsertAsync(int trabajadorId, DateTime fechaHora, string tipo, string origen, SqlConnection? existingConnection = null, SqlTransaction? existingTransaction = null)
    {
        const string sql = @"INSERT INTO dbo.Marcacion (TrabajadorId, FechaHora, Tipo, Origen)
VALUES (@TrabajadorId, @FechaHora, @Tipo, @Origen)";

        var closeConnection = false;
        SqlConnection conn = existingConnection ?? new SqlConnection(_connectionString);
        try
        {
            if (existingConnection == null)
            {
                await conn.OpenAsync();
                closeConnection = true;
            }

            await using var cmd = conn.CreateCommand();
            if (existingTransaction != null)
            {
                cmd.Transaction = existingTransaction;
            }

            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;

            cmd.Parameters.Add(new SqlParameter("@TrabajadorId", SqlDbType.Int) { Value = trabajadorId });
            cmd.Parameters.Add(new SqlParameter("@FechaHora", SqlDbType.DateTime2) { Value = fechaHora });
            cmd.Parameters.Add(new SqlParameter("@Tipo", SqlDbType.Char, 1) { Value = tipo });
            // Origen se almacena tal como viene en el CSV.
            cmd.Parameters.Add(new SqlParameter("@Origen", SqlDbType.VarChar, 20) { Value = (object)origen ?? string.Empty });

            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            if (closeConnection)
            {
                await conn.DisposeAsync();
            }
        }
    }
}
