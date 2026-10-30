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
        new("TI", "Tecnologia da Informação: equipamentos, sistemas, redes, acessos e segurança.",
        [
            new("Suporte ao Usuário", "Senhas, computadores, impressoras, periféricos e softwares de uso diário."),
            new("Infraestrutura e Redes", "Internet, Wi-Fi, VPN, servidores, pontos de rede e telefonia."),
            new("Sistemas Corporativos", "ERP, CRM e sistemas internos: acessos, perfis, erros e relatórios."),
            new("Segurança da Informação", "Phishing, liberação de sites, perda de equipamentos e incidentes de segurança.")
        ]),
        new("RH", "Recursos Humanos: vida funcional, benefícios e desenvolvimento dos colaboradores.",
        [
            new("Departamento Pessoal", "Folha de pagamento, ponto, férias e documentos funcionais."),
            new("Recrutamento e Seleção", "Abertura de vagas, processos seletivos e admissões."),
            new("Benefícios", "Plano de saúde, odontológico, vale-transporte e vale-refeição."),
            new("Treinamento e Desenvolvimento", "Cursos, treinamentos obrigatórios e avaliação de desempenho.")
        ]),
        new("Financeiro", "Pagamentos, recebimentos, impostos e registros contábeis.",
        [
            new("Contas a Pagar", "Pagamento de fornecedores, boletos e reembolsos."),
            new("Contas a Receber", "Faturamento, cobrança, boletos e baixa de recebimentos."),
            new("Contabilidade e Fiscal", "Lançamentos, notas fiscais, impostos e fechamento contábil.")
        ]),
        new("Compras e Suprimentos", "Aquisição de materiais e serviços, estoque e patrimônio.",
        [
            new("Compras", "Cotações e aquisição de materiais, equipamentos, licenças e serviços."),
            new("Almoxarifado e Patrimônio", "Material de escritório, estoque, transferência e baixa de bens.")
        ]),
        new("Facilities", "Estrutura física, segurança, limpeza e serviços gerais.",
        [
            new("Manutenção Predial", "Elétrica, hidráulica, ar-condicionado, civil e pequenos reparos."),
            new("Limpeza e Conservação", "Limpeza de ambientes, reposição de insumos e limpezas extras."),
            new("Portaria e Controle de Acesso", "Crachás, catracas, visitantes, chaves e armários."),
            new("Logística e Correspondência", "Envios, recebimentos, mudanças de layout e veículos da frota.")
        ]),
        new("Jurídico", "Contratos, proteção de dados e orientação jurídica.",
        [
            new("Contratos", "Elaboração, revisão, aditivos e renovação de contratos e NDAs."),
            new("Privacidade e LGPD", "Pedidos de titulares de dados, incidentes com dados pessoais e orientação.")
        ]),
        new("Comercial", "Vendas e relacionamento com clientes.",
        [
            new("Vendas", "Propostas, pedidos, descontos e cadastro de clientes."),
            new("Atendimento ao Cliente", "Dúvidas, reclamações, trocas e cancelamentos de clientes.")
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
            context.Sectors.AddRange(departmentSeed.Sectors.Select(sectorSeed => sectorSeed.ToSector(department)));
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private sealed record DepartmentSeed(string Name, string Description, SectorSeed[] Sectors)
    {
        public Department ToDepartment() =>
            Department.Create(Name, Description, responsibleUser: null);
    }

    private sealed record SectorSeed(string Name, string Description)
    {
        public Sector ToSector(Department department) =>
            Sector.Create(department, Name, Description);
    }
}
