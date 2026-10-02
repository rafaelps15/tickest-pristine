# Auditoria do backend — 29/09/2026

Estado do projeto: **build limpo e 221/221 testes passando** (154 unitários, 10 de arquitetura, 57 de integração).

Antes da auditoria, o build falhava no restore por causa de uma vulnerabilidade (NU1903) no `SSH.NET` 2025.1.0, uma dependência transitiva do `Testcontainers`. Isso foi resolvido atualizando o `Testcontainers.PostgreSql` de 4.12.0 para 4.15.0, que traz o `SSH.NET` 2026.0.0.

---

## 1. Onde o projeto parou

Sequência dos commits: RBAC (Users → Roles → Permissions) → CORS → CRUD de Departments/Sectors → mensagens, histórico e anexos de tickets.

| Módulo | Situação |
|---|---|
| **Tickets** | Criar, atualizar, reabrir, excluir (soft delete) e listar por usuário. Mensagens, anexos e histórico completos. |
| **Departments / Sectors** | CRUD completo, com desativação no lugar de exclusão. |
| **Roles / Permissions** | Criar role, atribuir permissões e listar. |
| **Users** | Apenas cadastro, login, refresh, consultas (`GetAll`, `GetById`, `GetByEmail`) e atribuição de roles. **É o módulo mais incompleto.** |

### Roles padrão (seeder)

> Atualizado em 2026-09-30: catálogo de permissões granular (`users:read`, `users:create`, `users:update`, `users:deactivate`, `users:assign-roles`, `roles:read`, `roles:manage`); `users:access`, `users:manage`, `users:delete` e `users:manage-roles` foram removidas. Ações sobre si mesmo (ver/editar o próprio perfil, trocar a própria senha) só exigem autenticação. A role padrão e a de administrador são identificadas pelas flags `IsDefault`/`IsAdministrator`, não pelo nome.

| Role | Flag | Permissões |
|---|---|---|
| **Admin** | `IsAdministrator` | Todas (`PermissionCodes.All`), repostas pelo seeder a cada inicialização; não editável pela API |
| **Agent** | – | `tickets:create`, `view-own`, `update-own`, `delete-own`, `reopen-own`, `tickets:manage` |
| **Requester** | `IsDefault` | `tickets:create`, `view-own`, `update-own`, `delete-own`, `reopen-own` |

Regra contra escalada: permissões administrativas (tudo que não é de ticket) só podem ser concedidas ou removidas por quem já as possui.

---

## 2. Bugs

### 2.1 Não existe caminho para um ticket chegar em `Closed` (alta)

- **Onde:** `src/TickestPristine.Application/Tickets/Update/UpdateTicketCommandHandler.cs:27`
- **Problema:** o handler recusa tickets que não estão ativos (ou seja, fora de `Open`/`InProgress`). Só que a transição `Resolved → Closed` definida em `TicketStatusTransitions` parte de `Resolved`, que não é ativo. Por isso nenhum ticket consegue ser fechado pela API. Apenas o seeder fecha tickets, porque chama `ticket.Update` diretamente.
- **Correção:** criar um comando `Tickets/Close` (com evento de domínio próprio) ou deixar o Update aceitar `Resolved → Closed`.

### 2.2 Qualquer requester define o responsável ao abrir um ticket (média, segurança)

- **Onde:** `src/TickestPristine.Application/Tickets/Create/CreateTicketCommandHandler.cs`
- **Problema:** o `ResponsibleId` é aceito sem nenhuma checagem: não se verifica se o usuário existe, se é Agent, nem se quem cria tem permissão para atribuir.
- **Correção:** só aceitar `ResponsibleId` de quem tem `tickets:manage`, e validar que o usuário existe e é um agente.

### 2.3 É possível abrir ticket em setor desativado (baixa)

- **Onde:** `src/TickestPristine.Application/Tickets/Create/CreateTicketCommandHandler.cs:40`
- **Problema:** o handler só confere se o setor existe, sem olhar `IsActive`.
- **Correção:** `context.Sectors.AnyAsync(s => s.Id == command.SectorId && s.IsActive, ...)`.

### 2.4 O dono do ticket altera o status livremente (decisão de negócio)

