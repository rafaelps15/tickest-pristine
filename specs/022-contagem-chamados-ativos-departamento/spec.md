---
id: 022
titulo: Listagem de departamentos informa quantos chamados ativos cada departamento e setor tem
status: Verificada       # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-28
branch: feat/SPEC-022-active-ticket-count-departments                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr: 60                  # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 022 — Listagem de departamentos informa quantos chamados ativos cada departamento e setor tem

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat, em 2026-10-27, respondendo à crítica de UX da tela Departamentos do front ("Na exclusão de departamento, peço ao back a contagem de chamados abertos nos setores, como fizemos na desativação de usuário?" → "Sim, descrevo a spec do back"; depois, "pode registrar").
- **Problema no front:** a tela confirma a exclusão ("Os N setores dele também serão excluídos. Essa ação não pode ser desfeita.") sem saber se o departamento tem chamados ativos. Quando tem, a pessoa confirma e só então recebe o 409, mostrado como alerta no topo da página, longe do card. O mesmo vale para o setor.
- **Referências:** `SYSTEM_SPECIFICATION.md` 2.6 RN-10 (departamento ou setor não pode ser desativado com chamados ativos; desativar o departamento desativa os setores) e 2.7 C-07 (departamentos e setores, sem reativação); spec 021 (aviso de chamados em aberto na desativação de usuário, que o front já mostra).
- **Fatos do repositório:**
  - `Departments/Deactivate/DeactivateDepartmentCommandHandler.cs:29-38`: recusa com `Departments.HasActiveTickets` (409) quando existe chamado ativo em algum setor do departamento, usando `WhereActive()`; os setores ativos são desativados junto (linhas 40-49). A rota é `DELETE departments/{id}` (`Endpoints/Departments/Deactivate.cs`), com `departments:manage`.
  - `Sectors/Deactivate/DeactivateSectorCommandHandler.cs:27-34`: mesma recusa no setor, com `Sectors.HasActiveTickets`.
  - `Tickets/TicketSlaFilters.cs:14-15`: `WhereActive()` = status Aberto ou Em andamento; o filtro global já tira os chamados excluídos.
  - `Departments/GetAll/GetDepartmentsQueryHandler.cs`: devolve só departamentos ativos, ordenados por nome, cada um com `id`, `name`, `description`, `responsibleUserName` e a lista de setores ativos (`id`, `name`, `description`), tudo numa única projeção.
  - `Departments/GetAll/DepartmentResponse.cs`: `DepartmentResponse` e `DepartmentSectorResponse`, sem contagem de chamados.
  - O front (`departments/feature-department-list`) só usa `GET departments` para montar a tela e a confirmação.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | Cada departamento devolvido por `GET departments` informa quantos chamados ativos existem nos seus setores, inclusive os desativados, com o mesmo alcance da recusa da desativação do departamento (DEC-8). | pedido; DEC-8 |
| REQ-2 | Cada setor dentro de um departamento em `GET departments` informa quantos chamados ativos tem. | pedido; DEC-2 |
| REQ-3 | A contagem usa exatamente o mesmo critério de "chamado ativo" das recusas de desativação (RN-10): status Aberto ou Em andamento, sem os excluídos. Contagem zero significa que a desativação não será recusada por chamados ativos. | DEC-3 |
| REQ-4 | O 409 de desativação (`Departments.HasActiveTickets` e `Sectors.HasActiveTickets`) continua igual, com o mesmo código e o mesmo texto, para não quebrar o front atual. | DEC-4 |

