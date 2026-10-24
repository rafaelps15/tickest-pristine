# Backlog — TickestPristine

Índice priorizado das demandas futuras. Cada item diz **o quê** e **em que ordem**; o **como** e o critério de pronto ficam na spec (`specs/NNN-slug/spec.md`), e as tarefas, só no §7 da spec.

## Regras

- **Um item, um problema.** Um item pode gerar uma ou mais specs; uma spec atende um único item.
- **Faixas:** `Agora`, `Próxima` e `Depois`. Dentro de cada faixa, a posição é a prioridade (o primeiro é o mais prioritário). Mudar a faixa ou a ordem é decisão do responsável pelo produto.
- **Origem:** cita a fonte pelo ID (`GAP-n`, `Q-n` e `C-n` do `SYSTEM_SPECIFICATION.md`, fase do `specs/_fontes/prompt-sla.md`, pedido no chat), sem copiar o conteúdo. O "Prompt de SLA" é o `specs/_fontes/prompt-sla.md`, cópia byte a byte do `prompt-sla-tickest.md` (não versionado), feita em 2026-10-23.
- **Estados do item:**

  | Estado | Significado |
  |---|---|
  | `Proposta` | Registrado; ainda não triado. |
  | `Pronta` | Problema claro, origem citada e nenhuma pergunta bloqueante em aberto, confirmado pelo responsável. Pode ir para a `spec-new`. |
  | `Especificada` | Tem spec; a coluna Specs aponta para ela. **Daqui em diante o andamento fica só no status da spec.** A única mudança permitida no item é acrescentar à coluna Specs, pela `spec-new`, uma spec posterior da mesma demanda; números já registrados não são removidos nem trocados, e as demais colunas não mudam. |
  | `Descartada` | Abandonada por decisão do responsável; vai para a seção "Descartadas" com o motivo, quem decidiu e quando. |

- **Transições:** `Proposta → Pronta` e `Proposta|Pronta → Descartada` pelo responsável; `Proposta|Pronta → Especificada` pela `spec-new`, ao criar a spec a partir do item. Num item já `Especificada`, a `spec-new` só acrescenta o número da nova spec da mesma demanda, sem transição.
- **Coluna Specs:** só números de specs que existem em `specs/`. Specs planejadas e ainda não criadas ficam numa nota abaixo da tabela.
- **Histórico:** toda mudança nas regras, e toda mudança de faixa, ordem ou estado feita fora da `spec-new`, ganha uma linha na seção "Histórico", no fim do arquivo. Linhas anteriores não são apagadas nem editadas.
- O backlog não aprova spec nem autoriza código: aprovação, implementação, verificação e entrega seguem o fluxo SDD do `CLAUDE.md` e os estados de `.claude/skills/spec-new/references/spec-status.md`.

**Carga inicial (2026-10-23):** itens tirados das specs 001 a 004, dos GAPs e das perguntas em aberto do `SYSTEM_SPECIFICATION.md` (§7.1, §7.2 e §9.5) e das fases do prompt de SLA. As faixas e a ordem seguem um critério provisório, ainda não revisado pelo responsável: `Agora` = itens com spec, na ordem das specs; `Próxima` = o que restava da Etapa 1 do §7.5 e os GAPs P2 fora das etapas; `Depois` = a Etapa 2 do §7.5, as fases do SLA sem spec (na ordem do prompt, com a Fase 0 antes da Fase 5, conforme a DEC-4 da spec 004), os GAPs P3 e as perguntas sem prioridade na origem. Todos os itens sem spec começam em `Proposta`.

## Agora

| ID | Demanda | Origem | Depende de | Estado | Specs |
|---|---|---|---|---|---|
| BL-001 | Remover o responsável de um chamado | Pedido de Rafael Santos no chat (2026-10-22); C-10 | — | Especificada | 001 |
| BL-002 | Registrar no histórico a alteração só da descrição do chamado | Pedido de Rafael Santos no chat (2026-10-22) | — | Especificada | 002 |
| BL-003 | Testes de arquitetura para as regras permanentes do `CLAUDE.md` | Missão corretiva do ambiente, Fase 2 (Rafael Santos, 2026-10-22) | — | Especificada | 003 |
| BL-004 | SLA, Fase 1: núcleo (calendário, plano e cronômetros, monitoramento) | Prompt de SLA, Fase 1; C-14, C-15 | Q-11 (só a spec 006: execução única do job, item 1.6 do prompt) | Especificada | 004 |
| BL-031 | Falsos positivos do portão SDD em comandos só de leitura e restauração de arquivos fora de `src/` e `tests/` | Pedido de Rafael Santos no chat (2026-10-23); auditoria arquitetural de 2026-10-23 (ACH-PLT-01); `SYSTEM_SPECIFICATION.md` 6.9 | — | Pronta | — |

