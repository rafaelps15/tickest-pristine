---
id: 025
titulo: Filtro de período (abertura e resolução) e vários setores na listagem de chamados
status: Em descoberta        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em:
branch: feat/SPEC-025-tickets-period-filter                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr:                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 025 — Filtro de período (abertura e resolução) e vários setores na listagem de chamados

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat da sessão do front-end (`tickest-pristine-front`), em 2026-10-28: "sim! pode criar na pasta do back", aprovando transformar em specs os itens "depende do back-end" do relatório do Início operacional (dashboard de chamados nível ITSM). Este é o item 1: filtro de período.
- **Por que:** o Início ganhou filtros de setor, prioridade e responsável, mas não tem período, porque nenhum endpoint de chamados filtra por data. Sem período, "cumpridos" soma o histórico inteiro e não dá para olhar "este mês".
- **Referências:**
  - `SYSTEM_SPECIFICATION.md` 4.3 (rotas de chamados).
  - Specs 026, 027 e 028, que usam o mesmo período e os mesmos setores.
- **Fatos do repositório:**
  - `GET tickets` (`Web.Api/Endpoints/Tickets/GetAll.cs:16-47`) aceita `status` repetido, mas `sectorId` uma vez só; não tem nenhum filtro de data. O filtro é montado em `Tickets/GetAll/GetTicketsQueryHandler.cs:20-29`.
  - O chamado tem `CreatedAtUtc` e `ResolvedAtUtc` (só a primeira resolução, `Tickets/Update/UpdateTicketCommandHandler.cs:79-83`), ambos `timestamptz`.
  - O único filtro de data do back é o de `GET users` (`createdFrom`/`createdTo` em `aaaa-mm-dd`), que trata a data como UTC (`Users/GetAll/GetUsersQueryHandler.cs:14-15`). O volume (`Tickets/GetVolume/GetTicketVolumeQueryHandler.cs:36`) já agrupa por `America/Sao_Paulo`.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | `GET tickets` aceita `createdFrom` e `createdTo`: datas locais `aaaa-mm-dd` em `America/Sao_Paulo`, intervalo inclusivo (do início do primeiro dia ao fim do último). Os dois são opcionais e independentes. | pedido; DEC-1; DEC-2 |
| REQ-2 | `GET tickets` aceita `resolvedFrom` e `resolvedTo`, com a mesma regra, aplicados à data da primeira resolução (`ResolvedAtUtc`); um chamado nunca resolvido fica fora quando qualquer um dos dois é informado. | pedido; DEC-1; DEC-5 |
| REQ-3 | Início depois do fim, em qualquer um dos dois intervalos, responde 400 com erro de validação claro. | DEC-2 |
| REQ-4 | `GET tickets` aceita `sectorId` repetido (vários setores); um valor só continua funcionando como hoje. | DEC-4 |
| REQ-5 | Os outros filtros, a ordenação, a paginação e a autorização (`tickets:manage`) não mudam; sem os parâmetros novos, a resposta é a de hoje. | fato do repositório |

## 3. Critérios de aceitação
Dados comuns: um atendente com `tickets:manage`; chamados abertos em 31/10 às 23h30 (horário de Brasília, 02h30 UTC de 01/11), em 01/11 às 10h e em 15/11; um deles resolvido em 02/11; setores Helpdesk e Infra.

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | os chamados acima | chama com `createdFrom=2026-11-01&createdTo=2026-11-01` | traz só o de 01/11 às 10h; o de 31/10 às 23h30 de Brasília fica fora, mesmo sendo 01/11 em UTC |
| AC-2 | REQ-1 | os chamados acima | chama só com `createdFrom=2026-11-01` | traz os de 01/11 e 15/11 |
| AC-3 | REQ-2 | o chamado resolvido em 02/11 e outro nunca resolvido | chama com `resolvedFrom=2026-11-02&resolvedTo=2026-11-02` | traz só o resolvido; o nunca resolvido fica fora |
| AC-4 | REQ-3 | — | chama com `createdFrom=2026-11-10&createdTo=2026-11-01` | 400 com erro de validação |
| AC-5 | REQ-4 | chamados no Helpdesk e na Infra | chama com `sectorId=<Helpdesk>&sectorId=<Infra>` e depois só com Helpdesk | primeiro os dois setores; depois só o Helpdesk, como hoje |
| AC-6 | REQ-5 | a suíte de `GET tickets` existente | roda depois da mudança | todos passam sem alteração |
| AC-7 | todos | a mudança aplicada | `dotnet build` e `dotnet test` | 0 avisos; todos os testes passam |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Quais períodos o filtro cobre? | (a) abertura e resolução, separados; (b) só abertura | (a) | Rafael Santos, 2026-10-28, no chat |
| DEC-2 | Formato das datas | (a) datas locais `aaaa-mm-dd` em `America/Sao_Paulo`, inclusivo; (b) instantes UTC | (a): o back converte para UTC; o front não precisa tratar fuso. Diferente do `GET users`, que trata a data como UTC | Rafael Santos, 2026-10-28, no chat |
| DEC-3 | Nomes dos parâmetros | — | `createdFrom`/`createdTo` (como `GET users`) e `resolvedFrom`/`resolvedTo` | consequência da DEC-2, registrada em 2026-10-28 |
| DEC-4 | `GET tickets` aceita vários setores? | (a) sim, `sectorId` repetido; (b) não | (a): o drill-down de um card filtrado por departamento (vários setores) precisa abrir a lista com o mesmo recorte | Rafael Santos, 2026-10-28, no chat |
| DEC-5 | Na listagem, "resolvido no período" usa qual data? | (a) a primeira resolução (`ResolvedAtUtc`); (b) qualquer resolução, pelo histórico | (a): é a única data de resolução do chamado; a vazão com cada resolução fica na série da 028 | Rafael Santos, 2026-10-28, no chat |

