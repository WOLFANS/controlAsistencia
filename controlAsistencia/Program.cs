// Integración mínima para ejecutar el flujo de importación desde la consola.
using System.Text.Json.Nodes;
using System.IO;
using controlAsistencia.Services;

async Task<int> RunAsync(string[] args)
{
    try
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Uso: dotnet run -- \"C:\\ruta\\marcaciones.csv\"");
            return 1;
        }

        var csvPath = args[0];
        if (!File.Exists(csvPath))
        {
            Console.WriteLine($"Archivo no encontrado: {csvPath}");
            return 1;
        }

        // Cargar appsettings.json desde el directorio de salida de la aplicación
        var basePath = AppContext.BaseDirectory;
        var candidate = Path.Combine(basePath, "appsettings.json");

        if (!File.Exists(candidate))
        {
            Console.WriteLine("Error: no se encontró appsettings.json. Coloque ConnectionStrings:DefaultConnection.");
            return 1;
        }

        string connectionString;
        try
        {
            var json = File.ReadAllText(candidate);
            var doc = JsonNode.Parse(json);
            connectionString = doc?["ConnectionStrings"]?["DefaultConnection"]?.GetValue<string>() ?? string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error leyendo appsettings.json: {ex.Message}");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine("Error: ConnectionStrings:DefaultConnection está vacío en appsettings.json");
            return 1;
        }

        var svc = new ImporterService();

        // 1) Procesar CSV (validaciones locales y detección de duplicados)
        var processResult = await svc.ProcessFileAsync(csvPath, connectionString);

        // 2) Si hay errores críticos, mostrarlos y terminar
        if (processResult.CriticalErrors.Count > 0)
        {
            Console.WriteLine("Errores críticos:");
            foreach (var e in processResult.CriticalErrors) Console.WriteLine($"- {e}");
            return 1;
        }

        // 3) Insertar candidatos dentro de una transacción (usando la implementación existente)
        processResult = await svc.InsertCandidatesInTransactionAsync(processResult, connectionString);

        // 4) Generar rechazos.csv en el mismo directorio que el CSV de entrada
        var outDir = Path.GetDirectoryName(Path.GetFullPath(csvPath)) ?? Directory.GetCurrentDirectory();
        var rechazosPath = Path.Combine(outDir, "rechazos.csv");
        await svc.WriteRejectionsCsvAsync(processResult, rechazosPath);

        // 5) Resumen final
        Console.WriteLine($"Filas leídas: {processResult.TotalRowsRead}");
        Console.WriteLine($"Aceptadas: {processResult.InsertedCount}");
        Console.WriteLine($"Rechazadas: {processResult.Rejections.Count}");
        Console.WriteLine($"Rechazos guardados en: {rechazosPath}");

        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error inesperado: {ex.Message}");
        return 1;
    }
}

// Ejecutar
var exit = await RunAsync(args);
Environment.Exit(exit);