- **Onde:** `src/TickestPristine.Application/Tickets/Update/UpdateTicketCommandHandler.cs`
- **Problema:** com `tickets:update-own`, o próprio requester pode mover o ticket para `InProgress` ou `Resolved`.
- **Decisão pendente:** o requester deve poder mudar o status? A sugestão é que ele só edite a descrição e cancele; as demais transições ficariam com quem tem `tickets:manage`.

---

## 3. Módulo de Usuário: o que falta

| Item | Detalhe |
|---|---|
| **Editar perfil** | `User.Update()` e `UserProfileUpdatedDomainEvent` existem, mas nenhum comando chama esse método. | -- CONCLUÍDO (`PUT /users/{userId}/profile`)
| **Desativar usuário** | A permissão `users:deactivate` existe sem uso. O `User` não tem `IsActive`, e o login não bloqueia usuários inativos. | 
| **Trocar senha** | Não existe. | -- CONCLUÍDO (`PUT /users/me/password`)
| **Logout** | Não há como revogar o refresh token. |
| **Vínculo organizacional** | O `User` não tem `DepartmentId`/`SectorId`. **É a decisão que define como funciona a fila do agente.** |
| **`GET /users/me`** | Falta um endpoint com os dados do usuário logado, suas roles e permissões, necessário para o front. |
| **Proteger o último Admin** | O `AssignRoles` substitui todas as roles do usuário, então é possível remover o último administrador do sistema. | -- CONCLUÍDO (`Roles.LastAdministrator`, 409)
| **Mensagem de login** | Com senha errada, o erro retornado é `Users.NotFoundByEmail` ("e-mail não encontrado"). O ideal é uma mensagem genérica ("credenciais inválidas"). | -- CONCLUÍDO (`Users.InvalidCredentials`, "E-mail ou senha inválidos")

---

## 4. Outras lacunas em Tickets

- Falta `GET /tickets/{id}` (detalhe de um ticket).
- Falta uma fila para o agente: tickets do setor dele e tickets atribuídos a ele. Hoje só existe a listagem por quem abriu o ticket.
- Falta um comando para atribuir ou trocar o responsável de um ticket já criado (`Tickets/Assign`).
- Não há paginação nem filtros (status, prioridade, setor) nas listagens.
- A permissão `tickets:view-own` existe, mas nenhum código a verifica.

---

## 5. Remover os dados de exemplo

- **Onde:** `src/TickestPristine.Infrastructure/Database/Seeding/SampleDataSeeder.cs`, chamado pelo `DatabaseSeeder` quando `"Seeding": { "SampleData": true }` está no `appsettings.Development.json`.
- **O que faz hoje:** num banco sem departamentos, cria 5 departamentos, 10 setores e 50 chamados fictícios (Bogus), todos abertos pelo admin.
- **Para remover:**
  - apagar o `SampleDataSeeder.cs`;
  - tirar a chamada dele no `DatabaseSeeder` e a leitura de `Seeding:SampleData`;
  - tirar o bloco `Seeding` do `appsettings.Development.json` e a linha `Seeding:SampleData` do `IntegrationTestWebAppFactory`;
  - remover o pacote `Bogus` do `TickestPristine.Infrastructure.csproj` e do `Directory.Packages.props`.
- **Banco de desenvolvimento:** os registros já criados continuam lá. Recriar o banco, ou apagar chamados, setores e departamentos.

---

## 6. Arquivos de anexo órfãos

- **Onde:** `UploadTicketAttachmentCommandHandler` grava o arquivo (`IFileStorage.SaveAsync`) antes do `SaveChangesAsync`.
- **Problema:** se o banco falhar ao salvar o anexo, o arquivo fica na pasta sem registro no banco.
- **Correção sugerida:** tratar na Infrastructure, sem `try/catch` no handler — por exemplo, uma limpeza periódica que apaga os arquivos cuja `StorageKey` não existe em `ticket_attachments`.

---

## 7. Ordem sugerida

1. **Definir o modelo de usuário:** se pertence a departamento ou setor (ou agentes a setores e requesters a departamentos), e adicionar `IsActive`.
2. **Completar Users:** `/me`, editar perfil, desativar, trocar senha, logout e proteção do último Admin.
3. **Corrigir os bugs 2.1 a 2.3** e decidir o 2.4.
4. **Completar Tickets:** detalhe, fila do agente, atribuição, paginação e filtros.

### Decisão pendente para iniciar

> A que o usuário pertence: **Setor**, **Departamento**, ou **agentes a setores e requesters a departamentos**?