## 3. Critérios de aceitação
Dados comuns: departamento TI com os setores Suporte (2 chamados Abertos e 1 Em andamento), Redes (1 Resolvido, 1 Cancelado e 1 Aberto excluído) e Infra (sem chamados); departamento Financeiro, sem setores.

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1, REQ-3 | os dados comuns | `GET departments` | TI informa 3 chamados ativos; Financeiro informa 0 |
| AC-2 | REQ-2, REQ-3 | os dados comuns | `GET departments` | em TI: Suporte informa 3, Redes 0 e Infra 0 |
| AC-3 | — | — | — | Removido (DEC-6): um setor desativado nunca tem chamado ativo, porque a desativação é recusada nesse caso (RN-10); o cenário não acontece pela API. Premissa corrigida pela DEC-8: o cenário acontece pela reabertura de um chamado de setor já desativado e fica coberto, no REQ-3, pelo teste da reabertura listado no AC-4. |
| AC-4 | REQ-3 | TI com contagem 3 | `DELETE departments/{TI}` | 409 `Departments.HasActiveTickets`; com os chamados de Suporte concluídos (contagem 0), o mesmo `DELETE` devolve 204 |
| AC-5 | REQ-3 | Suporte com contagem 3; Infra com contagem 0 | `DELETE sectors/{Suporte}` e `DELETE sectors/{Infra}` | 409 `Sectors.HasActiveTickets` e 204, respectivamente |
| AC-6 | REQ-4 | qualquer departamento com chamados ativos | `DELETE departments/{id}` | o corpo do problema tem o mesmo código e o mesmo texto de hoje |
| AC-7 | REQ-1, REQ-2 | a mudança aplicada | revisão do handler (verificação descrita, DEC-6) | a contagem fica dentro da projeção única que já existe em `GetDepartmentsQueryHandler`, sem laço nem consulta por departamento ou por setor |
| AC-8 | todos | a mudança aplicada | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | 0 avisos; todos os testes passam |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Onde expor a contagem | (a) campo novo em cada item de `GET departments`; (b) endpoint novo de contagem, chamado pelo front antes de abrir a confirmação (como na spec 021, que usa `GET tickets`); (c) só no `GET departments/{id}` | (a). A tela já carrega a lista inteira e a confirmação é aberta a partir dela; o front sabe a contagem sem outra requisição e marca o card antes de qualquer clique. | Rafael Santos, 2026-10-28 ("siga a resolução em padrão de sistemas de alto nível") |
| DEC-2 | Contar também por setor | (a) sim, no item de cada setor; (b) só no departamento | (a). A desativação de setor tem a mesma recusa (RN-10); o total do departamento é a soma dos setores ativos. | Rafael Santos, 2026-10-28 (idem) |
| DEC-3 | Qual critério de "ativo" | (a) o mesmo `WhereActive()` das recusas; (b) um critério próprio da listagem | (a). Contagem e recusa nunca discordam: 0 na tela significa "pode desativar". | Rafael Santos, 2026-10-28 (idem) |
| DEC-4 | Mudar o texto ou o corpo do 409 | (a) não, mantém código e texto; (b) incluir a contagem no texto ou num campo extra do problema | (a). Com a contagem na listagem, o 409 fica como proteção contra corrida (um chamado aberto entre o carregamento da tela e o clique). | Rafael Santos, 2026-10-28 (idem) |
| DEC-5 | Nome dos campos | (a) `activeTicketCount` no departamento e no setor; (b) `openTicketCount` | (a). "Ativo" é o termo da RN-10 e do `WhereActive()`; "open" confundiria com o status Aberto. | Rafael Santos, 2026-10-28 (idem) |
| DEC-6 | Critérios difíceis de comprovar | (a) manter o AC-3 (setor desativado com chamado ativo) e o AC-7 por teste de integração; (b) remover o AC-3, que não acontece pela API, e verificar o AC-7 na revisão | (b). O cenário do AC-3 é impossível pela RN-10, e contar consultas ao banco num teste seria frágil; a projeção única é conferida na revisão. | Rafael Santos, 2026-10-28 (idem) |
| DEC-7 | Restringir a desativação a quem criou o departamento, protegendo os pré-existentes, e exigir vários avisos | (a) só quem criou pode desativar, com várias confirmações; (b) quem tem `departments:manage` desativa qualquer um, bloqueado quando em uso, com uma confirmação proporcional ao impacto e possibilidade de reativação | (b), o padrão de helpdesks de alto nível: a autoridade vem da permissão, não da autoria (o criador pode sair da empresa; os departamentos do `DefaultOrganizationSeeder` são só a estrutura inicial e o sistema não depende de nenhum); avisos em série viram clique automático. A gravidade é tratada por: desativação lógica (já é o que o `DELETE` faz), bloqueio com chamados ativos (RN-10 e esta spec), uma confirmação no front que mostra os setores afetados e pede o nome digitado, e reativação (spec 033). | Rafael Santos, 2026-10-28 (pergunta "Ninguém pode excluir um departamento pré-existente… Como é em sistema de alto padrão?"; resposta "siga a resolução em padrão de sistemas de alto nível") |
| DEC-8 | Alcance do total do departamento, depois de a verificação mostrar que a premissa da DEC-6 não vale (a reabertura não confere o setor, e um chamado reaberto num setor já desativado volta a ser ativo e recusa a desativação do departamento) | (a) contar os chamados de todos os setores do departamento, como a recusa; (b) manter só os setores ativos e registrar a exceção no REQ-3 e na DEC-3; (c) recusar a reabertura em setor desativado (regra nova, fora do plano) | (a). Contagem e recusa nunca discordam, como pedem o REQ-3 e a DEC-3: o total do departamento passa a ter o mesmo alcance da recusa, e o do setor não muda. Corrige o REQ-1, a frase da DEC-2 sobre a soma dos setores ativos (com chamado ativo em setor desativado, o total passa da soma dos setores listados) e a premissa da DEC-6, que continuam registradas como foram aprovadas. Rafael Santos no chat, na verificação: escolheu "Contar todos os setores (Recomendado)" | Rafael Santos, 2026-10-31 |