## 5. Fora do escopo
- Período e setores no endpoint agregado (spec 026), nos tempos médios (027) e no volume (028), que usam esta mesma regra.
- Filtro por `departmentId` explícito (o front manda os setores do departamento).
- Período nas listagens de "Meus chamados" e em `GET tickets/sla-summary`.

## 6. Plano técnico
- **Arquivos a criar ou alterar:**
  - Application:
    - `Tickets/GetAll/GetTicketsQuery.cs`: os quatro limites de data (`DateOnly?`) e a lista de setores no lugar do setor único.
    - `Tickets/GetAll/GetTicketsQueryValidator.cs` (novo ou existente): início não pode passar do fim, em cada par.
    - `Tickets/GetAll/GetTicketsQueryHandler.cs`: converte cada data local de São Paulo para o intervalo UTC (início do dia inicial até o início do dia seguinte ao final) e filtra `CreatedAtUtc` e `ResolvedAtUtc`; setores por `Contains`.
    - Uma conversão única de "data local de São Paulo → intervalo UTC", reaproveitada pelas specs 026 a 028, no lugar que o padrão do projeto indicar (por exemplo, junto do calendário de SLA, que já usa o fuso).
  - Web.Api:
    - `Endpoints/Tickets/GetAll.cs`: lê `createdFrom`, `createdTo`, `resolvedFrom`, `resolvedTo` e `sectorId` como lista.
  - Testes:
    - unitários: o teste da conversão de fuso (inclusive a virada do dia) e o do validador;
    - integração: `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` (casos `GetAll_*` de período e de vários setores).
- **Contratos:** `GET tickets` ganha `createdFrom`, `createdTo`, `resolvedFrom` e `resolvedTo` (datas `aaaa-mm-dd`) e passa a aceitar `sectorId` repetido. Mudança só aditiva; 400 novo só para intervalo invertido.
- **Mudança no banco:** não. Os índices atuais por `CreatedAtUtc` atendem a abertura; a resolução filtra uma coluna já existente.
- **Documentação de estado afetada:** `SYSTEM_SPECIFICATION.md` 4.3 (parâmetros de `GET tickets`) e a nota de fuso das datas de filtro.
- **Análise:**
  - **Fuso:** o Brasil não tem horário de verão desde 2019, mas a conversão usa o fuso IANA, não um deslocamento fixo, para continuar certa se isso mudar.
  - **Desempenho:** os limites viram comparações simples em colunas indexadas ou já filtradas; a paginação não muda.
  - **Segurança:** a autorização não muda; o período só estreita o que a pessoa já via.
- **Riscos:**
  - R-1: o `GET users` continua tratando as datas como UTC, então as duas telas diferem em quem faz login perto da meia-noite. Fica registrado para uma revisão futura do `GET users`.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Conversão de data local de São Paulo para intervalo UTC, com teste | AC-1 | `add-feature` | [ ] |
| T-2 | Período de abertura e de resolução, vários setores e validação em `GET tickets` | AC-1 a AC-5 | `add-feature` | [ ] |
| T-3 | Testes unitários e de integração da seção 6 | AC-1 a AC-6 | `add-tests` | [ ] |
| T-4 | Atualizar o `SYSTEM_SPECIFICATION.md` | AC-7 | — | [ ] |
| T-5 | Revisão de convenções, build sem avisos e suíte inteira | AC-7 | `ca-review` | [ ] |

## 8. Rastreabilidade
Não se aplica até a aprovação.

## 9. Evidências de verificação
Não se aplica até a implementação.

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-28 | Rascunho | Criada a partir do pedido de Rafael Santos no chat do front-end; número reservado já na criação, porque outra sessão cria specs em paralelo. |
| 2026-10-28 | Em descoberta | Cinco rodadas de perguntas respondidas no chat (DEC-1 a DEC-5). Aguardando aprovação. |