BL-004: pela DEC-1 da spec 004, a demanda se divide em três specs. A 004 cobre o item 1.1 do prompt; a 005 (itens 1.2 a 1.5 e 1.7) e a 006 (item 1.6) ainda não existem e entram na coluna Specs quando a `spec-new` as criar.

BL-031 só muda o portão (`.claude/hooks/`) e o `SYSTEM_SPECIFICATION.md`, não `src/` nem `tests/`: a correção foi feita sem spec, com autorização explícita do responsável (`[sdd:autorizo]`, 2026-10-23), na branch `bugfix/BL-031-sdd-gate-false-positives`.

## Próxima

| ID | Demanda | Origem | Depende de | Estado | Specs |
|---|---|---|---|---|---|
| BL-005 | Regra do responsável indicado na abertura do chamado | GAP-02 (pendência restante); Q-05; C-08 | Q-05 | Proposta | — |
| BL-006 | Validar o CI num pull request para a `main` e avaliar exigir o check na proteção da branch | GAP-12 | — | Proposta | — |
| BL-007 | Política de retenção e remoção física de anexos excluídos | GAP-08; Q-08 | Q-08 | Proposta | — |
| BL-032 | Autorização obrigatória em todo endpoint (`HasPermission`, `RequireAuthorization` ou `AllowAnonymous`) e `FallbackPolicy` | Pedido de Rafael Santos no chat (2026-10-23); auditoria arquitetural de 2026-10-23 (ACH-ARQ-02); fora da spec 003 por decisão do responsável | — | Proposta | — |

BL-006 não muda `src/` nem `tests/`; se precisa de spec é uma decisão do responsável na triagem.

BL-032: o responsável pediu uma spec própria para o item. O número 004 já é da spec de SLA, e 005 e 006 estão reservados para o BL-004 (DEC-1 da spec 004); pela ordem atual, a spec do BL-032 seria a 007, número que a `spec-new` confirma ao criá-la.

## Depois

| ID | Demanda | Origem | Depende de | Estado | Specs |
|---|---|---|---|---|---|
| BL-008 | Armazenamento de anexos para várias instâncias e inspeção dos arquivos enviados | GAP-09; C-17 | Q-11 | Proposta | — |
| BL-009 | Procedimento de migração e de primeiro administrador em produção | GAP-10 | Q-11 | Proposta | — |
| BL-010 | Forwarded headers, HTTPS e partição do rate limit atrás de proxy | GAP-11; C-24 | Q-11 | Proposta | — |
| BL-011 | URL do Seq por ambiente e enrichers sem pacote | GAP-13 | — | Proposta | — |
| BL-012 | SLA, Fase 2: matriz impacto × urgência | Prompt de SLA, Fase 2; C-15 | BL-004 | Proposta | — |
| BL-013 | SLA, Fase 3: calendários por setor | Prompt de SLA, Fase 3; C-15 | BL-004 | Proposta | — |
| BL-014 | SLA, Fase 4: políticas configuráveis | Prompt de SLA, Fase 4; C-15 | BL-004 | Proposta | — |
| BL-015 | SLA, Fase 0: outbox | Prompt de SLA, Fase 0; GAP-05; DEC-4 da spec 004 | — | Proposta | — |
| BL-016 | SLA, Fase 5: escalonamento multinível | Prompt de SLA, Fase 5; C-15, C-21 | BL-004; BL-015 (DEC-4 da spec 004); Q-10 (parte em aberto) | Proposta | — |
| BL-017 | SLA, Fase 6: próxima resposta e atualização periódica | Prompt de SLA, Fase 6; C-15 | BL-004 | Proposta | — |
| BL-018 | SLA, Fase 7: relatórios de cumprimento | Prompt de SLA, Fase 7; C-15 | BL-004 | Proposta | — |
| BL-019 | Eventos de domínio sem consumidor e `TicketHistoryRecordedDomainEvent` sem uso | GAP-04; C-20 | — | Proposta | — |
| BL-020 | Exemplos de Todos no `CLAUDE.md` e nas skills | GAP-06; Q-09 | Q-09 | Proposta | — |
| BL-021 | Revogação do access token | GAP-14; C-02 | — | Proposta | — |
| BL-022 | SQL do relatório de volume na Application | GAP-15 | — | Proposta | — |
| BL-023 | Fuso fixo `America/Sao_Paulo` nos relatórios | GAP-16; C-13 | — | Proposta | — |
| BL-024 | Nível de log das falhas esperadas no `LoggingDecorator` | GAP-17 | — | Proposta | — |
| BL-025 | Permissão do solicitante para mudar o status do próprio chamado | Q-03; §5.4 | Q-03 | Proposta | — |
| BL-026 | Responsável sem `tickets:manage` tratando o chamado | Q-04 | Q-04 | Proposta | — |
| BL-027 | Edição de prioridade, título e setor, e recálculo do SLA | Q-06; C-09 | Q-06 | Proposta | — |
| BL-028 | Mensagens e anexos em chamados encerrados | Q-07; C-16 | Q-07 | Proposta | — |
| BL-029 | Reativação de departamentos e setores e responsável ativo | Q-12; C-07 | Q-12 | Proposta | — |
| BL-030 | Agrupar no Dependabot as atualizações minor e patch dos pacotes `Microsoft.*` | Pedido de Rafael Santos no chat (2026-10-23): PRs #6 a #10 | — | Proposta | — |
| BL-033 | `git stash list` e `git stash show` bloqueados pelo portão SDD, embora só leiam | Pedido de Rafael Santos no chat (2026-10-23); observado na sessão do BL-031 | — | Proposta | — |
| BL-034 | `cd /c/...` (Git Bash) não convertido pelo portão SDD: `git restore` e `git checkout -- <caminho>` fora de `src/` e `tests/` ficam bloqueados quando vêm depois desse `cd` | Pedido de Rafael Santos no chat (2026-10-23); observado depois do merge do BL-031 (`CD_PATTERN` em `sdd_gate.py` guarda `/c/...` sem converter para `C:/...`) | — | Proposta | — |

