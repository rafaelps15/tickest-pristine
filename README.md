# TickestPristine

## Extended Description

TickestPristine é um backend de sistema de chamados (helpdesk/ticketing) construído em **.NET 10** seguindo os princípios de **Clean Architecture**. O projeto nasceu a partir de um template genérico de Clean Architecture, que foi renomeado e evoluído para implementar um domínio de negócio real: abertura, atribuição e acompanhamento de chamados técnicos, com controle de acesso granular por permissão, autenticação JWT, cache, logging estruturado e observabilidade já integrados desde a base.

## Funcionalidades Principais

- Abertura, atualização, reabertura e exclusão (soft-delete) de chamados (tickets)
- Máquina de estados do chamado (`Open → InProgress → Resolved → Closed`, com cancelamento)
- Atribuição de chamados a responsáveis, organizados por departamento e setor
- Autenticação via JWT com rotação de refresh tokens
- Autorização por permissão atribuída diretamente a cada usuário (sem papéis fixos — flexível e auditável)
- Usuário `AdminMaster` provisionado automaticamente no primeiro start, com todas as permissões
- Gestão de usuários (registro, exclusão, consulta) com permissões mínimas concedidas por padrão
- Catálogo de permissões consultável via API (`GET /permissions`)
- Departamentos e setores como dados de apoio para categorização dos chamados

## Tecnologias Utilizadas

- **.NET 10 / C#** — ASP.NET Core Minimal APIs
- **Entity Framework Core** + **PostgreSQL** (convenção snake_case)
- **FluentValidation** para validação de comandos/queries
- **JWT Bearer Authentication**
- **HybridCache** — cache unificado em memória/distribuído
- **Serilog** + **Seq** — logging estruturado
- **OpenTelemetry** — tracing e métricas
- **Rate Limiting** nativo do ASP.NET Core
- **xUnit**, **NSubstitute**, **Shouldly** — testes unitários
- **Testcontainers** — testes de integração com PostgreSQL real
- **NetArchTest** — testes de arquitetura (limites entre camadas)
- **Docker** / **Docker Compose**

## Objetivos do Projeto

- Servir como base sólida e extensível para um sistema de chamados real, pronto para evoluir em produção
- Aplicar Clean Architecture de forma pragmática, sem over-engineering
- Garantir separação estrita de camadas (Domain, Application, Infrastructure, Web.Api), validada automaticamente por testes de arquitetura
- Oferecer um modelo de autorização flexível, granular e fácil de auditar
- Servir de referência para acelerar a criação de novas funcionalidades seguindo os mesmos padrões

## Getting started

```bash
docker compose up -d        # PostgreSQL + Seq
dotnet run --project src/TickestPristine.Web.Api
```

Run the full test suite (the integration tests spin up a throwaway PostgreSQL container, so
Docker must be running):

```bash
dotnet test TickestPristine.slnx
```

Seq (structured log viewer) is available at http://localhost:8081 once `docker compose up -d` is running.

To target .NET 8 or .NET 9 instead of .NET 10, see the notes in `Directory.Build.props`.

## Testing

- Architecture testing (`TickestPristine.ArchitectureTests`)
- Unit testing (`TickestPristine.Application.UnitTests`)
- Integration testing with **Testcontainers** (`TickestPristine.IntegrationTests`)
