using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Globalization;
using controlAsistencia.Models;
using controlAsistencia.Data;
using controlAsistencia.Validators;
using System.Text;

namespace controlAsistencia.Services;

/// <summary>
/// Servicio encargado de operaciones relacionadas con la importación.
/// En esta fase implementa únicamente la lectura básica del CSV y un resultado
/// que permite distinguir filas correctamente parseadas de aquellas con errores.
/// </summary>
public class ImporterService
{
    public ImporterService()
    {
    }

    /// <summary>
    /// Escribe un CSV de rechazos con encabezado 'linea;rut;motivo' en la ruta indicada.
    /// Utiliza UTF-8 y aplica escape sencillo para ;, comillas dobles y saltos de línea.
    /// </summary>
    public async Task WriteRejectionsCsvAsync(ImportProcessResult processResult, string outputFilePath)
    {
        if (processResult == null) throw new ArgumentNullException(nameof(processResult));
        if (string.IsNullOrWhiteSpace(outputFilePath)) throw new ArgumentException("outputFilePath no puede estar vacío", nameof(outputFilePath));

        // Preparar líneas: encabezado + una línea por cada rechazo
        var lines = new List<string>();
        lines.Add("linea;rut;motivo");

        foreach (var r in processResult.Rejections)
        {
            var linea = r.LineNumber.ToString();
            var rut = EscapeCsvField(r.Rut);
            var motivo = EscapeCsvField(r.Motivo);
            lines.Add(string.Join(";", linea, rut, motivo));
        }

        // Escribir con UTF8 sin BOM
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        await File.WriteAllLinesAsync(outputFilePath, lines, utf8NoBom);
    }

    private static string EscapeCsvField(string? value)
    {
        value ??= string.Empty;

        // Detectar si el campo contiene alguno de los caracteres especiales antes de transformaciones
        var containsSpecial = value.Contains(';') || value.Contains('"') || value.Contains('\r') || value.Contains('\n');

        // Normalizar saltos de línea a la secuencia literal \n (evita dividir la fila en varias líneas)
        var normalized = value.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\\n");

        // Doblar comillas internas para CSV
        normalized = normalized.Replace("\"", "\"\"");

        if (containsSpecial)
        {
            return '"' + normalized + '"';
        }

        return normalized;
    }

