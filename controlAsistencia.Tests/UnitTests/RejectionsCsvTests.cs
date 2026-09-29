using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using controlAsistencia.Services;
using controlAsistencia.Models;

namespace controlAsistencia.Tests.UnitTests;

public class RejectionsCsvTests
{
    [Fact]
    public async Task WritesHeaderOnly_WhenNoRejections()
    {
        var svc = new ImporterService();
        var tmp = Path.Combine(Path.GetTempPath(), $"rechazos_{Guid.NewGuid()}.csv");
        try
        {
            var result = new ImportProcessResult();
            await svc.WriteRejectionsCsvAsync(result, tmp);
            Assert.True(File.Exists(tmp));
            var content = await File.ReadAllTextAsync(tmp);
            Assert.Equal("linea;rut;motivo\n", content.Replace("\r\n", "\n"));
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public async Task WritesOneRejection_PreservesLineNumberAndRut()
    {
        var svc = new ImporterService();
        var tmp = Path.Combine(Path.GetTempPath(), $"rechazos_{Guid.NewGuid()}.csv");
        try
        {
            var result = new ImportProcessResult();
            result.Rejections.Add(new RejectionEntry { LineNumber = 3, Rut = "12345678-9", Motivo = "RUT inválido" });
            await svc.WriteRejectionsCsvAsync(result, tmp);
            var lines = await File.ReadAllLinesAsync(tmp);
            Assert.Equal("linea;rut;motivo", lines[0]);
            Assert.Equal("3;12345678-9;RUT inválido", lines[1]);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
	
	[Fact]
	public async Task EscapesFields_WithSemicolonAndQuotesAndNewline()
	{
		var svc = new ImporterService();
		var tmp = Path.Combine(
			Path.GetTempPath(),
			$"rechazos_{Guid.NewGuid()}.csv");
	
		try
		{
			var result = new ImportProcessResult();
	
			var motivo = "Texto con; punto y coma \"comillas\" y \n salto";
	
			result.Rejections.Add(new RejectionEntry
			{
				LineNumber = 5,
				Rut = "09876543-3",
				Motivo = motivo
			});
	
			await svc.WriteRejectionsCsvAsync(result, tmp);
	
			var content = await File.ReadAllTextAsync(tmp);
	
			// Normaliza los saltos de línea para Windows/Linux.
			content = content.Replace("\r\n", "\n");
	
			var expected = "linea;rut;motivo\n" +
					"5;09876543-3;\"Texto con; punto y coma \"\"comillas\"\" y \\n salto\"\n";
	
			Assert.Equal(expected, content);
		}
		finally
		{
			if (File.Exists(tmp))
			{
				File.Delete(tmp);
			}
		}
	}
}
