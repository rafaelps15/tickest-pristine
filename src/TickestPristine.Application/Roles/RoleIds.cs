namespace TickestPristine.Application.Roles;

/// <summary>
/// Ids das roles fixas, os mesmos que o HasData grava em todos os ambientes (<c>DeterministicGuid</c> de
/// <c>role:{nome}</c>, na Infrastructure). As regras das roles fixas usam o id, que não muda; o nome em
/// <see cref="RoleNames"/> fica para a exibição e as seeds. O teste de seed confere que os dois continuam iguais.
/// </summary>
public static class RoleIds
{
    public static readonly Guid Administrator = new("f0614f12-a27a-3667-406d-22e22fe33e86");
    public static readonly Guid Agent = new("726e4001-4a5f-44ee-bb07-9cd652d3ac12");
    public static readonly Guid Collaborator = new("3a989d00-fc17-bef6-ea31-c01d46507743");

    /// <summary>
    /// As três roles fixas, que não podem ser renomeadas nem excluídas.
    /// </summary>
    public static readonly IReadOnlyList<Guid> System = [Administrator, Agent, Collaborator];
}
