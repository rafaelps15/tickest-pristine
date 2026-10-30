using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetAssignees;

/// <summary>
/// Lista os responsáveis elegíveis pela mesma regra da atribuição (<see cref="TicketAssigneeEligibility"/>), para a
/// lista e a atribuição nunca discordarem. O endpoint exige <c>tickets:manage</c>.
/// </summary>
internal sealed class GetTicketAssigneesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetTicketAssigneesQuery, PagedResponse<TicketAssigneeResponse>>
{
    // Palavras do termo de busca que entram no filtro; as demais são ignoradas, como na busca de atendentes.
    private const int SearchTermMaxWords = 5;

    public async Task<Result<PagedResponse<TicketAssigneeResponse>>> Handle(
        GetTicketAssigneesQuery query,
        CancellationToken cancellationToken)
    {
        // O setor é opcional: a junção à esquerda mantém quem não tem setor, e aí o nome vem nulo do banco (o "!" só
        // existe para o compilador; operador "?." não é permitido em expressões do EF).
        var assignees =
            from user in context.Users.WhereEligibleAssignee(context)
            join sector in context.Sectors on user.SectorId equals (Guid?)sector.Id into userSectors
            from sector in userSectors.DefaultIfEmpty()
            select new { User = user, SectorName = (string?)sector!.Name };

        if (query.SectorId is Guid sectorId)
        {
            assignees = assignees.Where(x => x.User.SectorId == sectorId);
        }

        // Cada palavra precisa estar no nome ou no sobrenome. As colunas são citext, então o Contains não diferencia
        // maiúsculas, e o termo vai como parâmetro (%, _ e \ contam como texto).
        IEnumerable<string> words = (query.Search ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Take(SearchTermMaxWords);

        foreach (string word in words)
        {
            assignees = assignees.Where(x => x.User.FirstName.Contains(word) || x.User.LastName.Contains(word));
        }

        PagedResponse<TicketAssigneeResponse> page = await assignees
            .OrderBy(x => x.User.FirstName)
            .ThenBy(x => x.User.LastName)
            .ThenBy(x => x.User.Id)
            .Select(x => new TicketAssigneeResponse
            {
                Id = x.User.Id,
                FullName = x.User.FirstName + " " + x.User.LastName,
                SectorId = x.User.SectorId,
                SectorName = x.SectorName
            })
            .ToPagedListAsync(query.Paging, cancellationToken);

        return page;
    }
}
