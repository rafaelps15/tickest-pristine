---
id: 045
titulo: Chamado e SLA — agregados, ciclo do SLA e transições por intenção
status: Em implementação      # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-31
branch: refactor/SPEC-045-ticket-sla-aggregates                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr:                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 045 — Chamado e SLA: agregados, ciclo do SLA e transições por intenção

## 1. Contexto
- **Pedido original (Rafael Santos, 2026-10-30):** "The final architectural design is approved, subject to the implementation safeguards below. Proceed with SPEC-045 creation and its SDD workflow." O desenho veio de quatro rodadas de investigação no chat (relatório inicial da remodelagem do `Ticket`, revisão da D-1, documento de decisão e consolidação final), todas sem alteração de código.
- **Por que:** depois da spec 041, o `Ticket` ainda cria e muda cronômetros de SLA, que são outro agregado (com transação e tokens próprios); o `Update` genérico aceita qualquer status e deixa as regras de transição e de cancelamento no handler; o prazo do chamado e o do cronômetro saem de dois cálculos; o `TicketSlaTimer.Complete` público deixa um handler concluir um ciclo de resolução sem passar pelo chamado; e a primeira resposta classifica o autor por uma fonte (cache de permissões) diferente da do "aguardando" (banco).
- **Referências:**
  - Código: `src/TickestPristine.Domain/Tickets/Ticket.cs`, `TicketSlaTimer.cs`, `TicketSlaPlan.cs`, `TicketMessage.cs`, `TicketUpdatedDomainEvent.cs`, `TicketStatusChangedDomainEvent.cs`; `Domain/SlaCalendars/BusinessCalendar.cs`; `Application/Tickets/{Create,Update,Close,Reopen,Delete}/`, `Application/TicketMessages/Post/PostTicketMessageCommandHandler.cs`, `Application/Tickets/TicketAgentLookup.cs`; `Infrastructure/Database/Seeding/SampleDataSeeder.cs`; `Infrastructure/Tickets/TicketConfiguration.cs:53` (`xmin`), `TicketSlaTimerConfiguration.cs:23-25` (tokens) e o índice `ix_ticket_sla_timers_ticket_id_metric_cycle`; `Web.Api/Infrastructure/GlobalExceptionHandler.cs`.
  - Documento de estado: `SYSTEM_SPECIFICATION.md` RN-01, RN-02, RN-03, RN-06, RN-26, RN-27, RN-32, RN-34, C-14 e a lista de eventos.
  - Skills: `domain-modeling` (padrões 1, 2, 6, 7, 8, 9 e 10), `add-entity` (`rich-entity.md`), `add-feature` (`rich-domain-handler.md`, `authorization.md`).
  - Referências estudadas: Milan (`event-reminder`, template, Evently) e Kamil (`modular-monolith-with-ddd`), conforme a seção 2.3 da spec 041.
  - Specs: 005 (REQ-6, AC-10), 006, 009 (REQ-3, DEC-10), 016, 027, 029 (REQ-2, DEC-4), 039, 041 (DEC-11, DEC-24 a DEC-33), 043.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | **Fronteiras.** `Ticket` e `TicketSlaTimer` são raízes de agregado independentes, ligadas pelo id do chamado; `TicketMessage` e `TicketAttachment` continuam raízes próprias; `TicketHistory` é um registro só de inclusão; `TicketSlaPlan` e `BusinessCalendar` são objetos de valor. Nenhum método público do `Ticket` recebe `TicketSlaTimer`. | DEC-1, DEC-2 |
