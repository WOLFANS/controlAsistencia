using System.Data;
using Microsoft.Data.SqlClient;
using controlAsistencia.Models;

namespace controlAsistencia.Data;

/// <summary>
/// Acceso básico a datos para la entidad Trabajador usando ADO.NET (Microsoft.Data.SqlClient).
/// </summary>
public class TrabajadorRepository
{
    private readonly string _connectionString;

    public TrabajadorRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Busca un trabajador por RUT normalizado.
    /// Devuelve null si no existe.
    /// </summary>
    public async Task<Trabajador?> GetByRutAsync(string rut)
    {
        if (string.IsNullOrWhiteSpace(rut)) throw new ArgumentException("rut vacío", nameof(rut));

        const string sql = @"SELECT TrabajadorId, EmpresaId, Rut, Activo
FROM dbo.Trabajador
WHERE Rut = @Rut";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandType = CommandType.Text;
        cmd.Parameters.Add("@Rut", SqlDbType.VarChar, 12).Value = rut;

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        var t = new Trabajador();
        t.TrabajadorId = reader["TrabajadorId"] != DBNull.Value ? Convert.ToInt32(reader["TrabajadorId"]) : 0;
        t.EmpresaId = reader["EmpresaId"] != DBNull.Value ? Convert.ToInt32(reader["EmpresaId"]) : 0;
        t.Rut = reader["Rut"] != DBNull.Value ? reader["Rut"].ToString() ?? string.Empty : string.Empty;
        t.Activo = reader["Activo"] != DBNull.Value && Convert.ToBoolean(reader["Activo"]);

        return t;
    }

    /// <summary>
    /// Valida el trabajador por RUT: existencia, activo y pertenencia a EmpresaId = 1.
    /// Devuelve un resultado con motivos diferenciados en caso de rechazo.
    /// </summary>
    public async Task<TrabajadorValidationResult> ValidateByRutAsync(string rut)
    {
        var result = new TrabajadorValidationResult();
        var t = await GetByRutAsync(rut);
        if (t == null)
        {
            result.IsValid = false;
            result.Motivo = "Trabajador no existe";
            return result;
        }

        if (!t.Activo)
        {
            result.IsValid = false;
            result.Motivo = "Trabajador inactivo";
            result.Trabajador = t;
            return result;
        }

        if (t.EmpresaId != 1)
        {
            result.IsValid = false;
            result.Motivo = "Trabajador no pertenece a la empresa 1";
            result.Trabajador = t;
            return result;
        }

        result.IsValid = true;
        result.Trabajador = t;
        return result;
    }
}

/// <summary>
/// Resultado de la validación de trabajador (existencia, activo, empresa).
/// </summary>
public class TrabajadorValidationResult
{
    public bool IsValid { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public Trabajador? Trabajador { get; set; }
}
