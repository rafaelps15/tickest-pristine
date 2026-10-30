---
id: 038
titulo: Departamento e setor no domínio rico (piloto da migração)
status: Verificada        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-29
branch: refactor/SPEC-038-department-sector-rich-domain                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr:                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 038 — Departamento e setor no domínio rico (piloto da migração)

## 1. Contexto

- **Pedido (Rafael Santos, 2026-10-29):** "/spec-new department e sector", depois de perguntar "vc acha melhor ir corrigindo entidade por entidade ?" e de o agente recomendar migrar agregado por agregado, começando por um piloto pequeno antes do `Ticket`.
- É a primeira migração do legado listado em "Em migração" no `CLAUDE.md`, no padrão definido pela spec 012 (skills `add-entity` e `domain-modeling`).
- **Estado atual (comprovado em 2026-10-29):**
  - `Domain/Departments/Department.cs:5-12` e `Domain/Sectors/Sector.cs:5-12`: todas as propriedades com `{ get; set; }`; as duas entidades implementam `IAuditable`.
  - Seis handlers montam ou alteram as entidades por fora e chamam `Raise`: `Departments/Create`, `Update` e `Deactivate`, `Sectors/Create`, `Update` e `Deactivate` (`Application/`). A desativação do departamento desativa os setores ativos dele no próprio handler (`Departments/Deactivate/DeactivateDepartmentCommandHandler.cs:40-53`).
  - Regras que precisam de consulta ficam nos handlers: nome único entre os ativos, chamados ativos no departamento ou setor, responsável existente (`Departments/DepartmentResponsibleUser.cs`, classe estática).
  - Limites: `Departments/DepartmentValidationRules.cs` e `Sectors/SectorValidationRules.cs` (100 e 500), usados pelos validators; `DepartmentConfiguration` e `SectorConfiguration` repetem `100` e `500`.
  - `Infrastructure/Database/Seeding/DefaultOrganizationSeeder.cs:81-88` monta as entidades por inicializador; cerca de 20 arquivos de teste (unitários e de integração, inclusive `BaseIntegrationTest.cs` e `Database/DomainEventsPublishingTests.cs`) fazem o mesmo.
  - Nenhum handler escuta os eventos de departamento ou setor.
  - `Ensure` não existe no código.
- **Specs relacionadas:** 022 e 023 (`Aprovada`) mexem só nas consultas de departamento; 033 (`Rascunho`, reativar departamento e setor) vai precisar de um método `Reactivate` nas entidades e deve vir depois desta.

## 2. Requisitos

| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | `Department` rica: um único construtor privado vazio; `private set` em todas as propriedades; `Create(string name, string description, User? responsibleUser)` que monta a entidade com inicializador de objeto (`ResponsibleUserId = responsibleUser?.Id`, `IsActive = true`) e dispara `DepartmentCreatedDomainEvent`; `Update(string name, string description, User? responsibleUser)` que sempre dispara `DepartmentUpdatedDomainEvent`; `Deactivate()` `void` que desativa e dispara `DepartmentDeactivatedDomainEvent`; sem guardas. | pedido; DEC-4, 9, 10, 11, 12 |
| REQ-2 | `Sector` rica, no mesmo formato: `Create(Department department, string name, string? description)` (`DepartmentId = department.Id`), `Update(string name, string? description)` e `Deactivate()` `void`, com os eventos atuais. | pedido; DEC-4, 9, 10, 11, 12 |
| REQ-3 | Os seis handlers carregam, conferem o que precisa de consulta, chamam o método da entidade e salvam; nenhum atribui propriedade nem chama `Raise` para departamento ou setor. O handler carrega a entidade referenciada (`User` responsável, `Department` do setor), devolve `NotFound` se faltar e a passa ao `Create`/`Update`; perguntas sim ou não são `AnyAsync` direto no `if`; a conferência "já desativado" fica no handler; a desativação do departamento chama `Deactivate()` em cada setor ativo e depois no departamento, num único `SaveChangesAsync`; nenhuma classe estática auxiliar. | pedido; DEC-2, 11, 12, 13 |
| REQ-4 | **Removido pela DEC-10** (o `Ensure` saiu). | DEC-10 |
| REQ-5 | Os validators conferem só o formato (`NotEmpty()`); o tamanho fica só na configuração do EF (`HasMaxLength(100)`/`(500)`); saem `DepartmentValidationRules` e `SectorValidationRules`; nenhuma constante de limite. | DEC-7 |
| REQ-6 | O seeder e os testes criam e alteram as entidades só pelos métodos delas. | REQ-1, REQ-2 |
| REQ-7 | `Department` e `Sector` entram na lista `RichEntities` do teste de arquitetura da spec 012. | spec 012, REQ-11 |
| REQ-8 | Nenhum comportamento observável muda além do aceito: rotas, payloads e mensagens iguais. Exceções aceitas: texto acima do limite falha no banco (500) em vez de 400 (DEC-7, DEC-14); o seeder passa a publicar `DepartmentCreated`/`SectorCreated` no `SaveChangesAsync` (sem handler para eles, nada muda); a migration `Department_Sector_Name_Length` só acrescenta duas check constraints (DEC-14). | DEC-1, 7, 14 |
| REQ-9 | `CLAUDE.md`: `Department` e `Sector` fora de "Em migração"; `Department.cs` como referência viva de entidade; desvios do `Update` que sempre dispara o evento e do limite do nome pela constraint; regras permanentes no padrão das DEC-9 a DEC-11. `SYSTEM_SPECIFICATION.md` atualizado nas seções afetadas. | REQ-8 da spec 012; DEC-4, 9, 10, 11, 14 |
| REQ-10 | Skills (Tickest e biblioteca, com paridade) no padrão desta spec: validator só com o formato e tamanho na configuração; `Create` com inicializador e sem guardas; `void` quando o handler já conferiu o estado; o handler carrega a entidade referenciada e a passa ao `Create`; `AnyAsync` direto no `if`. | DEC-8 a DEC-13 |
| REQ-11 | Limite de 100 caracteres no nome de departamento e de setor pela check constraint da configuração (a coluna `citext` não guarda tamanho). | DEC-14 |

## 3. Critérios de aceitação

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | 1 | dados válidos | `Department.Create(...)` | as propriedades têm os valores informados, `IsActive` é `true`, `Id` não é vazio e há um `DepartmentCreatedDomainEvent` com o id |
| AC-2 | — | — | — | **Removido pela DEC-10** |
| AC-3 | 1 | um departamento ativo | `Update(...)` (com ou sem mudança) e `Deactivate()` | os campos mudam e há `DepartmentUpdatedDomainEvent`, inclusive sem mudança; `IsActive` fica `false` e há `DepartmentDeactivatedDomainEvent` |
| AC-4 | 3 | um departamento já desativado | `DeactivateDepartmentCommandHandler` | devolve `DepartmentErrors.AlreadyDeactivated(id)` sem disparar evento |
| AC-5 | 2, 3 | os casos do AC-1, AC-3 e AC-4 para o setor | `Sector.Create`, `Update`, `Deactivate` e `DeactivateSectorCommandHandler` | os mesmos resultados, com os eventos e o erro do setor |
| AC-6 | — | — | — | **Removido pela DEC-10** |
| AC-7 | 3, 8 | os testes de handler e de integração existentes de departamento e setor | `dotnet test` | todos passam com as asserções de antes (só o arranjo muda), inclusive responsável inexistente (`UserErrors.NotFound`) e existente no `Create` e no `Update`, departamento inexistente no setor (`DepartmentErrors.NotFound`), cascata de setores e erros 404/409 |
| AC-8 | 3 | o código da Application | busca por atribuição às propriedades, por `Raise(new Department…`/`Raise(new Sector…`, por `bool … = await …AnyAsync` e por classe estática em `Departments/`/`Sectors/` | nenhuma ocorrência; a revisão não aponta bloqueante |
| AC-9 | 5 | os validators e as configurações | testes de validator e leitura | os validators só têm `NotEmpty()`; as configurações mantêm `HasMaxLength(100)`/`(500)`; nenhuma constante de limite |
| AC-10 | 7 | `tests/TickestPristine.ArchitectureTests` | `dotnet test` | `RichEntities_Should_NotHaveNonPrivateSetters` passa com `Department` e `Sector` na lista |
| AC-11 | 8, 11 | o modelo do EF | `dotnet ef migrations has-pending-model-changes` | só a migration `Department_Sector_Name_Length` é nova (duas check constraints) e nenhuma mudança de modelo fica pendente |
| AC-12 | 6, 8 | build e suíte inteira | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | 0 avisos; os testes da linha de base continuam aprovados, menos os retirados pelas DEC-7 e DEC-10, mais os novos |
| AC-13 | 9 | `CLAUDE.md` e `SYSTEM_SPECIFICATION.md` | `python -m unittest discover -s scripts -p "test_*.py"` e leitura | os testes de instruções passam; "Em migração" não lista `Department` e `Sector`; os desvios do `Update` e do limite do nome estão em "Desvios" |
| AC-14 | 10 | as skills das duas árvores | `python tools/validate_library.py`, testes da biblioteca e `test_instructions.py` | todos verdes; paridade exata; nenhum `MaximumLength` em validator nem `Ensure` nos exemplos |
| AC-15 | 11 | um nome com 101 caracteres | `POST departments` e `POST sectors` | o banco recusa (500) e nada é gravado |

