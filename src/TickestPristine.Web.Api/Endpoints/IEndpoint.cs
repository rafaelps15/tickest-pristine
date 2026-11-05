namespace TickestPristine.Web.Api.Endpoints;

/// <summary>
/// Uma rota da minimal API: cada classe mapeia a sua, e a varredura do assembly registra todas.
/// </summary>
public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