## 5. Fora do escopo
- Reativação de departamento ou setor: fica na spec 033 (DEC-7).
- Restringir a desativação a quem criou o departamento (DEC-7).
- Mudar a regra da RN-10, por exemplo desativar e mandar os chamados para outro setor.
- Contagem por status, prioridade ou situação de SLA; só o total de ativos.
- Contagem no `GET departments/{id}`.

## 6. Plano técnico
- **Pré-requisito:** branch a partir da `origin/main` atualizada.
- **Arquivos a criar ou alterar:**
  - Application:
    - `Departments/GetAll/DepartmentResponse.cs`: um campo inteiro novo de chamados ativos em `DepartmentResponse` e em `DepartmentSectorResponse` (DEC-5).
    - `Departments/GetAll/GetDepartmentsQueryHandler.cs`: preencher os dois campos dentro da projeção que já existe, contando os chamados com o mesmo `WhereActive()` das recusas. No departamento, contar os chamados de todos os setores dele, inclusive os desativados, como a recusa (DEC-8). Manter uma única consulta (AC-7). Comentário em pt-BR dizendo que a regra precisa bater com a RN-10.
    - Se o `WhereActive()` não puder ser usado dentro da projeção por ser `internal` em outro namespace, deixar a extensão visível para os handlers de Departments, sem duplicar o critério.
  - Web.Api: nenhuma mudança de rota (`GET departments`, mesmas permissões de hoje).
  - Testes:
    - `tests/TickestPristine.Application.UnitTests/Departments/` (teste do handler de listagem): AC-1 e AC-2;
    - `tests/TickestPristine.IntegrationTests/Departments/DepartmentsTests.cs` e `Sectors/SectorsTests.cs`: AC-4 a AC-6;
    - AC-7 conferido na revisão (T-4).
- **Contratos:** `GET departments` ganha, em cada departamento e em cada setor da lista, o inteiro de chamados ativos (nunca nulo; 0 quando não há). Nada é removido nem renomeado, então o front atual continua funcionando. O 409 de desativação não muda (DEC-4).
- **Mudança no banco:** não.
- **Front-end (`tickest-pristine-front`), depois do merge:**
  - entidades `Department` e `Sector` ganham o campo;
  - card do departamento e linha do setor com contagem maior que 0 mostram "N chamados em andamento";
  - com contagem maior que 0, a ação de desativar não abre a confirmação de exclusão. Ela explica: "Este departamento tem N chamados em andamento nos setores; conclua ou cancele antes de desativar", com um link para a fila filtrada pelo setor;
  - com contagem 0, a confirmação diz "Desativar departamento e N setores" (o verbo passa a ser o que o back faz, "Desativar" no lugar de "Excluir"), lista os setores que serão desativados, explica que deixam de aparecer na abertura de chamados e só habilita o botão quando o nome do departamento é digitado (DEC-7); um aviso só, proporcional ao impacto;
  - quando o total do departamento passar da soma dos setores listados (chamado reaberto num setor já desativado, DEC-8), a mensagem não aponta um setor: link para a fila do departamento ou sem link;
  - o 409 continua tratado, agora junto do card ou do diálogo, para o caso de corrida.