## 4. Decisões

| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Escopo | só estrutura, sem mudar comportamento / incluir a reativação (033) | Só estrutura: rotas, payloads, status, mensagens, eventos e banco iguais, com a exceção da DEC-7 (limite de tamanho); a reativação fica para a 033, que usará os métodos novos | Rafael Santos, 2026-10-29 ("faça de 1 a 5") |
| DEC-2 | Desativação em cascata | (a) handler chama `Deactivate()` em cada setor e no departamento / (b) serviço de domínio / (c) coleção `Sectors` no `Department` | (a): o setor continua agregado próprio | Rafael Santos, 2026-10-29 |
| DEC-3 | Guardas | (a) `Ensure` com guardas no construtor / (b) sem guardas | (a). **Revista pela DEC-10.** | Rafael Santos, 2026-10-29 |
| DEC-4 | Evento de "atualizado" | (a) `Update` dispara sempre, como hoje / (b) `bool`, só quando muda | (a): sem mudança de comportamento | Rafael Santos, 2026-10-29 |
| DEC-5 | Limites de tamanho | (a) constantes na entidade, usadas por validator e EF / (b) como está | (a). **Revista pela DEC-7.** | Rafael Santos, 2026-10-29 |
| DEC-6 | Onde fica e como é o `Ensure` | `SharedKernel` (como na skill `domain-modeling`) / `Domain/Core/Guards` | `src/TickestPristine.Domain/Core/Guards/Ensure.cs`, com o código enviado pelo responsável no chat, adaptado: namespace `TickestPristine.Domain.Core.Guards` (o enviado cita outro projeto, o que a regra do repositório proíbe), com escopo de arquivo, como o resto do código; um único `<summary>`, em pt-BR, na classe, explicando o que ela faz, e os métodos sem `<summary>`, `<param>` e `<exception>` ("o summary é só um explicando o que essa classe ensure faaz ok ? e em port br"); `throw  new` sem o espaço duplo. O local diverge da skill e vira desvio no `CLAUDE.md` **Revista pela DEC-10 (o `Ensure` saiu).** | Rafael Santos, 2026-10-29 (local, código e adaptações: "sim, aprovo a spec 038") |
| DEC-7 | Onde fica o limite de tamanho | (a) constantes na entidade (DEC-5) / (b) validator e configuração, sem constante / (c) só na configuração do EF, validator só com o formato | (c). Rafael Santos no chat, 2026-10-29: "Pra mim não faz sentido isso na entidade", "creio que vc vai ter que deletar essas duas constantes. pra mim não seguem o meu padrao", "Essa questão não deveria ficar na configuration ?" e "tem que ser nesse padrao!", com os exemplos de validator (`RegisterUserCommandValidator`: `NotEmpty()`, `EmailAddress()`, `MinimumLength(6)`) e de configuration (`UserConfiguration`: `HasMaxLength(200)`). **Mudança de comportamento aceita:** um nome ou descrição acima do limite deixa de voltar como 400 de validação e passa a falhar no banco (500 pelo `GlobalExceptionHandler`); os 8 testes `*ExceedsMaxLength` dos validators de departamento e setor saem, e a DEC-1 fica com essa exceção | Rafael Santos, 2026-10-29 |
| DEC-8 | As skills seguem o padrão da DEC-7? | ajustar as skills / deixar | Ajustar nas duas árvores. Rafael Santos no chat, 2026-10-29: "sim" à proposta de tirar o `MaximumLength(255)` do exemplo de validator, acrescentar o exemplo de `HasMaxLength` na configuração e escrever a regra; registrado nesta spec, e não numa própria | Rafael Santos, 2026-10-29 |
| DEC-9 | Forma do `Create` | (a) construtor privado com parâmetros e guardas, mais um vazio para o EF (modelo anterior das skills) / (b) um único construtor privado vazio, e o `Create` roda as guardas e monta a entidade com inicializador de objeto | (b), em `Department`, `Sector` e nos exemplos das skills das duas árvores (`rich-entity.md`, `patterns.md`, `add-entity`, `domain-modeling`, `ca-review`); o filho `ProjectMember` passa a ter `internal static Create`. Rafael Santos no chat, 2026-10-29: "na entidade department no \"Create\" eu preciso que siga essa sintaxe" (com o exemplo `Order.Create` de inicializador) e "Sim ! ajuste em Sector e nas skills seguindo suas convenções ok" | Rafael Santos, 2026-10-29 |
| DEC-10 | Guardas na entidade | (a) `Ensure` no `Create` e no `Update` (DEC-3, DEC-6) / (b) sem guardas: o validator confere a entrada | (b). Rafael Santos no chat, 2026-10-29: "vamos remover então esse Ensure e deixar como no evently. Ajuste e se precisar ajuste na skill tb seguindo suas convenções". `Ensure.cs` e `EnsureTests.cs` excluídos; REQ-4, AC-2 e AC-6 removidos; skills das duas árvores sem guardas (seção 3 do `patterns.md` reescrita: "a entidade não repete o validator"); DEC-3 e DEC-6 revistas | Rafael Santos, 2026-10-29 |
| DEC-11 | `Deactivate()` com `Result` repetindo a conferência do handler | (a) `Result` com `AlreadyDeactivated` na entidade / (b) `void`, com a conferência só no handler, como `Category.Archive()` | (b). Rafael Santos no chat, 2026-10-29: "esse padão não segue as skills ... em nenhum caso se usa o result assim ok ? -> ajuste e veja se é necessário ajustar na skill ou inserir isso na skill". Skills: regra do método `void` quando o handler já conferiu o estado (seção 8 do `patterns.md`, `domain-modeling`, `add-entity`, `ca-review`) | Rafael Santos, 2026-10-29 |
| DEC-12 | Responsável do departamento e departamento do setor | (a) classe estática `DepartmentResponsibleUser` e `AnyAsync` de existência, passando só o id / (b) o handler carrega a entidade referenciada, devolve `NotFound` e a passa ao `Create`/`Update` | (b): `Department.Create`/`Update(..., User? responsibleUser)` e `Sector.Create(Department department, ...)`; `DepartmentResponsibleUser.cs` excluído. Rafael Santos no chat, 2026-10-29: "internal static class DepartmentResponsibleUser isso não deve existir ... veja como o milan faz nesse caso !" e "o CreateSectorCommandHandler está estranho esse bool logo de cara" | Rafael Santos, 2026-10-29 |
| DEC-13 | Pergunta sim ou não nos handlers | `bool x = await ...AnyAsync(...); if (x)` / `if (await ...AnyAsync(...))` | A segunda, nos seis handlers; skills das duas árvores com a regra (carregar a entidade referenciada e passá-la ao `Create`; `AnyAsync` direto no `if`; sem classe estática) e o exemplo `Project.Create(User owner, ...)`. Rafael Santos no chat, 2026-10-29: "sim, siga o formato do milan e ajuste as skills" | Rafael Santos, 2026-10-29 |
| DEC-14 | Limite do nome, já que a coluna `citext` não guarda tamanho | (a) check constraint na configuração do EF, com migration / (b) `MaximumLength(100)` só no nome, no validator | (a): `ck_departments_name_length` e `ck_sectors_name_length` (`char_length(name) <= 100`) na configuração e a migration `Department_Sector_Name_Length`; o nome longo é barrado no banco (500), como a descrição (`varchar(500)`). Apontado pela revisão independente; Rafael Santos no chat, 2026-10-29: "reposta "a" e prossiga" | Rafael Santos, 2026-10-29 |

