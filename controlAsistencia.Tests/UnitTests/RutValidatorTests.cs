using Xunit;
using controlAsistencia.Validators;

namespace controlAsistencia.Tests.UnitTests;

public class RutValidatorTests
{
    [Fact]
    public void Valida_Rut_Con_Puntos()
    {
        var validator = new MarcacionValidator();
        var input = "12.345.678-5";
        var res = validator.ValidateRut(input);
        Assert.True(res.EsValido, res.Motivo);
        Assert.Equal("12345678-5", res.RutNormalizado);
    }

    [Fact]
    public void Valida_Rut_Con_K_Minusc()
    {
        var validator = new MarcacionValidator();

        // Buscamos un cuerpo cuyo digito verificador sea 'K' para probar normalización de 'k'->'K'
        string cuerpoEncontrado = null!;
        for (int n = 1000000; n < 2000000; n++)
        {
            var cuerpo = n.ToString();
            var dv = MarcacionValidator.CalcularDigitoVerificador(cuerpo);
            if (dv == "K")
            {
                cuerpoEncontrado = cuerpo;
                break;
            }
        }

        Assert.False(string.IsNullOrEmpty(cuerpoEncontrado));

        var raw = cuerpoEncontrado + "-k"; // usar k minúscula
        var res = validator.ValidateRut(raw);
        Assert.True(res.EsValido, res.Motivo);
        Assert.EndsWith("-K", res.RutNormalizado);
    }

    [Fact]
    public void Rechaza_Rut_Por_Digito_Verificador_Incorrecto()
    {
        var validator = new MarcacionValidator();
        var input = "12.345.678-9"; // 12345678 tiene dv 5
        var res = validator.ValidateRut(input);
        Assert.False(res.EsValido);
        Assert.Contains("Dígito verificador incorrecto", res.Motivo);
    }

    [Fact]
    public void Normaliza_Rut_Con_Ceros_Izquierda()
    {
        var validator = new MarcacionValidator();
        var input = "09876543-3"; // debe normalizar a 9876543-3
        var res = validator.ValidateRut(input);
        Assert.True(res.EsValido, res.Motivo);
        Assert.Equal("9876543-3", res.RutNormalizado);
    }
}
