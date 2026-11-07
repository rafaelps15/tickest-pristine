namespace TickestPristine.Infrastructure.Users;

// Linha da view public.user_permissions: uma permissão que alguma das funções do usuário concede.
internal sealed record UserPermission(Guid UserId, string PermissionCode);
