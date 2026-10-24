using System.Security.Cryptography;
using System.Text;

namespace TickestPristine.Infrastructure.Database;

/// <summary>
/// Id fixo derivado de um texto, para os registros criados pelo HasData (roles, permissões e os vínculos padrão):
/// o mesmo texto gera sempre o mesmo Id, então as migrations não veem mudança a cada build. São os 16 primeiros bytes
/// do SHA-256 do texto em UTF-8, lidos como texto hexadecimal; a migration Add_Ids_To_Roles_And_Permissions calcula
/// o mesmo valor no PostgreSQL com encode(substring(sha256(...) from 1 for 16), 'hex')::uuid.
/// </summary>
internal static class DeterministicGuid
{
    public static Guid Create(string value) =>
        Guid.ParseExact(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)), 0, 16), "N");
}
