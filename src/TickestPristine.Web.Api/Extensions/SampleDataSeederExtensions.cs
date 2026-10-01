using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Web.Api.Extensions;

/// <summary>
/// Popula departamentos, setores, usuários e chamados de exemplo. Uso exclusivo em desenvolvimento.
/// </summary>
public static class SampleDataSeederExtensions
{
    private const string SamplePassword = "Password123!";

    private static readonly (string Name, string Description, string[] SectorNames)[] DepartmentSeeds =
    [
        ("Tecnologia da Informação", "Suporte técnico e infraestrutura de TI", ["Helpdesk", "Infraestrutura"]),
        ("Recursos Humanos", "Gestão de pessoas e benefícios", ["Recrutamento", "Departamento Pessoal"]),
        ("Financeiro", "Contas a pagar, receber e contabilidade", ["Contas a Pagar", "Contabilidade"]),
        ("Comercial", "Vendas e relacionamento com clientes", ["Vendas", "Atendimento ao Cliente"]),
        ("Facilities", "Manutenção predial e logística", ["Manutenção", "Logística"])
    ];

    private static readonly (string Email, string FirstName, string LastName)[] RequesterSeeds =
    [
        ("maria.silva@tickestpristine.dev", "Maria", "Silva"),
        ("joao.santos@tickestpristine.dev", "João", "Santos"),
        ("ana.costa@tickestpristine.dev", "Ana", "Costa")
    ];

    private static readonly (string Title, string Description, TicketPriority Priority, TicketStatus Status, string Sector, int Requester)[] TicketSeeds =
    [
        ("Impressora não funciona", "A impressora do 3º andar não está imprimindo.", TicketPriority.Medium, TicketStatus.Open, "Helpdesk", 0),
        ("Computador não liga", "O computador da recepção não liga desde ontem.", TicketPriority.High, TicketStatus.InProgress, "Helpdesk", 1),
        ("Servidor de e-mail fora do ar", "Ninguém está recebendo e-mails desde as 9h.", TicketPriority.Urgent, TicketStatus.Resolved, "Infraestrutura", 2),
        ("Solicitação de nova vaga", "Precisamos abrir uma vaga para desenvolvedor.", TicketPriority.Low, TicketStatus.Open, "Recrutamento", 0),
        ("Dúvida sobre férias", "Gostaria de entender o cálculo das minhas férias.", TicketPriority.Medium, TicketStatus.Open, "Departamento Pessoal", 1),
        ("Pagamento de fornecedor atrasado", "O fornecedor XPTO ainda não recebeu o pagamento do mês.", TicketPriority.High, TicketStatus.InProgress, "Contas a Pagar", 2),
        ("Cliente solicitando desconto", "Cliente grande pedindo desconto na renovação do contrato.", TicketPriority.Medium, TicketStatus.Open, "Vendas", 0),
        ("Ar condicionado com vazamento", "O ar condicionado da sala 3 está vazando água.", TicketPriority.Low, TicketStatus.Closed, "Manutenção", 1)
    ];

    public static async Task SeedSampleDataAsync(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        ApplicationDbContext context = services.GetRequiredService<ApplicationDbContext>();
        IPasswordHasher passwordHasher = services.GetRequiredService<IPasswordHasher>();

        if (await context.Departments.AnyAsync())
        {
            return;
        }

        Guid defaultRoleId = await context.Roles
            .Where(r => r.IsDefault)
            .Select(r => r.Id)
            .SingleAsync();

        var sectorsByName = new Dictionary<string, Sector>();

        foreach ((string name, string description, string[] sectorNames) in DepartmentSeeds)
        {
            var department = new Department { Id = Guid.NewGuid(), Name = name, Description = description, IsActive = true };

            department.Raise(new DepartmentCreatedDomainEvent(department.Id));
            context.Departments.Add(department);

            foreach (string sectorName in sectorNames)
            {
                var sector = new Sector { Id = Guid.NewGuid(), Name = sectorName, DepartmentId = department.Id, IsActive = true };

                sector.Raise(new SectorCreatedDomainEvent(sector.Id));
                sectorsByName[sectorName] = sector;
                context.Sectors.Add(sector);
            }
        }

        var requesters = new List<User>();

        foreach ((string email, string firstName, string lastName) in RequesterSeeds)
        {
            var requester = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                Code = $"usr_{Ulid.NewUlid()}"
            };

            requester.Raise(new UserRegisteredDomainEvent(requester.Id));

            context.Users.Add(requester);
            context.UserCredentials.Add(new UserCredential { Id = Guid.NewGuid(), UserId = requester.Id, PasswordHash = passwordHasher.Hash(SamplePassword) });
            context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = requester.Id, RoleId = defaultRoleId });

            requesters.Add(requester);
        }

        foreach ((string title, string description, TicketPriority priority, TicketStatus status, string sectorName, int requesterIndex) in TicketSeeds)
        {
            User requester = requesters[requesterIndex];
            Sector sector = sectorsByName[sectorName];

            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                Title = title,
                Description = description,
                Priority = priority,
                Status = TicketStatus.Open,
                CreatedByUserId = requester.Id,
                AssignedToUserId = null,
                SectorId = sector.Id,
                CreatedAtUtc = DateTime.UtcNow
            };

            ticket.Raise(new TicketCreatedDomainEvent(ticket.Id));

            if (status != TicketStatus.Open)
            {
                ticket.Status = status;

                ticket.Raise(new TicketStatusChangedDomainEvent(ticket.Id, TicketStatus.Open, status, null));
            }

            context.Tickets.Add(ticket);
        }

        await context.SaveChangesAsync();
    }
}
