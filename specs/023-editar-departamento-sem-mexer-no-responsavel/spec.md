---
id: 023
titulo: Listagem de departamentos traz o id do responsável, para editar sem buscar o detalhe
status: Verificada        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-28
branch: feat/SPEC-023-department-list-responsible-id                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr: "#52"                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 023 — Listagem de departamentos traz o id do responsável, para editar sem buscar o detalhe

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat, em 2026-10-28, respondendo à crítica 2 de UX da tela Departamentos do front ("Registro no back uma spec para 'responsibleUserId ausente = não mudar' (assim renomear um departamento não depende do detalhe carregar)?" → "Sim, spec 023").
- **Problema no front:** a listagem (`GET departments`) traz só o **nome** do responsável. Para editar, o front busca `GET departments/{id}` para saber o **id** dele, porque o `PUT` grava o que for enviado: mandar o responsável vazio o apaga. Se essa busca falha, o modal precisa bloquear o "Salvar" até o admin tentar de novo ou escolher alguém outra vez, mesmo que ele só queira corrigir o nome. (O front já trata isso com segurança desde o commit `43a3644`; esta spec remove a dependência.)
- **Referências:** `SYSTEM_SPECIFICATION.md` 2.7 C-07 (departamentos e setores; o responsável pode ser um usuário inativo) e 9.5 Q-12 (o responsável deve ser ativo? Fora desta spec); spec 022 (contagem de chamados ativos na mesma listagem).
- **Fatos do repositório:**
  - `Endpoints/Departments/Update.cs`: `PUT departments/{departmentId}` com `departments:manage`; o corpo tem `name`, `description` e `responsibleUserId` (anulável); devolve 204. É uma substituição completa: `UpdateDepartmentCommandHandler.cs` grava `ResponsibleUserId = command.ResponsibleUserId`, então nulo apaga o responsável.
  - `Departments/GetById/DepartmentResponse.cs`: o detalhe já traz `ResponsibleUserId`.
  - `Departments/GetAll/DepartmentResponse.cs` e `GetDepartmentsQueryHandler.cs`: a listagem traz `ResponsibleUserName` (subconsulta em `Users` dentro da projeção única), mas não o id, que já está em `d.ResponsibleUserId`.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | Cada departamento devolvido por `GET departments` traz `responsibleUserId` (o id do responsável, ou nulo quando não há), ao lado do `responsibleUserName` que já existe. | pedido; DEC-1 |
| REQ-2 | O `PUT departments/{id}` não muda: continua substituindo nome, descrição e responsável pelo que for enviado (o id troca, nulo remove). | DEC-1; DEC-2 |

## 3. Critérios de aceitação
Dados comuns: departamento TI com responsável Ana; departamento Financeiro sem responsável.

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | os dados comuns | `GET departments` | TI traz `responsibleUserId` = id de Ana e `responsibleUserName` = nome de Ana; Financeiro traz os dois nulos |
| AC-2 | REQ-1, REQ-2 | os dados comuns | o cliente lê TI em `GET departments` e envia `PUT departments/{TI}` com nome "Tecnologia", a descrição e o `responsibleUserId` lido da listagem | 204; `GET departments/{TI}` mostra o nome novo e o responsável continua Ana |
| AC-3 | REQ-2 | TI com responsável Ana e um usuário Bruno | `PUT departments/{TI}` com o `responsibleUserId` de Bruno; depois, `PUT departments/{TI}` com `responsibleUserId` nulo | 204 nos dois; `GET departments/{TI}` mostra Bruno depois do primeiro e nenhum responsável depois do segundo. Os testes existentes de `PUT departments/{id}` passam sem alteração |
| AC-4 | REQ-1 | a mudança aplicada | revisão do handler (verificação descrita) | o id vem da própria linha do departamento, dentro da projeção única que já existe, sem consulta nova |
| AC-5 | todos | a mudança aplicada | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | 0 avisos; todos os testes passam |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Como editar nome e descrição sem depender da busca do detalhe | (a) a listagem passa a trazer `responsibleUserId`, e o `PUT` continua igual; (b) campo booleano novo `keepCurrentResponsible` no `PUT`; (c) rota de atualização parcial (`PATCH`, campo ausente = não mudar) | (a), o padrão de APIs de alto nível: quem mostra um vínculo devolve o id junto com o nome. Mudança aditiva e pequena, sem remendo no contrato de escrita. (b) cria um campo só para contornar a falta do id e exige a regra do pedido contraditório; (c) é uma rota nova, trabalho demais para o problema. A versão anterior desta spec recomendava (b); a recomendação mudou na revisão de 2026-10-28. | Rafael Santos, 2026-10-28 ("parte pra próxima", em resposta a "seguimos com a opção A?") |
| DEC-2 | O que fazer com quem chama o `PUT` hoje | (a) nada muda; (b) passar a tratar nulo como "manter" | (a). Com (a) da DEC-1, o `PUT` não muda; (b) tiraria o único jeito de remover o responsável. | Rafael Santos, 2026-10-28 (idem) |