| REQ-2 | **Fórmula única de prazo.** `SlaDeadlines` (record com `WarningOnUtc` e `DueOnUtc`) e `TicketSlaPlan.GetDeadlines(SlaMetric, BusinessCalendar, DateTime startedAtUtc)`, que devolve `null` quando o plano não tem meta para o objetivo, são a única implementação do cálculo: meta em ticks (`TimeSpan.TicksPerMinute * metas`), alerta em `(long)(meta.Ticks * 0,75)` e os dois pelo `BusinessCalendar.AddBusinessTime`, como hoje. `TicketSlaTimer.Create` recebe os valores calculados; não calcula. | DEC-5 |
| REQ-3 | **Abertura.** `Ticket.Create` continua sendo a fábrica pública do agregado e grava no inicializador o retrato RN-02 (`DueAtUtc` e `SlaWarningAtUtc`) pelo `GetDeadlines(Resolution, …)`. `TicketSlaCycleService.Start(ticket, calendar)` cria os cronômetros do ciclo 1: o de resolução com os prazos já gravados no chamado (sem segundo cálculo) e o de primeira resposta, quando o plano tem essa meta. O `RecordOpening` devolve só a entrada de abertura; sai o `_openingSlaTimers`. | DEC-3, DEC-5 |
| REQ-4 | **Serviço de domínio.** `TicketSlaCycleService` segue o padrão 6: classe `sealed` de instância em `Domain/Tickets/`, criada pelo handler com `new`, sem repositório nem infraestrutura, falhas de negócio em `Result`, mudanças só pelos métodos dos agregados. Operações: `Start`, `Resolve`, `Cancel`, `Reopen` e `Delete`. | DEC-1, DEC-3 |
| REQ-5 | **Transições por intenção no `Ticket`.** Públicos: `ChangeDescription` (devolve `TicketHistory?`; nulo, sem histórico e sem evento quando a descrição não muda), `StartProgress` e `Close` (`Result<TicketHistory>`, com a transição conferida na entidade), `Assign`, `Unassign`, `PostMessage`. `internal`, chamados só pelo serviço: `Resolve`, `Cancel` (com a janela do dono e as permissões da RN-32), `Reopen` e `Delete`. Sai o `Update` genérico e o `EnsureCanBeCanceled` avulso. As perguntas do padrão 9 (`IsActive`, `CanTransitionTo`, `IsAssignedTo`, `IsParticipant`) e os estáticos sobre colunas (`GetSlaStatus`, `CanBeCanceled`, `OwnerCancelDeadline`, DEC-31 da 041) continuam. | DEC-4, DEC-6 |
| REQ-6 | **Cronômetro.** `TicketSlaTimer.Create`, `Complete`, `Stop` e `Dequeue` passam a `internal`. Entra o público `CompleteFirstResponse(Ticket ticket, Guid authorUserId, bool authorIsAgent, DateTime utcNow)`, que devolve `false` quando o cronômetro não é a primeira resposta do chamado, já está fechado, o autor é o solicitante ou não atende. Continuam públicos `ProcessDue`, `GetState`, `GetConsumed` e `IsOpen`. | DEC-7 |
| REQ-7 | **Atomicidade das operações.** Uma operação do serviço ou do `Ticket` que devolve falha (ou que lança exceção no cálculo de prazo) não deixa o `Ticket` nem nenhum `TicketSlaTimer` alterado em memória: estado, eventos e cronômetros ficam como antes. Para isso, tudo o que pode falhar é conferido ou calculado antes da primeira mudança: no `Reopen`, a conferência de ciclo aberto e o cálculo do novo cronômetro vêm antes de `ticket.Reopen`; `Complete` e `Stop` só rodam depois que o método do `Ticket` teve sucesso. | DEC-8 |
| REQ-8 | **Invariante do ciclo aberto.** No máximo um cronômetro de resolução aberto por chamado: `Resolve` e `Cancel` fecham o ciclo aberto, `Reopen` recusa com `TicketErrors.ResolutionCycleAlreadyOpen` quando recebe um ciclo de resolução aberto, e o ciclo novo é o maior + 1 entre os cronômetros do próprio chamado. Os cronômetros de outro chamado são ignorados. Todo caminho que abre ou fecha ciclo grava a linha do chamado (`xmin`) no mesmo `SaveChangesAsync`. | DEC-9 |
| REQ-9 | **Primeira resposta (DEC-29 da 041).** O `PostTicketMessageCommandHandler` classifica o autor pelo `TicketAgentLookup.IsAgentAsync` (ativo e com `tickets:manage` efetiva nas roles gravadas) e passa a classificação ao `Ticket.PostMessage` e ao `CompleteFirstResponse`; o `IPermissionProvider` continua só para autorizar a publicação (participante ou `tickets:manage`). Setor e atribuição não contam. | DEC-10 |
| REQ-10 | **Eventos.** Sai o `TicketUpdatedDomainEvent`. Entra o `TicketDescriptionChangedDomainEvent(Guid TicketId, string PreviousDescription)` (padrão 10), disparado só quando a descrição muda; as transições continuam com o `TicketStatusChangedDomainEvent` e o `TicketReopenedDomainEvent`; os demais eventos não mudam. | DEC-11 |
| REQ-11 | **Handlers e seeder.** Abrir, `PUT tickets/{id}`, fechar, reabrir, excluir e publicar mensagem usam as operações novas; o seeder abre pelo `Ticket.Create` + `Start` e fecha pelo `Close`. O contrato HTTP não muda: rotas, payloads, códigos, mensagens e a ordem dos erros (404, 403, 400). | DEC-12 |
| REQ-12 | **Conflito do índice de ciclo.** A violação de unicidade (`23505`) do índice `ix_ticket_sla_timers_ticket_id_metric_cycle` responde 409, como o conflito de concorrência; nenhuma outra violação muda. | DEC-13 |
| REQ-13 | **Comportamento preservado.** Fora o que as DECs desta spec mudam (eventos, histórico do `PUT` sem mudança, DEC-29), nenhum comportamento observável muda: SLA (RN-01, RN-02, RN-26, RN-27), cancelamento (RN-32), histórico, última atividade e "aguardando" (DEC-11 da 041). | DEC-12 |
| REQ-14 | **Documentação.** `SYSTEM_SPECIFICATION.md` atualizado (incluída a limitação de proteção contra alteração externa do banco). No `CLAUDE.md`, só o desvio do evento de alteração do chamado (DEC-14) e as atualizações adiadas pela DEC-34 da spec 041 (seção "Em migração", linha "Histórico de chamados" e seção de eventos de domínio) (DEC-17); nenhuma outra parte muda. | DEC-14, DEC-17 |