- **Documentação de estado afetada:** `SYSTEM_SPECIFICATION.md` 2.6 RN-10 (citar a contagem na listagem) e 2.7 C-07; a seção de contratos de `GET departments`, se existir.
- **Riscos:**
  - R-1: a subconsulta de contagem deixa a listagem mais lenta com muitos chamados. Os índices de `SectorId` e `Status` em `Tickets` cobrem isso; conferir o plano da consulta no teste de integração.
  - R-2: a contagem fica velha entre o carregamento da tela e o clique; o 409 continua sendo a proteção (DEC-4).

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Campos novos nas respostas e contagem na projeção da listagem | AC-1, AC-2, AC-7 | `add-feature` | [x] |
| T-2 | Testes unitários e de integração, incluindo a concordância com a recusa | AC-1, AC-2, AC-4, AC-5, AC-6 | `add-tests` | [x] |
| T-3 | Atualizar o `SYSTEM_SPECIFICATION.md` | AC-8 | — | [x] |
| T-4 | Revisão de convenções (inclui conferir a projeção única do AC-7), build sem avisos e suíte inteira | AC-7, AC-8 | `ca-review` | [x] |
| T-5 | Ajuste da DEC-8: total do departamento com todos os setores (handler e comentário), teste unitário ajustado, teste de integração da reabertura em setor desativado, RN-10 e rota no `SYSTEM_SPECIFICATION.md`, build sem avisos e suíte inteira | AC-1, AC-4, AC-7, AC-8 | `add-feature`, `add-tests` | [x] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | `Application.UnitTests/Departments/GetDepartmentsQueryHandlerTests.cs` › `Handle_Should_ReturnActiveTicketCountOfEachDepartment_WhenSectorsHaveTicketsInSeveralStatuses`, `Handle_Should_IncludeDeactivatedSectorsInDepartmentCount_WhenTheyHaveActiveTickets`; `IntegrationTests/Departments/DepartmentsTests.cs` › `GetAll_Should_ReturnActiveTicketCountOfDepartmentsAndSectors` | Com os dados comuns, o handler (banco em memória) e a API (PostgreSQL, tradução da subconsulta) devolvem TI com 3 e Financeiro com 0; o chamado ativo de um setor desativado entra no total do departamento, como na recusa (REQ-1 e DEC-8) | Aprovado (2026-10-31: unitários na execução da verificação, às 23:29; integração de departamentos e setores, 39 de 39, às 23:27, depois da T-5) |
| AC-2 | `Application.UnitTests/Departments/GetDepartmentsQueryHandlerTests.cs` › `Handle_Should_ReturnActiveTicketCountOfEachSector_WhenSectorsHaveTicketsInSeveralStatuses`; `IntegrationTests/Departments/DepartmentsTests.cs` › `GetAll_Should_ReturnActiveTicketCountOfDepartmentsAndSectors` | Em TI, Suporte informa 3 (2 Abertos e 1 Em andamento), Redes 0 (Resolvido, Cancelado e Aberto excluído não contam) e Infra 0, no handler e pela API | Aprovado (2026-10-31: unitários na execução da verificação, às 23:29; integração de departamentos e setores, 39 de 39, às 23:27, depois da T-5) |
| AC-3 | — | Removido (DEC-6) | — |
| AC-4 | `IntegrationTests/Departments/DepartmentsTests.cs` › `Deactivate_Should_ReturnConflictWithSameCodeAndText_WhenListingShowsActiveTickets`, `Deactivate_Should_ReturnNoContent_WhenActiveTicketsAreConcludedAndListingShowsZero`, `Deactivate_Should_ReturnConflictCountedByListing_WhenTicketIsReopenedInDeactivatedSector` | Com a listagem mostrando 3, o `DELETE departments/{TI}` responde 409 `Departments.HasActiveTickets`; com os 3 chamados de Suporte resolvidos pela API, a listagem mostra 0 e o mesmo `DELETE` responde 204 (os chamados resolvidos, cancelados e excluídos de Redes não bloqueiam); complementando o REQ-3 com o caso da DEC-8, pelo caminho real (resolver o chamado de Redes, desativar o setor e reabrir o chamado), a listagem mostra 1 e o `DELETE` responde 409 `Departments.HasActiveTickets` (DEC-8) | Aprovado (2026-10-31: unitários na execução da verificação, às 23:29; integração de departamentos e setores, 39 de 39, às 23:27, depois da T-5) |
| AC-5 | `IntegrationTests/Sectors/SectorsTests.cs` › `Deactivate_Should_ReturnConflictOnlyForSectorWithActiveTickets_WhenListingShowsTheirCounts` | Com a listagem mostrando Suporte 3, Redes 0 e Infra 0, o `DELETE sectors/{Suporte}` responde 409 `Sectors.HasActiveTickets`, com o código e o texto de hoje, e os de Redes e Infra respondem 204 | Aprovado (2026-10-31: unitários na execução da verificação, às 23:29; integração de departamentos e setores, 39 de 39, às 23:27, depois da T-5) |
| AC-6 | `IntegrationTests/Departments/DepartmentsTests.cs` › `Deactivate_Should_ReturnConflictWithSameCodeAndText_WhenListingShowsActiveTickets` | O problema do 409 tem o título `Departments.HasActiveTickets` e o detalhe igual, literalmente, ao texto de hoje do `DepartmentErrors.HasActiveTickets` | Aprovado (2026-10-31: unitários na execução da verificação, às 23:29; integração de departamentos e setores, 39 de 39, às 23:27, depois da T-5) |
| AC-7 | Revisão do handler (verificação descrita, DEC-6): `Departments/GetAll/GetDepartmentsQueryHandler.cs`; nova revisão independente (`ca-review`), sem bloqueantes | Uma única materialização (`ToListAsync`); a consulta dos chamados ativos (`WhereActive()`) é composta como subconsulta da projeção, no total do departamento e em cada setor, sem laço e sem consulta por departamento ou por setor; o contexto não configura consulta dividida; depois da DEC-8, o total do departamento usa o mesmo predicado da recusa | Verificado na revisão (2026-10-31, verificação) |
| AC-8 | `dotnet build TickestPristine.slnx --no-incremental` e `dotnet test TickestPristine.slnx --no-build --logger trx` (relatório TRX por teste) | Na verificação: build com 0 avisos e 0 erros; 57 de arquitetura e 840 unitários aprovados; a suíte de integração foi interrompida a pedido de Rafael Santos, com os 39 testes de integração de departamentos e setores aprovados depois da T-5 e a última suíte completa (514 de 514) anterior à DEC-8 | Aprovado em parte (2026-10-31); a suíte inteira fica para o CI do PR, por dispensa de Rafael Santos |