## 5. Fora do escopo

- Reativação de departamento e setor (spec 033).
- Qualquer outra entidade (`Ticket`, `User`, `Role`, `SlaCalendar` e filhos) e suas classes estáticas.
- `ValueObject`, extensões de `Result` e conversor UTC.
- Padrão de seed e skill de seed (spec própria).

## 6. Plano técnico

- **Domain:** `Department` e `Sector` no padrão do `rich-entity.md` da `add-entity`, com as DEC-9 a DEC-12; `<summary>` só no `Update` (dispara o evento mesmo sem mudança).
- **Application:** os seis handlers no padrão do REQ-3; validators só com `NotEmpty()`; saem `DepartmentValidationRules.cs`, `SectorValidationRules.cs` e `DepartmentResponsibleUser.cs`.
- **Infrastructure:** configurações com `HasMaxLength` como antes e as check constraints do nome; migration `Department_Sector_Name_Length`; `DefaultOrganizationSeeder` com `Create`.
- **Testes:** `DepartmentTests` e `SectorTests` novos; os existentes com o arranjo pelos métodos; `RichEntities` com as duas entidades; dois testes de integração do limite do nome.
- **Skills e documentação:** as duas árvores de skills (REQ-10), `CLAUDE.md` (REQ-9) e `SYSTEM_SPECIFICATION.md`.
- **Riscos:**

