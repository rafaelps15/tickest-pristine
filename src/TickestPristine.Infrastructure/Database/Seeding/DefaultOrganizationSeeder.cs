using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Infrastructure.Database.Seeding;

/// <summary>
/// Estrutura inicial de departamentos e setores, para o sistema já nascer com onde abrir chamados.
/// Só roda num banco sem departamentos: depois disso a estrutura é do administrador, que edita, desativa
/// ou cadastra o que quiser, e o seed não recria o que ele tirou.
/// </summary>
internal static class DefaultOrganizationSeeder
{
    private static readonly DepartmentSeed[] Catalog =
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

    public static void Seed(DbContext context)
    {
        if (context.Set<Department>().Any())
        {
            return;
        }

        AddCatalog(context);

        context.SaveChanges();
    }

    public static async Task SeedAsync(DbContext context, CancellationToken cancellationToken)
    {
        if (await context.Set<Department>().AnyAsync(cancellationToken))
        {
            return;
        }

        AddCatalog(context);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static void AddCatalog(DbContext context)
    {
        foreach (DepartmentSeed departmentSeed in Catalog)
        {
            var department = new Department
            {
                Id = Guid.NewGuid(),
                Name = departmentSeed.Name,
                Description = departmentSeed.Description,
                IsActive = true
            };

            context.Add(department);

            foreach (SectorSeed sectorSeed in departmentSeed.Sectors)
            {
                context.Add(new Sector
                {
                    Id = Guid.NewGuid(),
                    Name = sectorSeed.Name,
                    Description = sectorSeed.Description,
                    IsActive = true,
                    DepartmentId = department.Id
                });
            }
        }
    }

    private sealed record DepartmentSeed(string Name, string Description, SectorSeed[] Sectors);

    private sealed record SectorSeed(string Name, string Description);
}
