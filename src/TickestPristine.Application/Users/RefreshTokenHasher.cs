using System.Security.Cryptography;
using System.Text;

namespace TickestPristine.Application.Users;

internal static class RefreshTokenHasher
{
    /// <summary>
    /// Hash SHA-256 (hexadecimal) do refresh token, que é o valor gravado no banco. O token já é aleatório e longo,
    /// então não precisa de sal nem de iterações como a senha.
    /// </summary>
    public static string Hash(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