Decisões da spec 004 que alcançam itens ainda sem spec (a decisão completa está na spec):
- DEC-2 (estilo de código do `CLAUDE.md`, não o do prompt): BL-012 a BL-018.
- DEC-4 (outbox como pré-requisito da Fase 5): BL-015 e BL-016.
- DEC-5, DEC-6 e DEC-8 (Id reservado do calendário padrão, invalidação do cache e validação da grade no cadastro): BL-013.
- DEC-7, com o esclarecimento de 2026-10-23 (sem inserção automática de configuração padrão; migração de dados de negócio existentes permitida): BL-012, BL-014 e BL-016.

## Descartadas

| ID | Demanda | Motivo, quem decidiu e quando |
|---|---|---|

## Histórico

| Data | Mudança | Autorizado por |
|---|---|---|
| 2026-10-23 | Criação do arquivo e carga inicial (BL-001 a BL-029), pelo critério provisório descrito acima. | Rafael Santos, no chat |
| 2026-10-23 | Regras: a coluna Specs de um item `Especificada` passa a aceitar specs posteriores da mesma demanda, e só números de specs existentes; criada esta seção. BL-004: Q-11 em "Depende de" (só a spec 006); "005 e 006 previstas" saiu da coluna Specs e foi para uma nota. Notas novas: fonte do prompt de SLA e decisões da spec 004 que alcançam BL-012 a BL-018. Faixas, ordem e estados não mudaram. | Rafael Santos, no chat |
| 2026-10-23 | Novo item BL-030 (agrupamento do Dependabot), no fim da faixa `Depois`, em `Proposta`. | Rafael Santos, no chat |
| 2026-10-23 | Novo item BL-031 (falsos positivos do portão SDD), no fim da faixa `Agora`, em `Pronta`: problema e escopo confirmados pelo responsável, correção sem spec por não mudar `src/` nem `tests/`. | Rafael Santos, no chat |
| 2026-10-23 | Novo item BL-032 (autorização obrigatória em todo endpoint e `FallbackPolicy`), no fim da faixa `Próxima`, em `Proposta`, a especificar numa spec própria; não entra na spec 003. | Rafael Santos, no chat |
| 2026-10-23 | Novo item BL-033 (`git stash list` e `git stash show` bloqueados pelo portão SDD), no fim da faixa `Depois`, em `Proposta`; o BL-031 não muda. | Rafael Santos, no chat |
| 2026-10-23 | Novo item BL-034 (`cd /c/...` não convertido pelo portão SDD), no fim da faixa `Depois`, em `Proposta`; o BL-031 não muda. | Rafael Santos, no chat |
