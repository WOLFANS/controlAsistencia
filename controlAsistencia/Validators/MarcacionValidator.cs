using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace controlAsistencia.Validators;

/// <summary>
/// Resultado de la validación/normalización de un RUT.
/// </summary>
public class RutValidationResult
{
    public bool EsValido { get; set; }
    public string RutNormalizado { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
}

/// <summary>
/// Resultado de la validación local de una fila de marcación (RUT + Tipo).
/// </summary>
public class MarcacionRowValidationResult
{
    public bool IsValid { get; set; } = false;
    public List<string> Errors { get; } = new List<string>();
    public RutValidationResult? RutResult { get; set; }
}

/// <summary>
/// Validador y normalizador de RUTs chilenos (algoritmo Módulo 11).
/// Mantiene la lógica independiente de acceso a datos para poder probarla con xUnit.
/// </summary>
public class MarcacionValidator
{
    private static readonly Regex OnlyDigits = new("^\\d+$");

    /// <summary>
    /// Valida a nivel local una fila de marcación (MarcacionCsvRow).
    /// Comprueba el RUT (usando ValidateRut) y el campo Tipo (solo 'E' o 'S').
    /// No valida fecha ni existencia del trabajador en base de datos.
    /// </summary>
    public MarcacionRowValidationResult ValidateMarcacionRow(Models.MarcacionCsvRow row)
    {
        var res = new MarcacionRowValidationResult();

        if (row == null)
        {
            res.IsValid = false;
            res.Errors.Add("Fila nula");
            return res;
        }

        // Validar RUT
        var rutRes = ValidateRut(row.Rut);
        res.RutResult = rutRes;
        if (!rutRes.EsValido)
        {
            res.IsValid = false;
            res.Errors.Add($"RUT inválido: {rutRes.Motivo}");
        }

        // Validar Tipo: solo 'E' o 'S'
        if (string.IsNullOrWhiteSpace(row.Tipo))
        {
            res.IsValid = false;
            res.Errors.Add("Tipo vacío");
        }
        else
        {
            var t = row.Tipo.Trim().ToUpperInvariant();
            if (t != "E" && t != "S")
            {
                res.IsValid = false;
                res.Errors.Add($"Tipo inválido: '{row.Tipo}'");
            }
        }

        if (res.Errors.Count == 0)
        {
            res.IsValid = true;
        }

        return res;
    }

    /// <summary>
    /// Normaliza un RUT de entrada y calcula si el dígito verificador es correcto.
    /// La normalización devuelve el formato: [cuerpo-sin-puntos]-[DV en mayúscula], sin ceros iniciales en el cuerpo.
    /// </summary>
    public RutValidationResult ValidateRut(string? raw)
    {
        var res = new RutValidationResult();

        if (string.IsNullOrWhiteSpace(raw))
        {
            res.EsValido = false;
            res.Motivo = "RUT vacío";
            return res;
        }

        var s = raw.Trim();

        // Eliminar espacios y puntos
        s = s.Replace(".", string.Empty).Replace(" ", string.Empty);

        // Aceptar tanto con guion como sin guion; el último carácter es el dígito verificador
        string cuerpo;
        string dv;
        if (s.Contains('-'))
        {
            var parts = s.Split('-');
            if (parts.Length != 2)
            {
                res.EsValido = false;
                res.Motivo = "Formato de RUT inválido";
                return res;
            }
            cuerpo = parts[0];
            dv = parts[1];
        }
        else
        {
            if (s.Length < 2)
            {
                res.EsValido = false;
                res.Motivo = "RUT sin dígito verificador";
                return res;
            }
            cuerpo = s.Substring(0, s.Length - 1);
            dv = s.Substring(s.Length - 1, 1);
        }

        if (string.IsNullOrWhiteSpace(cuerpo) || string.IsNullOrWhiteSpace(dv))
        {
            res.EsValido = false;
            res.Motivo = "RUT incompleto";
            return res;
        }

        // El cuerpo debe contener solo dígitos
        if (!OnlyDigits.IsMatch(cuerpo))
        {
            res.EsValido = false;
            res.Motivo = "Cuerpo del RUT contiene caracteres inválidos";
            return res;
        }

        // Normalizar cuerpo: eliminar ceros a la izquierda
        cuerpo = cuerpo.TrimStart('0');
        if (string.IsNullOrEmpty(cuerpo))
        {
            // En caso de que el cuerpo quede vacío, dejar "0"
            cuerpo = "0";
        }

        // Normalizar DV
        dv = dv.ToUpperInvariant();

        if (!(OnlyDigits.IsMatch(dv) || dv == "K"))
        {
            res.EsValido = false;
            res.Motivo = "Dígito verificador inválido";
            return res;
        }

        // Calcular dígito verificador mediante módulo 11
        var esperado = CalcularDigitoVerificador(cuerpo);

        if (esperado != dv)
        {
            res.EsValido = false;
            res.Motivo = $"Dígito verificador incorrecto (esperado {esperado})";
            // también devolver la forma normalizada propuesta
            res.RutNormalizado = $"{cuerpo}-{dv}";
            return res;
        }

        res.EsValido = true;
        res.RutNormalizado = $"{cuerpo}-{esperado}";
        return res;
    }

    /// <summary>
    /// Calcula el dígito verificador ("0"-"9" o "K") para el cuerpo del RUT (sin puntos, sin guion).
    /// Algoritmo: suma productos por factores 2..7 repetidos desde la derecha, dv = 11 - (suma % 11)
    /// Si dv == 11 => '0', si dv == 10 => 'K', else dv.ToString()
    /// </summary>
    public static string CalcularDigitoVerificador(string cuerpoSinPuntos)
    {
        // Recorrer de derecha a izquierda
        int multiplicador = 2;
        int suma = 0;
        for (int i = cuerpoSinPuntos.Length - 1; i >= 0; i--)
        {
            var c = cuerpoSinPuntos[i];
            if (!char.IsDigit(c)) throw new ArgumentException("El cuerpo del RUT debe contener solo dígitos.");
            int dig = c - '0';
            suma += dig * multiplicador;
            multiplicador++;
            if (multiplicador > 7) multiplicador = 2;
        }

        int resto = suma % 11;
        int dv = 11 - resto;
        if (dv == 11) return "0";
        if (dv == 10) return "K";
        return dv.ToString(CultureInfo.InvariantCulture);
    }
}
