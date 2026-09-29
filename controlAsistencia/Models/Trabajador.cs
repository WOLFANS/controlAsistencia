namespace controlAsistencia.Models;

/// <summary>
/// Modelo sencillo que representa el resultado de consultar dbo.Trabajador.
/// </summary>
public class Trabajador
{
    public int TrabajadorId { get; set; }
    public int EmpresaId { get; set; }
    public string Rut { get; set; } = string.Empty;
    public bool Activo { get; set; }
}
