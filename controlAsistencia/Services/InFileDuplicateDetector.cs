using System.Collections.Generic;

namespace controlAsistencia.Services;

/// <summary>
/// Detector sencillo de duplicados dentro de un mismo archivo basándose en la clave:
/// TrabajadorId + FechaHora + Tipo
/// Origen NO participa.
/// </summary>
public class InFileDuplicateDetector
{
    private readonly HashSet<string> _seen = new HashSet<string>();

    private static string BuildKey(int trabajadorId, DateTime fechaHora, string tipo)
    {
        // Fecha en formato sortable/invariable para evitar problemas de cultura
        var fechaKey = fechaHora.ToString("o");
        var t = (tipo ?? string.Empty).Trim().ToUpperInvariant();
        return $"{trabajadorId}|{fechaKey}|{t}";
    }

    /// <summary>
    /// Intenta añadir la clave. Devuelve true si la entrada es nueva (no duplicada)
    /// y false si ya existía (es duplicado).
    /// </summary>
    public bool TryAdd(int trabajadorId, DateTime fechaHora, string tipo)
    {
        var key = BuildKey(trabajadorId, fechaHora, tipo);
        lock (_seen)
        {
            if (_seen.Contains(key)) return false;
            _seen.Add(key);
            return true;
        }
    }

    /// <summary>
    /// Comprueba si la combinación ya fue vista en el archivo.
    /// </summary>
    public bool IsDuplicate(int trabajadorId, DateTime fechaHora, string tipo)
    {
        var key = BuildKey(trabajadorId, fechaHora, tipo);
        lock (_seen)
        {
            return _seen.Contains(key);
        }
    }

    /// <summary>
    /// Para tests o reinicio entre archivos.
    /// </summary>
    public void Clear()
    {
        lock (_seen) { _seen.Clear(); }
    }
}