## 3. Critérios de aceitação
| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | 1, 6 | `tests/TickestPristine.ArchitectureTests` | `dotnet test` | nenhum método público do `Ticket` recebe `TicketSlaTimer` ou `BusinessCalendar` além do `Create`; o `TicketSlaTimer` só expõe como públicos `CompleteFirstResponse`, `ProcessDue`, `GetState`, `GetConsumed` e `IsOpen`; o `TicketSlaCycleService` é `sealed` e não tem dependências no construtor |
| AC-2 | 2 | cada prioridade, os planos Padrão e Legado e os calendários "Expediente" e "24 horas" | `TicketSlaPlan.GetDeadlines` | os mesmos prazos e alertas de hoje, incluídos 75% de 30 minutos = 22 min 30 s, o fim na borda da faixa e `null` para a primeira resposta do plano legado; os testes de prazo atuais passam sem mudar as asserções |
| AC-3 | 3 | um chamado aberto pela API ou pelo `Ticket.Create` + `Start` | a abertura | `DueAtUtc` e `SlaWarningAtUtc` iguais aos do cronômetro de resolução do ciclo 1; primeira resposta só quando o plano tem meta; o `RecordOpening` devolve só a entrada `Created` |
| AC-4 | 4, 8 | um chamado Em andamento com os cronômetros do ciclo 1, e cronômetros de outro chamado na mesma coleção | `Resolve` e `Cancel` do serviço | resolver conclui o ciclo corrente de resolução e a primeira resposta aberta com causa `Resolution`; cancelar para os abertos; os cronômetros do outro chamado não mudam |
| AC-5 | 4, 8 | um chamado Resolvido (ou Cancelado) com o ciclo 1 fechado | `Reopen` do serviço | status Aberto; um ciclo 2 de resolução com a meta integral; prazos do chamado e `ResolvedAtUtc` sem mudança |
| AC-6 | 7, 8 | um chamado inativo com um ciclo de resolução ainda aberto (dado inconsistente) | `Reopen` do serviço | falha `Tickets.ResolutionCycleAlreadyOpen`; o chamado e os cronômetros sem nenhuma mudança e sem eventos; nenhum cronômetro novo |
| AC-7 | 7 | para cada falha: `Resolve` com chamado Aberto e com chamado inativo; `Cancel` com o dono fora da janela, sem permissão e com chamado inativo; `Reopen` com chamado ativo e com transição inválida | a operação | `Result` com o erro de hoje; `Status`, `ResolvedAtUtc`, `LastActivityAtUtc`, `WaitingOn` e `DomainEvents` do chamado iguais aos de antes; `CompletedOnUtc`, `CompletionCause`, `StoppedOnUtc`, `NextActionOnUtc` e `DomainEvents` de cada cronômetro iguais aos de antes; nenhum cronômetro novo |
| AC-8 | 7 | um calendário sem tempo útil em 366 dias (o cálculo lança `InvalidOperationException`) | `Reopen` do serviço | a exceção sai antes de qualquer mudança: o chamado continua inativo, sem eventos, e os cronômetros não mudam |
| AC-9 | 5, 10 | um chamado ativo | `ChangeDescription` com a mesma descrição; depois com outra | a primeira devolve `null`, sem histórico e sem evento; a segunda devolve a entrada `DescriptionChanged` e dispara `TicketDescriptionChangedDomainEvent` com a descrição anterior |
| AC-10 | 5, 11 | um chamado nos estados de cada transição | `StartProgress`, `Close`, `Assign`, `Unassign` | as transições válidas mudam o status, gravam o histórico e disparam `TicketStatusChangedDomainEvent`; as inválidas devolvem `Tickets.InvalidStatusTransition` ou `Tickets.NotActive`, sem mudança |
| AC-11 | 11, 13 | as requisições de `PUT tickets/{id}`, `POST tickets/{id}/close`, `POST tickets/{id}/reopen` (com e sem motivo), `DELETE tickets/{id}` e `POST tickets` cobertas pelos testes de integração de hoje | a suíte de integração | os mesmos códigos, mensagens, histórico, cronômetros, última atividade e "aguardando" de hoje; um `PUT` sem mudança de descrição nem de status responde 204 e não grava histórico |
| AC-12 | 6, 9 | um atendente (ativo, `tickets:manage` nas roles gravadas, não solicitante), o solicitante, um responsável sem `tickets:manage` e um usuário que perdeu `tickets:manage` com o cache de permissões ainda válido | a primeira mensagem de cada um | só a do atendente conclui a primeira resposta com causa `Message`; o usuário que perdeu a permissão não conclui, porque o `TicketAgentLookup` lê o banco |
| AC-13 | 6 | um cronômetro de resolução, um de outro chamado e um já fechado | `CompleteFirstResponse` | devolve `false` e não muda nada |
| AC-14 | 8, 12 | duas reaberturas simultâneas do mesmo chamado | as duas requisições | uma responde 204 e a outra 409; o chamado tem um só ciclo novo de resolução |
| AC-15 | 11 | `SampleData` ligado num banco sem chamados | o seed roda | chamados, histórico e cronômetros iguais aos da spec 041 (comparação descrita, como nas specs 039 e 041), com os chamados fechados pelo `Close` |
| AC-16 | 13 | a suíte inteira | `dotnet build` e `dotnet test` | 0 avisos; todos os testes aprovados; `Model_Should_HaveNoPendingChanges` aprovado (sem migration) |
| AC-17 | 14 | `SYSTEM_SPECIFICATION.md` e `CLAUDE.md` | leitura, `git diff` do `CLAUDE.md` e `scripts/test_instructions.py` | RN, C-14, eventos e a limitação do REQ-8 registrados com a spec 045; no `CLAUDE.md`, só mudaram o desvio do evento de alteração do chamado e as três partes da DEC-17: "Em migração" sem os itens migrados pelas specs 041 e 045, "Histórico de chamados" e a seção de eventos de domínio de acordo com o código; `test_instructions.py` verde |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Fronteira entre `Ticket` e `TicketSlaTimer` (D-1) | (a) o `Ticket` recebe e muda os cronômetros / (b) cronômetros dentro do agregado `Ticket` / (c) raízes independentes + serviço de domínio / (d) raízes independentes + evento | (c). O cronômetro é carregado e gravado sem o chamado (monitoramento, primeira resposta) e tem tokens próprios; o padrão 6 descreve a regra entre agregados. Rafael Santos no chat: "We have reviewed the initial report and are provisionally favoring alternative C for D-1" e, na consolidação, "Keep TicketSlaTimer as an independent aggregate root" | Rafael Santos, 2026-10-30 |
| DEC-2 | Classificação dos demais | — | `TicketMessage` e `TicketAttachment` raízes próprias; `TicketHistory` registro só de inclusão; `TicketSlaPlan` e `BusinessCalendar` objetos de valor. Rafael Santos no chat: "The revised design is directionally approved" e "TicketHistory remains an append-only record" | Rafael Santos, 2026-10-30 |
| DEC-3 | Escopo do serviço | (a) o serviço também abre o chamado e publica mensagens / (b) só as transições com efeito no SLA | (b): `Start`, `Resolve`, `Cancel`, `Reopen` e `Delete`; `Ticket.Create` continua a fábrica e o `Ticket.PostMessage` fica fora. Rafael Santos no chat: "Keep Open and PostMessage outside TicketSlaCycleService" | Rafael Santos, 2026-10-30 |
| DEC-4 | `Update` genérico | (a) manter / (b) transições por intenção | (b). Rafael Santos no chat: "Use intention-revealing Ticket operations instead of the generic status-changing Update method" | Rafael Santos, 2026-10-30 |
| DEC-5 | Fórmula de prazo | (a) no cronômetro, como hoje / (b) `TicketSlaPlan.GetDeadlines`, com `SlaDeadlines` nomeado | (b). Rafael Santos no chat: "Move the single source of SLA deadline calculation to TicketSlaPlan.GetDeadlines" e "SlaDeadlines is a named record rather than an anonymous tuple" | Rafael Santos, 2026-10-30 |
| DEC-6 | Visibilidade | (a) tudo público, com a regra só por convenção / (b) `internal` nas operações com efeito no SLA | (b): `Ticket.Resolve`, `Cancel`, `Reopen` e `Delete`; `TicketSlaTimer.Create`, `Complete`, `Stop` e `Dequeue`. Os testes de domínio do que é interno passam pelo serviço; sem `InternalsVisibleTo`. Rafael Santos no chat: "Make the specified Ticket lifecycle methods and timer lifecycle methods internal" | Rafael Santos, 2026-10-30 |
| DEC-7 | Conclusão da primeira resposta | (a) no serviço / (b) método do cronômetro que recebe o chamado | (b): `TicketSlaTimer.CompleteFirstResponse(ticket, …)`, como o agregado relacionado passado como argumento (Kamil, `Meeting.AddAttendee`). Rafael Santos no chat: "TicketSlaTimer.CompleteFirstResponse handles first-response completion" | Rafael Santos, 2026-10-30 |
| DEC-8 | Atomicidade | — | Toda conferência e todo cálculo que pode falhar ou lançar exceção vêm antes da primeira mudança; o retorno `bool` de `Complete`/`Stop` não é tratado como prova de segurança, e os testes do AC-7 e do AC-8 conferem o estado completo. Rafael Santos no chat: "A failed Result must never leave Ticket or TicketSlaTimer instances partially mutated" e "Pay particular attention to Complete, Stop, and timer creation during Reopen. A bool return value alone does not prove that the operation is failure-safe." | Rafael Santos, 2026-10-30 |
| DEC-9 | Invariante do ciclo aberto e índice parcial | (a) só aplicação (domínio + `xmin`) / (b) também um índice único parcial | (a), com a limitação documentada: não há proteção contra alteração externa do banco. Erro novo `ResolutionCycleAlreadyOpen`. Rafael Santos no chat: "Introduce TicketErrors.ResolutionCycleAlreadyOpen" e "Do not introduce the partial unique index in this delivery. Document the limitation" | Rafael Santos, 2026-10-30 |
| DEC-10 | Fonte de "quem atende" na primeira resposta (DEC-29 da 041) | (a) `IPermissionProvider` (cache) / (b) `TicketAgentLookup` (banco) | (b); autorização e classificação separadas; setor e atribuição não contam. Rafael Santos no chat: "Adopt TicketAgentLookup as the authoritative source for determining whether a message author qualifies as an agent for first-response SLA purposes" | Rafael Santos, 2026-10-30 |
| DEC-11 | Eventos da mudança do chamado | (a) manter o `TicketUpdatedDomainEvent` sempre / (b) eventos por intenção | (b): `TicketDescriptionChangedDomainEvent` com o valor anterior, só quando a descrição muda; `TicketStatusChangedDomainEvent` nas transições; sem evento quando nada muda. Rafael Santos no chat: "Replace the generic TicketUpdatedDomainEvent behavior with intention-specific events" | Rafael Santos, 2026-10-30 |
| DEC-12 | Contrato HTTP e comportamento | — | O contrato do `PUT` e das demais rotas não muda; a DEC-11 da 041 (última atividade e "aguardando") continua. Rafael Santos no chat: "Keep the HTTP contract unchanged" e "Preserve DEC-11 for LastActivityAtUtc and WaitingOn" | Rafael Santos, 2026-10-30 |
| DEC-13 | Conflito do índice de ciclo | (a) deixar como erro 500 / (b) 409 só para esse índice | (b), sem converter outras violações. Rafael Santos no chat: "Map violations of the specific unique cycle constraint to an appropriate concurrency/conflict response, without broadly converting database errors into HTTP 409" | Rafael Santos, 2026-10-30 |
| DEC-14 | `CLAUDE.md` | — | Ajuste restrito: o desvio "`Update` de departamento, setor e chamado … disparam o evento de alteração sempre" deixa de citar o chamado e registra o comportamento novo. Rafael Santos no chat: "You are explicitly authorized to make the narrowly scoped CLAUDE.md adjustment required to remove the obsolete Ticket.Update event exception and document the approved behavior." | Rafael Santos, 2026-10-30 |
| DEC-15 | Ordem das specs | — | 041 → 045 → 043 parte 1 → 043 parte 2. A 043 parte 1 não depende desta spec no código de produção (as consultas usam só `Ticket.GetSlaStatus`, `CanBeCanceled`, `OwnerCancelDeadline`, `TicketSlaTimer.GetState`/`GetConsumed` e `BusinessCalendar`, que não mudam), mas usa os auxiliares de teste que esta spec muda; a parte 2 depende da orquestração nova. Rafael Santos no chat: "Use this order: 041 → 045 → 043 part 1 → 043 part 2." | Rafael Santos, 2026-10-30 |
| DEC-16 | Skill de desenho de agregados | (a) criar ou preparar nesta spec / (b) entrega separada, depois desta spec validada | (b). A skill reutilizável, baseada principalmente nos princípios de desenho de agregados de Kamil Grzybek e complementada pelas práticas de DDD e .NET de Milan Jovanović, é uma entrega futura e separada: ensina raciocínio arquitetural e tomada de decisão, extrai os princípios gerais deste trabalho e não transforma as decisões do Tickest em regras universais. Sequência: concluir e validar a 041; implementar e validar a 045; criar e validar a skill; seguir com a 043 parte 1 e a parte 2. Rafael Santos no chat: "Do not create, modify, or prepare this skill as part of SPEC-045" e "The skill must teach architectural reasoning and decision-making, not merely prescribe patterns or reproduce the current Tickest implementation." | Rafael Santos, 2026-10-30 |
| DEC-17 | Atualizações do `CLAUDE.md` adiadas pela spec 041 (DEC-34 dela) | (a) só o ajuste da DEC-14 / (b) também as três partes adiadas | (b), restrito a três partes: "Em migração", "Histórico de chamados" e a seção de eventos de domínio. Nenhuma outra alteração no `CLAUDE.md`. Rafael Santos no chat: "Amplio a autorização para atualizar o `CLAUDE.md` na T-8 da SPEC-045, incluindo estritamente os três pontos adiados pela DEC-34 da SPEC-041" e "Essa autorização não permite outras alterações no `CLAUDE.md`." | Rafael Santos, 2026-10-31 |