## 5. Fora do escopo
- Mudar o `PUT departments/{id}` ou criar o `PATCH` (DEC-1).
- Exigir que o responsável seja um usuário ativo (Q-12).
- Mudanças na criação de departamento (`POST departments`).

## 6. Plano técnico
- **Pré-requisito:** branch a partir da `origin/main` atualizada. Mexe no mesmo `GetDepartmentsQueryHandler` e no mesmo `DepartmentResponse` da spec 022; quem for implementada depois parte da `main` com a outra já mergeada.
- **Arquivos a criar ou alterar:**
  - Application:
    - `Departments/GetAll/DepartmentResponse.cs`: `ResponsibleUserId` (`Guid?`) em `DepartmentResponse`.
    - `Departments/GetAll/GetDepartmentsQueryHandler.cs`: `ResponsibleUserId = d.ResponsibleUserId` na projeção.
  - Web.Api: nenhuma mudança.
  - Testes:
    - `tests/TickestPristine.IntegrationTests/Departments/DepartmentsTests.cs`: AC-1 e AC-2;
    - teste unitário do handler de listagem, se existir no padrão dos testes de Departments: AC-1;
    - AC-3 pela suíte existente; AC-4 na revisão.
- **Contratos:** `GET departments` ganha `responsibleUserId` (anulável) em cada departamento. Mudança só aditiva. `PUT departments/{id}` não muda.
- **Mudança no banco:** não.
- **Front-end (`tickest-pristine-front`), depois do merge:**
  - a entidade `Department` ganha `responsibleUserId`;
  - o modal de edição usa o id da listagem e deixa de depender do `GET departments/{id}`: o "Salvar" fica sempre disponível;
  - trocar o responsável ou "Remover" continuam enviando o id ou o nulo, como hoje.
- **Documentação de estado afetada:** `SYSTEM_SPECIFICATION.md` 2.7 C-07 e a descrição de `GET departments` em 4.3, se listar os campos.
- **Riscos:**
  - R-1: conflito de merge com a spec 022 nos mesmos dois arquivos; pequeno, resolvido na ordem de entrega.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | `ResponsibleUserId` na resposta e na projeção da listagem | AC-1, AC-4 | `add-feature` | [x] |
| T-2 | Testes de listagem e do fluxo listar → editar | AC-1, AC-2, AC-3 | `add-tests` | [x] |
| T-3 | Atualizar o `SYSTEM_SPECIFICATION.md` | AC-5 | — | [x] |
| T-4 | Revisão de convenções (inclui o AC-4), build sem avisos e suíte inteira | AC-4, AC-5 | `ca-review` | [x] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | `Application.UnitTests/Departments/GetDepartmentsQueryHandlerTests.cs` › `Handle_Should_ReturnResponsibleIdAndName_WhenDepartmentHasResponsible`; `IntegrationTests/Departments/DepartmentsTests.cs` › `GetAll_Should_ReturnResponsibleIdAndName` | Departamento com responsável traz id e nome dele; o sem responsável traz os dois nulos (no handler e pelo HTTP) | Aprovado em 2026-10-28 (execução da verificação) |
| AC-2 | `IntegrationTests/Departments/DepartmentsTests.cs` › `Update_Should_KeepResponsible_WhenClientSendsTheIdReadFromGetAll` | Lê o departamento em `GET departments`, envia o `PUT` com nome novo e o `responsibleUserId` lido; 204 e o detalhe mostra o nome novo com o mesmo responsável | Aprovado em 2026-10-28 (execução da verificação) |
| AC-3 | `IntegrationTests/Departments/DepartmentsTests.cs` › `Update_Should_ReplaceResponsible_WhenAnotherUserIdIsSent` e `Update_Should_RemoveResponsible_WhenNullIsSent`; testes existentes do `PUT`, sem alteração: `DepartmentsTests.cs` › `Update_Should_ReflectChangesInGetById`, `Update_Should_ReturnNotFound_WhenDepartmentDoesNotExist`, `Update_Should_ReturnConflict_WhenAnotherActiveDepartmentHasTheName`, `Update_Should_ReturnNotFound_WhenResponsibleUserDoesNotExist`; `UpdateDepartmentCommandHandlerTests.cs` (4 testes) | Pelo HTTP, o `PUT` com o id de Bruno troca Ana por Bruno, e o `PUT` com nulo deixa o departamento sem responsável (204 e conferido no detalhe); os testes existentes passam sem alteração | Aprovado em 2026-10-28 (execução da verificação) |
| AC-4 | Revisão de `GetDepartmentsQueryHandler.cs` | `ResponsibleUserId = d.ResponsibleUserId` vem da própria linha do departamento, dentro do mesmo `.Select` da projeção única; nenhuma consulta nova | Aprovado na revisão de 2026-10-28 |
| AC-5 | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | Build com 0 avisos e 0 erros; suíte inteira: 50 de arquitetura, 757 unitários e 468 de integração aprovados, 0 falhas | Aprovado em 2026-10-28 (execução da verificação) |

