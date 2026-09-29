using System;
using System.IO;
using Xunit;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace controlAsistencia.Tests.UnitTests;

public class ConfigurationTests
{
    [Fact]
    public void AppsettingsJson_Exists_And_Has_ConnectionStrings()
    {
        // Preferir appsettings.json en el directorio de salida de los tests (AppContext.BaseDirectory)
        // pero conservar compatibilidad con la estructura del repositorio.
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "controlAsistencia", "appsettings.json")),
            Path.GetFullPath(Path.Combine("..", "controlAsistencia", "appsettings.json"))
        };

        string path = null!;
        foreach (var c in candidates)
        {
            if (File.Exists(c))
            {
                path = c;
                break;
            }
        }

        Assert.True(path != null, $"Se esperaba encontrar appsettings.json en alguna de las rutas: {string.Join(", ", candidates)}");

        var json = File.ReadAllText(path!);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("ConnectionStrings", out var _));
    }

    [Fact]
    public void Can_Read_DefaultConnection_From_Appsettings()
    {
        // Buscar appsettings.json en el directorio de salida primero. SetBasePath requiere ruta absoluta.
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "controlAsistencia", "appsettings.json")),
            Path.GetFullPath(Path.Combine("..", "controlAsistencia", "appsettings.json"))
        };

        string filePath = null!;
        foreach (var c in candidates)
        {
            if (File.Exists(c))
            {
                filePath = c;
                break;
            }
        }

        Assert.NotNull(filePath);

        var baseDir = Path.GetDirectoryName(Path.GetFullPath(filePath!));
        var config = new ConfigurationBuilder()
            .SetBasePath(baseDir!)
            .AddJsonFile(Path.GetFileName(filePath!), optional: false, reloadOnChange: false)
            .Build();

        var value = config["ConnectionStrings:DefaultConnection"];
        Assert.NotNull(value); // puede estar vacío pero la clave debe existir
    }
}
