using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.GetAttendants;

/// <summary>
/// Lista os atendentes elegíveis (<see cref="TicketAssigneeEligibility"/>). O endpoint exige só usuário ativo: a
/// permissão <c>users:read</c> é conferida aqui, para a falta dela responder 403 com mensagem de negócio.
/// </summary>
internal sealed class GetBacklogAttendantsQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : IQueryHandler<GetBacklogAttendantsQuery, PagedResponse<AttendantResponse>>
{
    // Tamanho aceito do termo de busca, depois do Trim.
    private const int SearchTermMinLength = 2;
    private const int SearchTermMaxLength = 100;

    // Palavras do termo de busca que entram no filtro; as demais são ignoradas.
    private const int SearchTermMaxWords = 5;

    public async Task<Result<PagedResponse<AttendantResponse>>> Handle(
        GetBacklogAttendantsQuery query,
        CancellationToken cancellationToken)
    {
        // Termo vazio conta como ausente; só com espaços, não.
        if (!string.IsNullOrEmpty(query.SearchTerm) &&
            query.SearchTerm.Trim().Length is < SearchTermMinLength or > SearchTermMaxLength)
        {
            return Result.Failure<PagedResponse<AttendantResponse>>(UserErrors.InvalidSearchTerm(
                SearchTermMinLength,
                SearchTermMaxLength));
        }

        bool canReadUsers = await permissionProvider.HasPermissionAsync(
            userContext.UserId,
            PermissionCodes.Users.Read,
            cancellationToken);

        if (!canReadUsers)
        {
            return Result.Failure<PagedResponse<AttendantResponse>>(UserErrors.ReadForbidden);
        }

        // Setor e departamento são opcionais: as junções à esquerda mantêm quem não tem setor, e aí os nomes vêm nulos
        // do banco (o "!" só existe para o compilador; operador "?." não é permitido em expressões do EF).
        var attendants =
            from user in context.Users.WhereEligibleAssignee(context)
            join sector in context.Sectors on user.SectorId equals (Guid?)sector.Id into userSectors
            from sector in userSectors.DefaultIfEmpty()
            join department in context.Departments on sector!.DepartmentId equals department.Id into sectorDepartments
            from department in sectorDepartments.DefaultIfEmpty()
            select new
            {
                User = user,
                SectorName = (string?)sector!.Name,
                DepartmentName = (string?)department!.Name
            };

        if (query.SectorId is Guid sectorId)
        {
            attendants = attendants.Where(x => x.User.SectorId == sectorId);
        }

        // Cada palavra precisa estar em algum dos campos. As colunas são citext, então o Contains não diferencia
        // maiúsculas, e o termo vai como parâmetro (%, _ e \ contam como texto).
        IEnumerable<string> words = (query.SearchTerm ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Take(SearchTermMaxWords);

        foreach (string word in words)
        {
            attendants = attendants.Where(x =>
                x.User.FirstName.Contains(word) ||
                x.User.LastName.Contains(word) ||
                x.User.Email.Contains(word) ||
                x.SectorName != null && x.SectorName.Contains(word) ||
                x.DepartmentName != null && x.DepartmentName.Contains(word));
        }

        var ordered = query.PrioritySectorId is Guid prioritySectorId
            ? attendants.OrderBy(x => x.User.SectorId == prioritySectorId ? 0 : 1).ThenBy(x => x.User.FirstName)
            : attendants.OrderBy(x => x.User.FirstName);

        PagedResponse<AttendantResponse> page = await ordered
            .ThenBy(x => x.User.LastName)
            .ThenBy(x => x.User.Id)
            .Select(x => new AttendantResponse
            {
                Id = x.User.Id,
                FullName = x.User.FirstName + " " + x.User.LastName,
                Email = x.User.Email,
                SectorId = x.User.SectorId,
                SectorName = x.SectorName,
                DepartmentName = x.DepartmentName
            })
            .ToPagedListAsync(query.Paging, cancellationToken);

        return page;
    }
}
