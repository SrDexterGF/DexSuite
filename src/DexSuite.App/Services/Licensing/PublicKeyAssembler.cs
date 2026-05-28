using System.Security.Cryptography;
using DexSuite.App.Services.Licensing.Keys;

namespace DexSuite.App.Services.Licensing;

/// <summary>
/// Ensambla la clave pública RSA-2048 a partir de las 4 partes embebidas en
/// archivos separados (<see cref="KeyPartA"/>..<see cref="KeyPartD"/>).
///
/// Diseño: partir la clave reduce la facilidad con la que un atacante puede
/// sustituirla parchéando un único string en el binario tras ofuscación.
/// Para reemplazarla hay que encontrar las 4 partes Y el orden en que se
/// concatenan. El XML resultante se importa con <see cref="RSA.ImportFromPem"/>
/// (formato SPKI).
/// </summary>
internal static class PublicKeyAssembler
{
    /// <summary>
    /// Devuelve un <see cref="RSA"/> listo para verificar firmas, o
    /// <c>null</c> si las partes están vacías (todavía no se ha corrido
    /// <c>DexSuite.KeyGen init</c>).
    /// </summary>
    public static RSA? TryCreatePublicKey()
    {
        // Concatenación: A + B + C + D. Si los 4 son cadena vacía → no configurado.
        var combined = string.Concat(
            KeyPartA.Value,
            KeyPartB.Value,
            KeyPartC.Value,
            KeyPartD.Value);

        if (string.IsNullOrWhiteSpace(combined))
            return null;

        try
        {
            // El formato esperado es Base64 de la clave SubjectPublicKeyInfo (DER).
            var rsa = RSA.Create();
            var spkiBytes = Convert.FromBase64String(combined);
            rsa.ImportSubjectPublicKeyInfo(spkiBytes, out _);
            return rsa;
        }
        catch
        {
            // Cualquier corrupción / formato inválido → tratamos como no configurada.
            return null;
        }
    }

    /// <summary>True si las 4 partes contienen datos (no necesariamente válidos).</summary>
    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(KeyPartA.Value) &&
        !string.IsNullOrWhiteSpace(KeyPartB.Value) &&
        !string.IsNullOrWhiteSpace(KeyPartC.Value) &&
        !string.IsNullOrWhiteSpace(KeyPartD.Value);
}
