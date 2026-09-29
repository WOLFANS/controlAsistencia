namespace controlAsistencia.Models;

/// <summary>
/// Representa una fila del archivo CSV de marcaciones.
/// Este modelo contiene únicamente los datos tal como vienen en el CSV
/// y metadatos necesarios para reportes de rechazo (número de línea).
/// No incluye reglas de negocio como EmpresaId ni comportamiento de validación.
/// </summary>
public class MarcacionCsvRow
{
    /// <summary>
    /// RUT tal como aparece en el CSV (sin normalizar aún).
    /// La normalización y validación se implementarán en la fase siguiente.
    /// </summary>
    public string Rut { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora tal como será representada en el modelo. El parseo se hará
    /// en fases posteriores; el formato esperado será "yyyy-MM-dd HH:mm:ss".
    /// </summary>
    public DateTime FechaHora { get; set; }

    /// <summary>
    /// Tipo tal como viene en el CSV. Posteriormente se validará que sea "E" o "S".
    /// </summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>
    /// Origen tal como aparece en el CSV. Debe conservarse exactamente igual
    /// y NO participa en la detección de duplicados.
    /// </summary>
    public string Origen { get; set; } = string.Empty;

    /// <summary>
    /// Número de línea original del archivo CSV. El encabezado es la línea 1,
    /// la primera fila de datos es la línea 2. Se usará para generar rechazos.csv.
    /// </summary>
    public int LineNumber { get; set; }
}