## 5. Fora do escopo
- Pausa e retomada do SLA (C-15).
- Índice único parcial do ciclo de resolução aberto (DEC-9).
- Limite de anexos por chamado sob concorrência: dois envios simultâneos podem passar do limite; fica para uma spec própria.
- Última atividade e "aguardando" como projeção por evento: continua a DEC-11 da 041.
- Persistência com Dapper (spec 043).
- Mudança na criação de `TicketMessage` e `TicketAttachment` além do REQ-9.
- A skill reutilizável de desenho de agregados (DEC-16): nada dela é criado, alterado ou preparado nesta spec.
- Edição simultânea da mesma mensagem (hoje vence a última) e mensagens em chamado inativo (Q-07).

## 6. Plano técnico
- **Arquivos a criar:** `Domain/Tickets/SlaDeadlines.cs`, `TicketSlaCycleService.cs`, `TicketDescriptionChangedDomainEvent.cs`; testes do serviço e da fórmula.
- **Arquivos a alterar:** `Domain/Tickets/Ticket.cs`, `TicketSlaTimer.cs`, `TicketSlaPlan.cs`, `TicketErrors.cs`; handlers de `Tickets/Create`, `Update`, `Close`, `Reopen`, `Delete` e `TicketMessages/Post`; `SampleDataSeeder.cs`; `GlobalExceptionHandler.cs`; auxiliares e testes (`TicketTestData`, `BaseHandlerTest`, testes de entidade, de handler, de integração e de arquitetura); `SYSTEM_SPECIFICATION.md`; `CLAUDE.md` (DEC-14 e DEC-17).
- **Arquivo a remover:** `Domain/Tickets/TicketUpdatedDomainEvent.cs`.
- **Contratos:** rotas, payloads e erros iguais. Erro de domínio novo `Tickets.ResolutionCycleAlreadyOpen` (não alcançável pela API com dados consistentes). Eventos: sai `TicketUpdatedDomainEvent`, entra `TicketDescriptionChangedDomainEvent`; nenhum handler consome esses eventos.
- **Mudança no banco:** não.
- **Dependência:** esta spec parte do código da 041, mergeada na `main` pelo PR #57 em 2026-10-31 (merge `ef1c981`). A branch desta spec é criada a partir da `origin/main` atualizada.
- **Riscos:**

