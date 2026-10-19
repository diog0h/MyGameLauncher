using System.Security.Cryptography;
using System.Text;

namespace GameLauncher.Services;

/// <summary>
/// Cifra pequenos segredos (ex.: a chave do SteamGridDB) com a proteção de dados do Windows (DPAPI):
/// só a tua conta do Windows, neste PC, os consegue ler.
/// </summary>
public static class Secrets
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MyGameLauncher/diog0_h");

    public static string? Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return null;
        try
        {
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(bytes);
        }
        catch (Exception ex)
        {
            AppLog.Error("Não foi possível cifrar um segredo", ex);
            return null;
        }
    }

    public static string? Unprotect(string? protectedBase64)
    {
        if (string.IsNullOrEmpty(protectedBase64)) return null;
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedBase64), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex)
        {
            // Ex.: dados copiados de outro PC ou de outra conta do Windows.
            AppLog.Error("Não foi possível decifrar um segredo", ex);
            return null;
        }
    }
}