| Risco | Mitigação |
|---|---|
| O EF não materializar entidade com `private set` e construtor privado | Padrão suportado pelo EF Core 10; testes de integração comprovam |
| Mudança de comportamento escondida | AC-7 com as asserções de antes; exceções aceitas listadas no REQ-8 |
| Dados atuais com nome acima de 100 quebrarem a migration | O validator da `origin/main` barrava acima de 100, então os dados já respeitam o limite |
| O seed passar a disparar `DepartmentCreated`/`SectorCreated` | Nenhum handler desses eventos existe hoje; a mudança não tem teste e fica para conferência manual com o seed ligado |
| Conflito com as specs 022, 023 e 033 | 022 e 023 só mexem em consultas; a 033 vem depois e usa os métodos novos |

## 7. Tarefas

| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Linha de base: build, testes e `has-pending-model-changes` | AC-11, AC-12 | manual | [x] |
| T-2 | `Ensure` — **desfeita pela DEC-10** | — | — | [x] |
| T-3 | `Department` rica e `DepartmentTests` | AC-1, AC-3 | domain-modeling, add-tests | [x] |
| T-4 | `Sector` rica e `SectorTests` | AC-5 | domain-modeling, add-tests | [x] |
| T-5 | Os seis handlers e os validators; saem as classes estáticas | AC-4, AC-7, AC-8, AC-9 | add-feature | [x] |
| T-6 | Seeder, arranjo dos testes existentes e `RichEntities` | AC-7, AC-10, AC-12 | add-tests | [x] |
| T-6b | Check constraints, migration e testes de integração do limite do nome | AC-11, AC-15 | add-entity, add-tests | [x] |
| T-7 | `CLAUDE.md` (feito) e `SYSTEM_SPECIFICATION.md` (na verificação) | AC-13 | manual | [x] |
| T-7b | Skills das duas árvores | AC-14 | manual | [x] |
| T-8 | Verificação | todos | spec-verify | [x] |

## 8. Rastreabilidade

| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1, AC-3 | `tests/TickestPristine.Application.UnitTests/Departments/DepartmentTests.cs` › `Create_Should_CreateActiveDepartmentAndRaiseEvent_WhenDataIsValid`, `Update_Should_ChangeFieldsAndRaiseEvent_WhenCalled`, `Update_Should_RaiseEvent_WhenNothingChanged`, `Deactivate_Should_DeactivateAndRaiseEvent_WhenDepartmentIsActive` | Conferem os campos, `IsActive`, o `Id`, o `ResponsibleUserId` vindo do `User` e os eventos com o id do departamento, inclusive o de alteração sem mudança | Aprovado em 2026-10-29 |
| AC-4 | `Departments/DeactivateDepartmentCommandHandlerTests.cs` › `Handle_Should_ReturnAlreadyDeactivated_WhenDepartmentIsInactive` | Confere o erro exato e nenhum evento novo (`ClearDomainEvents()` depois do arranjo) | Aprovado em 2026-10-29 |
| AC-5 | `Sectors/SectorTests.cs` › os mesmos quatro casos; `Sectors/DeactivateSectorCommandHandlerTests.cs` › `Handle_Should_ReturnAlreadyDeactivated_WhenSectorIsInactive` | Mesmas conferências, com o `DepartmentId` vindo do `Department` e o erro do setor | Aprovado em 2026-10-29 |
| AC-7 | testes existentes de `Departments/`, `Sectors/` e os que montam essas entidades (unitários e de integração) | Só o arranjo mudou (`Create`, `Deactivate()`, `ClearDomainEvents()` em dois testes, sem o `Raise` manual em `DomainEventsPublishingTests`); as asserções são as de antes | Aprovado em 2026-10-29 |
| AC-8 | busca no código e revisão independente | Nenhuma atribuição, `Raise`, `bool … = await …AnyAsync` nem classe estática em `Departments/`/`Sectors/` da Application | Aprovado em 2026-10-29 |
| AC-9 | `DepartmentValidatorsTests`, `SectorValidatorsTests` e leitura | Os testes de `NotEmpty` continuam; os 8 `*ExceedsMaxLength` saíram (DEC-7) | Aprovado em 2026-10-29 |
| AC-10 | `tests/TickestPristine.ArchitectureTests/Conventions/RichEntityTests.cs` › `RichEntities_Should_NotHaveNonPrivateSetters` | A lista tem `typeof(Department)` e `typeof(Sector)` | Aprovado em 2026-10-29 |
| AC-11 | `dotnet ef migrations has-pending-model-changes` e leitura da migration | Só a migration `Department_Sector_Name_Length`, com `AddCheckConstraint` e o `Down` correspondente | Aprovado em 2026-10-29 |
| AC-12 | `dotnet build` e `dotnet test` | Contagens na seção 9 | Aprovado em 2026-10-29 |
| AC-13 | `scripts/test_instructions.py` e leitura | 14 testes de instruções passam; "Em migração" sem `Department` e `Sector`; desvios do `Update` e do limite do nome em "Desvios"; `SYSTEM_SPECIFICATION.md` atualizado | Aprovado em 2026-10-29 |
| AC-14 | `validate_library.py`, testes da biblioteca e `scripts/test_instructions.py` | Validador `APROVADO`, testes e paridade verdes | Aprovado em 2026-10-29 |
| AC-15 | `tests/TickestPristine.IntegrationTests/Departments/DepartmentsTests.cs` › `Create_Should_BeRejectedByDatabase_WhenNameIsLongerThanColumnLimit`; `Sectors/SectorsTests.cs` › o mesmo método | Enviam um nome com 101 caracteres pela API, conferem o 500 e consultam o banco para confirmar que nada com esse nome foi gravado | Aprovado em 2026-10-29 |

## 9. Evidências de verificação

