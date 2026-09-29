using Xunit;
using controlAsistencia.Validators;
using controlAsistencia.Models;

namespace controlAsistencia.Tests.UnitTests;

public class TipoValidatorTests
{
    [Fact]
    public void Tipo_E_Valido()
    {
        var validator = new MarcacionValidator();
        var row = new MarcacionCsvRow { Rut = "12345678-5", Tipo = "E", Origen = "X", FechaHora = default, LineNumber = 2 };
        var res = validator.ValidateMarcacionRow(row);
        Assert.True(res.IsValid, string.Join(';', res.Errors));
    }

    [Fact]
    public void Tipo_S_Valido()
    {
        var validator = new MarcacionValidator();
        var row = new MarcacionCsvRow { Rut = "12345678-5", Tipo = "S", Origen = "X", FechaHora = default, LineNumber = 2 };
        var res = validator.ValidateMarcacionRow(row);
        Assert.True(res.IsValid, string.Join(';', res.Errors));
    }

    [Fact]
    public void Tipo_Otro_Invalido()
    {
        var validator = new MarcacionValidator();
        var row = new MarcacionCsvRow { Rut = "12345678-5", Tipo = "A", Origen = "X", FechaHora = default, LineNumber = 2 };
        var res = validator.ValidateMarcacionRow(row);
        Assert.False(res.IsValid);
        Assert.Contains("Tipo inválido", string.Join(';', res.Errors));
    }

    [Fact]
    public void Tipo_Vacio_Invalido()
    {
        var validator = new MarcacionValidator();
        var row = new MarcacionCsvRow { Rut = "12345678-5", Tipo = "", Origen = "X", FechaHora = default, LineNumber = 2 };
        var res = validator.ValidateMarcacionRow(row);
        Assert.False(res.IsValid);
        Assert.Contains("Tipo vacío", string.Join(';', res.Errors));
    }
}
