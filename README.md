# TickestPristine

## Extended Description

TickestPristine é um backend de sistema de chamados (helpdesk/ticketing) construído em **.NET 10** seguindo os princípios de **Clean Architecture**. O projeto nasceu a partir de um template genérico de Clean Architecture, que foi renomeado e evoluído para implementar um domínio de negócio real: abertura, atribuição e acompanhamento de chamados técnicos, com controle de acesso granular por permissão, autenticação JWT, cache, logging estruturado e observabilidade já integrados desde a base.

## Funcionalidades Principais

- Abertura, atualização, reabertura e exclusão (soft-delete) de chamados (tickets)
- Máquina de estados do chamado (`Open → InProgress → Resolved → Closed`, com cancelamento); quem atende a fila fecha os chamados resolvidos (`POST /tickets/{id}/close`)
- Atribuição de responsável na abertura e depois dela (`PUT /tickets/{id}/assignee`), sempre a um usuário ativo que atende chamados, com registro no histórico
- Chamados organizados por departamento e setor, com mensagens, anexos, histórico e SLA de resolução por prioridade
- Autenticação via JWT com rotação de refresh tokens, bloqueio temporário por senhas erradas e troca obrigatória da senha provisória
- Autorização por permissões agrupadas em funções (roles) fixas — Administrador, Atendente e Colaborador —, com as permissões de Atendente e Colaborador editáveis pela API
- Primeiro administrador criado automaticamente a partir da configuração `Admin` (com senha provisória), quando o banco não tem nenhum administrador ativo
- Gestão de usuários (cadastro por quem tem `users:create`, consulta, edição de perfil, desativação e reativação, atribuição de funções); novos usuários recebem a função Colaborador
- Catálogo de permissões consultável via API (`GET /permissions`)
- Departamentos e setores como dados de apoio para categorização dos chamados
- Log de auditoria das alterações (`GET /audit-logs/{entityName}/{entityId}`)

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

Seq (structured log viewer) is available at http://localhost:8090 once `docker compose up -d` is running.

PostgreSQL is published on host port **5440** (not 5432) and containers are prefixed `tickestpristine-`, so this stack can run alongside other local projects without port/name clashes. Connect from DBeaver/psql with `localhost:5440`, database `tickestpristine`, user/password `postgres`.

To target .NET 8 or .NET 9 instead of .NET 10, see the notes in `Directory.Build.props`.

## Testing

- Architecture testing (`TickestPristine.ArchitectureTests`)
- Unit testing (`TickestPristine.Application.UnitTests`)
- Integration testing with **Testcontainers** (`TickestPristine.IntegrationTests`)