- **Linha de base (T-1, 2026-10-29, `0eff7b6`):** `dotnet build` com 0 avisos e 0 erros; `dotnet test` com 1.288 aprovados e 0 falhas (56 de arquitetura, 761 unitários, 471 de integração); `dotnet ef migrations has-pending-model-changes`: "No changes have been made to the model since the last migration."
- **Build (2026-10-29):** `dotnet build TickestPristine.slnx` com 0 avisos e 0 erros.
- **Testes (2026-10-29, relatórios trx):** 1.291 aprovados e 0 falhas (56 de arquitetura, 762 unitários, 473 de integração); todos os testes da rastreabilidade aparecem como `Passed`. Fora os 8 `*ExceedsMaxLength` retirados pela DEC-7 e o `EnsureTests` pela DEC-10, entraram os 8 de entidade, o `Handle_Should_UpdateDepartment_WhenResponsibleUserExists` e os 2 de integração do limite. Auditoria SDD: 100%, código de saída 0. Biblioteca: `validate_library.py` APROVADO (357 verificações), 23 testes OK; `test_instructions.py`: 14 testes OK.
- **Modelo do EF (2026-10-29):** `has-pending-model-changes`: "No changes have been made to the model since the last migration."; só a migration `Department_Sector_Name_Length` é nova.
- **Revisão:** `ca-review` independente aprovou, sem bloqueantes; os não bloqueantes foram corrigidos, menos o teste do seeder (risco na seção 6).
- **Documentação atualizada:** `SYSTEM_SPECIFICATION.md` — seções 1 (domínio anêmico, exceto `Department` e `Sector`), RN-10 (linhas do handler), C-07 (responsável carregado pelo handler, limite do nome no banco, `Update` sempre com evento, seed com eventos) e 3.1 (domínio rico nas duas entidades).
- **Entrega:**

## 10. Histórico