## 9. Evidências de verificação
- **Build:** 2026-10-28, `dotnet build TickestPristine.slnx --no-incremental`: 0 avisos, 0 erros.
- **Testes:** 2026-10-28, `dotnet test TickestPristine.slnx`: 1.275 aprovados, 0 com falha (50 de arquitetura, 757 unitários, 468 de integração); os 14 testes da rastreabilidade conferidos um a um na execução filtrada (todos aprovados). Auditoria `python scripts/auditoria_sdd.py .`: score 100%, CONFORME, código de saída 0.
- **Revisão:** `ca-review` sobre os arquivos da spec: nenhum achado bloqueante nem violação de convenção (mudança no slice `GetAll`, projeção com `.Select`, sem command nem endpoint novos; testes unitário e de integração no padrão `Handle_Should_` / `GetAll_Should_` / `Update_Should_`).
- **Documentação atualizada:** `SYSTEM_SPECIFICATION.md` 2.7 (C-07) e 4.3.1 (linha de `GET departments`), no commit da T-3.
- **CI:** roda no PR para a `main`.
- **Entrega:** PR [#52](https://github.com/rafaelps15/tickest-pristine/pull/52) aberto para a `main` em 2026-10-29; a spec fica `Entregue` quando ele for mergeado.

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-28 | Rascunho | Criada a partir da crítica 2 de UX da tela Departamentos do front, a pedido de Rafael Santos no chat ("Sim, spec 023"). |
| 2026-10-28 | Em descoberta | Descoberta feita no código (endpoint, comando, handler e regra do responsável). DEC-1 a DEC-3 com recomendação, aguardando a resposta e a aprovação explícita de Rafael Santos. |
| 2026-10-28 | Em descoberta | Revisão pelo critério de sistemas de alto nível, pedido por Rafael Santos na spec 022: a recomendação passou do booleano `keepCurrentResponsible` para trazer `responsibleUserId` na listagem, sem mudar o `PUT`. Rafael respondeu "parte pra próxima" à pergunta "seguimos com a opção A?". REQs, ACs, DECs, plano, título e branch reescritos; a antiga DEC-3 (pedido contraditório) deixou de existir. Sem `[SUPOSIÇÃO]`; aguardando aprovação explícita. |
| 2026-10-28 | Aprovada | Aprovada por Rafael Santos no chat ("Aprovo a 023 e a 024."). |
| 2026-10-28 | Em implementação | `/spec-implement 023` pedido por Rafael Santos, na worktree `tickest-pristine-023`, branch `feat/SPEC-023-department-list-responsible-id` criada a partir da `origin/main` (`ad732da`). Mesmos arquivos da spec 022 (`GetAll/DepartmentResponse.cs`, `GetDepartmentsQueryHandler.cs`, `DepartmentsTests.cs`), ainda sem código da 022: segue isolada (R-1). Commits por tarefa aguardam autorização explícita do responsável. |
| 2026-10-28 | Em implementação | Ajuste do AC-3 aprovado no chat por Rafael Santos ("AC-3: aprovo a opção 1."): o texto supunha testes de troca e de remoção do responsável no `PUT` que não existiam; o AC passa a exigir esses dois casos por teste de integração, sem mudar o comportamento do `PUT` (REQ-2 e DEC-2 intactos). |
| 2026-10-28 | Verificada | `spec-verify`: build sem avisos, suíte inteira verde (1.275 testes), auditoria SDD 100% (saída 0), AC-1 a AC-5 cobertos por testes lidos e aprovados nesta execução, revisão sem achados bloqueantes. Documentação de estado já atualizada na T-3 (2.7 C-07 e 4.3.1). |
