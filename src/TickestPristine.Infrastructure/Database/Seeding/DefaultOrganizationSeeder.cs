using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;

namespace TickestPristine.Infrastructure.Database.Seeding;

/// <summary>
/// Estrutura inicial de departamentos e setores, para o sistema já nascer com onde abrir chamados. Liga com
/// "Seeding:DefaultOrganization" (ligado por padrão) e só roda num banco sem departamentos: depois disso a estrutura
/// é do administrador, e o seed não recria o que ele editou ou desativou.
/// </summary>
internal sealed class DefaultOrganizationSeeder(IOptions<SeedingOptions> seedingOptions)
{
    private static readonly DepartmentSeed[] Departments =
    [
        new("TI", "Tecnologia da Informação: equipamentos, sistemas, redes e acessos.",
        [
            new("Helpdesk", "Computadores, impressoras, acessos e softwares de uso diário."),
            new("Infraestrutura", "Internet, Wi-Fi, servidores, VPN e telefonia.")
        ]),
        new("RH", "Recursos Humanos: vida funcional dos colaboradores.",
        [
            new("Recrutamento", "Vagas, processos seletivos e admissões."),
            new("Departamento Pessoal", "Folha de pagamento, benefícios, férias e ponto.")
        ]),
        new("Financeiro", "Pagamentos, recebimentos e registros contábeis.",
        [
            new("Contas a Pagar", "Pagamento de fornecedores, boletos e reembolsos."),
            new("Contabilidade", "Lançamentos, notas fiscais e fechamento contábil.")
        ]),
        new("Comercial", "Vendas e relacionamento com clientes.",
        [
            new("Vendas", "Propostas, pedidos e contratos."),
            new("Atendimento ao Cliente", "Dúvidas, reclamações e solicitações de clientes.")
        ]),
        new("Facilities", "Estrutura física, materiais e serviços gerais.",
        [
            new("Manutenção", "Elétrica, hidráulica, ar-condicionado e reparos."),
            new("Logística", "Entregas, estoque, correspondência e transporte.")
        ])
    ];

    public async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        if (!seedingOptions.Value.DefaultOrganization || await context.Departments.AnyAsync(cancellationToken))
        {
            return;
        }

        foreach (DepartmentSeed departmentSeed in Departments)
        {
            var department = departmentSeed.ToDepartment();

            context.Departments.Add(department);
            context.Sectors.AddRange(departmentSeed.Sectors.Select(sectorSeed => sectorSeed.ToSector(department.Id)));
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private sealed record DepartmentSeed(string Name, string Description, SectorSeed[] Sectors)
    {
        public Department ToDepartment() =>
            new() { Id = Guid.NewGuid(), Name = Name, Description = Description, IsActive = true };
    }

    private sealed record SectorSeed(string Name, string Description)
    {
        public Sector ToSector(Guid departmentId) =>
            new() { Id = Guid.NewGuid(), Name = Name, Description = Description, IsActive = true, DepartmentId = departmentId };
    }
}