| Data | Status | Observação |
|---|---|---|
| 2026-10-29 | Rascunho | Spec criada a partir do pedido de Rafael Santos no chat ("/spec-new department e sector"). |
| 2026-10-29 | Em descoberta | Descoberta iniciada: código de `Department`, `Sector`, handlers, seeder e testes levantados; primeira rodada de perguntas enviada. |
| 2026-10-29 | Em descoberta | Respostas de Rafael Santos no chat ("faça de 1 a 5 com uma observação: Ensure ficará dentro de Domain/Core/Guards ok ? e o código deve ser esse"): DEC-1 a DEC-6 registradas; spec escrita. Pendente: confirmar as adaptações do código do `Ensure` (DEC-6, `[SUPOSIÇÃO]`). |
| 2026-10-29 | Aprovada | Aprovada por Rafael Santos no chat ("sim, aprovo a spec 038 -> o summary é só um explicando o que essa classe ensure faaz ok ? e em port br"); DEC-6 confirmada, com o `<summary>` único na classe do `Ensure`. Sem `[SUPOSIÇÃO]` pendente. A implementação começa com `/spec-implement 038`. |
| 2026-10-29 | Em implementação | Implementação iniciada por `/spec-implement 038` na branch `refactor/SPEC-038-department-sector-rich-domain`, criada a partir da `origin/main` (`0eff7b6`). |
| 2026-10-29 | Em implementação | T-1 a T-6 feitas em sequência, a pedido de Rafael Santos no chat ("a - vc vai atuar como senior e seguir exatamente todas minhas skills exemplo e o codigo tem que ser o espelho das skills modelo. nao faça commit quando finalizar que eu irei analizar o codigo"); sem commit. Pendentes de autorização: excluir `DepartmentValidationRules.cs` e `SectorValidationRules.cs` (sem uso) e alterar o `CLAUDE.md` (T-7). |
| 2026-10-29 | Em implementação | Execução depois da implementação: `dotnet build` com 0 avisos; `dotnet test` com 1.307 aprovados e 0 falhas (56 de arquitetura, 780 unitários, 471 de integração); `has-pending-model-changes` sem mudança de modelo. |
| 2026-10-29 | Em implementação | DEC-7 aprovada por Rafael Santos no chat ("tem que ser nesse padrao! vc vai ajustar no codigo"): constantes saíram da entidade, `DepartmentValidationRules.cs` e `SectorValidationRules.cs` excluídos, validators só com `NotEmpty()`, limites só na configuração do EF (igual à `origin/main`), 8 testes de tamanho máximo removidos. Build 0 avisos; 772 unitários e 56 de arquitetura aprovados. |
| 2026-10-29 | Em implementação | DEC-8 aprovada por Rafael Santos no chat ("sim"): REQ-10 e AC-14 acrescentados; skills ajustadas nas duas árvores (`command-slice.md`, `add-feature/SKILL.md`, `add-entity/SKILL.md`, `ca-review/SKILL.md`); validador da biblioteca `APROVADO`, testes da biblioteca e `test_instructions.py` verdes. |
| 2026-10-29 | Em implementação | DEC-9 aprovada por Rafael Santos no chat: `Department.Create` e `Sector.Create` com inicializador de objeto e guardas no início; skills das duas árvores no mesmo modelo (teste C-1 da biblioteca atualizado). Build 0 avisos; 772 unitários e 56 de arquitetura aprovados; validador da biblioteca `APROVADO`, 23 testes da biblioteca e `test_instructions.py` verdes. |
| 2026-10-29 | Em implementação | DEC-10 e DEC-11 a pedido de Rafael Santos no chat: guardas e `Ensure` removidos (REQ-4, AC-2 e AC-6 removidos), `Deactivate()` `void` com a conferência só no handler; skills das duas árvores ajustadas, com teste C-4 da biblioteca reescrito. Build 0 avisos; 759 unitários e 56 de arquitetura aprovados; validador da biblioteca `APROVADO`, 23 testes da biblioteca e `test_instructions.py` verdes. Como REQ-4 saiu, a spec precisa de nova aprovação do trecho revisto. |
| 2026-10-29 | Em implementação | DEC-12 e DEC-13 a pedido de Rafael Santos no chat: responsável e departamento carregados pelos handlers e passados às entidades, `DepartmentResponsibleUser.cs` excluído, `AnyAsync` direto no `if`, skills ajustadas nas duas árvores. Build 0 avisos; 759 unitários e 56 de arquitetura aprovados; validador da biblioteca `APROVADO`, 23 testes da biblioteca e `test_instructions.py` verdes. |
| 2026-10-29 | Em implementação | Revisão aprovada por Rafael Santos no chat ("sim"), depois da lista das DEC-7 a DEC-13 (inclusive a saída do REQ-4, do AC-2 e do AC-6); sem `[SUPOSIÇÃO]` pendente. T-7 autorizada no mesmo "sim" e feita: `CLAUDE.md` com `Department` e `Sector` fora de "Em migração", `Department.cs` como referência viva de entidade e o `Ensure` fora do desvio do `SharedKernel`. |
| 2026-10-29 | Em implementação | Primeira verificação reprovada pela revisão independente: o nome ficou sem limite (a coluna `citext` não guarda tamanho), textos de skills e do `CLAUDE.md` ainda falavam em guardas, e a spec não acompanhava as DECs. Correções: DEC-14 aprovada por Rafael Santos no chat ("reposta "a" e prossiga") — check constraints e migration `Department_Sector_Name_Length`, REQ-11 e AC-15; textos das skills, da biblioteca e do `CLAUDE.md` alinhados (desvios do `Update` e do limite do nome; regras permanentes; "Em migração"); testes `Update_Should_RaiseEvent_WhenNothingChanged` e de integração do limite; seções 2 a 8 da spec reescritas conforme o código. T-7 reaberta até a atualização do `SYSTEM_SPECIFICATION.md`. Nova verificação em seguida. |
| 2026-10-29 | Em implementação | Commits das tarefas autorizados por Rafael Santos no chat ("autorizo o commit L1, C1, C2, C3 e C4"). Achados não bloqueantes da revisão independente corrigidos (Rafael Santos: "vamos prosseguir", opção (a)): os testes do AC-15 conferem que nada foi gravado; novo `Handle_Should_UpdateDepartment_WhenResponsibleUserExists`; eventos de alteração e desativação conferidos pelo id nos testes de entidade; linha em branco em `BaseHandlerTest`; texto do agente nas DEC-10 e DEC-13 sem citar a origem dos padrões. Mudança do seeder sem teste, registrada como risco. |
| 2026-10-29 | Verificada | Verificação (`spec-verify`) aprovada: build com 0 avisos, 1.291 testes aprovados, auditoria SDD 100%, biblioteca e `test_instructions.py` verdes, todos os ACs cobertos por testes desta execução, revisão independente sem bloqueantes. `SYSTEM_SPECIFICATION.md` atualizado. |