| Risco | Mitigação |
|---|---|
| Mudança parcial em memória numa falha | DEC-8; testes do AC-7 e do AC-8 com o estado completo |
| Handler que esquece o `Start` na abertura | Testes de integração de abertura (AC-3) e do seed (AC-15) |
| A primeira resposta muda numa janela de cache defasado (DEC-10) | Esperado pela decisão; AC-12 |
| Muitos testes arranjados pelo `Update` | Mudança mecânica nos auxiliares; asserções preservadas |
| Ciclo aberto inserido fora da aplicação | Limitação documentada (DEC-9) |

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Linha de base: branch da `origin/main` com a 041 mergeada, build e testes | AC-16 | manual | [x] |
| T-2 | Fórmula única: `SlaDeadlines`, `TicketSlaPlan.GetDeadlines`, `TicketSlaTimer.Create` com os valores, `Ticket.Create` com o retrato | AC-2, AC-3 | domain-modeling, add-tests | [x] |
| T-3 | Cronômetro: visibilidade interna e `CompleteFirstResponse`; publicação com o `TicketAgentLookup` | AC-12, AC-13 | domain-modeling, add-tests | [ ] |
| T-4 | `Ticket` por intenção e `TicketSlaCycleService` (`Start`, `Resolve`, `Cancel`, `Reopen`, `Delete`), `ResolutionCycleAlreadyOpen`, eventos novos | AC-4, AC-5, AC-6, AC-7, AC-8, AC-9, AC-10 | domain-modeling, add-tests | [ ] |
| T-5 | Handlers e seeder nas operações novas | AC-3, AC-11, AC-15 | add-feature, add-tests | [ ] |
| T-6 | Conflito do índice de ciclo → 409 | AC-14 | add-feature, add-tests | [ ] |
| T-7 | Testes de arquitetura e suíte completa | AC-1, AC-16 | add-tests | [ ] |
| T-8 | `SYSTEM_SPECIFICATION.md` e `CLAUDE.md` (DEC-14 e as três partes da DEC-17) | AC-17 | manual | [ ] |
| T-9 | Verificação | todos | spec-verify | [ ] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-2 | `Application.UnitTests/Tickets/TicketSlaPlanTests.cs` › `GetDeadlines_Should_ReturnWarningAtThreeQuartersAndDueAtTarget_WhenPlanIsDefault`, `GetDeadlines_Should_CountContinuousTime_WhenCalendarIsContinuous`, `GetDeadlines_Should_ReturnResolutionHoursAndNoFirstResponse_WhenPlanIsLegacy`; os testes de prazo atuais (`TicketSlaTimerTests` › `RecordOpening_Should_CreateCycleOneTimersWithDeadlinesInBusinessTime`, `Reopen_Should_StartNewResolutionCycleWithFullTarget`; `TicketSlaPlanTests` › `Create_Should_SetDeadlinesInResolutionHoursOfPriority_WhenPlanIsLegacy`; `BusinessCalendarTests`) sem mudança nas asserções | Cada prioridade do plano Padrão no "Expediente" (22 min 30 s no alerta da Urgente, fim às 18:00 na borda da faixa), o "24 horas" e o plano Legado com `null` na primeira resposta | Aprovado (T-2) |
| AC-3 | `TicketSlaPlanTests` › `Create_Should_SetResolutionDeadlinesInBusinessTime_WhenPlanIsDefault`, `Create_Should_SetDeadlinesInResolutionHoursOfPriority_WhenPlanIsLegacy`; `TicketSlaTimerTests` › `Create_Should_TakeDeadlinesFromCycleOneResolution`, `RecordOpening_Should_CreateCycleOneTimersWithDeadlinesInBusinessTime` | O retrato do chamado tem os valores esperados da resolução (Padrão no "Expediente" e Legado no "24 horas") e é igual ao cronômetro de resolução do ciclo 1, com a meta e o calendário do plano; o `Start` e o `RecordOpening` só com a entrada `Created` entram na T-4 e na T-5 | Parcial (T-2) |
| AC-1, AC-4 a AC-17 | a preencher na implementação | | |