    /// <summary>
    /// Lee un archivo CSV de marcaciones y devuelve un resultado con filas parseadas
    /// y errores. No realiza ninguna validación de negocio ni acceso a datos.
    /// </summary>
    /// <param name="path">Ruta al archivo CSV</param>
    public async Task<CsvReadResult> ReadCsvAsync(string path)
    {
        var result = new CsvReadResult();

        if (string.IsNullOrWhiteSpace(path))
        {
            result.CriticalErrors.Add("Ruta de archivo no proporcionada.");
            return result;
        }

        if (!File.Exists(path))
        {
            result.CriticalErrors.Add($"Archivo no encontrado: {path}");
            return result;
        }

        var lines = await File.ReadAllLinesAsync(path);
        if (lines.Length == 0)
        {
            result.CriticalErrors.Add("El archivo está vacío.");
            return result;
        }

        // El encabezado debe estar en la primera línea y contener las columnas esperadas
        var header = lines[0] ?? string.Empty;
        var expectedHeader = new[] { "rut", "fecha_hora", "tipo", "origen" };
        var headerParts = header.Split(';').Select(p => p.Trim().ToLowerInvariant()).ToArray();
        if (headerParts.Length != 4 || !expectedHeader.SequenceEqual(headerParts))
        {
            result.CriticalErrors.Add("Encabezado incorrecto. Se esperaba: Rut;fecha_hora;tipo;origen");
            return result;
        }

        // Recorremos las líneas físicas del archivo; la primera línea de datos es index 1 -> lineNumber 2
        for (int i = 1; i < lines.Length; i++)
        {
            var physicalLineNumber = i + 1; // 1-based
            var line = lines[i];

            if (string.IsNullOrWhiteSpace(line))
            {
                // Ignorar líneas vacías (pero conservar su número en el cómputo físico)
                continue;
            }

            var parts = line.Split(';');
            var parseResult = new MarcacionParseResult();
            parseResult.Row = new MarcacionCsvRow { LineNumber = physicalLineNumber };

            if (parts.Length != 4)
            {
                parseResult.IsParsed = false;
                parseResult.Errors.Add($"Línea {physicalLineNumber}: cantidad incorrecta de columnas (se esperaban 4)");
                result.Rows.Add(parseResult);
                continue;
            }

            parseResult.Row.Rut = parts[0].Trim();
            var fechaRaw = parts[1].Trim();
            parseResult.Row.Tipo = parts[2].Trim();
            parseResult.Row.Origen = parts[3];

            // Intento de parseo de la fecha en el formato esperado
            if (DateTime.TryParseExact(fechaRaw, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            {
                parseResult.Row.FechaHora = fecha;
                parseResult.IsParsed = true;
            }
            else
            {
                // No detener el procesamiento; marcar la fila como no parseada correctamente
                parseResult.Row.FechaHora = default;
                parseResult.IsParsed = false;
                parseResult.Errors.Add($"Línea {physicalLineNumber}: fecha inválida ('{fechaRaw}'). Formato esperado: yyyy-MM-dd HH:mm:ss");
            }

            result.Rows.Add(parseResult);
        }

        return result;
    }

    /// <summary>
    /// Procesa el archivo coordinando validaciones locales y consultas a datos (trabajador, duplicados).
    /// No realiza inserciones en la base de datos.
    /// </summary>
    /// <param name="path">Ruta al CSV</param>
    /// <param name="connectionString">Cadena de conexión para crear repositorios</param>
    public async Task<ImportProcessResult> ProcessFileAsync(string path, string connectionString)
    {
        var result = new ImportProcessResult();

        var csv = await ReadCsvAsync(path);
        if (!csv.Success)
        {
            result.CriticalErrors.AddRange(csv.CriticalErrors);
            return result;
        }

        // Inicializar repositorios y detector de duplicados en memoria
        var trabajadorRepo = new TrabajadorRepository(connectionString);
        var marcacionRepo = new MarcacionRepository(connectionString);
        var inFileDetector = new InFileDuplicateDetector();
        var validator = new MarcacionValidator();

        result.TotalRowsRead = csv.Rows.Count;

        foreach (var rowParse in csv.Rows)
        {
            var row = rowParse.Row;

            if (!rowParse.IsParsed)
            {
                // Parse error (fecha u otros problemas detectados en la lectura)
                result.Rejections.Add(new RejectionEntry
                {
                    LineNumber = row.LineNumber,
                    Rut = row.Rut,
                    Motivo = string.Join("; ", rowParse.Errors)
                });
                continue;
            }

            // Validaciones locales: RUT y Tipo
            var rowValidation = validator.ValidateMarcacionRow(row);
            if (!rowValidation.IsValid)
            {
                result.Rejections.Add(new RejectionEntry
                {
                    LineNumber = row.LineNumber,
                    Rut = row.Rut,
                    Motivo = string.Join("; ", rowValidation.Errors)
                });
                continue;
            }

            // Obtener RUT normalizado desde el resultado del validador
            var rutNormalizado = rowValidation.RutResult?.RutNormalizado ?? row.Rut;

            // Consultar trabajador por RUT normalizado
            var trabajadorValidation = await trabajadorRepo.ValidateByRutAsync(rutNormalizado);
            if (!trabajadorValidation.IsValid)
            {
                result.Rejections.Add(new RejectionEntry
                {
                    LineNumber = row.LineNumber,
                    Rut = rutNormalizado,
                    Motivo = trabajadorValidation.Motivo
                });
                continue;
            }

            var trabajadorId = trabajadorValidation.Trabajador!.TrabajadorId;

            // Duplicado dentro del archivo?
            if (inFileDetector.IsDuplicate(trabajadorId, row.FechaHora, row.Tipo))
            {
                result.Rejections.Add(new RejectionEntry
                {
                    LineNumber = row.LineNumber,
                    Rut = rutNormalizado,
                    Motivo = "Duplicado dentro del archivo"
                });
                continue;
            }

            // Duplicado en base de datos?
            var existsInDb = await marcacionRepo.ExistsAsync(trabajadorId, row.FechaHora, row.Tipo);
            if (existsInDb)
            {
                result.Rejections.Add(new RejectionEntry
                {
                    LineNumber = row.LineNumber,
                    Rut = rutNormalizado,
                    Motivo = "Marcación ya existe en base de datos"
                });
                continue;
            }

            // Es candidata válida: añadir al detector de duplicados en memoria y a la lista de candidatos
            inFileDetector.TryAdd(trabajadorId, row.FechaHora, row.Tipo);
            result.Candidates.Add(new ValidCandidate
            {
                Row = row,
                TrabajadorId = trabajadorId,
                RutNormalizado = rutNormalizado
            });
        }

        return result;
    }

    /// <summary>
    /// Inserta las filas candidatas dentro de una única transacción.
    /// Devuelve el número de filas efectivamente insertadas en ImportProcessResult.InsertedCount.
    /// </summary>
    public async Task<ImportProcessResult> InsertCandidatesInTransactionAsync(ImportProcessResult processResult, string connectionString)
    {
        if (processResult == null) throw new ArgumentNullException(nameof(processResult));

        if (processResult.Candidates.Count == 0)
        {
            processResult.InsertedCount = 0;
            return processResult;
        }

        await using var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await conn.OpenAsync();
        var tran = (Microsoft.Data.SqlClient.SqlTransaction)await conn.BeginTransactionAsync();

        var repo = new MarcacionRepository(connectionString);
        int inserted = 0;
        try
        {
            foreach (var c in processResult.Candidates)
            {
                // Usar la misma conexión y transacción
                await repo.InsertAsync(c.TrabajadorId, c.Row.FechaHora, c.Row.Tipo, c.Row.Origen, conn, tran);
                inserted++;
            }

            await tran.CommitAsync();
            processResult.InsertedCount = inserted;
            return processResult;
        }
        catch
        {
            try { await tran.RollbackAsync(); } catch { }
            throw;
        }
        finally
        {
            await conn.DisposeAsync();
        }
    }
}

/// <summary>
/// Resultado de la lectura del CSV que incluye errores críticos (archivo/header)
/// y una colección de resultados por fila que permiten identificar filas con
/// problemas de parseo sin interrumpir el proceso completo.
/// </summary>
public class CsvReadResult
{
    /// <summary>
    /// Errores que impiden continuar (archivo no encontrado, encabezado inválido, etc.)
    /// </summary>
    public List<string> CriticalErrors { get; } = new List<string>();

    /// <summary>
    /// Resultados por cada fila no vacía encontrada en el archivo (excluye el encabezado).
    /// </summary>
    public List<MarcacionParseResult> Rows { get; } = new List<MarcacionParseResult>();

    /// <summary>
    /// Indica si no hubo errores críticos.
    /// </summary>
    public bool Success => CriticalErrors.Count == 0;
}

/// <summary>
/// Resultado del parseo de una fila individual del CSV.
/// IsParsed == true indica que los campos esperados (incluida la fecha) fueron parseados correctamente.
/// En caso contrario, Errors contendrá mensajes que expliquen el fallo.
/// </summary>
public class MarcacionParseResult
{
    public MarcacionCsvRow Row { get; set; } = new MarcacionCsvRow();
    public bool IsParsed { get; set; } = false;
    public List<string> Errors { get; } = new List<string>();
}

/// <summary>
/// Resultado del proceso de importación parcial (validaciones y detección de duplicados).
/// No realiza inserciones.
/// </summary>
public class ImportProcessResult
{
    public int TotalRowsRead { get; set; }
    public List<ValidCandidate> Candidates { get; } = new List<ValidCandidate>();
    public List<RejectionEntry> Rejections { get; } = new List<RejectionEntry>();
    public List<string> CriticalErrors { get; } = new List<string>();
    // Número de filas insertadas por la operación de inserción (si se ejecuta).
    public int InsertedCount { get; set; }
}

public class ValidCandidate
{
    public MarcacionCsvRow Row { get; set; } = new MarcacionCsvRow();
    public int TrabajadorId { get; set; }
    public string RutNormalizado { get; set; } = string.Empty;
}

public class RejectionEntry
{
    public int LineNumber { get; set; }
    public string Rut { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
}