## 9. Evidências de verificação
- **Build:** `dotnet build TickestPristine.slnx --no-incremental` em 2026-10-31, 23:29: 0 avisos, 0 erros.
- **Testes:** na execução da verificação (2026-10-31, 23:29), 57 de arquitetura e 840 unitários aprovados, 0 falhas; a suíte de integração foi interrompida a pedido de Rafael Santos ("pode parar esses testes. pule de etapa"). Depois da T-5, os testes de integração de departamentos e setores passaram (39 de 39, às 23:27, incluído o da reabertura). A última suíte de integração completa (514 de 514, das 23:15 às 23:21) é anterior à DEC-8, que só mudou o predicado do total do departamento e os testes da listagem. A suíte inteira fica para o CI do PR. Auditoria `python scripts/auditoria_sdd.py .`: 100%, CONFORME, 0 falhas críticas, código de saída 0; `scripts/test_instructions.py`: OK.
- **Revisão:** a primeira revisão independente (`ca-review`) apontou 1 bloqueante (o total do departamento só com setores ativos contra a recusa, que olha todos os setores, pela reabertura em setor desativado), resolvido pela DEC-8 na T-5. A nova revisão independente não tem bloqueantes; dos achados não bloqueantes, as evidências foram atualizadas nesta verificação, a linha do AC-3 e o plano do front foram corrigidos pela DEC-8, e o detalhe de estilo do teste da reabertura (`PostAsJsonAsync` com corpo vazio, em vez do `PostAsync` sem corpo dos testes vizinhos) foi mantido.
- **Documentação atualizada:** `SYSTEM_SPECIFICATION.md`: na T-3 e na T-5 (DEC-8), a RN-10, a C-07 e a rota `GET departments`; na verificação, a tabela 6.1. O cabeçalho "Estado documentado" não foi mudado (fora do plano; a branch parte de `ef1c981`, sem a spec 045).
- **CI:**
- **Entrega:** PR #60 (https://github.com/rafaelps15/tickest-pristine/pull/60), aberto em 2026-11-01 para a `main`.

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-27 | Rascunho | Criada a partir da crítica de UX da tela Departamentos do front, a pedido de Rafael Santos no chat ("pode registrar"). |
| 2026-10-27 | Em descoberta | Descoberta feita no código (handlers de desativação, listagem e `WhereActive`). DEC-1 a DEC-5 com recomendação, aguardando a resposta e a aprovação explícita de Rafael Santos. |
| 2026-10-28 | Em descoberta | Rafael Santos aceitou as recomendações ("siga a resolução em padrão de sistemas de alto nível"): DEC-1 a DEC-5 decididas; DEC-6 remove o AC-3 e passa o AC-7 para a revisão; DEC-7 descarta a restrição por autor e manda a reativação para a spec 033. Sem `[SUPOSIÇÃO]`; aguardando aprovação explícita. |
| 2026-10-28 | Aprovada | Aprovada por Rafael Santos no chat ("Quer aprovar a 022…?" → "sim"). |
| 2026-10-31 | Em implementação | `/spec-implement 022` iniciado por Rafael Santos na branch `feat/SPEC-022-active-ticket-count-departments`, criada da `origin/main` em `ef1c981` (sem a spec 045, cujo PR #59 ainda está aberto), com a instrução de implementar a spec inteira, sem commits intermediários, e apresentar um relatório final antes de qualquer commit. |
| 2026-10-31 | Em implementação | T-1 a T-4 concluídas na árvore de trabalho, sem commits, como pedido: `activeTicketCount` no departamento e no setor da listagem, contado pelo `WhereActive()` dentro da projeção única; 3 testes unitários e 4 de integração novos; `SYSTEM_SPECIFICATION.md` (RN-10, C-07 e a rota `GET departments`). Build com 0 avisos e 1.411 testes aprovados. Achado pendente de decisão de Rafael Santos: a premissa da DEC-6 não vale, porque a reabertura não confere o setor e um chamado reaberto num setor já desativado volta a ser ativo; nesse caso, a recusa da desativação do departamento (todos os setores) e a contagem do REQ-1 (setores ativos) discordam. A implementação segue o REQ-1 como aprovado. |
| 2026-10-31 | Em implementação | Verificação reprovada: a revisão independente (`ca-review`) apontou 1 bloqueante, o total do departamento só com setores ativos contra a recusa da desativação, que olha todos os setores (pelo caminho de reabertura em setor desativado, contagem 0 e 409); AC-7, convenções e testes sem outros achados; auditoria 100%. A suíte da verificação foi interrompida, porque o código muda. Decisão de Rafael Santos registrada como DEC-8, com o REQ-1 e o plano ajustados e a T-5 nova. A documentação de estado fica para a próxima verificação. |
| 2026-10-31 | Em implementação | T-5 (DEC-8): o total do departamento passa a contar os chamados de todos os setores dele, com o mesmo predicado da recusa, na mesma projeção única; o teste unitário do setor desativado passa a esperar o chamado no total, e entra o teste de integração do caminho real (desativar o setor, reabrir o chamado e tentar desativar o departamento: 409, com a contagem 1); RN-10 e rota ajustadas. Build com 0 avisos; 4 testes unitários da listagem e 39 de integração de departamentos e setores aprovados. |
| 2026-10-31 | Verificada | Segunda verificação, depois da DEC-8: build com 0 avisos; 57 de arquitetura e 840 unitários aprovados; a suíte de integração completa foi interrompida a pedido de Rafael Santos ("pode parar esses testes. pule de etapa"), com os 39 testes de departamentos e setores aprovados depois da T-5 e a suíte inteira a confirmar no CI do PR; auditoria 100%; nova revisão independente sem bloqueantes. Próximo passo: commit autorizado por Rafael Santos e PR. |