## 9. Evidências de verificação
- **Build:**
- **Testes:**
- **Revisão:**
- **Documentação atualizada:**
- **CI:**
- **Entrega:**

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-30 | Rascunho | Spec criada a partir do pedido de Rafael Santos, depois das quatro rodadas de investigação arquitetural registradas no chat. |
| 2026-10-30 | Em descoberta | Descoberta feita nas rodadas do chat; as respostas de Rafael Santos viraram as DEC-1 a DEC-15. Checklist de completude atendido. A implementação depende do merge da 041, que ainda não ocorreu. Aguarda aprovação. |
| 2026-10-30 | Em descoberta | Esclarecimento de escopo de Rafael Santos: a skill de desenho de agregados fica fora desta spec e vem depois dela validada, antes da 043 (DEC-16). |
| 2026-10-31 | Em descoberta | Ajustes antes da aprovação: a dependência passa a citar o merge da 041 (PR #57, `ef1c981`); DEC-17 registra a ampliação da autorização do `CLAUDE.md` para as três partes adiadas pela DEC-34 da 041, com o REQ-14, o AC-17 e a T-8 ajustados a ela. Os demais requisitos, critérios, decisões e tarefas não mudaram. |
| 2026-10-31 | Aprovada | Aprovação formal de Rafael Santos no chat: "A SPEC-045 está formalmente aprovada em 31/10/2026." A autorização do `CLAUDE.md` continua limitada aos três pontos da DEC-17. |
| 2026-10-31 | Em implementação | `/spec-implement 045` iniciado por Rafael Santos. T-1 aprovada: na branch criada da `origin/main` (`ef1c981`), build com 0 avisos e 1.404 testes aprovados (837 unitários, 57 de arquitetura, 510 de integração, incluído o `Model_Should_HaveNoPendingChanges`). |
