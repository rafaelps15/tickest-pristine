# SPEC 049 — Anexo C: análise por área

> Parte da [SPEC 049](spec.md). O estado, as decisões e a aprovação ficam no `spec.md`; este anexo não tem estado próprio. Análise de domínio de chamados (2.7) e, para cada área, o escopo lido, os invariantes e as fronteiras, as matrizes, o modelo-alvo, a conformidade com as skills, o que não foi determinado e as fases, com os números de seção da investigação. A 2.10.9 traz a análise da CT-26 (sessões e tokens depois da desativação).

## 2.7 Análise de domínio

**Conceitos.**

| Termo (pt-BR) | Tipo no código | Papel |
|---|---|---|
| chamado | `Ticket` | pedido interno com ciclo de vida, setor, solicitante, responsável, prioridade, plano de SLA e exclusão lógica |
| cronômetro (objetivo × ciclo) | `TicketSlaTimer` | medida de um objetivo do SLA (primeira resposta ou resolução) num ciclo do chamado: prazos, conclusão, parada e violação |
| plano de SLA | `TicketSlaPlan` | valor gravado na abertura: política, versão, calendário e metas |
| prazos | `SlaDeadlines` | alerta (75%) e prazo de um objetivo, em tempo útil |
| calendário útil | `BusinessCalendar` | cálculo de tempo útil sobre grade, fuso e feriados |
| mensagem | `TicketMessage` | conversa do chamado; a primeira de quem atende conclui a primeira resposta |
| anexo | `TicketAttachment` | arquivo enviado ao chamado, com metadados e chave de armazenamento |
| entrada do histórico | `TicketHistory` (alvo: `TicketHistoryEntry`) | registro só de inclusão de cada ação |
| última atividade, "aguardando" | `LastActivityAtUtc`, `WaitingOn` | **projeções de leitura**: a data da ação mais recente do histórico e quem deve agir em seguida |

**Invariantes.** O "hoje" vale na versão da 045.

| # | Invariante | Exige consistência imediata? | Protegido hoje por | Proteção-alvo |
|---|---|---|---|---|
| I-01 | Transições válidas de status (RN da seção 2.5 do estado) | sim | `Ticket.CanTransitionTo` | igual |
| I-02 | Cancelamento: o dono só nos 15 minutos; `tickets:cancel` a qualquer momento (RN-32) | sim | `Ticket.Cancel`, com as permissões vindas do handler | igual (`Cancel` público) |
| I-03 | Atribuir, remover o responsável e excluir só com o chamado ativo | sim | métodos do `Ticket` e pré-conferência no handler | igual (DEC-19 da 045) |
| I-04 | `ResolvedAtUtc` gravado só na primeira resolução | sim | `Ticket.Resolve` | igual |
| I-05 | O prazo e o alerta do chamado são iguais aos do cronômetro de resolução do ciclo 1 (RN-02) | sim, na abertura | `Ticket.Create` mais `TicketSlaCycleService.Start` (dois objetos) | `Ticket.Create` monta os dois |
| I-06 | No máximo um ciclo de resolução aberto, numerado como o maior + 1 | sim | serviço, `xmin` do chamado e índice único `(ticket_id, metric, cycle)` | métodos do `Ticket` sobre os próprios cronômetros, `xmin` e o índice como segunda barreira |
| I-07 | Resolver conclui o ciclo corrente e a primeira resposta aberta; cancelar para os abertos; excluir tira da fila (RN-26, RN-27) | sim | serviço | métodos do `Ticket` |
| I-08 | A primeira mensagem de quem atende e não é o solicitante conclui a primeira resposta, uma vez (RN-26, DEC-10 da 045) | sim | `TicketSlaTimer.CompleteFirstResponse(Ticket)` e tokens do cronômetro | `Ticket.PostMessage` e tokens do cronômetro |
| I-09 | A violação é gravada uma vez por cronômetro não cumprido, no próprio prazo (RN-27) | sim | trava consultiva e conferência de `BreachedOnUtc` | igual, mais o token em `NextActionOnUtc` (DEC-11) |
| I-10 | No máximo 20 anexos e 100 MB por chamado (RN-09) | sim, mas a 045 aceitou a falha rara | contagem no handler, sem proteção sob concorrência | regra no `Ticket` (DEC-6) e trava da linha do chamado na 043p2 (DEC-3) |
| I-11 | Só o autor edita a mensagem; o autor ou quem atende exclui (RN-08) | sim | `TicketMessage` | igual |
| I-12 | Toda mudança grava a entrada do histórico no mesmo save (desvio do `CLAUDE.md`) | sim | métodos que devolvem a entrada e o handler adiciona | igual |
| I-13 | A última atividade nunca diminui (RN-31) | não: é projeção | `Ticket` mais o `GREATEST` depois do commit | regra pura no Domain e gravação na transação (043p2) |
| I-14 | "Aguardando" segue a última mensagem válida e só existe com o chamado ativo (RN-34) | não: é projeção | `Ticket` mais o `CASE` depois do commit | regra pura no Domain e gravação na transação (043p2) |
| I-15 | Arquivo purgado só de anexo excluído | não | nada | `TicketAttachment.RecordFilePurge` devolve `false` quando não vale (DEC-6) |

**Classificação de cada tipo** (perguntas da seção 6 do pedido, condensadas):

| Tipo | Identidade e ciclo de vida próprios? | Estado e regras | Hoje | Alvo | Por quê |
|---|---|---|---|---|---|
| `Ticket` | sim e sim | status, plano, prazos, responsável, exclusão | raiz | raiz, com os cronômetros como entidades internas | dono dos invariantes I-01 a I-08 e I-10 |
| `TicketSlaTimer` | identidade sim (chave e histórico de ciclos); ciclo de vida **não**: nasce, conclui, para e sai da fila pelas transições do chamado, e o único ato isolado é a apuração do prazo pelo monitoramento | prazos e conclusão; invariantes de conjunto (I-06) | raiz própria | entidade interna do `Ticket` | I-05 a I-08 exigem o chamado e os cronômetros na mesma fronteira |
| `TicketMessage` | sim e sim (editar e excluir por id; volume sem limite) | autoria (I-11) | raiz | raiz, criada pelo `Ticket` | carregar a conversa inteira em cada operação do chamado não se justifica; nenhuma regra de conjunto |
| `TicketAttachment` | sim e sim (baixar e excluir por id) | regras do arquivo; exclusão pelo autor ou por quem atende; purga | raiz | raiz, criada pelo `Ticket` | o único invariante de conjunto (I-10) fica com quem o define (o chamado) e com a trava da 043p2; no máximo 20 itens, mas a leitura do chamado não precisa deles |
| `TicketHistory` | identidade só técnica (cursor) | nenhuma regra | registro só de inclusão | igual, renomeado (DEC-5) | é registro de auditoria funcional |
| `TicketSlaPlan` | não | metas e fórmula de prazo | record | igual | valor imutável gravado no chamado |
| `SlaDeadlines` | não | — | record | igual | valor de retorno da fórmula |
| `TicketReopening` | não | — | record de resultado | removido | só existia para coordenar dois agregados |
| `TicketSlaCycleService` | — | coordenação | serviço de domínio | removido | a regra deixa de ser entre agregados |
| `BusinessCalendar` | não | cálculo puro | objeto de cálculo do domínio | igual | sem I/O, imutável |
| `SlaCalendar` e `SlaCalendarHoliday` | sim | nenhuma | anêmicas, sem escrita | fora do escopo | Q-09 |

**Objetos de valor avaliados** (critério do `CLAUDE.md`: "só para valor com regra própria e vários usos"):

| Candidato | Resultado |
|---|---|
| prazo e duração do SLA | já existem (`SlaDeadlines`, `TicketSlaPlan`) |
| metadados do arquivo (nome, tipo, tamanho) | rejeitado: um só uso (o envio); as regras vão para a `TicketAttachmentPolicy` |
| ids fortemente tipados | rejeitado: convenção transversal do projeto inteiro, sem padrão na skill |
| título e descrição | rejeitado: limites no banco pela spec 042; o validator confere o formato |
| período de datas locais (`LocalDatePeriod`) | fica na Application: é regra das consultas (043p1) |
| status e prioridade | ficam enums |

Nenhum objeto de valor novo entra nesta spec, então `ValueObject`, `ResultExtensions` e as fábricas de `Result` do desvio do `CLAUDE.md` continuam para a primeira spec que precisar delas.

**Fronteiras avaliadas para o SLA** (a decisão está na DEC-1):

| Opção | Protege I-05 a I-08 no modelo? | Serviço, filtros e tipos de coordenação | Monitoramento | Persistência | Custo |
|---|---|---|---|---|---|
| (a) manter a 045 (raízes independentes e serviço) | não: dependem do serviço, do `xmin` e do índice | ficam | carrega só cronômetros | como hoje | nenhum agora; a dívida de F-01 a F-05 continua |
| (b) cronômetros como entidades internas do `Ticket` | sim | saem o serviço, `TicketReopening`, `BelongsTo`, `CompleteFirstResponse(Ticket)` e o `internal` entre classes | carrega o chamado dos cronômetros vencidos (no máximo dois abertos por chamado) | sem migration: a mesma tabela, mapeada pela navegação da raiz (7.7.1) | médio: `Ticket` cresce cerca de 60 linhas e perde cerca de 40 (projeções, DEC-2); os testes do serviço migram para `TicketTests` |
| (c) agregado `TicketSla` próprio (um por chamado) | só entre cronômetros; I-05 e I-07 continuam entre agregados | o serviço continua | carrega o SLA sem o chamado | precisa de uma linha raiz (tabela nova) ou continua dependendo do `xmin` do chamado | alto, com migration e consultas novas |

**Conclusão.** A opção (b) é a recomendada.
- **Consistência:** todo efeito nos cronômetros, exceto a apuração do prazo, acontece na mesma transação de uma transição do chamado, e a persistência já usa a linha do chamado como trava do ciclo (REQ-8 da 045).
- **Evidência:** MM faz exatamente isso com as entidades internas do `Meeting`.
- **Contraponto honesto:** a DEC-1 da 045 avaliou esta mesma alternativa (a opção (b) dela, "cronômetros dentro do agregado `Ticket`") e escolheu a (c) dela, raízes independentes com serviço, porque "o cronômetro é carregado e gravado sem o chamado (monitoramento, primeira resposta) e tem tokens próprios". Em (b), os tokens próprios continuam (o EF grava só a linha do cronômetro que mudou), a primeira resposta passa a ser feita pelo chamado e o monitoramento passa a carregar o chamado dos cronômetros vencidos, um custo pequeno.
- **Revisão de decisão aprovada:** como (b) revê decisões aprovadas da 045 (DEC-1, DEC-3, DEC-6, DEC-7, DEC-20 e DEC-21 dela), ela só entra com aprovação explícita (pergunta 1).

## 2.9 Usuários e identidade

Esta seção cobre o ciclo da conta (cadastro, convite, ativação, desativação e reativação), as credenciais (hash, senha provisória, tentativas e bloqueio), as sessões (refresh token com rotação, reuso, limite de 30 dias e revogação), o perfil, o setor, as funções vistas do lado do usuário, a lista de atendentes e os totais por situação.

- **Linha de base:** o commit `bc2395c`, lido por `git show`; toda referência `arquivo:linha` desta seção aponta para ele. O front-end foi conferido no `e977f64`, só para saber quais rotas de usuário ele chama.
- **Lido:** Domain `Users/` (18 arquivos); Application `Users/` (68) e `Abstractions/{Authentication,Notifications}` (7); Web.Api `Endpoints/Users/` (20); Infrastructure `Authentication/` (8), `Users/` (5) e `Notifications/` (5); o `AdminUserSeeder`, a parte de usuários do `SampleDataSeeder`; as 12 migrations que criam ou alteram as tabelas de usuário; 22 classes de teste unitário (187 métodos), 5 de integração (135 métodos) e o `RichEntityTests`.
- **Specs que tocam esta área e ainda não foram implementadas** (lidas na working tree, onde estão sem commit):
  - **036** (`Aprovada`): as funções passam a ser identificadas pelo id; `UserRole.RoleName` vira `RoleId`, e a atribuição de funções, o cadastro, a listagem e o usuário atual passam a usar ou expor o id.
  - **037** (`Aprovada`): o usuário passa a ter quatro situações. "Ativo" é ativo e com senha; os outros são "Convite pendente", "Convite expirado" e "Inativo". `GET users?status=Active` passa a excluir os pendentes (DEC-1 dela), e a busca de responsável de departamento só oferece quem já tem senha (DEC-6 dela).
  - **046** (`Em descoberta`): o refresh token passa a viajar num cookie `HttpOnly`, o que muda o transporte da sessão no login, na renovação e no logout.
  - Esta seção não repete o que essas specs decidem: usa os conceitos delas e marca onde o modelo-alvo depende delas.
- **Fora desta seção:** funções, permissões, guards e o pipeline de autorização são da 2.10; aqui entra só o uso que os slices de usuário fazem deles.
- **Sem execução:** nenhum build nem teste foi rodado.

**Resumo.** O domínio de usuários é o maior legado anêmico do projeto. As cinco entidades têm `{ get; set; }`, e as regras da conta ficam em quatro classes estáticas da Application e em oito handlers, com 14 `Raise` fora das entidades. O comportamento está bem coberto por testes e tem boas defesas: hash forte, refresh token guardado só como hash, rotação, carimbo de segurança e convite com hash e token no fragmento do link. Os problemas que pesam são cinco:
1. "conta ativa" inclui a conta pendente nas regras que pedem conta utilizável (último administrador, seed e atendente elegível), embora a spec 037 já tenha definido "Ativo" como ativo e com senha;
2. a contagem de senhas erradas perde tentativas simultâneas;
3. a troca de senha autenticada não conta tentativas;
4. o SMTP segue sem TLS quando o servidor não o oferece;
5. nenhuma fronteira de agregado protege as invariantes da conta.

### 2.9.2 Invariantes e fronteiras

| # | Invariante | Exige consistência imediata? | Protegido hoje por | Proteção-alvo |
|---|---|---|---|---|
| I-US-01 | E-mail único, sem diferenciar maiúsculas (RN-12) | sim | `AnyAsync` no cadastro (`RegisterUserCommandHandler.cs:26-29`) e índice único em `citext` (`UserConfiguration.cs:15,27`); na corrida, 409 "Registro duplicado" pelo índice | igual: o índice é a barreira e o handler confere antes para devolver `Users.EmailNotUnique`; o e-mail não muda depois do cadastro (não há caso de uso) |
| I-US-02 | Código público `usr_<ULID>` único | sim | `UserCodes.cs:11` e índice `ix_users_code` (`UserConfiguration.cs:29-31`) | gerado pela abstração da DEC-25 e passado à fábrica; índice igual |
| I-US-03 | Nome e sobrenome até 100 caracteres e e-mail até 254 | sim | checks `ck_users_*_length` (`UserConfiguration.cs:20-25`); os validators conferem só o formato | igual (desvio "citext com limite") |
| I-US-04 | Conta pendente é a ativa sem credencial; só a ativação (ou o seed) cria a credencial; ninguém define a senha de outra pessoa (RN-30) | sim | convenção: nenhum outro caminho cria `UserCredential`; a regra está escrita em seis lugares (US-05) | `User.IsPendingActivation`; credencial criada só por `User.ActivateAccount` e pela fábrica do seed |
| I-US-05 | No máximo um convite utilizável; emitir revoga os anteriores; validade de 72 h; uso único (RN-30) | sim | `UserInvitations.IssueAsync` (`UserInvitations.cs:38-62`) e tokens `UsedAtUtc` e `RevokedAtUtc` (`UserInvitationConfiguration.cs:25-26`); sem barreira quando não há convite utilizável anterior (US-04) | `User.IssueInvitation`; trava da linha do usuário na 043p2 (DEC-22) **Decisão de 2026-11-01 (CT-20 a CT-25):** índice único parcial `user_invitations (user_id) WHERE used_at_utc IS NULL AND revoked_at_utc IS NULL` (CT-20). Não existe hoje; a migration entra na G4, com a T-111. A trava fica pela I-US-06. |
| I-US-06 | No máximo 5 convites em 24 h (RN-30) | sim, com impacto baixo | contagem antes de inserir (`UserInvitations.cs:86-98`), sem barreira | `User.IssueInvitation` conta os convites do agregado; trava da DEC-22 **Decisão de 2026-11-01 (CT-20 a CT-25):** trava da linha do usuário no reenvio (CT-20). |
| I-US-07 | Ativação: convite utilizável, usuário ativo e sem credencial geram uma credencial e o convite usado, numa transação; toda recusa com o mesmo erro (RN-30) | sim | handler (`ActivateUserAccountCommandHandler.cs:27-59`), tokens do convite e índice único `ix_user_credentials_user_id` | `User.ActivateAccount`; barreiras de hoje mais a trava da DEC-22 |
| I-US-08 | Desativar revoga os convites utilizáveis e todas as sessões; ninguém desativa a si mesmo (RN-19, RN-30) | sim | handler e duas funções estáticas (`DeactivateUserCommandHandler.cs:21-24,60-69`) | pré-conferências no handler; `User.Deactivate` revoga os convites; o handler remove as sessões no mesmo save |
| I-US-09 | A 5ª senha errada seguida bloqueia por 15 min; durante o bloqueio a senha não é conferida; login correto zera (RN-14) | sim | `UserCredentialsVerifier.cs:56-85`, com gravação imediata e sem token de concorrência (US-03) | métodos do `User` sobre a credencial; trava da DEC-22 **Decisão de 2026-11-01 (CT-20 a CT-25):** `UPDATE` atômico na credencial, sem trava (CT-20 (a)); a etapa e a auditoria estão na CT-28. |
| I-US-10 | Trocar a senha (comum ou provisória) gera carimbo novo, tira a pendência e revoga todas as sessões no mesmo save (RN-15, RN-16) | sim | `UserPasswords.cs:24-30` e `UserSessions.RevokeAllAsync` | `User.ChangePassword` e `ChangeProvisionalPassword`; o handler remove as sessões |
| I-US-11 | Sessão: limite de 30 dias desde o login; rotação a cada renovação; reuso depois de 30 s revoga todas; carimbo diferente invalida (RN-15) | sim | `UserSessions.cs:16,39-49`; `RefreshTokenCommandHandler.cs:15,52-61,74-79,91-104`; `TokenHash` como token de concorrência (`RefreshTokenConfiguration.cs:21`) | `RefreshToken.Create`, `Rotate` e perguntas; token igual |
| I-US-12 | Senha provisória: o login devolve 403 até a troca; a troca exige credenciais válidas, conta ativa e pendência (RN-16) | sim | `LoginUserCommandHandler.cs:32-42`; `ChangeRequiredUserPasswordCommandHandler.cs:21-44` | igual, com a regra nos métodos do `User` |
| I-US-13 | Sempre um administrador ativo (RN-18) | sim | `AdministratorGuard.cs:24-58` e o `Version` da função (2.10); "ativo" inclui a conta pendente (US-02) | o guard da 2.10 com "conta utilizável" (DEC-21) |
| I-US-14 | Ninguém gerencia quem tem permissão administrativa que ele não tem (RN-17) | sim | `PrivilegeEscalationGuard` em `Activate`, `Deactivate`, `ChangeSector` e `ResendInvitation`; `EnsureCallerCanChangeAsync` em `AssignRoles`; ausente em `UpdateProfile` (US-16) | forma da 2.10; a lacuna do perfil vai para a Q-20 |
| I-US-15 | Setor: só ativo na troca; `null` remove; o mesmo setor não muda nada; desativar o setor não apaga o vínculo (RN-29) | sim | handler (`ChangeUserSectorCommandHandler.cs:44-63`) e FK `Restrict` | consulta no handler e `User.ChangeSector` devolvendo `bool` |
| I-US-16 | Perfil editado pelo próprio usuário ou com `users:update`; sem mudança, sem evento (RN-20) | sim | handler (`UpdateUserProfileCommandHandler.cs:20-44`) | `User.UpdateProfile` devolvendo `bool` |
| I-US-17 | Usuário desativado não faz login nem renova a sessão e perde o acesso na requisição seguinte (RN-19) | não: vale na requisição seguinte, depois da invalidação do cache | login e renovação (`LoginUserCommandHandler.cs:33-36`; `RefreshTokenCommandHandler.cs:65-68`, que também cobre a corrida entre login e desativação), `ActiveUserRequirement` e `PermissionProvider.cs:18,41` **Correção (2026-11-01):** a renovação cobre a corrida enquanto a conta estiver inativa. A reativação não revoga sessões nem troca o carimbo, e a sessão criada na corrida volta a valer (CT-26). | igual |
| I-US-18 | As funções do usuário existem e não se repetem | sim | consulta no handler (`AssignUserRolesCommandHandler.cs:29-41`), FK pelo nome e índice único `(user_id, role_name)` (`UserRoleConfiguration.cs:18-27`) | `User.AssignRoles` sobre a coleção; consulta e índices iguais |

**Classificação de cada tipo.**

| Tipo | Identidade e ciclo de vida próprios? | Estado e regras | Hoje | Alvo | Por quê |
|---|---|---|---|---|---|
| `User` | sim e sim | perfil, situação, setor, código, pendência | entidade anêmica | raiz | dono das invariantes I-US-04 a I-US-10, I-US-15, I-US-16 e I-US-18 |
| `UserCredential` | identidade técnica; o ciclo de vida é o do usuário (nasce na ativação ou no seed e só sai com ele) | senha, carimbo, tentativas, bloqueio | anêmica, tratada como raiz | interna | a existência dela define "pendente", e a ativação precisa do usuário e do convite na mesma transação |
| `UserInvitation` | identidade técnica (hash); o ciclo de vida é o do usuário | validade, uso, revogação | anêmica | interna | "um utilizável", o limite e a revogação na desativação são regras do conjunto do usuário |
| `UserRole` | não: é o par usuário × função | nenhuma | anêmica | interna | o conjunto é trocado inteiro e é pequeno (três funções fixas) |
| `RefreshToken` | sim e sim: nasce no login, roda a cada renovação e sai no logout, na troca de senha, na desativação ou no reuso | validade, limite da sessão, hash anterior, carimbo | anêmica | raiz própria | volume e frequência altos; renovar não deve carregar nem travar o usuário; com o usuário, a sessão só lê o carimbo e a situação |

**Fronteiras avaliadas** (a decisão está na DEC-20):

| Opção | Protege I-US-04 a I-US-10 no modelo? | Classes estáticas e coordenação | Concorrência | Persistência | Custo |
|---|---|---|---|---|---|
| (a) cinco raízes ricas, coordenadas pelos handlers e por um serviço de domínio na ativação | em parte: cada regra numa entidade, mas "a ativação exige usuário ativo e sem credencial" e "desativar revoga os convites" ficam entre raízes | saem as estáticas; entra um serviço de domínio | as barreiras de hoje (tokens do convite, índice da credencial, `TokenHash`) | sem migration | médio |
| **(b) `User` raiz com credencial, convites e funções internos; sessão como raiz própria** | sim | saem as estáticas, sem serviço de domínio | a linha do usuário vira a trava das operações da conta na 043p2 (DEC-22); as barreiras de hoje continuam como segunda proteção | sem migration: as mesmas tabelas, com os filhos mapeados pela navegação da raiz (técnica da 7.7.1 da 049) | médio: o `User` ganha cerca de 18 membros; os handlers encolhem |
| (c) dois agregados de conta: `User` (perfil, situação, setor, funções) e `UserAccount` (credencial, convites, bloqueio), mais a sessão | só dentro da conta; "pendente" e "desativar revoga os convites" continuam entre agregados | serviço de domínio na desativação e na ativação | precisa de uma linha raiz para a conta | tabela nova ou credencial criada no cadastro sem senha, o que muda o conceito de pendente da RN-30 | alto |

**Conclusão.** A opção (b) é a recomendada.
- **Consistência:** cadastro, convite, ativação, troca de senha e desativação mudam o usuário e os filhos dele na mesma transação. Hoje isso depende de cada handler chamar as funções estáticas certas (US-01).
- **Sessão fora do agregado:** a sessão muda a cada renovação do access token e não precisa do usuário inteiro. As regras dela com o usuário são leituras (carimbo e situação), e as revogações em massa (troca de senha, desativação, reuso) são exclusões que o handler faz no mesmo save.
- **Concorrência:** a linha do usuário passa a ser a trava das operações da conta na 043p2 (DEC-22), como a linha do chamado na 049.
- **Agregado sempre completo:** o `User` é lido sempre com a credencial, os convites e as funções. Sem os convites carregados, o limite e a revogação dariam errado.
- **Contraponto:** o login passa a carregar também os convites. Eles são poucos (no máximo 5 por dia, e só enquanto a conta está pendente), e o login já carrega o usuário e a credencial. A técnica de mapear filhos pela navegação da raiz ainda não existe no projeto e é a mesma da DEC-1 (b) da 049; se ela for recusada lá, entra aqui pela primeira vez.

**Objetos de valor avaliados** (critério do `CLAUDE.md`: "só para valor com regra própria e vários usos"):

| Candidato | Resultado |
|---|---|
| e-mail | rejeitado: o formato é do validator (`EmailAddress()`); o tamanho e a unicidade sem diferenciar maiúsculas são do banco (`citext`, `ck_users_email_length`, DEC-20 da 042); não há regra de domínio sobre ele |
| senha | rejeitada: nunca é guardada (só o hash); a força é formato de entrada e ficou escrita em cada validator pela DEC-21 da 042 |
| código `usr_<ULID>` | rejeitado: só é gerado, nunca lido nem conferido; a geração é técnica (DEC-25) |
| hash de token | rejeitado: valor opaco calculado pela Infrastructure |
| limites de bloqueio, sessão e convite | não são valores: viram constantes das entidades que os aplicam |

Nenhum objeto de valor entra nesta área; `ValueObject` e as extensões de `Result` continuam para a primeira spec que precisar delas.

### 2.9.4 Modelo-alvo

Recomendações aceitas, com as DECs pendentes na recomendação.

```
User (raiz)  ──contém──►  UserCredential (0..1, interna)
   │         ──contém──►  UserInvitation (0..n, interna)
   │         ──contém──►  UserRole (0..n, interna)
   │  referencia por id ► Sector
RefreshToken (raiz da sessão) ──referencia por id──► User (lê só o carimbo e a situação, pelo handler)
```

**Responsabilidades.**

| Membro | Regra | Erros | Evento | Chamado por |
|---|---|---|---|---|
| `User.Register(email, firstName, lastName, code, role, utcNow)` | usuário ativo, sem credencial, com a função informada (o id fixo do Colaborador, depois da spec 036) | — | `UserRegisteredDomainEvent` | `Register` |
| `User.CreateWithPassword(email, firstName, lastName, code, roleName, passwordHash, mustChangePassword, utcNow)` | usuário com credencial; só para o seed (RN-16) | — | `UserRegisteredDomainEvent` | `AdminUserSeeder`, `SampleDataSeeder` |
| `User.EnsureCanReceiveInvitation()` | desativado; já ativado | `InvitationUserDeactivated`, `AlreadyActivated` | — | `ResendInvitation` (precedência sobre a RN-17) e `IssueInvitation` |
| `User.IssueInvitation(tokenHash, utcNow)` → `Result<UserInvitation>` | as duas acima, mais o limite de 5 em 24 h; revoga os utilizáveis; 72 h de validade | mais `InvitationLimitReached(5, 24)` | `UserInvitationIssuedDomainEvent(UserId, UserInvitationId)` | `Register`, `ResendInvitation` |
| `User.ActivateAccount(tokenHash, passwordHash, utcNow)` | convite utilizável, usuário ativo, sem credencial; cria a credencial e marca o convite | `InvalidActivationToken` (sempre o mesmo) | `UserInvitationAcceptedDomainEvent(UserId, UserInvitationId)` | `ActivateAccount` |
| `User.IsPendingActivation` | ativo e sem credencial | — | — | handlers; as consultas usam o trecho de SQL com nome |
| `User.IsUsable` (DEC-21) | ativo e com credencial (a situação "Ativo" da spec 037) | — | — | o guard da 2.10 e o seed; as consultas usam o trecho de SQL com nome |
| `User.IsLockedOutAt(utcNow)`, `VerifyPassword(password, IPasswordHashChecker)`, `RecordFailedSignIn(utcNow)`, `RecordSuccessfulSignIn()` → `bool` | RN-14: a 5ª seguida bloqueia por 15 min e zera; o acerto zera | — (o verificador escolhe `LockedOut` ou `InvalidCredentials`) | — | verificador injetado (login, troca obrigatória e, com a DEC-23, troca autenticada) |
| `User.ChangePassword(passwordHash)` e `ChangeProvisionalPassword(passwordHash)` | carimbo novo, pendência retirada | `PasswordChangeNotRequired` (só a provisória) | `UserPasswordChangedDomainEvent` | `ChangePassword`, `ChangeRequiredPassword` |
| `User.Deactivate(utcNow)` (`void`) | desativa e revoga os convites utilizáveis | — (o handler confere antes: a si mesmo, já desativado, RN-17, RN-18) | `UserDeactivatedDomainEvent` | `Deactivate` |
| `User.Reactivate()` (`void`) | reativa, sem devolver convite | — (o handler confere `AlreadyActive` e a RN-17) | `UserReactivatedDomainEvent` | `Reactivate` |
| `User.UpdateProfile(firstName, lastName)` → `bool` | — | — | `UserProfileUpdatedDomainEvent` com os nomes anteriores | `UpdateProfile` |
| `User.ChangeSector(sectorId)` → `bool` | — (setor ativo conferido no handler) | — | `UserSectorChangedDomainEvent` | `ChangeSector` |
| `User.AssignRoles(roleIds)` → `bool` (nomes até a spec 036) | troca o conjunto | — (existência, RN-17 e RN-18 no handler) | `UserRolesAssignedDomainEvent` com as funções anteriores | `AssignRoles` |
| `RefreshToken.Create(userId, tokenHash, tokenExpiresOnUtc, securityStamp, utcNow)` | limite da sessão = `utcNow` + 30 dias; validade cortada nele | — | `RefreshTokenCreatedDomainEvent` | `Login`, `ChangeRequiredPassword` |
| `RefreshToken.Rotate(newTokenHash, tokenExpiresOnUtc, utcNow)` (`void`) | guarda o hash anterior e a data; validade cortada no limite | — (o handler confere antes) | `RefreshTokenRotatedDomainEvent` | `Refresh` |
| `RefreshToken.IsExpiredAt(utcNow)`, `WasOpenedWith(securityStamp)`, `IsReuseAfterGracePeriod(utcNow)` | perguntas da renovação; 30 s de carência | — | — | `Refresh` |

- **Constantes:** `MaxFailedSignInAttempts` (5) e `LockoutDuration` (15 min) na credencial; `InvitationValidity` (72 h), `MaxInvitationsInWindow` (5) e `InvitationLimitWindow` (24 h) no `User`; `MaxSessionDuration` (30 dias) e `ReuseGracePeriod` (30 s) no `RefreshToken`.
- **Interface do Domain:** `IPasswordHashChecker` em `Domain/Users/` (padrão 5), implementada pelo `PasswordHasher`.
- **Abstrações da Application (DEC-25):** `IPasswordHasher`; `ITokenProvider`, que passa a gerar também o token do convite (`GeneratedActivationToken`, com o token e o hash); `ITokenHasher` para as buscas por hash (renovação, logout, ativação); `IUserCodeGenerator`.
- **Peças injetadas da Application** (instâncias, como pede `authorization.md:93`):
  - `UserCredentialsVerifier`: busca por e-mail, tempo de um hash para e-mail inexistente e conta pendente, chamadas às regras da entidade e gravação da tentativa errada;
  - `UserInvitationEmailSender`: envio depois do commit, com a falha no log e sem o token (DEC-24);
  - `UserInvitationEmail` continua estático, como modelo do texto, mas recebe valores e não a entidade.
- **O que fica no handler:** carregar o agregado completo; as pré-conferências que dão a ordem de erros de hoje; os guards (2.10); chamar os métodos; incluir e remover sessões; um único save; depois do commit, invalidar o cache de permissões e enviar o e-mail.
- **Consultas (043p1):** os seis slices em Dapper; "pendente" e "conta utilizável" como trechos de SQL com nome, com teste de concordância; `GetAttendantsQuery`.
- **Persistência, fase EF (sem migration; o carregamento segue a DEC-70, decidida em 2026-11-01: consulta de carga nomeada por operação, CT-22):** os filhos mapeados pela navegação da raiz, com campos privados e as mesmas chaves estrangeiras em cascata. O `User` é sempre lido completo, porque o limite e a revogação de convites dependem disso; com o EF, isso é o `AutoInclude`, como a 7.7.1 da 049. `users.id` passa a `ValueGeneratedNever()` se isso não gerar diferença de modelo.
- **Persistência na 043p2:** commands em Dapper com transação explícita. Cada command da conta trava a linha do usuário (`SELECT … FOR NO KEY UPDATE`) antes de ler o agregado (DEC-22). Esse modo não conflita com o `FOR KEY SHARE` das chaves estrangeiras que apontam para `users`, então não bloqueia a abertura de chamados.

**Eventos.**

| Evento | Hoje disparado por | No alvo, disparado por | Consumidor | Destino |
|---|---|---|---|---|
| `UserRegisteredDomainEvent` | `RegisterUserCommandHandler.cs:44` | `User.Register` e `CreateWithPassword` | nenhum (sai o handler vazio) | manter |
| `UserReactivatedDomainEvent` (hoje `UserActivatedDomainEvent`) | `ActivateUserCommandHandler.cs:47` | `User.Reactivate` | nenhum | renomear |
| `UserDeactivatedDomainEvent` | `DeactivateUserCommandHandler.cs:66` | `User.Deactivate` | nenhum | manter |
| `UserProfileUpdatedDomainEvent` | `UpdateUserProfileCommandHandler.cs:43` | `User.UpdateProfile`, só com mudança, com os nomes anteriores | nenhum | refatorar |
| `UserSectorChangedDomainEvent` | `ChangeUserSectorCommandHandler.cs:63` | `User.ChangeSector` | nenhum | manter |
| `UserRolesAssignedDomainEvent` | `AssignUserRolesCommandHandler.cs:89`, sempre | `User.AssignRoles`, só com mudança, com as funções anteriores | nenhum | refatorar |
| `UserPasswordChangedDomainEvent` | `UserPasswords.cs:28` | `User.ChangePassword` e `ChangeProvisionalPassword` | nenhum | manter |
| `UserInvitationIssuedDomainEvent` | `UserInvitations.cs:57` | `User.IssueInvitation`, com o id do convite | nenhum | refatorar |
| `UserInvitationAcceptedDomainEvent` | `ActivateUserAccountCommandHandler.cs:48` | `User.ActivateAccount`, com o id do convite | nenhum | refatorar |
| `RefreshTokenCreatedDomainEvent` | `UserSessions.cs:51` | `RefreshToken.Create` | nenhum | mover |
| `RefreshTokenRotatedDomainEvent` | `RefreshTokenCommandHandler.cs:79` | `RefreshToken.Rotate` | nenhum | mover |
| `RefreshTokenRevokedDomainEvent` | `UserSessions.cs:69`, `RefreshTokenCommandHandler.cs:54`, `LogoutUserCommandHandler.cs:30`; nunca despachado | — | nenhum | remover (DEC-26) |

Com esta área e a de funções (2.10) migradas, nenhum código fora das entidades chama `Raise`, e o `Entity.Raise` pode passar a `protected` (F-40).

**Pastas e namespaces** (namespace = caminho, com o prefixo `TickestPristine.`):

```
src/TickestPristine.Domain/
  Users/            User (raiz), UserCredential, UserInvitation, UserRole (internas), UserErrors,
                    IPasswordHashChecker e os 9 eventos User*
  RefreshTokens/    RefreshToken, RefreshTokenCreatedDomainEvent, RefreshTokenRotatedDomainEvent
src/TickestPristine.Application/
  Abstractions/Authentication/  IPasswordHasher, ITokenProvider, GeneratedRefreshToken, GeneratedActivationToken,
                                ITokenHasher, IUserCodeGenerator, IUserContext
  Abstractions/Notifications/   sem mudança
  Users/            os slices (Reactivate no lugar de Activate; GetAttendants com GetAttendantsQuery),
                    AccessTokensResponse, UserCredentialsVerifier e UserInvitationEmailSender (instâncias),
                    UserInvitationEmail (modelo do texto)
src/TickestPristine.Infrastructure/
  Authentication/   PasswordHasher (IPasswordHasher e IPasswordHashChecker), TokenProvider, TokenHasher,
                    UserCodeGenerator e os demais de hoje
  Users/            UserConfiguration (com os filhos), UserCredentialConfiguration, UserInvitationConfiguration,
                    UserRoleConfiguration
  RefreshTokens/    RefreshTokenConfiguration
  Notifications/    SmtpEmailSender (TLS obrigatório) e os demais de hoje
src/TickestPristine.Web.Api/Endpoints/Users/   Reactivate.cs no lugar de Activate.cs; o resto igual
```

### 2.9.5 Conformidade com as skills

**Cumpre:**
- Uma pasta por caso de uso, nomes `{Verb}{Entity}Command` e `Get{X}Query`, handlers `internal sealed` com construtor primário (`add-feature/SKILL.md:36-37,56-65`; `HandlerShapeTests`). Exceção de nome: US-15.
- Todo command tem validator só de formato, e os tamanhos ficam no banco (`add-feature/SKILL.md:44`; `command-slice.md:62`; spec 042).
- `Result`, `UserErrors` com códigos `Users.*` e tipos coerentes com o status HTTP (`add-feature/SKILL.md:43`; `ca-review/SKILL.md:45-47`). As inconsistências de contrato estão na US-26.
- Endpoints com `IEndpoint`, `Match`, `WithTags(Tags.Users)` e `.HasPermission` ou `.RequireAuthorization()` (`endpoint.md:121-127`).
- Consultas que projetam com `Select` num DTO por slice e paginam com `PageRequest` (`query-slice.md:36-79`; `pagination.md:5`).
- Hash de senha, JWT, e-mail e link atrás de interface, com implementação `internal sealed` na Infrastructure (`abstraction.md:3,25-72`).
- Permissão fixa na rota e permissões efetivas pelo `IPermissionProvider` (`authorization.md:5-21,47-60`).
- Nenhum `DateTime.UtcNow` na Application (`ca-review/SKILL.md:54`).
- Chaves estrangeiras no estilo sombra, índices únicos e `ValueGeneratedNever()` em quatro das cinco configurações (`add-entity/SKILL.md:116`).
- Eventos como `sealed record` com ids e sem `OccurredOnUtc` (`patterns.md:381-391`); as exceções estão na US-14.
- Testes com `Handle_Should_*`, Arrange/Act/Assert, `BaseHandlerTest` e substitutos só de interfaces (`tests.md:159-165`; `add-tests/SKILL.md:37-44`).

**Viola** (legado do "Em migração", salvo onde indicado):
- **Entidades anêmicas**, sem `Create`, sem métodos e com `set` público: `add-entity/SKILL.md:42-45`; `rich-entity.md:88-91`; `ca-review/SKILL.md:26-28` (US-01).
- **Handlers que atribuem propriedades e chamam `Raise`:** `add-feature/SKILL.md:28,38`; `domain-modeling/SKILL.md:23`; `rich-domain-handler.md:7-19,67`; `ca-review/SKILL.md:30` (US-01).
- **Classes estáticas que mudam entidades e fazem consultas:** `ca-review/SKILL.md:32,42`; `domain-modeling/SKILL.md:53-64`; `rich-domain-handler.md:137` (US-01). O `CLAUDE.md:162` só lista uma delas (US-25).
- **Criptografia e geração técnica em classe estática da Application:** `add-feature/SKILL.md:49`; `domain-modeling/SKILL.md:64` (US-12).
- **Senha conferida fora da entidade, sem interface do Domain:** `domain-modeling/SKILL.md:42`; `patterns.md:228-249` (US-01).
- **Criação sem `Create` e sem o evento `Created` pela entidade:** `add-entity/SKILL.md:44,66`; `patterns.md:35-97`.
- **Credencial, convites e funções criados fora do agregado:** `patterns.md:276-333`; `add-feature/SKILL.md:40`.
- **Evento disparado sem mudança e evento de alteração sem o valor anterior:** `patterns.md:356,390` (US-14).
- **Handler de evento vazio, com nome fora do padrão, e efeito colateral copiado em dois handlers:** `domain-event-handler.md:35-36,42,50`; `add-feature/SKILL.md:41`. A cópia tem motivo (o token em claro) e vai como exceção registrada na DEC-24.
- **Verificação compartilhada como classe estática:** `authorization.md:93`; `ca-review/SKILL.md:52` (US-01).
- **Comentários que citam spec ou DEC, `<summary>` em DTO e em teste, interfaces sem `<summary>`:** `CLAUDE.md:151`; `ca-review/SKILL.md:60`; `add-tests/SKILL.md:43`; `add-feature/SKILL.md:51`. Fora do legado: violação de convenção (US-24).
- **Testes que montam entidades por setters e falta de testes de entidade:** `add-tests/SKILL.md:29,40`; `tests.md:5-9,162` (US-23).
- **`User.Id` sem `ValueGeneratedNever()`:** `add-entity/SKILL.md:116`. A configuração não está no "Em migração" (US-20).
- **Guards estáticos usados pelos slices:** `authorization.md:93`; `ca-review/SKILL.md:52`. Legado da 2.10.

### 2.9.6 Não determinado

- **Evento de revogação:** a perda do `RefreshTokenRevokedDomainEvent` foi inferida da ordem do EF Core (`AcceptAllChanges` antes do `SavedChangesAsync`) e não foi executada; o teste que a prova fica com a Q-23.
- **`ValueGeneratedNever()`:** não se sabe se trocar o `User.Id` para ele faz o `Model_Should_HaveNoPendingChanges` apontar diferença (não executado).
- **SMTP de produção:** a configuração é desconhecida; logo, não se sabe se a US-07 já acontece hoje.
- **Dados reais:** o banco não foi consultado; não se sabe se há administrador ou atendente pendente (US-02), nem o volume de `refresh_tokens` e `user_invitations` (US-21).
- **Outros clientes:** não se sabe se algum cliente além do front `e977f64` usa `users/by-email`, `users/attendants` e `users/{id}/sector`.
- **Tempo de resposta:** a equivalência entre e-mail inexistente, conta pendente e senha errada não foi medida; a conta bloqueada responde sem o hash (US-08).
- **Leitura do id do usuário:** ela usa `ClaimTypes.NameIdentifier` (`ClaimsPrincipalExtensions.cs:12`), e o token grava `sub` (`TokenClaimTypes.cs:10`). Funciona hoje (os testes de integração autenticam), mas depende do mapeamento padrão das claims de entrada do JwtBearer, que não foi conferido na versão do pacote.
- **Opção (c) da DEC-22:** não se sabe se o EF ordenaria a revogação antes da inclusão do convite com um índice único parcial (não testado; na 043p2 a ordem é explícita).
- **Specs 036, 037 e 046:** foram lidas na working tree, onde estão sem commit, e podem mudar até a implementação. Só os pontos usados aqui foram conferidos: identidade da função, situações do usuário e transporte da sessão.

### 2.9.7 Fases da área

- **U0, linha de base.** Depende do PR #59 (045) mergeado; das specs 036 (funções pelo id) e 037 (situações do usuário) entregues; da 043 parte 1 entregue (as seis consultas de usuários já em Dapper, com a DEC-11 da 043 decidida); e da DEC-29. Se a 037 entrar depois da 043p1, ela escreve as situações já em SQL. **Reavaliação de 2026-11-01 (DEC-29, confirmada):** a 036 e a 037 vêm depois da 043p1, e a 037 escreve as situações em SQL. A U0 e a U1 dependem da 043p1 e da 036; a 037 só precisa estar entregue antes da U2 (7.15.4 do `spec.md`).
- **U1, mecânica e sem mudança de comportamento.** Nomes (DEC-27), comentários, `<summary>`, remoção do handler vazio e peças técnicas (DEC-25). Depende de U0. Risco baixo (a 7.15 avalia médio, e vale a 7.15).
- **U2, domínio rico.** Sessão e agregado `User` (DEC-20), handlers, seeders e testes. Depende de U1 e da técnica de mapeamento de entidade interna (DEC-1 da 049); se a DEC-1 da 049 for (a), a técnica entra aqui pela primeira vez. Risco médio (a 7.15 avalia alto, e vale a 7.15).
- **U3, mudanças de comportamento aprovadas.** DEC-21, coordenada com a 2.10 (dona do `AdministratorGuard`) e com a área de chamados e a 043 (donas da elegibilidade e do AC-10 da 043); DEC-23; DEC-28. Depende de U2 e das respostas. Risco médio.
- **U4, dentro da 043 parte 2.** Commands da conta em Dapper com transação explícita e a trava da DEC-22. O contrato desta área entra como insumo da 043p2, como a 7.7.2 da 049. Depende de U2, e de U3 se aprovada.
- **U5, fechamento.** `Entity.Raise` protegido (depois da 2.10, F-40), documentação (REQ-117) e verificação. Depende de U2 a U4 e da DEC-14 da 049 (para o `CLAUDE.md`).
- **Independentes.** A Q-23 (plataforma) pode entrar antes, sem bloquear esta área, porque a DEC-26 remove o único evento afetado. **Ajuste (reconferência):** vale se a DEC-26 for (b); com (a), a U2 depende da T-402 (PL-06) para o evento ser despachado. A spec 046 (cookie `HttpOnly`) muda só os endpoints de sessão e o front; o modelo da sessão (REQ-101) não depende do transporte. Se a 046 entrar antes da U2, os handlers de login, renovação e logout chegam com o token vindo do cookie.

## 2.10 Funções, permissões, autorização e segurança

Esta seção cobre as funções (`Role`), as permissões e os vínculos (`Permission`, `RolePermission`, `UserRole`), o catálogo de permissões, a autorização de rota e por recurso (inclusive os três guards estáticos), o cache de permissões e a segurança transversal da API: JWT, sessões, senha, bloqueio por tentativas, rate limit, CORS, HTTPS, cabeçalhos, anexos, logs, respostas de erro e seed do administrador.

- **Linha de base:** o commit `bc2395c`, lido por `git show` e `git grep`; nada foi lido da working tree. Toda referência `arquivo:linha` desta seção aponta para `bc2395c`.
- **Lido por inteiro:** `CLAUDE.md`; as skills `add-entity`, `domain-modeling`, `add-feature` (com `references/authorization.md`, `endpoint.md`, `rich-domain-handler.md`, `abstraction.md` e `tests.md`), `add-tests` e `ca-review`; as seções 2.2, 2.6 (RN-06, RN-07, RN-17 a RN-19, RN-26, RN-28, RN-35), 4.3.1, 4.6, 4.8, 6.4 a 6.6, 7 e 9.5 do `SYSTEM_SPECIFICATION.md`; as specs 036, 043, 046 e 047; todo arquivo de `Domain/Roles`, `Application/{Roles,Permissions,Authorization,Abstractions/Authorization}`, `Infrastructure/{Authorization,Roles,Authentication}`; os arquivos de autorização, rate limit, CORS, erros e log de `Web.Api/{Extensions,Infrastructure,Middleware}`; `Program.cs`; os dois `DependencyInjection.cs`; `appsettings*.json` (segredos não reproduzidos); as migrations `Role_Seed_Only_Administrator`, `Role_Name_As_Key` e `Add_Ids_To_Roles_And_Permissions`; os trechos de autorização dos handlers de usuários e de chamados.
- **Percorrido por busca:** os 61 endpoints (mapeamento e proteção); as demais migrations de permissões; os testes de funções, permissões, guards, CORS, seed, rate limit e usuários. As contagens de teste vêm da busca por `[Fact]` e `[Theory]`.
- **Sem build nem testes:** nenhum arquivo do projeto mudou.
- **Fronteira com as outras áreas:**
  - os achados de chamados que tocam autorização (F-26, F-29, F-30 e F-33) são citados pelo número, não repetidos;
  - o modelo do agregado `User` é da 2.9: aqui entra só o que a autorização exige dele.

### 2.10.2 Invariantes e fronteiras

| # | Invariante | Exige consistência imediata? | Protegido hoje por | Proteção-alvo |
|---|---|---|---|---|
| I-AZ-01 | Toda rota não anônima exige usuário autenticado, e as de negócio exigem usuário ativo, exceto `users/me` e `users/logout` (RN-19) | sim, por requisição | metadado em cada endpoint, sem `FallbackPolicy` (`DependencyInjection.cs:188-196`); `ActiveUserRequirement` na policy padrão; filtro `IsActive` no provedor (`PermissionProvider.cs:18`) | `FallbackPolicy` e teste da matriz (DEC-34, REQ-200) |
| I-AZ-02 | Uma permissão vale só se alguma função do usuário ativo a contém (união das funções) | não: o cache de até 5 min é aceito | `PermissionProvider` com `HybridCache` e `InvalidateAsync` depois do commit | uma consulta só, com leitura sem cache nas decisões que gravam estado (DEC-32) |
| I-AZ-03 | Ninguém concede nem remove permissão administrativa que não possui (RN-17, primeira parte) | sim | `PrivilegeEscalationGuard.EnsureCallerCanChangeAsync` na troca de permissões e na atribuição de funções | peça injetada, com o mesmo erro (DEC-30); também no cadastro (DEC-35) |
| I-AZ-04 | Ninguém desativa, reativa, muda o setor ou reenvia o convite de quem tem permissão administrativa que ele não tem (RN-17, segunda parte) | sim | `EnsureCallerCanManageUserAsync` em quatro handlers; não na edição do perfil | peça injetada; também no perfil (DEC-35) |
| I-AZ-05 | Sempre existe pelo menos um administrador ativo, mesmo com duas remoções simultâneas (RN-18) | sim, entre transações | `AdministratorGuard` mais o token `Version` da função Administrador | peça injetada com o efeito explícito; trava de linha na 043p2 (DEC-33) |
| I-AZ-06 | As permissões da função Administrador são o catálogo inteiro e não mudam pela API | sim | handler (`AssignRolePermissionsCommandHandler.cs:29-32`); seed (`RolePermissionConfiguration.cs:39`) | método da `Role` (`ReplacePermissions` recusa a função gerenciada); teste de seed |
| I-AZ-07 | Um par função × permissão e um par usuário × função não se repetem | sim | índices únicos (`RolePermissionConfiguration.cs:18`, `UserRoleConfiguration.cs:18`) e diferença de conjuntos nos handlers | coleção interna da raiz, com o índice como segunda barreira |
| I-AZ-08 | Todo código de permissão gravado existe no catálogo | sim | validator (`AssignRolePermissionsCommandValidator.cs:14-16`) e chave estrangeira para `permissions.code` | igual |
| I-AZ-09 | O chamado e os itens dele são vistos só pelo solicitante, pelo responsável ou por `tickets:manage` (RN-07) | sim | `TicketAccessGuard` (6 consultas) e duas cópias inline (F-33) | `Ticket.CanBeViewedBy` no chamado carregado; peça da DEC-9 da 043 nas consultas |
| I-AZ-10 | Indicar outra pessoa como responsável exige `tickets:assign`, e o responsável precisa ser atendente (RN-28) | sim | `AssignTicketCommandHandler.cs:28-39,63-66`, só no `PUT tickets/{id}/assignee` | a mesma regra em todo caminho que define o responsável, inclusive a abertura (DEC-35) |
| I-AZ-11 | O usuário desativado perde o acesso na requisição seguinte (RN-19) | sim, pelo texto da RN | invalidação do cache depois do commit; vale só na instância que invalidou (SE-15) | documentar o limite real; Q-38 |
| I-AZ-12 | Uma sessão dura no máximo 30 dias, gira o refresh token a cada uso e morre na troca de senha e na desativação | sim | `UserSessions`, `RefreshTokenCommandHandler`, token de concorrência no hash | igual (046 muda o transporte) |
| I-AZ-13 | Cinco senhas erradas seguidas bloqueiam a conta por 15 minutos | sim | `UserCredentialsVerifier`, sem token: tentativas simultâneas perdem incremento | incremento atômico (REQ-215) |
| I-AZ-14 | O seed cria um administrador só quando não há nenhum ativo, nunca promove conta existente e exige a troca da senha | sim, na implantação | `AdminUserSeeder.cs:23-57` | igual, com a senha pela política (REQ-216) |

**Fronteiras.**

- **`Role` e `RolePermission`.** A troca de permissões é uma operação sobre o conjunto da função: o pedido traz o conjunto final, e há duas regras sobre o próprio estado (a função Administrador não muda; um código não se repete). Hoje o handler apaga e insere linhas soltas (`AssignRolePermissionsCommandHandler.cs:57-67`), sem token na função para essa operação; o `Version` só muda quando um administrador sai (`AdministratorGuard.cs:55`). Por isso duas trocas simultâneas se misturam (AZ-09). **Alvo:** `Role` raiz, com a coleção interna e a concorrência pela linha da função.
- **`UserRole`.** É o conjunto de funções de um usuário:
  - muda por usuário (`PUT users/{id}/roles` e o cadastro);
  - é lido por função só em consultas (os membros cujo cache é invalidado, os administradores ativos);
  - dentro da `Role`, cada atribuição carregaria todos os membros da função; dentro do `User`, o conjunto é pequeno e muda junto com o usuário.

  **Alvo:** entidade interna do `User` (2.9).
- **`Permission`.** Não tem ciclo de vida em execução: nasce e morre por migration, a partir do catálogo. A Application decide se uma permissão é administrativa pelo catálogo (`PrivilegeEscalationGuard.cs:74-77`), e não pela tabela. **Alvo:** dado de referência.
- **Último administrador (RN-18).** Cruza três fontes: `UserRole` (quem tem a função), `User.IsActive` e a linha da função Administrador, usada como trava pelo token `Version`. Nenhum agregado protege a regra sozinho.
  - **Proteção certa:** uma conferência com consulta, que é peça injetada porque se repete em dois handlers, mais a serialização pela linha da função.
  - **Na 043p2:** a trava explícita (DEC-33) substitui o token, como a DEC-3 desta spec faz com a linha do chamado.
- **Permissões efetivas.** São uma leitura (a união das funções de um usuário ativo), não um agregado. O cache é uma projeção com validade. As decisões que gravam estado (atribuição, primeira resposta, "aguardando") precisam da leitura atual, e hoje a fazem por cópias da consulta (AZ-02, DEC-32).
- **Acesso ao chamado (RN-07).** É uma regra sobre o estado do chamado (os participantes) mais uma permissão efetiva:
  - no chamado carregado, fica na entidade, que recebe a permissão do handler, como o `Ticket.Cancel` já faz;
  - nas consultas, fica no SQL da peça da DEC-9 da 043.

### 2.10.3 Matriz de autorização

**Legenda da coluna "Na rota".**

| Valor | Mecanismo | Fonte |
|---|---|---|
| código da permissão | `.HasPermission(código)`: policy dinâmica só com `PermissionRequirement`, sem `ActiveUserRequirement`; o usuário inativo falha porque o provedor só devolve permissões de usuário ativo | `PermissionAuthorizationPolicyProvider.cs:14-18`; `PermissionProvider.cs:18` |
| usuário ativo | `.RequireAuthorization()`, a policy padrão: autenticado e `ActiveUserRequirement` | `DependencyInjection.cs:190-193` |
| só autenticado | `.RequireAuthenticatedUser()`, que aceita usuário desativado | `EndpointExtensions.cs:49-52` |
| anônima | `.AllowAnonymous()` | — |
| sem metadado | nenhuma chamada de autorização; sem `FallbackPolicy`, a rota fica anônima | — |

O limite global de requisições vale em todas as rotas (`RateLimitingExtensions.cs:29-32`). Os caminhos dos endpoints são relativos a `Web.Api/Endpoints/`.

| Rota | Verbo | Na rota | No handler | Regra por recurso | Observação |
|---|---|---|---|---|---|
| `users/register` | POST | `users:create` (`Users/Register.cs:30`) | — | — | concede a função padrão sem RN-17 (`RegisterUserCommandHandler.cs:47`; AZ-05) |
| `users/{userId}/invitation` | POST | `users:create` (`ResendInvitation.cs:25`) | RN-17 sobre o alvo (`ResendUserInvitationCommandHandler.cs:48-58`) | — | o texto do 403 fala em "desativar nem reativar" (AZ-05) |
| `users/activation` | POST | anônima, com o limite `authentication` (`ActivateAccount.cs:31-32`) | token do convite (hash, validade, uso único) | — | mesmo erro para token desconhecido, vencido, usado, revogado ou de usuário desativado (`UserInvitationsTests.cs:237`) |
| `users/login` | POST | anônima, com o limite `authentication` (`Login.cs:28-29`) | bloqueio por tentativas (`UserCredentialsVerifier.cs:56-75`); conta desativada e senha provisória só depois da senha certa (`LoginUserCommandHandler.cs:32-42`) | — | `Users.LockedOut` revela que a conta existe (SE-03) |
| `users/login/new-password` | POST | anônima, com o limite `authentication` (`ChangeRequiredPassword.cs:28-29`) | mesma conferência de credenciais | — | primeira troca do administrador semeado (SE-14) |
| `users/refresh-token` | POST | anônima, com o limite `authentication` (`Refresh.cs:28-29`) | hash, validade, carimbo de segurança, conta ativa, reuso (`RefreshTokenCommandHandler.cs:22-79`) | — | token de concorrência no hash (`RefreshTokenConfiguration.cs:21`) |
| `users/logout` | POST | só autenticado (`Logout.cs:27`) | só o refresh token do próprio usuário (`LogoutUserCommandHandler.cs:19-22`) | dono do refresh token | aceita usuário desativado (RN-19); exige access token válido (046) |
| `users/me` | GET | só autenticado (`GetCurrent.cs:22`) | 403 `Users.Deactivated`, lido do banco (`GetCurrentUserQueryHandler.cs:47-50`) | o próprio | permissões do cache (`:53`) |
| `users/me/password` | PUT | usuário ativo, com o limite `authentication` (`ChangePassword.cs:27-28`) | senha atual (`ChangeUserPasswordCommandHandler.cs:26-29`) | a própria credencial | a senha atual errada não conta tentativa (SE-03) |
| `users/{userId}/profile` | PUT | usuário ativo (`UpdateProfile.cs:28`) | o próprio usuário ou `users:update` (`UpdateUserProfileCommandHandler.cs:20-29`) | — | sem RN-17, ao contrário do setor (AZ-05); 403 antes de 404 |
| `users/{userId}` | GET | usuário ativo (`Users/GetById.cs:25`) | o próprio usuário ou `users:read` (`GetUserByIdQueryHandler.cs:21-30`) | — | 403 antes de 404 |
| `users` | GET | `users:read` (`Users/GetAll.cs:43`) | — | — | filtro `roleName` pelo nome (036) |
| `users/by-email` | GET | `users:read` (`GetByEmail.cs:25`) | — | — | e-mail na query string (SE-12) |
| `users/stats/status` | GET | `users:read` (`Users/GetStatusCounts.cs:24`) | — | — | — |
| `users/{userId}/activate` | PUT | `users:deactivate` (`Activate.cs:25`) | RN-17 sobre o alvo (`ActivateUserCommandHandler.cs:32-42`) | — | invalida o cache depois do commit (`:53`) |
| `users/{userId}/deactivate` | PUT | `users:deactivate` (`Users/Deactivate.cs:25`) | não a si mesmo (`DeactivateUserCommandHandler.cs:21-24`); RN-17 (`:38-48`); RN-18 (`:50-58`) | — | revoga as sessões e invalida o cache (`:69,76`) |
| `users/{userId}/roles` | PUT | `users:assign-roles` (`AssignRoles.cs:29`) | RN-17 sobre as funções que mudam (`AssignUserRolesCommandHandler.cs:53-67`); RN-18 (`:69-80`) | — | funções pelo nome (036); concede a si funções sem permissão administrativa (AZ-08) |
| `users/{userId}/sector` | PUT | `users:update` (`ChangeSector.cs:28`) | RN-17 sobre o alvo (`ChangeUserSectorCommandHandler.cs:32-42`) | — | mesma permissão do perfil, regra diferente (AZ-05) |
| `users/attendants` | GET | usuário ativo (`GetAttendants.cs:31`) | `users:read` (`GetBacklogAttendantsQueryHandler.cs:43-51`) | — | permissão fixa no handler, para ter mensagem (AZ-14); atendentes lidos do banco (AZ-02) |
| `roles` | GET | `roles:read` (`Roles/GetAll.cs:22`) | — | — | "padrão" e "administrador" pelo nome (`GetRolesQueryHandler.cs:19-20`) |
| `roles/{roleName}/permissions` | PUT | `roles:manage` (`AssignPermissions.cs:36`) | função Administrador gerenciada (`AssignRolePermissionsCommandHandler.cs:29-32`); RN-17 (`:45-54`) | só função comum | rota pelo nome (036); invalida os membros lidos antes do commit (AZ-10) |
| `permissions` | GET | usuário ativo (`Permissions/GetAll.cs:22`) | — | — | catálogo, sem ler o banco (`GetPermissionsQueryHandler.cs:10-21`) |
| `departments` | POST | `departments:manage` (`Departments/Create.cs:37`) | — | — | — |
| `departments/{departmentId}` | PUT | `departments:manage` (`Departments/Update.cs:39`) | — | — | — |
| `departments/{departmentId}` | DELETE | `departments:manage` (`Departments/Deactivate.cs:25`) | — | — | desativa, não exclui |
| `departments` | GET | usuário ativo (`Departments/GetAll.cs:22`) | — | — | — |
| `departments/{departmentId}` | GET | usuário ativo (`Departments/GetById.cs:25`) | — | — | — |
| `sectors` | POST | `sectors:manage` (`Sectors/Create.cs:37`) | — | — | — |
| `sectors/{sectorId}` | PUT | `sectors:manage` (`Sectors/Update.cs:37`) | — | — | — |
| `sectors/{sectorId}` | DELETE | `sectors:manage` (`Sectors/Deactivate.cs:25`) | — | — | desativa, não exclui |
| `sectors` | GET | usuário ativo (`Sectors/GetAll.cs:22`) | — | — | — |
| `sectors/{sectorId}` | GET | usuário ativo (`Sectors/GetById.cs:25`) | — | — | — |
| `tickets` | POST | `tickets:create` (`Tickets/Create.cs:44`) | em nome de outra pessoa, `tickets:manage` (`CreateTicketCommandHandler.cs:28-33`) | — | o responsável indicado não passa por `tickets:assign` nem pela elegibilidade (`:37-50,79`; AZ-06) |
| `tickets` | GET | `tickets:manage` (`Tickets/GetAll.cs:53`) | — | — | — |
| `tickets/assignees` | GET | `tickets:manage` (`GetAssignees.cs:28`) | — | — | elegíveis lidos do banco (`TicketAssigneeEligibility.cs:15-21`) |
| `tickets/mine` | GET | `tickets:view-own` (`GetOwn.cs:28`) | — | filtro por `CreatedByUserId` (`GetOwnTicketsQueryHandler.cs:43`) | `tickets:view-own` só vale aqui e no resumo (AZ-07) |
| `tickets/mine/summary` | GET | `tickets:view-own` (`GetOwnSummary.cs:22`) | — | filtro por `CreatedByUserId` (`GetOwnTicketSummaryQueryHandler.cs:19`) | idem |
| `tickets/sla-summary` | GET | `tickets:manage` (`GetSlaSummary.cs:25`) | — | — | — |
| `tickets/stats/status` | GET | usuário ativo (`Tickets/GetStatusCounts.cs:25`) | — | — | números da empresa para todo papel (RN-35) |
| `tickets/stats/volume` | GET | usuário ativo (`GetVolume.cs:48`) | `tickets:manage` só com filtro de recorte (`GetTicketVolumeQueryHandler.cs:63-74`) | — | permissão condicional: lugar certo é o handler |
| `tickets/stats/overview` | GET | `tickets:manage` (`GetOverview.cs:47`) | — | — | — |
| `tickets/stats/times` | GET | `tickets:manage` (`GetTimes.cs:47`) | — | — | — |
| `tickets/{ticketId}` | GET | usuário ativo (`Tickets/GetById.cs:25`) | `TicketAccessGuard` (`GetTicketByIdQueryHandler.cs:26-35`) | participante ou `tickets:manage` | não exige `tickets:view-own` (AZ-07); 404 antes de 403 |
| `tickets/{ticketId}/sla` | GET | usuário ativo (`GetSla.cs:25`) | `TicketAccessGuard` (`GetTicketSlaQueryHandler.cs:27-35`) | participante ou `tickets:manage` | — |
| `tickets/{ticketId}` | PUT | usuário ativo (`Tickets/Update.cs:38`) | matriz de permissões (`UpdateTicketCommandHandler.cs:35-49,66-69`) | descrição: dono com `tickets:update-own` ou `tickets:manage`; Em andamento e Resolvido: `tickets:manage`; Cancelado: `Ticket.Cancel` (`Ticket.cs:278-295,438-445`) | F-26 |
| `tickets/{ticketId}` | DELETE | usuário ativo (`Tickets/Delete.cs:25`) | `tickets:delete` (`DeleteTicketCommandHandler.cs:29-38`) | — | permissão fixa no handler; 404 antes de 403 (F-29) |
| `tickets/{ticketId}/reopen` | POST | usuário ativo (`Reopen.cs:34`) | dono: `tickets:reopen-own`; demais: `tickets:manage` (`ReopenTicketCommandHandler.cs:33-41`) | dono | não usa guard, embora o `CLAUDE.md` diga que usa (AZ-15) |
| `tickets/{ticketId}/close` | POST | `tickets:manage` (`Close.cs:25`) | transição (`CloseTicketCommandHandler.cs:29-32`) | — | — |
| `tickets/{ticketId}/assignee` | PUT | `tickets:manage` (`Assign.cs:28`) | `tickets:assign` para outra pessoa (`AssignTicketCommandHandler.cs:28-39`); atendente lido do banco (`:63-66`) | — | 403 antes de 404 (AZ-14) |
| `tickets/{ticketId}/assignee` | DELETE | `tickets:manage` (`Unassign.cs:25`) | — | — | tirar outra pessoa não exige `tickets:assign` (F-29) |
| `tickets/{ticketId}/messages` | GET | usuário ativo (`TicketMessages/GetAll.cs:27`) | `TicketAccessGuard` (`GetTicketMessagesQueryHandler.cs:21`) | participante ou `tickets:manage` | — |
| `tickets/{ticketId}/messages` | POST | usuário ativo (`Post.cs:35`) | cópia da RN-07: `ticket.IsParticipant` ou `tickets:manage` (`PostTicketMessageCommandHandler.cs:36-41`) | participante ou `tickets:manage` | cópia da regra (F-33); chamado inativo é aceito (Q-02) |
| `ticket-messages/{messageId}` | PUT | usuário ativo (`Edit.cs:35`) | — | só o autor (`TicketMessage.cs:50-55`) | não reconfere o acesso ao chamado (F-29) |
| `ticket-messages/{messageId}` | DELETE | usuário ativo (`TicketMessages/Delete.cs:25`) | `tickets:manage` pelo provedor (`DeleteTicketMessageCommandHandler.cs:36-41`) | autor ou `tickets:manage` (`TicketMessage.cs:72-77`) | forma híbrida da skill; não reconfere o acesso (F-29) |
| `tickets/{ticketId}/attachments` | GET | usuário ativo (`TicketAttachments/GetAll.cs:27`) | `TicketAccessGuard` (`GetTicketAttachmentsQueryHandler.cs:21`) | participante ou `tickets:manage` | — |
| `tickets/{ticketId}/attachments` | POST | usuário ativo, com o limite `attachment-upload`, 11 MB e antiforgery desligado (`Upload.cs:43-47`) | cópia da RN-07 (`UploadTicketAttachmentCommandHandler.cs:42-47`) | participante ou `tickets:manage` | conteúdo conferido pela assinatura (SE-11) |
| `ticket-attachments/{attachmentId}/download` | GET | usuário ativo (`Download.cs:33`) | `TicketAccessGuard` (`GetTicketAttachmentContentQueryHandler.cs:45-55`) | participante ou `tickets:manage` | `nosniff` e `attachment` (`Download.cs:20,29`) |
| `ticket-attachments/{attachmentId}` | DELETE | usuário ativo, com o limite `attachment-delete` (`TicketAttachments/Delete.cs:25-26`) | `tickets:manage` pelo provedor (`DeleteTicketAttachmentCommandHandler.cs:34-38`) | quem enviou ou `tickets:manage` (`TicketAttachment.cs:142-147`) | não reconfere o acesso (F-29) |
| `ticket-attachments/policy` | GET | usuário ativo (`GetPolicy.cs:24`) | — | — | — |
| `tickets/{ticketId}/history` | GET | usuário ativo (`TicketHistories/GetAll.cs:27`) | `TicketAccessGuard` (`GetTicketHistoryQueryHandler.cs:22`) | participante ou `tickets:manage` | — |
| `audit-logs/{entityName}/{entityId}` | GET | `audit:read` (`AuditLogs/GetHistory.cs:29`) | — | — | qualquer entidade auditável; hash de senha, carimbo e hash do convite fora dos valores (`AuditLoggingInterceptor.cs:19-24`) |
| `health` | GET | sem metadado (`Program.cs:50`) | — | — | anônima por omissão; relatório detalhado só em Development (`:45-48`); com o fallback, precisa de `.AllowAnonymous()` (DEC-34) |

Fora da numeração: em Development, o Swagger (`Program.cs:34-37`) é middleware que responde antes da autenticação, e não endpoint.

**Totais (62 rotas).**

| Proteção na rota | Rotas |
|---|---|
| permissão na rota | 29 |
| usuário ativo | 26: 19 com conferência no handler ou na entidade, 7 abertas a qualquer usuário ativo |
| só autenticado | 2 |
| anônimas | 4 |
| sem metadado | 1 |

**Inconsistências entre operações parecidas** (detalhe em 2.10.4):
- indicar responsável: `tickets:assign` no `PUT` e nada na abertura (AZ-06); tirar o responsável sem `tickets:assign` (F-29);
- RN-17: no setor e não no perfil (mesma `users:update`); no reenvio de convite e não no cadastro (mesma `users:create`) (AZ-05);
- permissão fixa: na rota, em geral; no handler, em `tickets:delete` (F-29) e em `users:read` dos atendentes (AZ-14);
- ordem de 404 e 403 diferente entre rotas (AZ-14);
- RN-07 em três formas (F-33);
- `tickets:view-own` exigida nas listas e não no detalhe (AZ-07);
- "quem atende": banco na atribuição e na primeira resposta; cache no acesso (AZ-02).

### 2.10.5 Modelo-alvo

**Onde cada conferência fica.** A forma é a do `CLAUDE.md` (Desvios, as três formas de autorização) e da `authorization.md`.

| Conferência | Lugar | Exemplos no alvo |
|---|---|---|
| permissão fixa | rota, com `.HasPermission(...)` | as 29 rotas de hoje |
| regra sobre o estado da entidade | método da entidade, que recebe do handler a permissão efetiva | `Ticket.Cancel(canCancelAny, …)`, `TicketMessage.Delete(…, canDeleteAny)`, `Role.ReplacePermissions` (função gerenciada), `Ticket.CanBeViewedBy(userId, canManageTickets)` |
| permissão efetiva condicional | handler, pelo `IPermissionProvider` | `tickets:assign` só para outra pessoa; `tickets:manage` só com recorte no volume; "o próprio ou `users:read`" |
| conferência com consulta repetida | peça injetada `internal sealed`, na pasta da regra | `PrivilegeEscalationCheck` (6 handlers), `LastAdministratorCheck` (2), acesso ao chamado nas consultas (DEC-9 da 043) |
| conferência com consulta única | método privado `HasPermissionTo{Action}` do handler | nenhuma hoje; padrão para as novas |
| dados do próprio usuário | filtro por `IUserContext.UserId` | `tickets/mine`, `tickets/mine/summary`, `users/me` |
| negar por padrão | `FallbackPolicy` e teste da matriz | todas as rotas (DEC-34, REQ-200) |

**Agregados e peças.**

```
Role (raiz) ──contém──► RolePermission (entidade interna: código)                         [DEC-31]
   ReplacePermissions(codes) → Result com o que entrou e o que saiu;
   recusa a função gerenciada; dispara RolePermissionsAssignedDomainEvent
User (raiz, 2.9) ──contém──► UserRole (entidade interna: função)                          [DEC-31]
Permission: dado de referência (catálogo → migrations), fora do IApplicationDbContext
PermissionCatalog e PermissionCodes (Application): código, nome, grupo e marca administrativa
RN-17: PrivilegeEscalationCheck (Application, injetada)                                   [DEC-30]
RN-18: LastAdministratorCheck (Application, injetada) + linha da função Administrador
       como trava (token hoje; FOR UPDATE na 043p2)                                       [DEC-33]
Permissões efetivas: IPermissionProvider, com leitura em cache e sem cache                [DEC-32]
RN-07: Ticket.CanBeViewedBy (chamado carregado) + peça das consultas (DEC-9 da 043)       [DEC-30]
```

**Responsabilidades.**

| Responsabilidade | Camada | Peça |
|---|---|---|
| regras sobre o estado da função (gerenciada; conjunto de permissões sem repetição) | Domain | `Role` |
| conjunto de funções do usuário | Domain | `User` (2.9) |
| códigos, nomes e marca administrativa | Application | `PermissionCatalog`, `PermissionCodes` |
| permissões efetivas, "ativo", cache e invalidação | Application (abstração) e Infrastructure | `IPermissionProvider` e `PermissionProvider` |
| RN-17 e RN-18 | Application | `PrivilegeEscalationCheck`, `LastAdministratorCheck` |
| acesso ao chamado | Domain (carregado) e Application (consulta) | `Ticket.CanBeViewedBy` e a peça da DEC-9 da 043 |
| policies, requisitos, provedor de policy, fallback, JWT | Infrastructure | `Authorization/`, `Authentication/`, `DependencyInjection.cs` |
| proteção de cada rota | Web.Api | endpoints, conferidos pelo teste da matriz |
| cabeçalhos, rate limit e CORS | Web.Api | `Program.cs`, `Extensions/`, `Middleware/` |

**Pastas e nomes (DEC-39).**
- `Application/Authorization/` deixa de existir:
  - `TicketAccessGuard` → `Application/Tickets/TicketViewAccess.cs`, se a DEC-9 da 043 for a (b);
  - `PrivilegeEscalationGuard` → `Application/Roles/PrivilegeEscalationCheck.cs`;
  - `AdministratorGuard` → `Application/Roles/LastAdministratorCheck.cs`.
- Ficam como estão `Application/Abstractions/Authorization/`, `Infrastructure/Authorization/` e `Infrastructure/Roles/`.
- Novo na Web.Api: `Middleware/SecurityHeadersMiddleware.cs`, para `nosniff` e `no-store` (DEC-37).

**Persistência.**
- **Nesta spec (ainda com o EF):**
  - sem migration; a `Role` passa a mapear a coleção pelo campo;
  - o token `Version` continua, com o nome num lugar só;
  - `Permission` sai do `IApplicationDbContext`;
  - o `Model_Should_HaveNoPendingChanges` prova que o esquema não muda.
- **Contrato para a 043 parte 2 (REQ-220):**
  1. trocar as permissões de uma função é uma transação com `SELECT … FOR UPDATE` na linha da função, `DELETE` das que saem e `INSERT … ON CONFLICT DO NOTHING` das que entram;
  2. a RN-18 usa a trava da DEC-33;
  3. as permissões efetivas são uma consulta SQL só, usada com e sem cache;
  4. a invalidação roda depois do commit, com os membros lidos depois do commit.

**Contrato com o cliente.** Nada muda para o cliente, salvo:
- as DECs pendentes que forem aprovadas: DEC-34 (rota inexistente sem token responde 401), DEC-35, DEC-36 e DEC-38;
- o texto da mensagem do REQ-209;
- os cabeçalhos e o corpo do 429 do REQ-214.

### 2.10.6 Conformidade com as skills

| Regra | Fonte | Situação hoje | Evidência | Alvo |
|---|---|---|---|---|
| Permissão fixa na rota | `authorization.md` ("Permissão fixa — no endpoint"); `CLAUDE.md`, Desvios, forma 1 | cumprida em 29 rotas; exceções: `tickets:delete` e `users:read` dos atendentes, no handler | `DeleteTicketCommandHandler.cs:29-38`; `GetBacklogAttendantsQueryHandler.cs:43-51` | Q-35 |
| Regra sobre o estado na entidade | `authorization.md`; `domain-modeling` (padrão 1) | cumprida em `Ticket`, `TicketMessage` e `TicketAttachment`; não na função gerenciada | `Ticket.cs:278-295`; `TicketMessage.cs:50-77`; `TicketAttachment.cs:142-147`; `AssignRolePermissionsCommandHandler.cs:29-32` | `Role.ReplacePermissions` (REQ-204) |
| Permissões efetivas pelo `IPermissionProvider`; o handler não lê tabelas de funções | `authorization.md` | não cumprida em `TicketAgentLookup`, `TicketAssigneeEligibility` e na escalada sobre o alvo | AZ-02 | DEC-32 |
| Conferência com consulta em `HasPermissionTo{Action}`; peça compartilhada injetada, nunca estática | `authorization.md`; `ca-review` | três guards estáticos (legado declarado) e duas cópias inline | AZ-03 | DEC-30, DEC-39 |
| Endpoint com `.RequireAuthorization()` ou `.HasPermission(...)` | `add-feature/SKILL.md`; `references/endpoint.md` | cumprida nas 61 rotas, sem teste nem fallback | AZ-01 | REQ-200, REQ-201 |
| Fluxo do handler: carrega → não encontrado → permissão | `add-feature/SKILL.md` | cumprida na maioria; `Assign`, o perfil e `GET users/{id}` conferem a permissão antes | AZ-14 | Q-35 |
| Entidade rica e handler sem atribuir propriedade nem chamar `Raise` | `add-entity`; `domain-modeling`; `ca-review` | não cumprida em funções e vínculos (legado "Em migração") | AZ-11 | DEC-31 |
| `<summary>` em toda interface | `CLAUDE.md`, Regras permanentes | falta em `IPermissionProvider` (e em interfaces de autenticação, na 2.9) | `IPermissionProvider.cs:3` | REQ-219 |
| Invalidação de cache em todo command que altera os dados | `ca-review` (Cache); `CLAUDE.md` (Cache) | cumprida nos 4 commands; membros lidos antes do commit | AZ-10 | REQ-208 |
| Testes de cada falha do handler e do caminho feliz; 401 sem token | `add-tests` | cumprida nos handlers; lacunas na infraestrutura de autorização | AZ-17 | REQ-218 |
| SQL e acesso a dados | não há skill de SQL; convenção da 043 (DEC-2 dela) | EF; `Entry` usado na Application para o token | AZ-04 | REQ-220 (043p2) |

### 2.10.7 Não determinado

- **Corpo das respostas 401 e 403 da autorização de rota.** Não há `UseStatusCodePages` nem `IAuthorizationMiddlewareResultHandler` em `src`, e o comentário em `GetBacklogAttendantsQueryHandler.cs:13-16` indica corpo sem mensagem; nenhum teste confere. O teste do AC-200 pode fixar o comportamento.
- **Invalidação com o Redis fora do ar.** Não se sabe o que o `HybridCache.RemoveAsync` faz, depois do commit, nesse caso: exceção para o cliente ou só log.
- **Cópia local sem Redis.** A validade efetiva foi assumida igual à de `LocalCacheExpiration` (1 min).
- **Produção.** A topologia (proxy, TLS, número de instâncias; Q-11 do estado) e o valor de `Jwt:ExpirationInMinutes`.
- **OpenTelemetry.** Se ele grava a query string (o e-mail de `users/by-email`).
- **Front-end (2.13).** Se envia `assignedToUserId` na abertura e se depende do texto do 403 da RN-17. **Correção (reconferência):** respondido: o front não envia `assignedToUserId` (o `responsibleId` de `CreateTicketData` nunca é preenchido) nem depende do texto do 403 da RN-17 (2.13.2, FE-22).
- **Migration.** Se mudar o tipo `Permission` de lugar gera migration (prova pelo `Model_Should_HaveNoPendingChanges`).

### 2.10.8 Fases da área

- **Fase AZ-0 — Caracterização (só testes):** T-200 e T-213.
  - Depende de: nada.
  - Pode ir antes de tudo e protege as fases seguintes.
- **Fase AZ-1 — Endurecimento sem mudança para o cliente legítimo:** T-202 (sem o teto, enquanto a DEC-36 estiver pendente), T-203 e T-204; T-201 depois da DEC-34.
  - Depende de: AZ-0 e DEC-37.
- **Fase AZ-2 — Peças de autorização:** T-205 e T-206; T-207 depois da DEC-9 da 043 e da 043 parte 1, na ordem da DEC-4.
  - Depende de: AZ-0, DEC-30, DEC-32 e DEC-39.
- **Fase AZ-3 — Funções no modelo rico:** T-208 e T-210; T-209 com a 2.9.
  - Depende de: DEC-31.
  - Vai antes da 036, ou a 036 a absorve, conforme a DEC-31.
- **Fase AZ-4 — Mudanças de comportamento aprovadas:** T-211 (DEC-35) e T-212 (DEC-38, com a 2.9).
  - Depende de: as duas DECs e AZ-2 (usa as peças injetadas).
- **Fase AZ-5 — Documentação e verificação:** T-214 e T-215.
  - Depende de: as fases anteriores.
  - O `CLAUDE.md` só muda com a DEC-14.
- **Fora destas fases (043 parte 2) — Contrato de SQL da autorização:** REQ-220 e DEC-33, executados na 043 parte 2; a T-216 entrega o contrato.
  - Depende de: esta spec aprovada.

### 2.10.9 Sessões e tokens depois da desativação: carimbo de segurança e trava (CT-26)

> Escrito em 2026-11-01, a pedido do responsável. Na quinta resposta, ele decidiu pelo carimbo com cache de validade curta (item 6). A CT-26 continua aberta até a reprodução da corrida e a validação dos cenários, que essa decisão não substitui. Base: `main` `74a8a5f`; os arquivos citados não mudaram desde `3305fb5`. A ordem das operações de hoje e os casos A a F vêm logo abaixo, movidos da 2.8.4 do `spec.md`.

#### Ordem das operações e casos (movido da 2.8.4 do `spec.md` em 2026-11-01)

**Ordem das operações na CT-26 (análise de 2026-11-01, a reproduzir por teste).** Na `main` `3305fb5`:
- **Login:**
  - L1 lê o usuário pelo e-mail, com a situação (`UserCredentialsVerifier.cs:32-34`);
  - L2 lê a credencial, com o carimbo, em outra consulta (`:44-45`);
  - L3 confere a senha;
  - L4 grava a sessão com o carimbo lido em L2 e emite o access token. O token leva só `sub`, `email` e `name` (`TokenProvider.cs:26-28`) e vale `Jwt:ExpirationInMinutes` (60 no `appsettings.Development.json`). A autenticação não confere carimbo a cada requisição.
- **Desativação:**
  - D1 carrega o usuário;
  - D2 confere escalada e último administrador;
  - D3 marca inativo;
  - D4 revoga os convites;
  - D5 lê as sessões existentes e as marca para remoção;
  - D6 grava tudo num save (`DeactivateUserCommandHandler.cs:33-71`). O carimbo não muda.
- **Reativação:** R1 carrega, R2 confere a escalada, R3 marca ativo e R4 grava (`ActivateUserCommandHandler.cs:27-49`). Não revoga sessões nem muda o carimbo.
- **Renovação:** lê usuário e carimbo numa consulta só (`RefreshTokenCommandHandler.cs:38-49`):
  - com carimbo diferente, remove a sessão e recusa (`:52-61`);
  - com a conta inativa, recusa sem remover (`:65-68`);
  - senão, troca o token na mesma linha.

| Caso | Ordem | Hoje | Com a revogação na reativação | Com a alternativa abaixo |
|---|---|---|---|---|
| A | a sessão é gravada antes de D5 (a emissão termina antes da desativação) | D remove a sessão | igual | igual; a sessão teria também o carimbo antigo |
| B | L1 antes de D6; a sessão é gravada depois de D5 (a emissão termina depois da desativação) | sessão residual: recusada enquanto a conta está inativa, volta a valer depois da reativação, até 30 dias | removida na reativação | recusada e removida na renovação: carimbo antigo |
| C | L1 depois de D6 | login recusado | igual | igual |
| D | L1 antes de D6; a sessão é gravada depois de R4 (login mais longo que a desativação e a reativação) | sessão válida | **continua válida**: a revogação de R já passou | recusada: carimbo antigo |
| E | access token emitido em B ou D | recusado enquanto a conta está inativa; vale de novo depois da reativação, até expirar | igual: a revogação não alcança o access token | recusado, se o carimbo for conferido a cada requisição (item 3) |
| F | renovação junto da desativação | a renovação troca a linha que D remove; uma das duas falha por concorrência (`TokenHash`), sem sessão residual (hipótese) | igual | igual |

**Alternativa de menor impacto, sem trava (para avaliação; a CT-26 continua aberta):**
1. a desativação gera carimbo novo, no mesmo save em que revoga convites e sessões, como a troca de senha (I-US-10);
2. o login lê a situação e o carimbo numa consulta só, como a renovação já faz. Com duas leituras, um login pode ler a conta ativa antes da desativação e o carimbo novo depois dela;
3. o access token leva o carimbo, e a autorização de cada requisição o compara com o atual, com o cache invalidado quando o carimbo muda (troca de senha e desativação).

Com 1 e 2, toda sessão emitida antes ou durante a desativação tem o carimbo antigo. Ela é recusada e removida na renovação, inclusive depois da reativação (casos A, B e D), e a reativação não precisa revogar nada. Sem o item 3, o caso E fica limitado à validade do access token. Nenhum item trava a linha do usuário no login (CT-20).

**Para encerrar a CT-26:**
- um teste que reproduz B e D hoje, registrado como falho antes da correção (REQ-28), com um gatilho temporário que segura a gravação da sessão até a desativação e a reativação terminarem;
- depois da correção, os casos A a F, com a emissão terminando antes e depois da desativação;
- a prova de que nenhuma sessão nem access token emitido na corrida vale depois da reativação.

A reprodução exige execução, fora da autorização desta revisão.

**Fatos de hoje:**
- **Carimbo (`SecurityStamp`):**
  - fica na credencial e nasce na ativação da conta (`ActivateUserAccountCommandHandler.cs:56`);
  - muda na troca de senha (`UserPasswords.cs:26`); a desativação e a reativação não o mudam;
  - a sessão guarda o carimbo lido no login (`UserSessions.cs:48`).
- **Renovação:** lê a conta e o carimbo atual numa consulta só (`RefreshTokenCommandHandler.cs:38-49`). Confere o carimbo (`:52-61`) e só depois a situação da conta (`:65-68`).
- **Access token:** leva só `sub`, `email` e `name` (`TokenProvider.cs:26-28`). Vale `Jwt:ExpirationInMinutes`, sem tolerância de relógio (`ConfigureJwtBearerOptions.cs:32`).
- **A cada requisição:** a autorização pergunta ao `IPermissionProvider` se a conta está ativa (`ActiveUserAuthorizationHandler.cs:24`).
  - A resposta fica em cache (`PermissionProvider.cs:38-42`): HybridCache com 5 min de validade e 1 min no cache local de cada instância (`DependencyInjection.cs:64-67`).
  - O cache é invalidado depois do commit na desativação, na reativação e nas trocas de funções e de permissões (`DeactivateUserCommandHandler.cs:76`, `ActivateUserCommandHandler.cs:53`, `AssignUserRolesCommandHandler.cs:94`, `AssignRolePermissionsCommandHandler.cs:81`).
  - A troca de senha não o invalida.

**Desenho por carimbo, nos sete pontos pedidos:**
1. **Desativação:** gera um carimbo novo para a credencial (um `Guid` aleatório, como na troca de senha).
   - Isso acontece no mesmo save em que marca a conta inativa e revoga convites e sessões; na G4, na mesma transação, sob a trava da linha do usuário (DEC-22).
   - Conta pendente não tem credencial nem sessão, então não há o que trocar.
   - No modelo rico (DEC-20), é efeito do método de desativação do `User`.
2. **Login:** lê a situação da conta, o carimbo, o hash e o contador numa consulta só, no mesmo instante de leitura do banco.
   - Grava a sessão com esse carimbo e põe o mesmo carimbo numa claim nova do access token.
   - A consulta única garante que um login que viu a conta ativa viu o carimbo de antes da desativação; com duas consultas, isso não vale.
   - Pela CT-29, a restrição da CT-28 cobre só o `UPDATE` atômico do contador e a auditoria dele. Esta leitura não fica presa à G4, e a etapa da implementação continua a decidir.
3. **Renovação:** a situação passa a ser conferida antes do carimbo.
   - Conta inativa recebe `Users.Deactivated`, como hoje, e a sessão é removida, porque já não serviria pelo carimbo.
   - Conta ativa com carimbo diferente perde a sessão e recebe a recusa, como hoje (`:52-61`).
   - Sem trocar a ordem, a conta desativada passaria a receber `Users.InvalidRefreshToken` no lugar de `Users.Deactivated`, o que mudaria o contrato de hoje.
   - O access token novo leva o carimbo atual, lido na mesma consulta.
   - **Decisão de 2026-11-01 (quinta resposta):** a renovação aplica a mesma validação das requisições: primeiro a situação, depois o carimbo. Ela continua lendo o banco na consulta que já faz, e por isso não tem janela residual.
4. **Cada requisição:** a conferência fica no evento de validação do token (`JwtBearer`) e usa a mesma entrada de cache da situação, que passa a guardar situação e carimbo.
   - Conta ativa com claim diferente do carimbo atual, ou sem a claim: 401, como token inválido.
   - Conta inativa: segue para a autorização, que devolve 403 como hoje (com o código `Users.Deactivated` quando o AC-505 valer); o `users/me` continua mostrando a conta desativada.
   - Quando o cache diz "carimbo diferente", a conferência relê o banco antes de recusar. Assim um token novo não é recusado por causa de uma entrada velha.
5. **Token emitido antes da desativação:**
   - a renovação o recusa e remove a sessão;
   - o access token recebe 403 enquanto a conta está inativa e 401 depois da reativação, porque leva o carimbo antigo;
   - vale também para o emitido na corrida (casos B, D e E acima), porque ele leva o carimbo lido antes da desativação.
6. **Cache, consistência e invalidação:**
   - **Entrada por usuário:** guarda situação e carimbo e é invalidada depois do commit da desativação, da reativação e da troca de senha. A troca de senha passa a invalidar, senão o token novo da própria sessão seria recusado enquanto a entrada velha durasse.
   - **Consistência:** a mesma que vale hoje para a situação (I-US-17). Na mesma instância, vale a partir da requisição seguinte à invalidação. Em outra instância, um token antigo pode passar por até 1 min (cache local). Um token novo não é recusado, porque a diferença manda reler o banco.
   - **Corrida conhecida do cache:** uma requisição que leu o banco antes do commit e grava o cache depois da invalidação deixa a entrada velha por até 5 min. Hoje isso já vale para a situação e as permissões (I-AZ-02, aceito).
   - **Escolhas para o carimbo, a decidir:**
     - (a) aceitar o mesmo limite;
     - (b) validade curta só para esta entrada (por exemplo, 30 s), com mais leituras;
     - (c) ler o carimbo no banco a cada requisição (uma leitura pela chave), sem cache e sem resíduo.
   - **Decisão de 2026-11-01 (quinta resposta): (b).**
     - **Validade:** 30 s no início, configurável. A validade local de cada instância também fica nesse teto.
     - **Leitura:** cada requisição autenticada lê o carimbo no cache; vencido ou ausente, ele é buscado no banco e o cache é atualizado. Carimbo diferente no cache manda reler o banco antes de recusar.
     - **Invalidação:** as mudanças de segurança (desativação, reativação e troca de senha) tentam invalidar a entrada na hora, depois do commit. A validade é a proteção para quando a invalidação não alcança todas as instâncias.
   - **Janela residual de revogação, com essa decisão:**
     - **Na instância que fez a mudança:** a invalidação vale a partir da requisição seguinte. Só fica uma entrada velha se uma requisição leu o banco antes do commit e gravou o cache depois da invalidação; nesse caso, até 30 s contados dessa gravação tardia, e não da mudança.
     - **Em outra instância que a invalidação não alcançou:** até 30 s.
     - **Teto (corrigido na sexta resposta de 2026-11-01):** sem leitura em andamento na hora da mudança, 30 s depois dela. Com uma leitura em andamento, a janela passa de 30 s; a investigação está no fim desta seção.
     - **Na renovação:** nenhuma janela, porque ela lê o banco.
     - **Token novo:** nunca é recusado por entrada velha, porque a diferença manda reler o banco.
     - **Comparação com hoje:** a situação da conta já tem janela de até 1 min no cache local e de até 5 min na corrida do cache (I-AZ-02). O carimbo passa a ter validade de 30 s, com a ressalva da investigação abaixo.
7. **Reativação:**
   - não muda o carimbo e não precisa revogar sessões: o que existia antes da desativação ficou com o carimbo antigo;
   - invalida o cache, como hoje, para a situação voltar a ativa;
   - sessões removidas não voltam, e o usuário entra de novo;
   - um login simultâneo à reativação vê a conta inativa e é recusado, ou a vê ativa e recebe o carimbo atual, legitimamente.

**Impacto:**
- **Middleware e autenticação:**
  - claim nova no `TokenProvider`;
  - conferência no evento de validação do token, em `ConfigureJwtBearerOptions`;
  - o `IPermissionProvider` passa a devolver situação e carimbo, e a troca de senha passa a invalidar o cache;
  - a renovação inverte a ordem das conferências.
  - Um token emitido antes da mudança não tem a claim e recebe 401. Se o cliente renovar pela sessão, que tem o carimbo gravado, recebe um token novo sem entrar de novo. O comportamento do front nesse 401 precisa ser conferido (FE).
- **Contrato observável:**
  - 401 para access token com carimbo antigo, depois da reativação e, nas outras sessões, depois da troca de senha (hoje esse token vale até expirar);
  - claim nova no token, aditiva;
  - 403 para conta inativa e `Users.Deactivated` na renovação, como hoje.
- **Persistência:**
  - sem mudança de esquema: a coluna do carimbo existe na credencial e na sessão;
  - a desativação passa a gravar a credencial: no EF, mais uma linha no mesmo save, com registro de auditoria (o valor do carimbo fica de fora, `AuditLoggingInterceptor.cs:19-24`); na G4, um `UPDATE` a mais na transação, com a auditoria em SQL.
- **Desempenho:**
  - o login faz uma consulta em vez de duas;
  - a renovação fica igual;
  - por requisição, nada a mais com o cache, só uma releitura quando o carimbo difere; com a escolha (c), uma leitura pela chave em toda requisição.
- **Testes:**
  - os da ordem das operações acima: reprodução de B e D hoje e, depois, os casos A a F;
  - a janela residual: com a invalidação bloqueada numa instância, um token antigo deixa de passar em até 30 s; com a validade alterada na configuração, o teto acompanha;
  - access token antigo depois da reativação (401);
  - conta inativa (403) e renovação de conta inativa (`Users.Deactivated`);
  - troca de senha: 401 nas outras sessões, e a sessão que trocou segue com os tokens devolvidos;
  - token sem a claim (401) e renovação bem-sucedida;
  - invalidação na desativação, na reativação e na troca de senha;
  - releitura do banco quando o cache está velho.

**Comparação: carimbo × trava no login.** Na trava, o login pega `FOR SHARE` na linha do usuário, da leitura da situação até gravar a sessão, na transação explícita da 043p2.

| | Carimbo (itens 1 a 7) | Trava no login |
|---|---|---|
| Sessão (refresh) emitida na corrida | recusada e removida na renovação | não sobra: a desativação espera o login e remove a sessão, ou o login espera e é recusado |
| Access token emitido antes ou durante a desativação, usado depois da reativação | recusado (401) | **vale até expirar**: a trava não alcança o access token, e fechar exige o item 4 de qualquer modo |
| Access token das outras sessões depois da troca de senha | recusado na hora | como hoje: vale até expirar |
| Trava no login | nenhuma: mantém a CT-20 (a) | reabre a CT-20. `FOR SHARE` não conflita entre logins, mas conflita com o `FOR NO KEY UPDATE` da DEC-22: logins e commands da conta esperam uns pelos outros, inclusive durante a conferência do hash |
| Esquema e contrato | sem esquema; 401 novo para carimbo antigo; claim nova | sem esquema nem contrato |
| Custo por requisição | nenhum com cache (releitura só na diferença); uma leitura com (c) | nenhum |
| Consistência | sessões: imediata; access token: até 30 s depois da mudança, com a validade curta decidida (até 1 min em outra instância e 5 min na corrida, com o cache de hoje; imediata com (c)) | sessões: imediata; access token: nenhuma |
| Etapa | a decidir. Desde a CT-29, a restrição da CT-28 não alcança a leitura do login | G4: precisa da transação explícita |

**Leitura:** só o carimbo atende ao pedido de que nenhum token emitido antes da desativação valha depois dela, porque a trava não alcança o access token. A trava fecharia só as sessões residuais, com o custo de reabrir a CT-20 e de esperas no login, e ainda precisaria da conferência por requisição. Em 2026-11-01, o responsável decidiu pelo carimbo com validade curta no cache (item 6). A CT-26 continua aberta até a reprodução da corrida e a validação dos cenários acima.

#### Repovoamento do cache e janela efetiva (investigação de 2026-11-01, sexta resposta)

> O responsável pediu que se investigasse quatro pontos, avaliando mecanismos como geração ou versão de invalidação, sem escolher de antemão:
> - se uma leitura antiga pode repovoar o cache depois da invalidação;
> - se o cache é compartilhado entre instâncias;
> - como a invalidação funciona entre instâncias;
> - qual é a garantia temporal efetiva.
>
> A CT-26 continua aberta até esta análise e a reprodução das corridas. Fontes: o código da `main` `74a8a5f` e a documentação oficial do HybridCache (Microsoft Learn, ASP.NET Core 10.0).

**Correção do registro anterior.** O "teto de 30 s depois da mudança", registrado com a decisão do item 6, não vale quando há uma leitura em andamento na hora da mudança: a validade começa a contar na gravação tardia, e não na mudança.

**Topologia do cache (fatos):**
- **Cache em memória:** cada instância tem o seu, sempre. Pela documentação, "each server has a separate cache".
- **Segundo nível (Redis):** só existe com a `ConnectionStrings:Cache` preenchida (`DependencyInjection.cs:52-60`). No `appsettings.json` do repositório ela está vazia, então o padrão é só memória; a configuração de produção não está no repositório.
- **Validade:**
  - hoje, 5 min no total e 1 min na memória de cada instância (`DependencyInjection.cs:62-67`);
  - para a entrada do carimbo, 30 s, pela decisão da quinta resposta.
- **Pacote:** `Microsoft.Extensions.Caching.Hybrid` 10.11.0 (`Directory.Packages.props:20`).

**Invalidação entre instâncias (documentação oficial):**
- a remoção por chave apaga a entrada da instância que remove e do segundo nível;
- "the in-memory cache in other servers isn't affected": a memória das outras instâncias só perde o valor quando ele vence, como o comentário de `DependencyInjection.cs:62-63` já registra;
- a proteção contra leituras simultâneas da mesma chave junta só as chamadas da mesma instância;
- a invalidação por tag é lógica ("ignorar o que foi criado antes deste ponto") e também não alcança a memória das outras instâncias;
- a documentação não diz o que acontece com uma leitura que termina depois de uma remoção da mesma chave. Sem prova em contrário, o valor antigo é gravado. **Sétima resposta:** o código confirma a gravação (fato 7 da subseção do mecanismo 3, abaixo); falta a reprodução (R1).

**A corrida apontada pelo responsável:**
1. a instância A não acha o carimbo no cache e começa a ler o banco, que ainda tem o valor antigo;
2. a desativação ou a troca de senha confirma a transação e invalida a entrada;
3. A termina a leitura e grava o valor antigo na memória dela e, com Redis, no segundo nível;
4. a validade de 30 s começa nessa gravação.

**Garantia temporal efetiva, sem proteção do repovoamento:**

| Situação | Até quando um token com o carimbo antigo pode passar |
|---|---|
| sem leitura em andamento na mudança, na instância que invalidou | até a invalidação, logo depois do commit |
| sem leitura em andamento, em outra instância | até a entrada local vencer: no máximo 30 s depois da mudança |
| com leitura em andamento na mudança (a corrida) | até 30 s depois da gravação tardia. A leitura pode durar até o tempo limite do comando, que o repositório não configura (o padrão do Npgsql é de 30 s), além de atrasos de agendamento: não há teto fixo |
| com Redis | a gravação tardia também vai para o segundo nível, e outras instâncias podem copiá-la para a memória. Quanto isso alonga a janela depende de como a biblioteca calcula a validade da cópia local, o que a documentação não diz. **Sétima resposta:** pelo código, a cópia herda o prazo do payload, contado do início da operação, salvo invalidação por tag durante a leitura (fatos 2, 5 e 6, abaixo) |
| renovação | nenhuma janela: ela lê o banco |

**Mecanismos avaliados, sem escolha:**

| Mecanismo | Como funciona | Fecha a gravação tardia? | Outras instâncias | Custo e limites |
|---|---|---|---|---|
| (1) geração de invalidação, com gravação condicional | um contador por usuário sobe a cada mudança de segurança; quem repovoa guarda a geração vista antes de ler o banco e só grava se ela não mudou | sim, onde o contador é compartilhado | com o contador em memória, só quando a mudança e a leitura acontecem na mesma instância; no Redis (operação atômica) ou no banco, em todas | o HybridCache não tem gravação condicional, então exige código de cache próprio; com o contador no banco, toda requisição lê o banco |
| (2) geração na chave | a chave inclui a geração atual; a gravação tardia vai para uma chave velha, que ninguém mais lê | sim | exige saber a geração atual a cada requisição, no banco ou no Redis | o mesmo de (1) |
| (3) validade contada do início da leitura | a entrada guarda o instante em que a leitura do banco começou e é descartada 30 s depois desse instante | não impede a gravação, mas a anula até 30 s depois do início da leitura, que é anterior à mudança | vale na memória e no segundo nível | exige um envoltório na leitura do cache; no segundo nível, depende de os relógios das instâncias estarem acertados. **Sétima resposta:** detalhado abaixo, nas formas (3a) e (3b) |
| (4) invalidação por tag (`RemoveByTagAsync`) | regra lógica "ignorar o que foi criado antes" | só se "criado" contar do início da leitura, o que a documentação não diz. **Sétima resposta:** pelo código, não fecha: a invalidação durante a leitura recomeça a contagem na gravação (fato 6, abaixo) | não alcança a memória das outras | a conferir por teste (R4) |
| (5) segunda invalidação atrasada | invalidar de novo depois de um intervalo maior que a leitura mais longa, isto é, o tempo limite do comando | sim, na instância que invalida e no segundo nível | não alcança a memória das outras | exige um agendamento depois do commit, que se perde se o processo cair |
| (6) carimbo lido no banco a cada requisição | sem cache para o carimbo | não há o que repovoar | todas | uma leitura pela chave por requisição |
| (7) aviso entre instâncias | uma mensagem de invalidação para todas as instâncias (por exemplo, pelo Redis) | não, sozinho | sim | o HybridCache não traz; exige infraestrutura e código próprios; combina com (1) a (3) |

**Para encerrar a análise (reprodução, que exige execução):**
- **R1:** segurar a leitura do carimbo com um gancho de teste, trocar a senha com a invalidação, liberar a leitura e medir até quando o carimbo antigo passa;
- **R2:** duas instâncias só com memória: a mudança na A e as requisições na B;
- **R3:** com Redis: a gravação tardia na A e a leitura na B pelo segundo nível;
- **R4:** invalidação por tag com uma leitura em andamento, para saber como a biblioteca conta "criado".

Cada cenário roda antes e depois do mecanismo escolhido.

#### Mecanismo 3: validade contada do início da leitura (investigação de 2026-11-01, sétima resposta)

> O responsável pediu que se investigasse primeiro o mecanismo 3 da tabela acima, no código e na documentação da versão usada do HybridCache, em cinco perguntas, e que ele fosse comparado com a geração de invalidação e a gravação condicional, com as garantias e as limitações de cada opção. Nada foi implementado e nenhuma opção foi escolhida. A CT-26 continua aberta até a decisão e a reprodução dos cenários de corrida.

**Fontes, nas versões do repositório:**
- `Microsoft.Extensions.Caching.Hybrid` 10.11.0 (`Directory.Packages.props:20`): código no commit `25f07afa` de `dotnet/extensions`, pasta `src/Libraries/Microsoft.Extensions.Caching.Hybrid/Internal/`. Os arquivos abaixo são dessa pasta; `StampedeStateT.cs` e `L2.cs` abreviam `DefaultHybridCache.StampedeStateT.cs` e `DefaultHybridCache.L2.cs`.
- Documentação XML de `Microsoft.Extensions.Caching.Abstractions` 10.0.14, onde ficam `HybridCacheEntryOptions`, `HybridCacheEntryFlags` e `SetAsync`.
- `Microsoft.Extensions.Caching.StackExchangeRedis` 10.0.14 (`Directory.Packages.props:28`): `RedisCache.cs` na tag `v10.0.14` de `dotnet/aspnetcore`.
- Repositório (`origin/main`):
  - o Redis só é registrado com a `ConnectionStrings:Cache` preenchida (`DependencyInjection.cs:55-59`), e ela está vazia no `appsettings.json:20`;
  - o `docker-compose.yml` não tem Redis;
  - não há `EnableRetryOnFailure` nem `CommandTimeout` em `src/`;
  - a verificação de conta ativa usa hoje o `GetOrCreateAsync` sem envoltório (`PermissionProvider.cs:38-42`).

**Como a biblioteca conta a validade (fatos do código):**

| # | Fato | Onde |
|---|---|---|
| 1 | Quando a chave não está na memória, a operação nasce com o instante atual como "criação". Isso acontece antes de ler o Redis e antes de chamar a função que lê o banco. O relógio é o `TimeProvider` registrado na injeção de dependência ou, sem ele, o do sistema | `StampedeStateT.cs:33-40`, `:190`, `:268`; `DefaultHybridCache.cs:78`; `DefaultHybridCache.TagInvalidation.cs:198` |
| 2 | O payload do Redis leva esse instante e a duração. Quem lê calcula o restante (criação + duração − agora) pelo próprio relógio e descarta o payload vencido | `L2.cs:227`; `HybridCachePayload.cs:18`, `:23`, `:203`, `:238-244` |
| 3 | A chave do Redis vence `Expiration` depois da gravação. Ela dura mais que o payload, que já é recusado na leitura | `L2.cs:258-280`; `RedisCache.cs:169`, `:219`, `:609` |
| 4 | Na memória da instância que leu o banco, a entrada vence `LocalCacheExpiration` depois da gravação, sem teto pelo instante de criação. Sem `LocalCacheExpiration`, vale a `Expiration` da entrada | `StampedeStateT.cs:357`, `:521`, `:524-530`; `L2.cs:179-198`; `DefaultHybridCache.cs:239-262` |
| 5 | Na memória de quem copia do Redis, a entrada vence no restante do payload, isto é, na criação + `Expiration` | `StampedeStateT.cs:237`, `:464`; `L2.cs:191-196` |
| 6 | Se houve invalidação por tag ou geral durante a leitura, a criação passa a ser o instante da gravação. O valor lido vale então depois da invalidação, na memória e no Redis | `StampedeStateT.cs:293-318` |
| 7 | A `RemoveAsync` apaga a memória local e o Redis, sem tocar na operação em andamento, que grava ao terminar | `DefaultHybridCache.cs:206-210`; `StampedeStateT.cs:357`, `:368` |
| 8 | Um acerto na memória só confere as invalidações por tag e geral. A validade é a expiração absoluta do `MemoryCache`, contada da gravação | `DefaultHybridCache.cs:156-167`, `:309-318`; `L2.cs:198` |
| 9 | Na mesma instância, as chamadas com a mesma chave e as mesmas flags se juntam à operação em andamento e recebem o resultado dela. Elas são liberadas antes da gravação no Redis. A junção não atravessa instâncias | `DefaultHybridCache.Stampede.cs:14`, `:19`, `:31-32`; `DefaultHybridCache.cs:178-200`; `StampedeStateT.cs:359` |
| 10 | A entrada aceita só durações relativas (`Expiration`, `LocalCacheExpiration`) e flags. O `SetAsync` "sets or overwrites", sem condição | XML de `Microsoft.Extensions.Caching.Abstractions` 10.0.14 |

**1. É possível estabelecer a expiração absoluta, contada do início da leitura?**

Não como opção da biblioteca. A entrada só aceita durações relativas, definidas antes da leitura, e a função que lê o banco não muda a duração nem a criação (fato 10). A biblioteca já conta do início da operação a validade do payload do Redis (fatos 1 e 2), mas há dois limites:
- a memória da instância que leu conta da gravação (fato 4);
- a invalidação por tag durante a leitura desfaz essa contagem (fato 6).

Com a API pública, sem gravação condicional, há duas formas de ter o prazo absoluto:
- **(3a) Gravação pelo tempo restante.**
  - A leitura do banco não grava no cache (flags `DisableLocalCacheWrite` e `DisableDistributedCacheWrite`, ou leitura fora do `GetOrCreateAsync`).
  - Depois, o `SetAsync` grava com `Expiration` e `LocalCacheExpiration` iguais a (início da leitura + 30 s) − agora. Se o resultado for zero ou negativo, nada é gravado.
  - O `SetAsync` grava com o instante dele e essa duração, então o payload e a memória vencem no prazo original.
- **(3b) Prazo dentro do valor.**
  - A entrada guarda o carimbo e o instante em que a leitura começou.
  - Cada uso confere esse instante + 30 s.
  - Com o prazo vencido, o valor é descartado e lido de novo no banco, por fora do cache.

**2. Uma leitura que termina depois do prazo pode gravar uma entrada válida?**
- **Biblioteca sem envoltório:**
  - na memória da instância que leu, sim: a entrada vale 30 s a partir da gravação (fato 4);
  - no Redis, não: o payload chega vencido aos leitores (fato 2);
  - a exceção é a invalidação por tag durante a leitura: aí a entrada também vale 30 s a partir da gravação no Redis (fato 6).
- **(3a):** não. Com restante zero ou negativo, não há gravação; com restante positivo, a entrada vence no prazo original nos dois níveis.
- **(3b):** a gravação acontece, mas nenhum uso aceita o valor depois do prazo.
- **Em todas as formas:** o valor lido volta para a requisição que leu e para as chamadas que se juntaram a ela, mesmo depois do prazo (fato 9). Só a (3b), conferindo o prazo no retorno da chamada ao cache, recusa esse valor.

**3. O prazo original é preservado na propagação pelo Redis?**
- **Biblioteca sem envoltório:** sim, nas cópias feitas a partir do Redis, porque o restante sai da criação gravada no payload (fatos 2 e 5). Ressalvas:
  - a criação é medida pelo relógio da instância que gravou, e o restante pelo relógio de quem lê: uma diferença de relógio desloca o prazo na mesma medida;
  - a chave do Redis dura mais que o payload (fato 3), sem efeito na validade;
  - a invalidação por tag durante a leitura troca a criação (fato 6).
- **(3a):** sim. A duração gravada é o restante contado do início da leitura, com a mesma ressalva de relógio.
- **(3b):** sim, porque o prazo é um dado do valor. A conferência usa o relógio de quem usa o valor.

**4. A validade pode ser renovada por acesso ou por outra operação?**
- **Por acesso, não:**
  - a memória só tem expiração absoluta (fato 8);
  - a cópia do Redis herda o restante (fato 5);
  - o Redis só renova a chave com expiração deslizante ("Refresh has no effect if there is just an absolute expiration", `RedisCache.cs:523`, `:551`, `:779`), e o HybridCache não a usa (`L2.cs:258-280`).
- **Por outra operação, sim:**
  - o `SetAsync` grava de novo, com o instante dele e a duração inteira (`DefaultHybridCache.cs:212-219`; `StampedeStateT.cs:39-40`);
  - uma leitura nova, depois do vencimento ou da remoção, grava uma entrada nova, com o próprio início;
  - sem envoltório, a leitura tardia (fato 4) e a invalidação por tag durante a leitura (fato 6) dão à entrada 30 s a partir da gravação.
- **Consequência para as formas:**
  - na (3a), uma gravação da mesma chave por fora do envoltório renova a validade, então todo acesso à chave precisa passar por ele;
  - na (3b), o prazo só muda com uma leitura nova do banco.

**5. Como se comporta com duas instâncias e leituras concorrentes?**
- **Sem Redis (a configuração do repositório):**
  - cada instância tem só a própria memória, e a invalidação alcança só a instância onde a mudança acontece;
  - na outra instância, uma entrada gravada antes da mudança vale até o próprio prazo, que já corria antes da mudança;
  - com uma leitura em andamento na outra instância, a entrada vale até 30 s depois da gravação tardia, sem envoltório, ou até 30 s depois do início da leitura, na (3a) e na (3b).
- **Com Redis:**
  - cada instância faz a própria leitura;
  - a gravação no Redis é a última que chega, sem condição (`RedisCache.cs:169-188`, `:219-237`), então uma gravação tardia pode trocar um valor novo por um antigo;
  - o valor antigo vence no prazo dele: início + 30 s na (3a), na (3b) e, se não houve invalidação por tag, na biblioteca sem envoltório. As instâncias que o copiam herdam esse prazo.
- **Leituras concorrentes na mesma instância:** uma requisição que chega depois da mudança pode se juntar a uma leitura iniciada antes dela. Ela recebe o carimbo antigo quando a leitura termina (fato 9), e, se a leitura passar de 30 s, isso acontece depois do prazo. Só a (3b), conferindo no retorno, recusa esse valor.
- **Relógios:** com Redis, nas três formas, o prazo entre instâncias depende de os relógios estarem acertados.

**Janela efetiva depois da mudança.** Notação:
- t1: commit da mudança;
- t0: início de uma leitura que está em andamento em t1;
- d: duração dessa leitura;
- V: validade (30 s).

O repositório não configura um teto para d. Os padrões do Npgsql (30 s de comando e 15 s de conexão) limitam etapas, e não a operação inteira.

| Forma | Memória da instância que leu | Outras instâncias, pelo Redis | Chamadas juntadas à leitura | Janela depois de t1 |
|---|---|---|---|---|
| biblioteca sem envoltório | até t0 + d + V | até t0 + V; até t0 + d + V com invalidação por tag durante a leitura | recebem o valor em t0 + d | menor que d + V, sem teto fixo |
| (3a) | até t0 + V | até t0 + V | recebem o valor em t0 + d | menor que o maior entre V e d |
| (3b), conferida no retorno | até t0 + V | até t0 + V | recusam o valor depois de t0 + V | menor que V, com os relógios acertados |

**Comparação com a geração de invalidação e a gravação condicional, sem escolha:**

| Opção | Garantias | Limitações | Estado compartilhado e custo |
|---|---|---|---|
| biblioteca sem envoltório (a forma usada hoje no `PermissionProvider`) | prazo do Redis contado do início da operação; nenhuma renovação por acesso; as cópias de outras instâncias herdam esse prazo | a memória da instância que leu conta da gravação; a invalidação por tag durante a leitura recomeça a contagem; as chamadas juntadas recebem o valor antigo; a janela não tem teto fixo | nenhum |
| (3a) gravação pelo tempo restante | nenhuma entrada vale depois de início + V, em nenhum nível; nenhuma gravação depois do prazo; nenhuma renovação por acesso | as chamadas juntadas recebem o valor antigo até o fim da leitura; toda gravação da chave precisa passar pelo envoltório; com Redis, o prazo depende dos relógios; exige código próprio em volta do cache | nenhum; a gravação passa a ser um `SetAsync` depois da leitura do banco |
| (3b) prazo dentro do valor | nenhum uso aceita um valor lido há mais de V, inclusive nas chamadas juntadas e nas cópias do Redis; a gravação tardia fica inofensiva | com Redis, o prazo depende dos relógios; o valor vencido precisa ser lido de novo por fora do cache, porque a chamada ao cache o devolveria até a expiração da biblioteca; um campo a mais no valor guardado; a janela continua de até V, e não zero | nenhum |
| (1) geração de invalidação, com gravação condicional | com o contador no mesmo armazenamento da entrada e a comparação e a gravação atômicas (por exemplo, um script no Redis), nenhuma leitura iniciada antes da mudança grava depois dela: a corrida do repovoamento fecha | o HybridCache não tem gravação condicional (fato 10), então a gravação vai direto ao Redis ou a outro armazenamento, por fora da biblioteca; sem Redis (configuração do repositório), um contador em memória só protege a instância onde a mudança acontece; com o contador no banco, sobra a janela entre a conferência e a gravação; com o contador no Redis, fora da transação da mudança, uma falha entre o commit e a subida perde a proteção daquela mudança, e sobra a validade; as cópias feitas antes da mudança valem até o próprio prazo; as chamadas juntadas recebem o valor antigo | o contador no Redis ou no banco; uma leitura dele antes de cada leitura do banco e uma gravação condicional depois; nada por requisição com o valor em cache |
| (2) geração na chave | depois da mudança, toda instância procura outra chave: entradas antigas, gravações tardias e leituras em andamento deixam de ser usadas, sem depender de relógio | exige a geração atual a cada requisição; no banco, o cache deixa de poupar a leitura; no Redis, depende de ele estar disponível e de uma regra para quando não estiver; tem a mesma falha entre o commit e a subida da geração, se ela ficar fora da transação; chaves antigas ocupam memória até vencer | o Redis ou o banco a cada requisição |
| (6) carimbo lido no banco a cada requisição, como referência | janela zero, salvo a requisição que leu antes do commit | uma leitura por chave a cada requisição autenticada; não segue a decisão da quinta resposta (cache de 30 s) | o banco a cada requisição |

**Leitura, sem escolha:** as opções se combinam.
- A (3b) limita a janela de todos os usos a V, sem estado compartilhado.
- A (1) e a (2) fecham a gravação tardia com estado compartilhado, que o repositório hoje não configura.
- O aviso entre instâncias, mecanismo (7) da tabela anterior, encurta a validade nas outras instâncias.

A escolha é do responsável.

**Reprodução que exige execução.** Complementa a R1 a R4; nada foi executado nem escrito em código.
- **R5:** biblioteca sem envoltório, com validade de 30 s. Segurar a leitura do banco além da invalidação e além de 30 s contados do início. Medir até quando a memória da instância que leu aceita o carimbo antigo e se outra instância, pelo Redis, recusa o payload depois de início + 30 s.
- **R6:** (3a), no mesmo roteiro. Conferir que nada é gravado com restante zero ou negativo e que a validade do payload e da memória é o restante.
- **R7:** chamada juntada. Uma requisição que chega depois da mudança se junta à leitura em andamento; conferir o resultado sem envoltório, na (3a) e na (3b).
- **R8:** duas instâncias com Redis e um deslocamento de relógio conhecido, pelo `TimeProvider` da biblioteca. Medir o deslocamento do prazo.
- **R4, atualizada:** pelo código, a invalidação por tag durante a leitura recomeça a contagem na gravação (fato 6). A reprodução confirma ou desmente isso.

#### Decisão pela (3b) e tarefa de validação (2026-11-01, oitava resposta)

> **Decisão do responsável:** a (3b) é o mecanismo principal contra a aceitação de carimbos antigos, condicionada à validação por execução. A invalidação imediata continua como otimização, e não como garantia principal. Neste momento, não são adotadas como solução principal a biblioteca sem envoltório, a (3a), a geração de invalidação e a geração na chave; as garantias e as limitações de cada uma ficam registradas na comparação acima. A CT-26 continua aberta até a validação por execução, e a decisão não comprova que a (3b) funciona.

**Requisitos da decisão:**
1. o valor guarda o instante em que a leitura começou;
2. a idade do valor é conferida em todo uso, inclusive quando ele vem de uma leitura compartilhada em andamento;
3. um valor que passou do prazo não valida a requisição, e uma leitura nova acontece antes da decisão de autenticação;
4. nenhum acesso renova a validade;
5. o comportamento entre instâncias e com Redis é considerado;
6. a diferença de relógio entre instâncias é documentada e controlada: a garantia de 30 s não depende de suposição não verificada sobre a sincronização dos relógios.

**Consequências para o desenho, tiradas dos fatos acima (nada implementado):**
- **Releitura (requisito 3):** uma releitura pelo `GetOrCreateAsync` pode se juntar a outra leitura antiga em andamento na mesma instância (fato 9). Para cumprir o requisito, a releitura vai direto ao banco, fora da junção, e só depois atualiza o cache.
- **Idade (requisitos 1, 2 e 4):**
  - a idade sai do instante guardado no valor, e não da expiração da biblioteca, que conta da gravação (fato 4), então nem os acessos nem uma gravação tardia renovam o prazo;
  - a conferência acontece depois que a chamada ao cache devolve o valor, então vale também para as chamadas juntadas.
- **Relógio (requisitos 5 e 6):** um instante só se compara com o relógio que o mediu. Há três formas de cumprir o requisito 6; a escolha é do responsável.

| Forma | Como | Garantia | Custo e limites |
|---|---|---|---|
| (i) carimbo só na memória de cada instância | a chave do carimbo não usa o segundo nível (flag `DisableDistributedCache`), e a idade é medida pelo relógio monotônico da própria instância (`TimeProvider.GetTimestamp`) | a janela não depende do relógio de outra instância nem da hora do sistema | uma leitura do banco por usuário e por instância a cada 30 s; a invalidação imediata só alcança a instância da mudança |
| (ii) Redis, com a hora do banco | a consulta do carimbo devolve também a hora do PostgreSQL (`clock_timestamp()`); a cada leitura, a instância mede a diferença entre o próprio relógio e essa hora, com o erro limitado pela ida e volta da consulta; acima de um limite configurado de diferença ou de erro, ela descarta os valores vindos do Redis e lê o banco | a idade é medida no relógio do banco, com o erro conhecido | uma coluna a mais na consulta, o registro da diferença por instância e a regra do limite |
| (iii) Redis, com tolerância declarada | o instante é a hora UTC de quem leu, e a validade efetiva é 30 s menos a tolerância; o desvio de cada instância é medido periodicamente contra a hora do banco, e, acima da tolerância, a instância descarta os valores vindos do Redis | 30 s enquanto o desvio medido ficar dentro da tolerância | a medição periódica, a tolerância como configuração e a regra de descarte |

**Recomendação para o relógio:** a (i). O repositório não configura Redis (`appsettings.json:20` e `docker-compose.yml`), e ela é a única forma sem dependência de relógio entre instâncias. A (ii) ou a (iii) só entram se o Redis for adotado para o carimbo.

##### Tarefa de validação da CT-26 (proposta; nenhum arquivo criado e nada executado)

**Identificação e escopo:**
- **Identificação:** tarefa isolada de testes da CT-26. O identificador definitivo (por exemplo, T001) vem da spec que a hospedar (ver o conflito abaixo).
- **Escopo:** só testes.
  - Fica de fora qualquer mudança em código de produção, autenticação, cache, infraestrutura, migrations, contratos, CI e configuração de pacotes, e qualquer refatoração.
  - A R6 sai do escopo, porque a (3a) não foi adotada.

**Conflito com o fluxo SDD (decisão do responsável antes de qualquer arquivo):**
- **Portão SDD:** o portão só permite escrever em `tests/` dentro de um `/spec-implement` de uma spec `Em implementação` com `aprovada-em`; a 049 continua `Em descoberta`.
- **Branch:** a pasta de trabalho está na branch da 035, com alterações de outra sessão, e a troca de branch não está autorizada.
- **Caminho (A), spec derivada só de testes (recomendado; segue a DEC-17 e o `CLAUDE.md`):**
  - uma spec nova, no próximo número livre (hoje, 050, a confirmar);
  - branch `test/SPEC-050-ct26-stamp-cache-validation` a partir da `origin/main` atualizada, numa worktree separada, para não tocar na pasta da 035;
  - aprovação própria, `/spec-implement` e PR para a `main`;
  - exige autorizar a pasta da spec nova, a branch e a worktree.
- **Caminho (B), experimento fora do repositório:**
  - um projeto de teste avulso na pasta de rascunho, só com a parte 1;
  - os resultados viram evidência na 049, e nada é versionado;
  - as partes 2 e 3 ficam de fora, porque exigiriam uma cópia limpa da `origin/main`.

**Parte 1: biblioteca (HybridCache 10.11.0 e Redis 10.0.14, sem a API).**
- **Instâncias:** cada instância é um contêiner de serviços montado no teste, com `AddHybridCache` e, nas variantes com Redis, `AddStackExchangeRedisCache`.
- **Validade:** V é de 3 s nos testes.
- **Leitura:** a função de leitura fica presa num portão que o teste abre e fecha.

| Cenário | Fato ou reprodução | Previsto (aprovado se observado) |
|---|---|---|
| B1 | fato 4 (R5) | com a leitura presa além de V e uma remoção no meio, a instância que leu devolve o valor antigo, sem nova leitura, até gravação + V − 0,5 s, e lê de novo depois de gravação + V + 0,5 s |
| B2 | fatos 2 e 5 (R5 e R3) | com Redis e a leitura maior que V, a outra instância não aceita o payload; com a leitura menor que V, ela aceita até início + V e não depois de início + V + 0,5 s |
| B3 | fato 7 | depois da remoção e da liberação da leitura, a memória e o Redis guardam o valor antigo |
| B4 | fato 9 (R7) | uma chamada feita depois da remoção, durante a leitura, recebe o valor antigo, e a função de leitura dela não é chamada |
| B5 | fato 6 (R4) | com invalidação por tag durante a leitura, o valor antigo é devolvido depois da invalidação até gravação + V |
| B6 | fato 8 | com acessos a cada 0,5 s, a releitura acontece em gravação + V ± 0,5 s, e o tempo de vida da chave no Redis não aumenta |
| B7 | R8 | com o relógio da instância A adiantado em X s (relógio do teste), o prazo visto pela instância B pelo Redis se desloca em X ± 0,5 s |

**Parte 2: reprodução no código atual.**
- **Caminho reproduzido:** hoje não há conferência de carimbo por requisição, e a troca de senha não invalida o cache. A mesma corrida só existe na verificação de conta ativa: `PermissionProvider.IsActiveAsync` usa `GetOrCreateAsync` (`PermissionProvider.cs:38-42`), e a desativação invalida a entrada (`DeactivateUserCommandHandler.cs:76`).
- **Hosts:** são derivados do `IntegrationTestWebAppFactory` pelo `WithWebHostBuilder`, como em `SlaMonitorBackgroundServiceTests.cs:176`, e compartilham o PostgreSQL da coleção.
- **Ajustes só no host de teste:**
  - as validades padrão do cache caem para 6 s no total e 3 s na memória, por `Configure<HybridCacheOptions>`, que vale depois do `AddHybridCache` (`HybridCacheServiceExtensions.cs:24-29`);
  - a consulta de conta ativa fica presa depois de executada por um interceptador do EF Core, registrado por `ConfigureDbContext`, que existe no EF Core 10.0.14 (`EntityFrameworkServiceCollectionExtensions.cs:1059`).
- **Rota:** `GET tickets/mine`, que exige conta ativa.

| Cenário | Montagem | Previsto (aprovado se observado) |
|---|---|---|
| R1 | uma instância | com a leitura presa, a desativação é confirmada e invalida a entrada; depois da liberação, as requisições com o token antigo passam até gravação + 3 s e são recusadas depois de gravação + 3,5 s |
| R2 | duas instâncias sem Redis | depois da desativação na A, a B aceita até a própria entrada vencer (gravação na B + 3 s) e recusa depois de + 0,5 s; a A recusa na requisição seguinte |
| R3 | duas instâncias com Redis | com a gravação tardia na A, a B aceita pelo Redis só até o início da leitura na A + 6 s, e a A aceita pela memória até gravação + 3 s |
| R7 | uma instância, chamada juntada | uma requisição feita depois da invalidação, durante a leitura presa, é aceita |

**Parte 3: aceitação da (3b).** Ela precisa de uma implementação da (3b), que esta tarefa não autoriza. Há duas formas, à escolha do responsável:
- **(a) protótipo da (3b) no projeto de teste:** valida o desenho contra a biblioteca e o Redis reais, mas não o código que vai para produção;
- **(b) só na tarefa que implementar a (3b):** a conferência por requisição e a parte de login da U2 rodam com os mesmos critérios.

A CT-26 só fecha quando a implementação de produção passar nestes critérios:

| Critério | Aprovado | Reprovado |
|---|---|---|
| C1 janela | nenhuma requisição com o carimbo antigo é aceita depois de t0 + V + 0,5 s (t0: início da leitura mais antiga em andamento na mudança): em uma instância, em duas sem Redis e, se a forma de relógio usar Redis, em duas com Redis | qualquer aceitação depois desse limite |
| C2 leitura compartilhada | um valor recebido de uma leitura compartilhada iniciada há mais de V não decide a requisição: o contador de comandos mostra uma leitura nova antes da resposta | o valor vencido decide a requisição |
| C3 sem renovação | com acessos a cada 0,5 s, a releitura acontece em leitura + V ± 0,5 s | o prazo se estende com os acessos |
| C4 relógio | na forma escolhida, com um deslocamento de teste entre instâncias (por exemplo, ±5 s), a janela não passa de V + 0,5 s; na (i), o deslocamento não muda a janela | a janela passa de V + 0,5 s |
| C5 invalidação imediata | na instância da mudança, a requisição seguinte à invalidação é recusada (é uma otimização; a garantia continua sendo a C1) | ela é aceita |
| C6 sem regressão | a suíte inteira passa | algum teste existente falha |

**Regras de aprovação:**
- **Repetições:** cada cenário roda 10 vezes e só é aprovado com 10 de 10; uma falha ou um tempo esgotado reprova o cenário.
- **Partes 1 e 2:** "aprovado" quer dizer que o comportamento observado é o previsto. Se ele divergir, os fatos acima são revistos antes de qualquer implementação.
- **Parte 2:** os cenários mostram o defeito de hoje. Eles viram evidência e não ficam como testes de regressão; na implementação, a C1 a C6 os substituem.

**Arquivos (no caminho A; nenhum criado):**
- `tests/TickestPristine.IntegrationTests/Caching/RedisContainerFixture.cs`: contêiner Redis de teste (`redis:7.4`) criado pelo construtor genérico do Testcontainers, que já vem com o `Testcontainers.PostgreSql` 4.15.0, sem pacote novo;
- `tests/TickestPristine.IntegrationTests/Caching/HybridCacheBehaviorTests.cs`: parte 1 (B1 a B7);
- `tests/TickestPristine.IntegrationTests/Abstractions/EfCommandGate.cs`: interceptador que prende um comando, escolhido pelo texto, até o teste liberar, registrado só nos hosts de teste (no mesmo espírito do `EfCommandCounter.cs`);
- `tests/TickestPristine.IntegrationTests/Users/ActiveUserCacheRaceTests.cs`: parte 2 (R1, R2, R3 e R7);
- só com o protótipo autorizado: `tests/TickestPristine.IntegrationTests/Caching/StampAgePrototype.cs` e `Caching/StampAgePrototypeTests.cs` (parte 3).

Ficam sem mudança o `IntegrationTestWebAppFactory.cs`, o `.csproj` de testes, o `Directory.Packages.props`, o CI, `src/` e as migrations.

**Dependências e infraestrutura:**
- **Docker:** já é exigido pelos testes de integração e está disponível no CI, que roda o `dotnet test` em `ubuntu-latest` (`.github/workflows/build.yml:51`). O CI baixaria a imagem do Redis sem mudança no workflow.
- **Duas instâncias:** dois hosts no mesmo processo de teste, cada um com a própria memória, compartilhando o PostgreSQL e, nas variantes com Redis, o mesmo Redis.
- **Pacotes:** nenhum novo. O `Microsoft.Extensions.Caching.Hybrid` e o `Microsoft.Extensions.Caching.StackExchangeRedis` chegam pela referência à API. O módulo `Testcontainers.Redis` seria um pacote novo, isto é, mudança de configuração, que depende de autorização.
- **Relógio:**
  - na parte 1, cada contêiner de serviços recebe um `TimeProvider` próprio;
  - nos hosts da API, o `AddHybridCache` registra o `TimeProvider.System` na injeção de dependência (`HybridCacheServiceExtensions.cs:120`). Trocá-lo pode alcançar outros componentes que usam o mesmo registro, o que precisaria ser conferido antes.
- **Tempo:** cerca de 6 s por execução de cenário. Com 10 repetições, a validação leva uns 12 minutos; como teste de regressão, seria uma execução por cenário.

#### Validação pela SPEC-050 e relógio (i) (2026-11-01, nona resposta)

> **Decisões do responsável:**
> - **Caminho (A):** a SPEC-050, derivada e só de testes, em branch e worktree independentes, a partir da `origin/main`, sem trocar a branch da sessão e sem tocar na 035.
> - **Protótipo da (3b):** entra agora, só no projeto de testes, depois da aprovação formal da 050. Ele valida o instante monotônico, a conferência da idade em todo uso e a requisição que recebe o resultado de uma leitura compartilhada.
> - **Relógio (i):** medição monotônica local por instância, com a condição de que o valor não passe entre processos pelo Redis.
> - **Redis de teste:** contêiner genérico, sem pacote novo e sem mudar `.csproj`, `Directory.Packages.props` e CI.
> - **Escopo da 050:** B1 a B7, R1, R2, R3 e R7, o protótipo e a C1 a C6. A R6 fica fora, e as evidências do comportamento atual ficam separadas dos testes permanentes.
>
> A 049 continua `Em descoberta`, e a CT-26 continua aberta até a validação por execução.

**As cinco condições do relógio (i) no HybridCache 10.11.0 (sem implementação):** a API pública permite cumprir as cinco, sem depender da configuração do repositório. O desenho e os fatos de código estão na seção 1.1 da SPEC-050.
1. **Cache distribuído:**
   - o protótipo usa uma instância própria do HybridCache (`AddKeyedHybridCache`), cujo `DistributedCacheServiceKey` aponta para um `MemoryDistributedCache` registrado com chave;
   - a biblioteca descarta esse segundo nível e liga a flag fixa `DisableDistributedCache` (`DefaultHybridCache.cs:82-93` e `:115-120`);
   - toda chamada também passa a flag.
2. **Instantes do próprio processo:** o valor leva o instante monotônico e o identificador da instância. Um valor de outra instância é tratado como vencido.
3. **Leitura vencida:** o protótipo lê direto no banco e grava o valor novo pelo `SetAsync`.
4. **Sem junção:**
   - o `GetOrCreateAsync` só consulta a memória, com `DisableUnderlyingData`, que não chama função de leitura nem guarda resultado (`StampedeStateT.cs:257`, `:407-410` e `:428-436`);
   - as leituras do banco ficam fora do cache.
5. **Idade antes da decisão:** a conferência da instância e da idade vem antes da comparação com o carimbo do token.

**Limites do desenho, para decisão na aprovação da 050 (DEC-5, DEC-6 e DEC-7 dela):**
- a gravação não é condicional;
- a memória é compartilhada com a instância padrão do HybridCache;
- faltas simultâneas leem o banco cada uma.

**SPEC-050, preparada e à espera de aprovação formal:**
- **Onde está:** `specs/050-validacao-ct26-carimbo-cache/spec.md`, na worktree `C:/dev/tickest-pristine-spec050`.
- **Branch:** `test/SPEC-050-ct26-stamp-cache-validation`, criada da `origin/main` em `2804b07`.
- **Situação:** `Em descoberta`. Nenhum arquivo de teste foi criado, e nada foi executado.

**Atualização de 2026-11-02 (decisões na SPEC-050):**
- **HybridCache fora do protótipo:** a forma descrita acima, com uma instância nomeada do HybridCache, foi rejeitada, porque essa instância usa o mesmo `IMemoryCache` da aplicação (`HybridCacheServiceExtensions.cs:100-106`; `DefaultHybridCache.cs:75`). O responsável decidiu abandonar o HybridCache como armazenamento do carimbo no protótipo, sem classes internas, reflexão ou APIs não públicas.
- **Armazenamento (DEC-5 da 050):** cada instância tem um `ConcurrentDictionary` privado e exclusivo, mais `Guid` e `TimeProvider` próprios; nada de HybridCache, `IMemoryCache` ou Redis.
- **Leitura que passa de V (DEC-11 da 050):** é recusada antes de ser gravada ou usada, sem nova tentativa automática.
- **Gravação (DEC-7 da 050):** continua não condicional, sem `AddOrUpdate`; a idade é sempre contada do início da leitura que produziu o carimbo.
- **Testes (DEC-8 da 050):** os permanentes do protótipo usam relógio controlado, com fronteiras exatas; a tolerância de 0,5 s fica só nas evidências de tempo real.
- **Situação:** a SPEC-050 continua `Em descoberta`, à espera de aprovação formal, e a CT-26 continua aberta até a validação por execução.

#### Histórico da conciliação na tabela da 2.8.4 (movido do `spec.md` em 2026-11-04)

> Movido da coluna "Conciliação" da CT-26, na 2.8.4 do `spec.md`, em 2026-11-04, por autorização do responsável, sem mudança de conteúdo. No `spec.md` ficou a situação atual, com referência a esta seção.

**Em aberto, para o responsável.** A corrida já existe hoje, porque não há trava; a trava no login, da DEC-22 original, a fecharia na G4. Natureza: hipótese, a reproduzir por teste de corrida antes de qualquer correção (REQ-28). Alternativas:<br>(1) aceitar e registrar o risco residual: a sessão da conta inativa não serve enquanto ela estiver inativa e volta a valer só com a reativação dentro de 30 dias;<br>(2) a reativação revoga as sessões que restarem. Fecha o caso sem trava no login e muda a RN-19 só para sessões criadas antes da desativação;<br>(3) trava compartilhada da linha do usuário só na criação da sessão do login bem-sucedido, o que contraria o "sem trava" da CT-20 (a).<br>Recomendação: (2), porque fecha o caso sem trava no login e sem efeito para o usuário legítimo.<br>**Orientação de 2026-11-01 (terceira resposta):** a revogação na reativação não basta. A CT-26 fica aberta até: reproduzir a corrida entre login e desativação; registrar a ordem exata de emissão, desativação e reativação; demonstrar que a solução invalida toda sessão emitida indevidamente; testar a emissão que termina antes e depois da desativação. Nenhuma trava no login que contrarie a CT-20 sem demonstrar que é necessária, e a solução deve ter o menor impacto sobre a política de concorrência. **Análise:** a recomendação (2) não garante isso. Um login que lê a conta ativa antes da desativação e grava a sessão depois da reativação passa pela revogação, e o access token emitido na corrida não é alcançado por ela. A ordem das operações, os casos e a alternativa sem trava estão no anexo C (2.10.9), movidos da 2.8.4 em 2026-11-01. **Situação:** aberta, até as validações.<br>**Orientação de 2026-11-01 (quarta resposta):** manter aberta, sem solução aprovada. O desenho por carimbo nos sete pontos pedidos, o impacto e a comparação com a trava estão no anexo C (2.10.9). Em resumo, só o carimbo impede que um token emitido antes da desativação valha depois dela; a trava não alcança o access token e reabriria a CT-20. **Situação:** aberta. **Decisão de 2026-11-01 (quinta resposta):** o carimbo, com cache de validade curta: 30 s no início, configurável. Cada requisição autenticada lê o carimbo no cache; vencido ou ausente, ele é buscado no banco e o cache é atualizado. As mudanças de segurança tentam invalidar a entrada na hora, e a validade protege quando a invalidação não alcança todas as instâncias. A renovação aplica a mesma validação. A janela residual de revogação, de no máximo 30 s, está no anexo C (2.10.9). **Situação:** aberta até a reprodução da corrida e a validação dos cenários; esta decisão não substitui essa verificação. **Orientação de 2026-11-01 (sexta resposta):** investigar o repovoamento do cache depois da invalidação, o cache e a invalidação entre instâncias e a garantia temporal efetiva, avaliando mecanismos como geração ou versão de invalidação, sem escolher de antemão. A investigação está no anexo C (2.10.9) e corrige o teto registrado na quinta resposta: com uma leitura em andamento na hora da mudança, a janela passa de 30 s. **Situação:** aberta até a análise ser avaliada e as corridas reproduzidas. **Orientação de 2026-11-01 (sétima resposta):** investigar primeiro o mecanismo 3 (validade contada do início da leitura) no código e na documentação da versão usada do HybridCache (10.11.0), em cinco perguntas, e compará-lo com a geração de invalidação e a gravação condicional, com as garantias e as limitações de cada opção, sem implementar e sem escolher. As respostas e a comparação estão no anexo C (2.10.9). **Situação:** aberta até a decisão e a reprodução dos cenários de corrida. **Decisão de 2026-11-01 (oitava resposta):** a opção (3b) é o mecanismo principal contra a aceitação de carimbos antigos, condicionada à validação por execução. O valor guarda o instante em que a leitura começou; a idade é conferida em todo uso, inclusive no valor de uma leitura compartilhada em andamento; um valor vencido não valida a requisição, e uma leitura nova acontece antes da decisão de autenticação; nenhum acesso renova a validade; o comportamento entre instâncias e com Redis é considerado; a diferença de relógio entre instâncias é documentada e controlada, sem depender de sincronização não verificada. A invalidação imediata fica como otimização, e não como garantia. Não são adotadas como solução principal, neste momento, a biblioteca sem envoltório, a (3a), a geração de invalidação e a geração na chave (limitações no anexo C, 2.10.9). **Situação:** aberta até a validação por execução; a decisão não comprova que a (3b) funciona. A tarefa de testes está proposta no anexo C (2.10.9), à espera de autorização. **Decisão de 2026-11-01 (nona resposta):** a validação vai para a SPEC-050, derivada e só de testes, em branch e worktree próprias (`test/SPEC-050-ct26-stamp-cache-validation`, a partir da `origin/main`), com o relógio (i), sem o valor passar pelo Redis, o protótipo da (3b) só no projeto de testes depois da aprovação da 050 e o Redis de teste por contêiner genérico (anexo C, 2.10.9). **Situação:** aberta até a validação por execução. **Decisão de 2026-11-02 (SPEC-050):** o protótipo da (3b) não usa HybridCache, `IMemoryCache` nem Redis: cada instância tem `ConcurrentDictionary` privado, `Guid` e `TimeProvider` próprios; leitura nova com idade igual ou maior que V é recusada antes de gravar ou usar, sem nova tentativa; a gravação continua não condicional; os testes permanentes usam relógio controlado (anexo C, 2.10.9). **Situação:** aberta até a validação por execução.

#### Validação aceita e dependência da implementação (2026-11-04, décima primeira resposta)

- **Decisão do responsável:** "Aceito a validação técnica da SPEC-050 como evidência de que o mecanismo 3b está validado. A implementação de produção poderá ser entregue em uma spec futura, com dependência explicitamente registrada. Não marque a implementação de produção como concluída nem apresente a CT-26 como integralmente implementada."
- **Resultado da SPEC-050** (a referência prevista na DEC-14 dela, registrada por esta decisão): "Validação por testes na SPEC-050 (branch `test/SPEC-050-ct26-stamp-cache-validation`): as 11 evidências conforme a análise, nas 10 execuções. No B7, com o relógio de quem lê adiantado em 2 s, o prazo percebido por ele se antecipa cerca de 2 s. Com o relógio de quem grava adiantado, o deslocamento fica escondido, porque a cópia em memória e a chave no Redis valem V a partir da gravação. O protótipo da (3b), com dicionário privado e relógio local, passou em todos os critérios, 10 de 10, inclusive na recusa da leitura nova com idade negativa, comprovada por injeção de falha."
- **Dependência registrada:** a implementação de produção da (3b), com a conferência por requisição e o carimbo no login e na renovação (U2 e T-105, na G3-b), fica para uma spec futura. A parte de login da U2 depende da validação da (3b), e não mais do encerramento da CT-26, que se encerra com essa implementação (7.15, 7.15.1 e 7.15.4 do `spec.md`).
- **Situação:** mecanismo validado; implementação de produção pendente; CT-26 aberta.
- **Aprovação da 049 (décima segunda resposta, 2026-11-04):** o responsável autorizou retirar a CT-26 da lista de conciliações que seguram a aprovação formal, com a condição de validação técnica satisfeita pela SPEC-050. A implementação de produção continua pendente, vinculada a uma spec futura, e não está concluída.

## 2.11 Organização (departamentos e setores) e auditoria

Esta seção cobre os agregados `Department` e `Sector` (no domínio rico desde a spec 038), os slices e as rotas deles, o seed da estrutura inicial e a auditoria técnica: o marcador `IAuditable`, o `AuditLoggingInterceptor`, o `AuditLogEntry`, a tabela `audit_logs` e a rota `GET audit-logs/{entityName}/{entityId}`. O setor do usuário e o histórico do chamado entram só onde tocam esta área (eles são das seções 2.9 e 2.1 a 2.7).

- **Linha de base:** o commit `bc2395c`, lido por `git show`. Toda referência `arquivo:linha` aponta para ele, salvo as marcadas com `@e1985a2` (branch da 022) ou `@4ffd79e` (branch da 023).
- **Tamanho:**
  - Domain: 11 arquivos (5 de departamento, 5 de setor e 1 de auditoria), mais o `IAuditable` do `SharedKernel`.
  - Application: 33 arquivos (15 de departamento, 15 de setor e 3 de auditoria).
  - Web.Api: 14 arquivos, com 11 rotas e 3 classes de permissão.
  - Infrastructure: 5 arquivos (duas configurações da organização, a configuração e o interceptor da auditoria e o seed) e 6 migrations.
  - Testes: 15 arquivos unitários (61 métodos), 3 de integração dedicados (39 métodos) e asserções de auditoria em outros 3 arquivos de integração.
- **Método:** leitura direta do código, das skills obrigatórias, do `CLAUDE.md`, do `SYSTEM_SPECIFICATION.md` e das specs 022, 023, 033, 038 e 043. As branches foram comparadas por `git diff` e `git merge-tree --trivial-merge`, que só lê e imprime. Sem build nem testes.

### 2.11.2 Invariantes e fronteiras

| # | Invariante | Exige consistência imediata? | Protegido hoje por | Proteção-alvo |
|---|---|---|---|---|
| I-OR-01 | Nome do departamento único entre os ativos, sem diferenciar maiúsculas (RN-11) | sim | pré-conferência no handler (`CreateDepartmentCommandHandler.cs:15`, `UpdateDepartmentCommandHandler.cs:23-28`) e índice único filtrado `ix_departments_name` em `citext` (`DepartmentConfiguration.cs:15,21`); na corrida, 409 genérico (`GlobalExceptionHandler.cs:35-46`) | igual; espaço nas pontas recusado [DEC-44] |
| I-OR-02 | Nome do setor único no departamento, entre os ativos (RN-11) | sim | `CreateSectorCommandHandler.cs:24-29`, `UpdateSectorCommandHandler.cs:21-26` e `ix_sectors_department_id_name` (`SectorConfiguration.cs:21`) | igual |
| I-OR-03 | Nome com até 100 caracteres; descrição com até 500 | sim | `ck_departments_name_length`, `ck_sectors_name_length` e `varchar(500)` (`DepartmentConfiguration.cs:18,23`; `SectorConfiguration.cs:18,23`) | igual |
| I-OR-04 | Setor ativo só em departamento ativo | sim | criação só em departamento ativo (`CreateSectorCommandHandler.cs:15-22`) e cascata na desativação (`DeactivateDepartmentCommandHandler.cs:38-48`); nada sob concorrência | igual, mais a trava da 043p2 (DEC-41; S-OR-3 e S-OR-4) |
| I-OR-05 | Desativar o departamento desativa os setores ativos dele, na mesma transação (RN-10) | sim | laço no handler e um `SaveChangesAsync` (`DeactivateDepartmentCommandHandler.cs:38-50`) | igual (DEC-40 (a)) |
| I-OR-06 | Departamento ou setor só é desativado sem chamado ativo nos setores dele (RN-10) | sim, no instante da desativação | consulta com `WhereActive` no handler (`DeactivateDepartmentCommandHandler.cs:29-36`; `DeactivateSectorCommandHandler.cs:27-32`), sem trava | conferência depois da trava da linha (DEC-41), com a condição de "ativo" da 043 (REQ-301) |
| I-OR-07 | Setor desativado não tem chamado ativo (o que a RN-10 sugere depois da desativação) | depende da DEC-42 | nada: a reabertura não confere o setor (teste `Deactivate_Should_ReturnConflictCountedByListing_WhenTicketIsReopenedInDeactivatedSector`, `DepartmentsTests.cs:492-514` @e1985a2) | reabertura recusada [DEC-42] e trava da DEC-41 |
| I-OR-08 | Chamado novo só em setor ativo (RN-05) | sim | `CreateTicketCommandHandler.cs:52-59`, sem trava; o departamento não é conferido | `FOR SHARE` na linha do setor (S-OR-6) |
| I-OR-09 | Usuário só é vinculado a setor ativo; desativar o setor não desfaz o vínculo (RN-29) | só na troca | `ChangeUserSectorCommandHandler.cs:44-52` | igual (seção 2.9) |
| I-OR-10 | Responsável do departamento, quando informado, existe (C-07) | sim | carga no handler (`CreateDepartmentCommandHandler.cs:20-32`; `UpdateDepartmentCommandHandler.cs:30-42`) e FK com `SetNull` (`DepartmentConfiguration.cs:25-28`); "ativo" não é exigido | igual; "ativo" na Q-41 |
| I-OR-11 | A desativação não se repete (409 `AlreadyDeactivated`) | sim | handler antes do `Deactivate()` `void` (`DeactivateDepartmentCommandHandler.cs:24-27`; `DeactivateSectorCommandHandler.cs:22-25`) | igual (padrão 8) |
| I-AU-01 | Toda inclusão, alteração ou exclusão de entidade `IAuditable` gera um registro na mesma transação | sim | `AuditLoggingInterceptor.cs:26-51` (só o `SavingChangesAsync`); não cobre SQL direto (`TicketActivityWriter.cs:12,29`) nem migrations | `IAuditLog` chamado pelo command, na transação dele [DEC-45], e teste de cobertura (REQ-308) |
| I-AU-02 | Segredos (`PasswordHash`, `SecurityStamp`, `TokenHash`) não entram nos valores | sim | lista global por nome de propriedade (`AuditLoggingInterceptor.cs:19-24,92-95`) | lista por tipo e teste de nomes de segredo [DEC-46] |
| I-AU-03 | Um save que falha não deixa registros para a tentativa seguinte | sim | nada no interceptor; um handler desanexa à mão (`PostTicketMessageCommandHandler.cs:77-83`) | REQ-16 da 049 no interceptor; na 043p2, o rollback da transação |
| I-AU-04 | O registro só é incluído (a aplicação nunca o altera nem apaga) e sobrevive ao usuário | não (registro) | nenhum caminho de escrita além do interceptor (busca por `AuditLogs` e `AuditLogEntry` em `src`); sem FK para `users` (`AuditLogEntryConfiguration.cs:18`); nada no banco | igual; expurgo só pela política de retenção (Q-42) |
| I-AU-05 | Ler a trilha exige `audit:read` | sim | `GetHistory.cs:29` | igual |
| I-AU-06 | A trilha de uma entidade continua legível depois de mudanças de código (`entity_name` e chaves do JSON estáveis) | não | implícito: nome do tipo e das propriedades CLR (`AuditLoggingInterceptor.cs:58,92-95`) | teste dos nomes (REQ-311) e decisão explícita em cada renome |

**Fronteira entre departamento e setor** (a decisão está na DEC-40):

| Opção | Protege I-OR-04 e I-OR-05 no modelo? | Referências externas | Rotas e permissões | Concorrência | Custo |
|---|---|---|---|---|---|
| (a) duas raízes, com o handler coordenando a cascata e o departamento ativo (hoje; DEC-2 da 038) | não: dependem dos handlers | chamado e usuário guardam o id do setor, como hoje | sem mudança | trava de linha na 043p2 (DEC-41) | nenhum agora |
| (b) setor como entidade interna do `Department` | sim | `Ticket.Create(Sector, …)` e a troca de setor do usuário passariam a carregar o departamento para chegar ao setor | `sectors/{id}` carrega o departamento; `sectors:manage` continua | exige versão da raiz incrementada a cada mudança de setor, porque o EF não grava a linha do departamento quando só um setor muda; a skill não cobre isso | alto |
| (c) setor como raiz própria criada por `Department.AddSector` (padrão 7) | só a criação; a cascata continua no handler | como hoje | sem mudança | igual a (a) | baixo, com ganho pequeno: o `Sector.Create` já exige o `Department` carregado (`Sector.cs:22-31`) |

**Conclusão.** A opção (a) é a recomendada:
- **Identidade e uso:** o setor tem identidade referenciada por chamados (`TicketConfiguration.cs:80-83`) e por usuários (`UserConfiguration.cs:42-45`), rotas e permissão próprias e ciclo de vida independente.
- **Poucas regras entre os dois:** a cascata e o "departamento ativo" ficam num handler cada, e a corrida é fechada pela trava da DEC-41.
- **Contraste com os cronômetros de SLA:** a DEC-1 (b) da 049 os põe dentro do `Ticket`, porque eles não têm referência externa nem rota, e a linha do chamado já é gravada em toda operação que os toca. Com o setor é o contrário.

**Acoplamento entre organização, chamados e usuários:**

| De → para | Ponto de acoplamento | Evidência | Avaliação |
|---|---|---|---|
| Organização → chamados | "chamado ativo" na desativação; na 022, também na listagem | `DeactivateDepartmentCommandHandler.cs:3,29-33`; `DeactivateSectorCommandHandler.cs:3,27-29`; `TicketSlaFilters.cs:14-15` | regra entre agregados legítima, na forma errada: extensão estática do módulo de chamados (OR-03) |
| Chamados → organização | abertura só em setor ativo; `Ticket.Create(Sector, …)`; leituras com o nome do setor | `CreateTicketCommandHandler.cs:52-59,73-83`; `GetTicketsQueryHandler.cs:58`; `GetTicketByIdQueryHandler.cs:63`; `GetOwnTicketsQueryHandler.cs:62` | legítimo; a reabertura não confere o setor (OR-02) |
| Usuários → organização | `User.SectorId` com FK `Restrict`; troca só para setor ativo; leituras com setor, departamento e `sectorIsActive`; atendentes por setor | `ChangeUserSectorCommandHandler.cs:44-52`; `UserConfiguration.cs:42-47`; `GetUsersQueryHandler.cs:46-54`; `GetUserByIdQueryHandler.cs:45-53`; `GetBacklogAttendantsQueryHandler.cs:57-59`; `GetTicketAssigneesQueryHandler.cs:26` | legítimo; usuário em setor desativado é permitido (RN-29) |
| Organização → usuários | responsável do departamento, com FK `SetNull` e sem regra | `DepartmentConfiguration.cs:25-28`; `CreateDepartmentCommandHandler.cs:20-32` | informativo (OR-09, Q-41) |
| Auditoria → todos | interceptor sobre os 13 tipos `IAuditable` | `AuditLoggingInterceptor.cs:45`; busca por `IAuditable` em `src` | técnico, preso ao EF (AU-01) |
| Chamados → auditoria | o handler de mensagem conhece o `AuditLogEntry` | `PostTicketMessageCommandHandler.cs:77-83` | F-24 e REQ-16 da 049 |

**Auditoria técnica × histórico do chamado.** São duas trilhas com públicos diferentes, e a sobreposição é intencional:
- `ticket_histories` é funcional: criada pelo agregado (padrão 7), com ações de negócio, e mostrada a quem vê o chamado.
- `audit_logs` é técnica: uma linha por linha alterada, com valores antes e depois, só para quem tem `audit:read`.

As duas são gravadas na mesma transação da mudança. Ficam fora da auditoria o `TicketHistory` (já é trilha), o `RefreshToken`, o `Role` e a `Permission`. O `SYSTEM_SPECIFICATION.md` chama essa escolha de "não documentada" (`SYSTEM_SPECIFICATION.md:254`); o REQ-313 a documenta.

### 2.11.4 Mudanças pendentes nas branches 022 e 023

**Estado em 2026-11-01, no fim desta investigação:** o PR #60 (022) foi mergeado às 20:09 UTC (`05e4f0f`). A branch da 023 incorporou a `main` com a 022 (`d266b07`) e foi verificada de novo (`f3a16e1`). Depois, o PR #52 também foi mergeado (20:58 UTC, `e2eb2f6`). A análise abaixo foi feita nos topos de então (`e1985a2` e `4ffd79e`), e a atualização da 023 está no fim desta subseção. Nenhuma das duas specs mexe no domínio, nos commands, na auditoria ou nas migrations: as duas acrescentam campos à leitura `GET departments`.

**022 — `feat/SPEC-022-active-ticket-count-departments` (`e1985a2`, PR #60).**
- **Base:** `ef1c981`, a `main` com as specs 038, 039, 041 e 042, sem a 045.
- **Código:**
  - `DepartmentResponse` e `DepartmentSectorResponse` ganham `ActiveTicketCount` (`GetAll/DepartmentResponse.cs:9,18` @e1985a2).
  - O `GetDepartmentsQueryHandler` conta os chamados por `context.Tickets.WhereActive()`, dentro da projeção única e com o alcance da recusa da desativação (`GetDepartmentsQueryHandler.cs:15-18,32-33,42` @e1985a2):
    - no departamento, os chamados de todos os setores dele, inclusive os desativados (DEC-8 da 022);
    - no setor, os chamados dele.
- **Testes:** 8 novos (`git diff ef1c981...e1985a2 -- tests`):
  - 3 unitários do handler da listagem;
  - 4 em `DepartmentsTests`, entre eles `Deactivate_Should_ReturnConflictCountedByListing_WhenTicketIsReopenedInDeactivatedSector` (`:492` @e1985a2);
  - 1 em `SectorsTests`.
- **Merge com a 045 (`bc2395c`):** pela simulação de merge, só o `SYSTEM_SPECIFICATION.md` conflita no texto. O `TicketTestData.Create(sector:, status:, createdAtUtc:, …)` unitário que os testes usam tem a mesma assinatura nas duas linhas de base.
- **Relação com o modelo-alvo:** compatível, com três pontos.
  1. Acrescenta o terceiro uso de `TicketSlaFilters.WhereActive` fora de `Tickets/` e a terceira cópia da subconsulta "chamados dos setores do departamento" (OR-03). A 043p1 reescreve essa leitura em SQL com a condição que a DEC-10 e a DEC-12 da 043 decidirem.
  2. Fixa por teste, como comportamento aceito, a reabertura em setor desativado. Com a DEC-42 (b), a reabertura desse teste passa a ser recusada; o arranjo precisa montar o caso pelo banco (dado legado) ou o teste sai.
  3. A contagem do departamento é uma subconsulta correlacionada por departamento, aceitável num catálogo administrado. Na 043p1, um `GROUP BY` resolve.
- **Situação:** mergeada antes da 023 (PR #60, `05e4f0f`), como recomendava a DEC-48.

**023 — `feat/SPEC-023-department-list-responsible-id` (`4ffd79e`, PR #52).**
- **Base:** `ad732da` (merge da 029), anterior à 038 (`a72e07b` não é ancestral de `4ffd79e`). Na base, `Department` ainda tem `{ get; set; }` (`Department.cs` @4ffd79e).
- **Código:** uma linha em cada arquivo, com contrato aditivo; o `PUT` não muda (DEC-1 da 023):
  - `ResponsibleUserId` no `DepartmentResponse` da listagem (`GetAll/DepartmentResponse.cs:8` @4ffd79e);
  - o mesmo campo na projeção (`GetDepartmentsQueryHandler.cs:21` @4ffd79e).
- **Testes:**
  - 1 unitário, que monta `Department` e `User` por inicializador (`GetDepartmentsQueryHandlerTests.cs:13-48`, com o inicializador nas linhas 26-27, @4ffd79e);
  - 4 de integração, com um `DepartmentListItemDto` de cinco campos (`DepartmentsTests.cs:454` @4ffd79e).
- **Merge com a 045 (`bc2395c`):** o texto só conflita no `SYSTEM_SPECIFICATION.md`, mas o teste unitário novo **não compila**. Desde a 038, `Department` tem construtor privado e `private set` (`Department.cs:8-20`). O merge de texto não acusa nada.
- **Merge com a 022:** conflitam `GetDepartmentsQueryHandlerTests.cs`, `DepartmentsTests.cs` e `SYSTEM_SPECIFICATION.md`. As duas branches definem um `DepartmentListItemDto` diferente (023 `:454`; 022 `:528` @e1985a2). `DepartmentResponse.cs` e `GetDepartmentsQueryHandler.cs` se juntam sem conflito de texto.
- **Relação com o modelo-alvo:** compatível (um campo de leitura). Precisa de rebase sobre a `main` com a 038 e a 022, com o teste montado por `Department.Create` e um único record de listagem. O plano da própria 023 já previa que a segunda a ser implementada partiria da `main` com a outra mergeada.
- **Front:** hoje, o front busca `GET departments/{id}` para saber o id do responsável (`departments.store.ts:115`, `e977f64`). Com a 023, ele pode deixar de buscar, numa spec do front.
- **Atualização (`f3a16e1`, 2026-11-01):**
  - A branch passou a partir da `main` com a 022: a base comum com `ef1c981` é a própria `ef1c981`, e a branch contém `e1985a2`.
  - O teste unitário monta `Department` por `Department.Create` (`GetDepartmentsQueryHandlerTests.cs:29-30` @f3a16e1).
  - O teste que não compilava e os conflitos com a 022, descritos acima, ficaram resolvidos na própria branch. O PR #52 foi mergeado em 2026-11-01 às 20:58 UTC (`e2eb2f6`).

### 2.11.5 Modelo-alvo

**Domínio.** A fronteira não muda (DEC-40 (a)):

```
Department (raiz)                              Sector (raiz própria, referenciada por chamados e usuários)
  Create(name, description, User?)               Create(Department, name, description?)
  bool Update(name, description, User?) [DEC-43] bool Update(name, description?)         [DEC-43]
  void Deactivate()                              void Deactivate()
  eventos: Created, Updated (com os valores anteriores) [DEC-43], Deactivated — o mesmo no setor
```

- **Sem objeto de valor:** nome, descrição e responsável não passam no critério "regra própria e vários usos" (`CLAUDE.md:150`). O formato do nome fica no validator, o tamanho no banco (DEC-7 da 038) e a unicidade no índice. É a mesma conclusão da DEC-15 da 049.
- **Regras que precisam de consulta ficam nos handlers:**
  - a unicidade, com pré-conferência e o índice como barreira;
  - o departamento ativo na criação do setor;
  - os chamados ativos na desativação;
  - o setor ativo na abertura e, com a DEC-42, na reabertura.
- **Cascata:** continua no handler de desativação do departamento, que chama `Deactivate()` em cada setor ativo e no departamento, numa transação.

**Auditoria** (técnica, fora do Domain; DEC-45 e DEC-47):
- **O que é auditado:** o `IAuditable` (`SharedKernel`) marca os tipos auditados.
- **Até a 043p2:** o `AuditLoggingInterceptor` grava, com os ajustes do REQ-16 da 049 e dos REQ-309 e REQ-312.
- **A partir da 043p2:** a interface `IAuditLog`, em `Application/Abstractions/Audit/`, com implementação `internal sealed` em `Infrastructure/Audit/`, faz a gravação:
  - recebe o tipo, o id, a ação e os valores antes e depois;
  - calcula as colunas alteradas e aplica a lista de exclusão do tipo;
  - grava pela conexão e pela transação do command.

  A forma exata da interface é decisão da 043p2; aqui fica só a semântica (S-AU-1).
- **Registro:** o `AuditLogEntry` vira um registro de persistência da Infrastructure, usado pelo modelo das migrations (043p3).
- **Fora da auditoria, por regra escrita (REQ-313):**
  - o histórico do chamado, que já é trilha;
  - os refresh tokens: segredo de sessão, com alta rotação;
  - papéis e permissões do catálogo, que só mudam por migration (os vínculos são auditados). **Correção (reconferência):** a coluna `version` da função de Administrador muda em execução (`AdministratorGuard.cs:55`, token de concorrência) e também fica fora da auditoria;
  - as projeções do chamado;
  - as migrations.

**Fronteiras e consistência:**

| Agregado | Raiz | Invariantes próprios | Regras entre agregados (onde) | Concorrência | Eventos |
|---|---|---|---|---|---|
| Departamento | `Department` | I-OR-01, I-OR-03, I-OR-11 | cascata (handler, I-OR-05); chamados ativos (handler, I-OR-06) | índice único filtrado; trava da linha na 043p2 (DEC-41) | criado, alterado, desativado |
| Setor | `Sector` | I-OR-02, I-OR-03, I-OR-11 | departamento ativo (handler, I-OR-04); chamados ativos (I-OR-06); setor ativo para chamado e usuário (I-OR-07 a I-OR-09, nos handlers deles) | idem | criado, alterado, desativado |
| Log de auditoria | — (registro técnico) | I-AU-02, I-AU-04, I-AU-06 | I-AU-01 e I-AU-03, gravado junto de cada mudança | só inclusão, na transação do command | — |

**Responsabilidades:**

| Responsabilidade | Camada | Componente |
|---|---|---|
| criar, alterar e desativar departamento e setor; eventos | Domain | `Department`, `Sector` |
| unicidade, departamento ativo, chamados ativos, cascata, travas | Application (e o SQL da 043p2) | handlers da organização |
| "chamado ativo" | Domain (predicado com nome) e SQL | definido pela 043 (DEC-10 e DEC-13 dela) |
| setor ativo na abertura e na reabertura | Application | handlers de chamado (S-OR-6; DEC-42) |
| gravar a auditoria | Infrastructure | interceptor até a 043p2; depois, `IAuditLog` chamado pelo command |
| política de exclusão de segredos | Infrastructure, conferida por teste de arquitetura | lista por tipo (DEC-46) |
| ler a trilha | Application | `GetAuditHistoryQueryHandler` (SQL na 043p1) |

**Contrato de SQL para a 043 parte 2 (organização e auditoria).** Segue a convenção aprovada da 043: Dapper, `IDbConnectionFactory`, SQL no handler, parâmetros por objeto, transação explícita e eventos depois do commit. Como na 7.7.2 da 049, o template de command é decisão da 043p2; aqui fica a semântica. A ordem "travar a linha-pai, depois conferir" não cria ciclo de espera: a abertura de chamado só trava o setor, e a criação de setor só trava o departamento.

| # | Operação | Semântica exigida (PostgreSQL) |
|---|---|---|
| S-OR-1 | Criar departamento | pré-conferência de nome como hoje; `INSERT`; na corrida, o índice `ix_departments_name` continua a barreira (409 genérico; opcional, Q-47) |
| S-OR-2 | Editar departamento ou setor | `SELECT … FROM public.departments` (ou `public.sectors`) `WHERE id = @Id FOR NO KEY UPDATE`; pré-conferência de nome; `UPDATE` só das colunas alteradas; auditoria (S-AU-1) |
| S-OR-3 | Desativar departamento | numa transação: `SELECT is_active FROM public.departments WHERE id = @Id FOR NO KEY UPDATE` (404 ou 409 como hoje); `SELECT id FROM public.sectors WHERE department_id = @Id ORDER BY id FOR NO KEY UPDATE`; `SELECT EXISTS (SELECT 1 FROM public.tickets t JOIN public.sectors s ON s.id = t.sector_id WHERE s.department_id = @Id AND t.deleted_at_utc IS NULL AND t.status IN ('Open','InProgress'))`, com 409 `Departments.HasActiveTickets`; `Sector.Deactivate()` e `Department.Deactivate()` no domínio; `UPDATE` dos setores ativos e do departamento; uma linha de auditoria por linha alterada |
| S-OR-4 | Criar setor | `SELECT … FROM public.departments WHERE id = @DepartmentId AND is_active FOR SHARE` (sem linha = 404 `Departments.NotFound`, como hoje); pré-conferência de nome; `INSERT`. Se a desativação travou primeiro, a releitura depois da espera não acha a linha ativa |
| S-OR-5 | Desativar setor | `SELECT is_active FROM public.sectors WHERE id = @Id FOR NO KEY UPDATE`; `SELECT EXISTS (… t.sector_id = @Id …)` com a mesma condição de "ativo", e 409 `Sectors.HasActiveTickets`; `UPDATE`; auditoria |
| S-OR-6 | Abrir chamado e, com a DEC-42 (b), reabrir | na transação do command de chamado: `SELECT 1 FROM public.sectors WHERE id = @SectorId AND is_active FOR SHARE`; sem linha, 404 `Sectors.NotFound` na abertura (como hoje) ou o erro da DEC-42 na reabertura. É o contrato que os commands de chamado da 043p2 cumprem, junto com a 7.7.2 da 049 |
| S-AU-1 | Gravar a auditoria | na transação do command: `INSERT INTO public.audit_logs (id, entity_name, entity_id, action, user_id, timestamp, old_values, new_values, affected_columns)`, com uma linha por linha incluída, alterada ou excluída de tipo `IAuditable`, pelo `IAuditLog`. O formato é o de hoje: `entity_name` = nome do tipo; `action` = `Added`, `Modified` ou `Deleted`; JSON com as chaves = nomes das propriedades e, em `Modified`, só as colunas alteradas; segredos fora; autor = `UserIdOrDefault`. Um instante único por command; nenhuma linha quando nada muda |
| S-AU-2 | Ler a trilha (043p1) | `SELECT … FROM public.audit_logs WHERE entity_name = @EntityName AND entity_id = @EntityId`, com o filtro depois do cursor composto (`timestamp`, `id`), `ORDER BY timestamp DESC, id DESC` e um item a mais (REQ-3 da 043), sobre `ix_audit_logs_entity_name_entity_id_timestamp_id` |
| S-AU-3 | O que não é auditado | as projeções do chamado (S-5 da 049) e as migrations, registradas no estado (REQ-313) |

**Pastas:**

```
src/TickestPristine.Domain/
├── Departments/      sem mudança
├── Sectors/          sem mudança
└── (Audit/ sai)      [DEC-47]
src/TickestPristine.Application/
├── Departments/, Sectors/, AuditLogs/   sem mudança
└── Abstractions/Audit/IAuditLog.cs      [043p2, DEC-45]
src/TickestPristine.Infrastructure/
├── Departments/, Sectors/               sem mudança
└── Audit/  AuditLogEntry.cs [DEC-47], AuditLogEntryConfiguration.cs,
            AuditLoggingInterceptor.cs (até a 043p2), implementação do IAuditLog (043p2)
```

**Testes-alvo:**

| Camada | O que cobrir | Onde |
|---|---|---|
| Domínio | `Update` devolve `false`, sem evento, quando nada muda; `true` com o evento e os valores anteriores [DEC-43] | `DepartmentTests`, `SectorTests` |
| Handler | desativar setor com chamado Aberto (409) e com Resolvido, Fechado e Cancelado (204); edição de desativado com o comportamento de hoje; reabertura em setor desativado [DEC-42] | `DeactivateSectorCommandHandlerTests`, `UpdateDepartmentCommandHandlerTests`, `UpdateSectorCommandHandlerTests`, `ReopenTicketCommandHandlerTests` |
| Validator | espaço nas pontas do nome [DEC-44] | `DepartmentValidatorsTests`, `SectorValidatorsTests` |
| Arquitetura | nenhum arquivo de `Departments/` ou `Sectors/` usa `TicketSlaFilters`; cobertura da auditoria por command; nomes de segredo fora da lista; instantâneo dos nomes `IAuditable`; `AuditLogEntry` fora do Domain [DEC-47] | `Conventions/`, com checkers como os de hoje |
| Integração | corridas (AC-300, AC-301) na 043p2; trilha igual à de hoje depois da 043p2 (AC-308); `Deleted`, `SecurityStamp`, mesmo instante, save que falha sem registro | `DepartmentsTests`, `SectorsTests`, `TicketsTests`, `AuditLogsTests` |

### 2.11.6 Conformidade com as skills

| Regra | Fonte | Situação | Evidência |
|---|---|---|---|
| Entidade rica: construtor privado único, `private set`, `Create` com inicializador e sem guardas | `add-entity/SKILL.md`; `domain-modeling/SKILL.md:39-40` | conforme | `Department.cs:8-36`; `Sector.cs:8-36`; `RichEntityTests.cs:12-21` |
| `Create` recebe a entidade referenciada, carregada pelo handler, com `NotFound` quando falta | `ca-review/SKILL.md:42`; DEC-12 da 038 | conforme | `CreateDepartmentCommandHandler.cs:20-34`; `CreateSectorCommandHandler.cs:15-31` |
| Pergunta sim ou não como `AnyAsync` direto no `if` | `ca-review/SKILL.md:42`; DEC-13 da 038 | conforme nos seis handlers | `CreateDepartmentCommandHandler.cs:15`; `CreateSectorCommandHandler.cs:24`; o `ChangeUserSectorCommandHandler.cs:46` (Users, legado) guarda o resultado num `bool` antes |
| `void` quando o handler já conferiu o estado | padrão 8 (`domain-modeling/SKILL.md:45`) | conforme | `Department.cs:48-53`; `DeactivateDepartmentCommandHandler.cs:24-27` |
| `bool` para o que pode não mudar nada, com evento só quando muda e com o valor anterior | padrões 8 e 10 (`domain-modeling/SKILL.md:45,47`) | desvio documentado (`CLAUDE.md:145`) | `Department.cs:38-46`; `Sector.cs:38-45`; DEC-43 |
| Validator só com o formato; tamanho só na configuração | `add-feature/SKILL.md`; `ca-review/SKILL.md:50`; DEC-7 da 038 | conforme | `CreateDepartmentCommandValidator.cs:9-10`; `DepartmentConfiguration.cs:15-23`. Os validators são `internal sealed`, e o exemplo da skill é `public class`; é padrão do projeto inteiro, tratado fora desta seção |
| Nenhuma classe estática da Application inspeciona o estado de uma entidade | `ca-review/SKILL.md:32` | violação herdada: a organização não tem classe estática própria desde a 038, mas usa a do módulo de chamados | `DeactivateDepartmentCommandHandler.cs:29-30`; `TicketSlaFilters.cs:9-15`; OR-03 |
| Predicado traduzível nas consultas | padrão 9; `ca-review/SKILL.md:33` | parcial: extensão estática no lugar de um `Expression` com nome | OR-03; DEC-13 da 043 |
| Eventos `sealed record` com ids, sem `OccurredOnUtc` | padrão 10 | conforme, salvo o valor anterior na alteração | `DepartmentUpdatedDomainEvent.cs:5`; DEC-43 |
| Registro de auditoria criado pelo agregado e inserido pelo handler | padrão 7; `ca-review/SKILL.md:31`; `add-feature/SKILL.md:40` | não se aplica literalmente: o log técnico é gravado pela infraestrutura, e o desvio do `CLAUDE.md:140` não diz isso | `AuditLoggingInterceptor.cs:26-51`; AU-06; DEC-47 |
| Trabalho técnico atrás de interface | `add-feature/SKILL.md:49`; `references/abstraction.md` | conforme hoje (o interceptor fica na Infrastructure); alvo: `IAuditLog` | DEC-45 |
| Pasta por agregado; uma classe por rota; tag; `HasPermission` ou `RequireAuthorization` | `CLAUDE.md` (convenções); `add-feature/references/endpoint.md` | conforme | `Endpoints/Departments/*.cs`; `Tags.cs:7-8,14` |
| Erros `{Entity}Errors` com código `{Feature}.{Reason}` e tipo correto | `add-entity/SKILL.md` | conforme | `DepartmentErrors.cs:7-21`; `SectorErrors.cs:7-21` |
| Paginação: catálogo administrado em lista; trilha por cursor composto | `add-feature/references/pagination.md` | conforme | `GetDepartmentsQuery.cs:5`; `GetAuditHistoryQuery.cs:10-13` |
| Testes: entidade, cada falha do handler, validator e integração (persistência e 401) | `add-tests/SKILL.md`; `add-feature/references/tests.md` | conforme na maior parte; lacunas OR-12 e AU-12 | `DepartmentsTests.cs:30`; `SectorsTests.cs:43`; `AuditLogsTests.cs:53` |
| Comentários: `<summary>` só onde explica | `CLAUDE.md` (regras permanentes) | conforme; o `<summary>` do `GetAuditHistoryQuery` (um DTO) fica, porque a regra não remove os existentes | `Department.cs:38`; `Sector.cs:38`; `AuditLoggingInterceptor.cs:11-14`; `GetAuditHistoryQuery.cs:6-9` |
| Datas pelo `IDateTimeProvider` | `CLAUDE.md` (regras permanentes) | conforme | `AuditLoggingInterceptor.cs:61` |
| Domain só com o `SharedKernel`; sem repositório, `IUnitOfWork` nem MediatR | `ca-review/SKILL.md` | conforme; o `AuditLogEntry` comenta o `EntityState` do EF dentro do Domain | `AuditLogEntry.cs:10`; AU-06 |

### 2.11.7 Não determinado

- **Corrida de OR-01:** não foi reproduzida. A conclusão vem do código e da semântica de travas do PostgreSQL (2.5). Não existe teste de corrida da organização.
- **Espaço nas pontas do nome:** não testado se o `citext` trata `'TI '` e `'TI'` como diferentes (esperado: sim, porque compara pelo `lower` do texto). Também não se sabe se há nomes assim no banco real.
- **Dados de bancos já existentes:**
  - o volume real de `audit_logs`, porque não há banco de produção;
  - se algum banco tem departamento desativado com setor ativo, ou chamado ativo em setor desativado.
- **Simulação de merge:** o resultado do `git merge-tree --trivial-merge` é aproximado (o modo trivial não resolve renomeações). A falha de compilação da 023 vem da leitura do teste, não de um build.
- **Mover o `AuditLogEntry` de namespace:** espera-se que o `Model_Should_HaveNoPendingChanges` continue aprovado, porque a tabela é a mesma; a 049 supõe o mesmo para os renomes de classe. Não foi executado.
- **Consumidores de `GET audit-logs` fora do front:** nenhuma tela do `e977f64` usa a rota.
- **Seed e auditoria depois da 043p3:** não se sabe como ficam quando o EF ficar só nas migrations (o seed passa pelo `UseAsyncSeeding`), nem se o seed continua gerando auditoria e eventos.
- **Testes da 022 sobre a 045:** só a assinatura do `TicketTestData.Create` unitário foi conferida contra a 045. O arranjo dos testes de integração da 022 não foi conferido.

### 2.11.8 Fases da área

- **OA-0 — Linha de base das branches:** a 022 e a 023 já mergeadas (PR #60 e PR #52); falta conferir o AC-307 na `main` (T-301). Depende do PR #59 (045) e da ordem global (DEC-4 da 049).
- **OA-1 — Rede de segurança, sem mudar comportamento:** T-300, T-302, T-303 (com a DEC-46 aprovada) e T-304. Depende da OA-0 e vai junto com a fase 2 da 049 (REQ-16 dela).
- **OA-2 — Regras da organização, com mudança de comportamento:** T-305 (DEC-43), T-306 (DEC-44) e T-307 (DEC-42), cada uma só com a sua DEC aprovada. Depende da OA-1; a T-307 se coordena com a fase 3 da 049, que mexe no handler de reabertura.
- **043p1:** T-308, com as consultas da área em SQL e o "chamado ativo" pela DEC-10 e pela DEC-13 da 043. Depende da OA-1, porque a T-302 fixa os nomes antes da reescrita.
- **OA-3 — Log técnico fora do Domain:** T-309 (DEC-47). Depende da 043p1 (a leitura sai do EF) e do REQ-16 da 049 (nenhum handler referencia `AuditLogs`).
- **043p2:** T-310 (travas, S-OR-1 a S-OR-6) e T-311 (`IAuditLog`, S-AU-1). Depende da OA-1, porque a cobertura da auditoria precisa estar ativa antes da troca do primeiro command, e da OA-3.
- **OA-4 — Documentação e verificação:** T-312 e T-313. Depende das fases anteriores e da autorização para o `CLAUDE.md` (DEC-43 e DEC-47).

## 2.12 Plataforma: SharedKernel, Application transversal, Web.Api, banco, migrations, testes e CI

Esta seção avalia o que todas as áreas usam: as peças do `SharedKernel`; as abstrações, os decorators e a composição da Application; a montagem da Infrastructure (contexto do EF, conexão do Dapper, eventos de domínio, tempo e seed); o host da Web.Api (pipeline, contrato de erro, limites, observabilidade, saúde e imagem); o esquema inteiro do banco, com as 31 migrations; e a infraestrutura de testes, as regras de arquitetura e o CI. As regras de negócio de cada área ficam nas seções delas.

- **Linha de base:** `bc2395c` (`main` `ef1c981` mais a spec 045), lido só por `git show bc2395c:<caminho>`, `git ls-tree` e `git grep`. Toda referência `arquivo:linha` desta seção aponta para esse commit; a working tree não foi lida. Nenhum build, teste, script ou comando do EF rodou; a última execução registrada é a da verificação da 045 (`SYSTEM_SPECIFICATION.md:764`: 0 avisos e 1.466 casos aprovados). Nenhum bloqueio do portão SDD ocorreu.
- **Gravidade e destino:** a escala da 2.6 (**A**, **M**, **B**). Destinos: as fases `P0` a `P6` desta seção (bloco de fases no fim), as partes da 043 (`043p1` queries, `043p2` commands, `043p3` EF fora da aplicação) ou `fora` (Q-50 a Q-59).
- **Achados já registrados na 2.6** (F-21, F-24, F-30, F-34, F-35, F-37, F-38, F-39, F-40 e F-42) são citados pelo número; aqui entra só o que é da plataforma.
- **Números em `bc2395c`:**
  - 5 projetos de produção e 3 de teste; o `SharedKernel` tem 9 arquivos de código e nenhuma dependência.
  - Application: 247 arquivos `.cs`, 28 em `Abstractions/`; 33 command handlers, 29 query handlers e 2 handlers de evento (um vazio); 33 arquivos de validator com 62 `RuleFor`; 68 arquivos usam `IApplicationDbContext` e 57 importam `Microsoft.EntityFrameworkCore`; 2 query handlers usam Dapper (`GetOverview`, `GetVolume`).
  - Eventos de domínio: 33 tipos; 19 pontos de `Raise` no Domain e 15 na Application (todos em `Users` e `Roles`).
  - Web.Api: 61 endpoints; pipeline com 7 middlewares explícitos.
  - Banco: 18 tabelas, 21 chaves estrangeiras, 34 índices (11 únicos e 4 parciais), 2 chaves alternativas, 7 check constraints, 7 colunas `citext`, 4 `jsonb` e 6 colunas de enum gravadas como texto; 31 migrations, 31 designers e o snapshot.
  - Testes: 1.203 métodos `[Fact]`/`[Theory]` (707 unitários, 462 de integração e 34 de arquitetura), 1.466 casos na execução da 045.

### 2.12.2 Esquema e integridade do banco

Fonte: `ApplicationDbContextModelSnapshot.cs` (linhas por tabela) e as configurações em `src/TickestPristine.Infrastructure/*/*Configuration.cs`. Todas as tabelas ficam no esquema `public`, em snake_case; todas as chaves primárias são `uuid` `id`.

| Tabela | FKs (ação na exclusão) | Únicos e chaves alternativas | Outros índices | Checks | Tipos especiais | Token | Filtro global | Lacunas |
|---|---|---|---|---|---|---|---|---|
| `users` (`:1078-1152`) | `sector_id` → sectors (Restrict) | `email`, `code` | `sector_id`; `(first_name, last_name, id)` | `ck_users_email_length`, `ck_users_first_name_length`, `ck_users_last_name_length` | `citext` em e-mail, nome e sobrenome; `is_active DEFAULT true` no modelo | — | — | sem token: atualizações concorrentes ficam com a última (DB-08) |
| `user_credentials` (`:1154-1196`) | `user_id` → users (Cascade) | `user_id` | — | — | `security_stamp DEFAULT gen_random_uuid()` no modelo | — | — | contador de falhas sem proteção sob concorrência (DB-08, A); padrões residuais `0`/`false` |
| `user_invitations` (`:1198-1243`) | `user_id` → users (Cascade) | `token_hash` | `(user_id, created_at_utc)` | — | — | `used_at_utc`, `revoked_at_utc` | — | — |
| `user_roles` (`:1245-1272`) | `user_id` → users (Cascade); `role_name` → roles.name (Cascade, chave alternativa) | `(user_id, role_name)` | `role_name` | — | — | — | — | FK por nome, sem `ON UPDATE` (a 036 troca por id) |
| `refresh_tokens` (`:1024-1076`) | `user_id` → users (Cascade) | `token_hash` | `previous_token_hash`; `user_id` | — | — | `token_hash` | — | padrões residuais `''`, `Guid.Empty` e sentinela (DB-02) |
| `roles` (`:334-378`) | — | AK `name` | — | — | `HasData` das 3 roles | `version` (int, sombra, incremento à mão em `AdministratorGuard.cs:55`) | — | — |
| `permissions` (`:126-333`) | — | AK `code` | — | — | `HasData` do catálogo | — | — | — |
| `role_permissions` (`:380-574`) | `role_name` → roles.name (Cascade); `permission_code` → permissions.code (Cascade) | `(role_name, permission_code)` | `permission_code` | — | `HasData`: Administrador (18), Atendente (5), Colaborador (4) | — | — | o `HasData` é dono de vínculos editáveis pela API (DB-04) |
| `departments` (`:82-124`) | `responsible_user_id` → users (SetNull) | `name` parcial `WHERE is_active` | `responsible_user_id` | `ck_departments_name_length` | `citext` em `name` | — | — | sem token (última gravação vence) |
| `sectors` (`:575-613`) | `department_id` → departments (**Cascade**) | `(department_id, name)` parcial `WHERE is_active` | — | `ck_sectors_name_length` | `citext` em `name` | — | — | cascata latente (DB-03); `department_id` só tem o índice parcial |
| `tickets` (`:679-792`) | `created_by_user_id` (Restrict); `assigned_to_user_id` (SetNull); `sector_id` (Restrict) | — | `assigned_to_user_id`; `sector_id`; `waiting_on` parcial; `(created_at_utc desc, id desc)`; `(status, due_at_utc)`; `(created_by_user_id, last_activity_at_utc desc, id desc)` | `ck_tickets_title_length`; `ck_tickets_description_length` (10 a 500) | `citext` em título e descrição; `jsonb` em `sla_plan`; enum em texto: `status`, `priority`, `waiting_on` | `xmin` | `deleted_at_utc IS NULL` | enum sem check (DB-01); padrões sentinela residuais (DB-02) |
| `ticket_sla_timers` (`:947-1022`) | `ticket_id` → tickets (Cascade) | `(ticket_id, metric, cycle)` | `next_action_on_utc` parcial | — | enum em texto: `metric`, `completion_cause` | `completed_on_utc`, `stopped_on_utc` | — | enum sem check; `calendar_id` sem FK (calendários em código); sem check de `cycle >= 1` |
| `ticket_messages` (`:902-945`) | `ticket_id` (Cascade); `author_user_id` (Restrict) | — | `author_user_id`; `(ticket_id, created_at_utc desc, id desc)` | — | — | — | `deleted_at_utc IS NULL` | sem token (F-19) |
| `ticket_attachments` (`:794-853`) | `ticket_id` (Cascade); `uploaded_by_user_id` (Restrict) | — | `uploaded_by_user_id`; `(ticket_id, uploaded_at_utc desc, id desc)` | — | — | — | `deleted_at_utc IS NULL` | sem token; sem check de tamanho do arquivo nem de `purged_at_utc` × `deleted_at_utc` (F-15) |
| `ticket_histories` (`:855-900`) | `ticket_id` (Cascade); `changed_by_user_id` (SetNull) | — | `changed_by_user_id`; `(ticket_id, occurred_at_utc desc, id desc)` | — | enum em texto: `action` | — | — | enum sem check; a cascata apaga o histórico numa exclusão física |
| `sla_calendars` (`:615-647`) | — | — | — | — | `jsonb` em `work_schedule` | — | — | sem escrita (F-39) |
| `sla_calendar_holidays` (`:649-677`) | `sla_calendar_id` (Cascade) | `(sla_calendar_id, date)` | — | — | — | — | — | — |
| `audit_logs` (`:27-80`) | — (`user_id` sem FK, de propósito) | — | `timestamp`; `(entity_name, entity_id, timestamp desc, id desc)` | — | `jsonb` em `old_values` e `new_values` | — | — | só inclusão; `action` em texto livre |

**Concorrência por entidade**

| Entidade | Token | Onde | O que acontece no conflito | Lacuna |
|---|---|---|---|---|
| `Ticket` | `xmin` | `TicketConfiguration.cs:53` | `DbUpdateConcurrencyException` → 409 | o `UPDATE` do gravador de atividade muda o `xmin` (F-21) |
| `TicketSlaTimer` | `CompletedOnUtc`, `StoppedOnUtc` | `TicketSlaTimerConfiguration.cs:23-25` | 409; tratado à mão no `Post` (F-24); lote desfeito no monitoramento (F-34) | `NextActionOnUtc` fora dos tokens (F-08) |
| `RefreshToken` | `TokenHash` | `RefreshTokenConfiguration.cs:21` | a segunda renovação recebe 409 | — |
| `UserInvitation` | `UsedAtUtc`, `RevokedAtUtc` | `UserInvitationConfiguration.cs:25-26` | a segunda ativação recebe 400 ou 409 | — |
| `Role` | `Version` (sombra) | `RoleConfiguration.cs:21`; `AdministratorGuard.cs:55` | 409 na retirada simultânea do último administrador | manipulado pela Application via `Entry` (PL-08) |
| `UserCredential` | — | `UserCredentialConfiguration.cs:11-25` | a última gravação vence | `FailedLoginAttempts++` perde incrementos (DB-08, A) |
| `User`, `Department`, `Sector` | — | configurações | a última gravação vence | — |
| `TicketMessage`, `TicketAttachment` | — | configurações | exclusões simultâneas passam as duas | F-19 (043p2) |
| `UserRole`, `RolePermission` | — | índice único | 409 "Registro duplicado" | — |
| `TicketHistory`, `AuditLogEntry` | — | — | só inclusão | — |

**Lacunas de integridade** (as que viram achado estão na 2.12.3):
- Seis colunas de enum em texto sem check: qualquer texto entra por SQL, e o erro só aparece na leitura (DB-01).
- Padrões residuais em colunas obrigatórias, fora do modelo do EF (DB-02).
- Cascatas latentes de `departments` para `sectors` e de `tickets` para mensagens, anexos, histórico e cronômetros; hoje não há exclusão física desses registros (DB-03).
- Vínculos de role semeados pelo `HasData` e editáveis pela API (DB-04).
- Sem checks de regras baratas de conferir no banco: `ticket_sla_timers.cycle >= 1`, `ticket_attachments.file_size_bytes > 0`, `purged_at_utc` só com `deleted_at_utc` (F-15), `audit_logs.action` em (`Added`, `Modified`, `Deleted`). Ficam como recomendação do modelo-alvo, sem REQ.
- `sectors.department_id` só aparece no índice parcial `WHERE is_active`: a busca pela FK e a listagem de setores inativos por departamento não usam índice. Sem impacto medido no volume atual.
- Nenhum índice cobre `deleted_at_utc IS NULL`, presente em toda leitura de chamados; os índices de paginação não são parciais. Sem impacto medido.

### 2.12.4 Testes: inventário e lacunas

**Por projeto** (contagem em `bc2395c`; os casos somam os `[Fact]` e as linhas de dados das teorias, e batem com a execução da 045):

| Projeto | Arquivos `*Tests.cs` | Métodos | `[Fact]` | `[Theory]` | Linhas de dados das teorias | Casos na execução da 045 |
|---|---|---|---|---|---|---|
| `TickestPristine.Application.UnitTests` | 92 | 707 | 639 | 68 | 253 (`[InlineData]`) | 892 |
| `TickestPristine.IntegrationTests` | 27 | 462 | 444 | 18 | 48 (`[InlineData]`) e 22 (2 `[MemberData]`, `AttachmentContentInspectorTests.cs:18-46`) | 514 |
| `TickestPristine.ArchitectureTests` | 7 (mais verificadores e fixtures) | 34 | 28 | 6 | 32 (`[InlineData]`) | 60 |
| **Total** | 126 | **1.203** | 1.111 | 92 | 355 | **1.466** |

**Por área** (métodos):

| Área | Unitários | Integração |
|---|---|---|
| Tickets | 289 | 188 |
| Users | 187 | 128 |
| TicketAttachments | 54 | 27 |
| TicketMessages | 45 | 15 |
| Departments | 31 | 17 |
| Sectors | 28 | 17 |
| SlaCalendars | 21 | 6 |
| Abstractions (paginação) | 14 | — |
| TicketHistories | 13 | 13 |
| Roles | 11 | 10 |
| Authorization (guards) | 7 | — |
| Permissions | 5 | 2 |
| AuditLogs | 2 | 5 |
| Database (eventos, migrations, concorrência, tamanho de texto, `is_active`) | — | 13 |
| Storage | — | 7 |
| Middleware (correlação) | — | 5 |
| Seeding | — | 5 |
| Cors | — | 3 |
| Swagger | — | 1 |

**Regras de arquitetura** (34 métodos: 19 regras de produção e 15 autotestes dos verificadores):

| # | Regra | Arquivo | Escopo e lista de exceções |
|---|---|---|---|
| 1-4 | `SharedKernel` não depende de Domain, Application, Infrastructure nem Web.Api | `Layers/LayerTests.cs:8-70` | NetArchTest por assembly |
| 5-7 | Domain não depende de Application, Infrastructure nem Web.Api | `Layers/LayerTests.cs:72-118` | idem |
| 8-9 | Application não depende de Infrastructure nem de Web.Api | `Layers/LayerTests.cs:120-150` | idem |
| 10 | Infrastructure não depende de Web.Api | `Layers/LayerTests.cs:152-166` | idem |
| 11 | `BusinessCalendar` no Domain, sem interface | `Layers/LayerTests.cs:168-183` | namespace `TickestPristine.Domain.SlaCalendars` |
| 12 | `Ticket` só recebe cronômetro ou calendário no `Create` | `Aggregates/TicketSlaAggregateTests.cs:13-31` | lista: `Create` (trocada pela 049, F-41) |
| 13 | `TicketSlaTimer` expõe só os membros aprovados | `Aggregates/TicketSlaAggregateTests.cs:33-55` | lista: `CompleteFirstResponse`, `GetConsumed`, `GetState`, `ProcessDue`; propriedade `IsOpen` |
| 14 | `TicketSlaCycleService` `sealed`, sem dependências no construtor | `Aggregates/TicketSlaAggregateTests.cs:57-71` | — |
| 15 | todo command tem validator | `Conventions/CommandValidatorTests.cs:8-19` | todos os commands da Application |
| 16 | sem relógio do sistema | `Conventions/DateTimeUsageTests.cs:7-21` | só `src/TickestPristine.Application`; busca textual (strings contam; alias não) |
| 17 | endpoint com tag de `Tags` | `Conventions/EndpointTagTests.cs:97-110` | `src/TickestPristine.Web.Api`; busca textual |
| 18 | handlers e decorators `internal sealed` | `Conventions/HandlerShapeTests.cs:8-19` | famílias command, query e evento; família vazia reprova |
| 19 | entidades ricas só com setters privados | `Conventions/RichEntityTests.cs:11-34` | lista: `Department`, `Sector`, `Ticket`, `TicketSlaTimer`, `TicketHistory`, `TicketMessage`, `TicketAttachment` |

Os verificadores leem o repositório pela raiz do `TickestPristine.slnx` e ignoram `bin`, `obj` e pastas iniciadas por ponto (`RepositoryFiles.cs:9-62`).

**Infraestrutura e lacunas** (detalhe nos achados TE):
- **Unitários:** `BaseHandlerTest` (contexto em memória, relógio, calendários, usuários, roles e chamados com SLA pelo `TicketSlaCycleService`), `TestDbContext` (TE-01) e `EntityTestState` (TE-05). Os handlers em Dapper só têm teste unitário dos caminhos que não abrem conexão (`GetTicketVolumeQueryHandlerTests.cs:21-52`); o SQL é provado na integração, como manda a DEC-5 da 043.
- **Integração:** Testcontainers com `postgres:17`, uma coleção e um banco; o administrador faz o primeiro acesso na fixture; monitoramento de SLA desligado e chamado pelos testes; limites de taxa elevados; e-mail capturado (`IntegrationTestWebAppFactory.cs:34-109`). Pontos fortes: os backfills das migrations são reexecutados por teste; constraints testadas por SQL direto (`TextLengthLimitsTests.cs:20`); contador de comandos do EF (`EfCommandCounter`).
- **Lacunas da plataforma:** ordem e comportamento dos decorators, `CustomResults`, ramos 401 e 500 do `GlobalExceptionHandler` e eventos de entidade removida (TE-02); ambiente de produção (TE-03); autorização declarada em endpoint e as demais regras do REQ-416 (TE-04); `SampleDataSeeder` (F-42); o corpo do `health` (os testes só o usam como rota para conferir o `Correlation-Id`, `RequestContextLoggingTests.cs:18-83`); o contrato do 429 (há só o status, `TicketAttachmentsTests.cs:624-665`); nenhum teste executa o `Down` de uma migration.

### 2.12.5 Modelo-alvo da plataforma

**Fluxo de uma requisição (alvo):**

```
Correlation-Id → log de requisição → exceções (IProblemDetailsService) → páginas de status → CORS
  → autenticação → rate limiter → autorização → endpoint
      → ValidationDecorator (só commands; falha: ValidationError com property)
          → LoggingDecorator (span "TickestPristine.Application", nível por ErrorType, tempo, contadores)
              → handler
                  hoje: IApplicationDbContext → SaveChangesAsync → interceptors (auditoria, eventos)
                  043p2: IDbConnectionFactory → transação explícita → SQL → commit
                         → IDomainEventsDispatcher.DispatchAsync(eventos dos agregados alterados)
      → result.Match(Results.Ok/NoContent, CustomResults.Problem) → Problem Details com code e traceId
```

**Peças:**

| Peça | Hoje | Alvo | Quando |
|---|---|---|---|
| `Result`, `Error`, `ErrorType` | sem erros compartilhados; `Forbidden` como 403 | os mesmos, mais os erros `General.*` usados pelo `GlobalExceptionHandler` e pelo "zero linhas" | P1 |
| Contrato de erro | três formas (PL-01) | uma só, aditiva: `application/problem+json`, `status`, `title` (código nas falhas de `Result`, texto nas demais, como hoje), `detail`, `type`, `traceId`, `code`; `errors[].property` na validação | P2 (DEC-51) |
| Códigos `General.*` | — | `General.ConcurrencyConflict` (409), `General.DuplicateRecord` (409), `General.TextOutOfRange` (400), `General.PayloadTooLarge` (413), `General.InvalidRequest` (400), `General.Unauthenticated` (401), `General.Forbidden` (403), `General.RouteNotFound` (404), `General.MethodNotAllowed` (405), `General.TooManyRequests` (429), `General.ServerFailure` (500, também no lugar do código de uma falha `Failure`) | P1 (constantes) e P2 (resposta) |
| Decorators | Validation → Logging → handler; toda falha em `Error` | mesma ordem, documentada e testada; nível por tipo, tempo, span e contadores | P1 |
| Eventos de domínio | coletados depois do save (perde os de entidade removida) | guardados antes do save e despachados depois do commit (P1); na 043p2, despacho explícito pelo handler depois do `CommitAsync`, com a interface na Application | P1, 043p2 (DEC-52) |
| Acesso a dados | `IApplicationDbContext` (18 `DbSet`, `Entry`) e `IDbConnectionFactory` | só `IDbConnectionFactory`, com SQL no handler e transação explícita quando várias operações precisam ser atômicas (fonte da 043, seções 2 a 4) | 043p1 a 043p3 |
| Paginação | extensões do EF | contratos iguais; normalização reaproveitada e SQL de offset e cursor | 043p1 |
| Banco | enums sem check; padrões sentinela; `Down` sem política | checks `ck_*_valid`; sem sentinelas; migrations só para frente, com conferência de dados antes de check ou único | P3 (DEC-54) |
| Migrations em produção | só manual | pacote `efbundle` gerado no CI e aplicado na implantação; o `DbContext` só das migrations depois da 043p3 | P5 (DEC-53) |
| Observabilidade | automática | mais `ActivitySource` e `Meter` "TickestPristine.Application" (commands, queries, eventos, monitoramento), `service.version` | P1 (DEC-57) |
| Saúde | `health` com o banco | `health/live` e `health/ready` (banco pela fonte registrada, pasta de arquivos, Redis se configurado); `health` mantido | P1 |
| Limites | depois da autorização; 429 sem corpo | entre autenticação e autorização; 429 em Problem Details com `Retry-After` | P1 (DEC-58) |
| Testes | `TestDbContext` em memória; integração em Development | regras do banco só na integração; `TestDbContext` congelado até sair; teste em `Production`; regras de arquitetura do REQ-416 | P1 a 043p3 (DEC-55, DEC-56) |
| CI | build, testes e auditoria | mais pacotes vulneráveis, cobertura, imagem, pacote de migrations, `trx`, `global-json-file`, Dependabot de actions e Docker | P1, P5 |

**Exemplo do contrato-alvo** (ilustrativo; os textos são os de hoje):

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
  "title": "Conflito de concorrência",
  "status": 409,
  "detail": "Os dados foram alterados por outra operação ao mesmo tempo. Atualize a tela e tente novamente.",
  "code": "General.ConcurrencyConflict",
  "traceId": "00-…"
}
```

Numa falha de `Result`, `title` e `code` trazem o mesmo código (ex.: `Tickets.NotFound`). Num erro de validação, cada item de `errors` mantém `code`, `description` e `type` e ganha `property` (ex.: `title`).

**O que não muda:** sem MediatR, sem repositório, sem `IUnitOfWork` e sem behavior de transação (a DEC-52 recomenda o despacho explícito justamente para não criar um); os eventos continuam despachados depois do commit, sem outbox (DEC-3 (a) da 043); o `Correlation-Id`; as rotas e os payloads de sucesso.

### 2.12.6 Conformidade com as skills

| Regra | Fonte | Situação na plataforma | Evidência | Ação |
|---|---|---|---|---|
| Sem MediatR; handlers próprios com decorators do Scrutor | `CLAUDE.md:73`; `add-feature/SKILL.md:23` | cumpre; a ordem é a do `CLAUDE.md` e difere da do modelo de referência | `Application/DependencyInjection.cs:29-35` | fixar em teste (REQ-414) |
| Handlers `internal sealed`, construtor primário | `add-feature/SKILL.md:37` | cumpre, por regra de arquitetura | `HandlerShapeTests.cs:8-19` | — |
| Falha esperada em `Result`; exceção só para o inesperado | `CLAUDE.md`, "Padrão Result"; `ca-review/SKILL.md:45` | cumpre na Application; concorrência, unicidade e tamanho viram 409 e 400 no `GlobalExceptionHandler`, por decisão registrada | `GlobalExceptionHandler.cs:20-62` | preparar o SQL direto (REQ-401) |
| Códigos `{Feature}.{Reason}` e tipo correto | `add-entity/SKILL.md:58`; `ca-review/SKILL.md:46` | cumpre; `Forbidden` é desvio registrado; `Unauthorized` com 403 confunde | `ErrorType.cs:3-11`; `CLAUDE.md:144` | PL-15 |
| Endpoint só mapeia e converte com `Match` | `add-feature/references/endpoint.md:121-127` | cumpre; nenhum endpoint usa o contexto | busca em `Web.Api/Endpoints` | — |
| `<summary>` em toda interface | `CLAUDE.md:151`; `ca-review/SKILL.md:60` | não cumpre em 18 interfaces | PL-13 | REQ-409 |
| Sem relógio do sistema na Application | `CLAUDE.md`, "Regras permanentes"; `ca-review/SKILL.md:54` | cumpre em todo `src/` (só o `DateTimeProvider`) | `DateTimeProvider.cs:7` | ampliar a regra (REQ-416) |
| Sem registro manual na DI para slice | `add-feature/SKILL.md:42` | cumpre; o `TicketAgentLookup` é peça compartilhada registrada à mão | `Application/DependencyInjection.cs:44` | — |
| Trabalho técnico atrás de interface | `add-feature/references/abstraction.md:3`; `ca-review/SKILL.md:32` | não cumpre no `RefreshTokenHasher` (estática) | `RefreshTokenHasher.cs:6` | área de usuários (PL-16) |
| Handler de evento: um por reação, `{Action}On{Event}DomainEventHandler`, pasta do evento | `add-feature/references/domain-event-handler.md:35-36,50` | não cumpre nos 2 handlers | PL-14 | REQ-409 |
| Registro filho, histórico e auditoria no mesmo save | `domain-modeling`, padrão 7; `ca-review/SKILL.md:31` | auditoria no mesmo save pelo interceptor; escrita por SQL fica sem auditoria (F-22) | `AuditLoggingInterceptor.cs:26-51` | 043p2 (DEC-3 (a) da 043) |
| Sem repositório, `IUnitOfWork` nem behavior de transação | `add-feature/SKILL.md:52`; `ca-review/SKILL.md:73` | cumpre | — | DEC-52 mantém |
| Paginação: ajuste na extensão; índice que acompanhe a ordenação | `add-feature/references/pagination.md:142,150` | cumpre: há índice para as ordenações paginadas de chamados, mensagens, anexos, histórico, auditoria e usuários | snapshot `:75-77,775-784,848-850,895-897,940-942,1141-1142` | manter na 043p1 |
| Persistência do modelo (conversor UTC, auditoria pelo contexto) | `add-entity/references/persistence.md` | não se aplica: desvio "Persistência" | `CLAUDE.md:140` | — |
| Bases de teste; nunca simular o contexto; montar pelo domínio | `add-tests/SKILL.md:39-41`; `add-feature/references/tests.md:162` | cumpre as bases; "montar pelo domínio" com 66 ajustes por reflexão | TE-05 | REQ-413 |
| Domínio rico; legado declarado em "Em migração" | `CLAUDE.md:146,158-166` | 7 entidades ricas; 11 anêmicas declaradas; classes estáticas de `Users` fora da lista | PL-16 | REQ-418 (DEC-59) |
| Query sem cache sem decisão explícita | `CLAUDE.md:134` | cumpre; a frase "único uso" está errada | F-38 | REQ-418 |

**Padrões por área** (consistência entre os domínios):

| Área | Entidades ricas | Entidades anêmicas | `Raise` na Application | Classes estáticas com regra ou estado | Guards estáticos |
|---|---|---|---|---|---|
| Chamados | `Ticket`, `TicketSlaTimer`, `TicketMessage`, `TicketAttachment`, `TicketHistory` | `SlaCalendar`, `SlaCalendarHoliday` | 0 | `TicketSlaFilters`, `TicketAssigneeEligibility`, `TicketHistoryDescriptions`, `TicketTimeStatistics`, `TicketVolumePeriods` (consultas; 043p1) | `TicketAccessGuard` |
| Organização | `Department`, `Sector` | — | 0 | — | — |
| Usuários | — | `User`, `UserCredential`, `UserInvitation`, `UserRole`, `RefreshToken` | 14 | `UserCodes` (declarada); `UserSessions`, `UserPasswords`, `UserInvitations`, `UserCredentialsVerifier` (não declaradas); `RefreshTokenHasher` (técnica); `UserInvitationEmail` (texto do e-mail) | `PrivilegeEscalationGuard`, `AdministratorGuard` |
| Acesso | — | `Role`, `Permission`, `RolePermission` | 1 | `PermissionCatalog`, `PermissionCodes`, `RoleNames` (catálogos) | — |
| Auditoria | — | `AuditLogEntry` | 0 | — | — |

### 2.12.7 Não determinado

- **Valor e efeito dos padrões residuais:** se o `DEFAULT` das colunas de data ficou como `-infinity` ou `0001-01-01` (depende da conversão de infinito do Npgsql na geração) e se há linhas com esses valores (2.2). Conferir com `information_schema.columns` e uma contagem por coluna antes da migration do REQ-410.
- **Dados que barrariam as constraints novas:** valores fora das listas de enum e descrições com menos de 10 caracteres em bancos antigos. A migration do REQ-410 confere antes.
- **Corpo exato das respostas de erro:** por leitura, o `Results.Problem` passa pelo `IProblemDetailsService` registrado pelo `AddProblemDetails` (`application/problem+json`, provavelmente com `traceId`); o `GlobalExceptionHandler` escreve com `WriteAsJsonAsync` (`application/json`, sem `traceId`); e as respostas 401, 403, 404, 405 e 429 de middleware saem sem corpo, porque não há `UseStatusCodePages` nem `OnRejected`. Não executado; nenhum teste confere o tipo de conteúdo nem a ausência de corpo.
- **Log duplicado de exceção:** se o ASP.NET Core 10 ainda registra a exceção no middleware quando o `IExceptionHandler` a trata (além do `LogError` do `GlobalExceptionHandler`).
- **Pool da checagem de saúde:** como o `AspNetCore.HealthChecks.NpgSql` 9.0.0 cria a conexão a partir da connection string no .NET 10.
- **Partição por usuário do rate limiter:** depende do `sub` mapeado para `NameIdentifier` (padrão do JwtBearer); nenhum teste confere a partição.
- **Imagem Docker:** a faixa do SDK da tag `sdk:10.0` frente ao `global.json` (10.0.401, `latestFeature`); a imagem não foi construída.
- **CI no GitHub:** duração e resultado da última execução para `bc2395c` (não consultados).
- **Varredura do Scrutor:** se as classes de decorator entram como implementações na varredura (a aplicação funciona nos testes; não muda nenhum achado).
- **Carga:** desempenho sob carga e efeito dos índices ausentes (`sectors.department_id`, `deleted_at_utc`).

### 2.12.8 Fases da área

- **Fase P0 — pré-condições da plataforma:** PR #59 (045) mergeado; respostas às DEC-51, DEC-52, DEC-53, DEC-54 e DEC-59; ordem geral da DEC-4 da 049. Entrega: T-400.
- **Fase P1 — correções sem mudança de contrato nem de esquema (T-401 a T-410, T-412, T-414 e T-416):** depende só da P0; recomendada antes da 043p1, porque T-401 e T-405 preparam a 043p2 e os testes do pipeline protegem a troca de tecnologia. O T-406 coordena com a fase 1 da 049 no arquivo do handler de purga.
- **Fase P2 — contrato de erro (T-411):** depende da DEC-51 e do T-401 (tabela de mapeamento e códigos `General.*`); deve terminar antes da 043p2, para os commands em SQL nascerem no contrato final. O front não precisa mudar (aditivo).
- **Fase P3 — integridade do banco (T-413):** depende da DEC-54; depois da 043p1 (que não muda o esquema) e antes da 043p2 (que passa as escritas para SQL). Se a migration manual da 036 estiver em curso, as duas são aplicadas em sequência, uma depois da outra, para não conflitarem no snapshot.
- **Fase P4 — dentro da 043p2 (T-417):** despacho explícito de eventos (DEC-52); fim do `ITicketActivityWriter` (S-5 da 7.7.2); esvaziamento da lista de exceções do REQ-405; testes de handler de command na integração; incremento atômico do contador de login, se a Q-50 não tiver sido resolvida antes.
- **Fase P5 — com a 043p3 (T-415):** pacote de migrations no CI (DEC-53), que pode ser antecipado para a P1 se a DEC-53 for respondida antes; saída do `TestDbContext`, do `IApplicationDbContext` e do EF da Application; `DbContext` só das migrations (DEC-4 da 043).
- **Fase P6 — documentação e verificação (T-418, T-419):** o `SYSTEM_SPECIFICATION.md` é atualizado ao fim de cada fase que mudar o que ele descreve; o `CLAUDE.md` só com a DEC-59; a verificação fecha a seção pela `spec-verify`.

## 2.13 Front-end Angular: contratos, regras e fluxos

**Linha de base.** Front-end `tickest-pristine-front` no commit `e977f64` (`main`, merge do PR #4). A working tree dele tem mudanças não commitadas de outra pessoa (entre elas `auth.interceptor.ts`, `current-user.service.ts`, `department.data.service.ts`), que não entraram. Back-end no `bc2395c`. Tudo foi lido por `git show` e `git grep`, sem build nem testes. Caminhos do front são relativos a `src/app/` (ou à raiz do repositório quando começam por `src/environments/` ou `docs/`); caminhos do back omitem o prefixo `src/TickestPristine.` (ex.: `Web.Api/Endpoints/Tickets/Close.cs:13`).

**O que foi lido.** No front: `CLAUDE.md`, `PRODUCT.md`, títulos do `DESIGN.md`, `docs/audit/FRONTEND_AUDIT.md` (A-01 a A-46, C-01 a C-12) e `FINAL_ACCEPTANCE_AUDIT.md`; os 9 Data Services, os 18 Stores, os 3 guards, o interceptor, os serviços de sessão, as rotas, os `environment` e as telas que decidem ações; os 92 arquivos de teste, por busca. No back: as 61 rotas de negócio (mais `GET health`), os DTOs e handlers citados, a 4.3.1 e a 2.6 do `SYSTEM_SPECIFICATION.md`, as specs 023, 024, 034, 036, 044, 046, 047 e 048 e `specs/_fontes/contratos-central-atendimento.md`.

**Arquitetura do front, em resumo.** Angular 22 com NgRx Signal Store, organizado por contexto: `tickets`, `users`, `departments`, `roles`, `dashboard` (o Início, shell) e `shared` (sessão, layout, utilitários). Cada contexto tem `entities/` (interfaces e enums que espelham os DTOs), `infrastructure/` (Data Services com `HttpClient` direto e `Observable` cru), `application/` (Stores com `rxResource`) e `feature-*` (telas). Há um interceptor (`authInterceptor`), três guards (`authGuard`, `adminMasterGuard`, `pendingPasswordChangeGuard`), a sessão no `SessionService` e flags das rotas do back no `environment` (`DASHBOARD_FEATURES`). O SSR só entrega o shell nas rotas com sessão. Não há cliente gerado nem tipo compartilhado com o back: os 55 métodos HTTP e os tipos foram escritos à mão.

**Relação com a auditoria do front.** A auditoria de 2026-10-29/30 já corrigiu contratos (A-04 `waitingOn`, A-38 `tickets/assignees`, A-39 política de anexos) e levou ao back A-23 (spec 046), A-24 (047) e A-30 (048). Esta seção não repete o que já está resolvido; cita os ids quando o achado continua.

### 2.13.2 Matriz de contratos

São 55 métodos HTTP em 9 Data Services, para 54 rotas distintas (`GET tickets` aparece duas vezes). Todas as rotas chamadas existem no back. As divergências são de uso, de campo ou de regra; "—" quer dizer contrato e uso coerentes.

| Chamada do front (arquivo) | Verbo e rota | Rota no backend | Campos usados | Divergência |
|---|---|---|---|---|
| `TicketDataService.create` (`tickets/infrastructure/ticket.data.service.ts:34`) | `POST tickets` `{title, description, priority, sectorId}` | `Web.Api/Endpoints/Tickets/Create.cs:24` (`tickets:create`) | id (descartado; a lista recarrega) | `CreateTicketData.responsibleId` (`tickets/entities/ticket.ts:75`) não existe no back, que usa `assignedToUserId` (`Create.cs:18`); nunca é enviado. Abrir em nome de outro (`requesterId`) não tem tela |
| `TicketDataService.update` (`:39`) | `PUT tickets/{id}` `{description, status}` | `Tickets/Update.cs:20` (usuário ativo; regras no handler) | — (204) | Usado para Em andamento, Resolvido, Cancelado **e Fechado**; Fechado é recusado (400 `Tickets.NotActive`) (FE-01). Exige o estado completo, sem versão (FE-02) |
| `TicketDataService.reopen` (`:47`) | `POST tickets/{id}/reopen` `{reason}` ou sem corpo | `Tickets/Reopen.cs:21` | — (204) | Motivo limitado a 500 no front; o back aceita até 4000 (RN-33) (FE-19) |
| `TicketDataService.delete` (`:52`) | `DELETE tickets/{id}` | `Tickets/Delete.cs:13` (`tickets:delete` no handler) | — | Declarada e sem chamador: não há tela de exclusão |
| `TicketDataService.getMine` (`:69`) | `GET tickets/mine?page&pageSize&status=1 ou 2&search` | `Tickets/GetOwn.cs:14` (`tickets:view-own`) | id, title, description, status, assignedToName, sectorName, dueAtUtc, slaStatus, lastActivityAtUtc, canCancel, ownerCancelDeadlineUtc; paginação | Espera `currentDueAtUtc` e `currentSlaStatus` (opcionais, spec 024 não entregue): prazo do 1º ciclo depois de reabrir (FE-11). `openedByUserId` sem uso (o front assume o dono). Sondagem extra `page=1&pageSize=1` quando o filtro volta vazio (`tickets/application/tickets.store.ts:53-62`) |
| `TicketDataService.getById` (`:74`), no detalhe e no painel do Monitor | `GET tickets/{id}` | `Tickets/GetById.cs:13` (participante ou `tickets:manage`) | detalhe: dados, `canCancel`, `ownerCancelDeadlineUtc`, `sla`; painel: só `description` | Sem uso: `slaWarningAtUtc`, `sla.objectives[].{targetMinutes, startedAtUtc, warningAtUtc}`, `sla.plan.{policyId, policyVersion, calendarId}`. O painel lê o detalhe inteiro para mostrar a descrição (FE-28) |
| `TicketDataService.getSla` (`:83`) | `GET tickets/{id}/sla` | `Tickets/GetSla.cs:13` | `serverNowUtc`, `plan`, `objectives` | — (atualiza o painel depois de uma mensagem) |
| `TicketDataService.getHistory` (`:103`) | `GET tickets/{id}/history?cursorDate&cursorId&pageSize` | `TicketHistories/GetAll.cs:14` | id, description, occurredAtUtc; cursor | Enum `TicketHistoryAction` do front sem `DescriptionChanged = 12` e `Assigned = 13` (sem efeito: o front não lê `action`). `changedByUserId`, `oldValue` e `newValue` sem uso |
| `TicketDataService.removeAssignee` (`:111`) | `DELETE tickets/{id}/assignee` | `Tickets/Unassign.cs:13` (`tickets:manage`) | — | — |
| `TicketDataService.assignTicket` (`:120`) | `PUT tickets/{id}/assignee` `{assignedToUserId}` | `Tickets/Assign.cs:15` (`tickets:manage`; `tickets:assign` para outra pessoa) | — | O detalhe compara `title` com `Tickets.AssigneeCannotManageTickets`, código que não existe mais (hoje `Tickets.AssigneeNotAttendant`) (FE-04) |
| `TicketDataService.getAll` (`:159`), no Monitorar e nos Alertas | `GET tickets?page&pageSize&sort&status(repetido)&priority&sectorId(repetido)&createdFrom&createdTo&waitingOn&slaStatus&search&unassignedOnly ou assignedToUserId` | `Tickets/GetAll.cs:17` (`tickets:manage`) | id, title, priority, status, requesterName, requesterEmail, assignedToUserId, assignedToName, sectorName, dueAtUtc, resolvedAtUtc, slaStatus, waitingOn; paginação | Sem hora do servidor: os Alertas chamam `sla-summary` só para o relógio (FE-10). `slaStatus` aceita um valor: os Alertas fazem duas listagens. SLA do 1º ciclo (048). Sem uso: `requesterId`, `createdAtUtc`, `slaWarningAtUtc` e os filtros `requesterId`, `resolvedFrom`, `resolvedTo` |
| `TicketDataService.getOverview` (`:167`) | `GET tickets/stats/overview?sectorId(repetido)&priority&assignedToUserId ou unassignedOnly&createdFrom&createdTo` | `Tickets/GetOverview.cs:17` (`tickets:manage`) | `serverNowUtc`, `volume.*`, `sla.*`, `attention.*` | — (o Início pede duas vezes, período e período anterior; o Monitor converte em resumo de SLA) |
| `TicketDataService.getTimes` (`:175`) | `GET tickets/stats/times` (recorte e `resolvedFrom`/`resolvedTo`) | `Tickets/GetTimes.cs:17` (`tickets:manage`) | `firstResponse`, `resolution`, `minimumSampleSize` | — |
| `TicketDataService.getAssignees` (`:187`) | `GET tickets/assignees?search&sectorId&page&pageSize` | `Tickets/GetAssignees.cs:14` (`tickets:manage`) | id, fullName, sectorName; `totalCount` | O filtro do Início e do Monitor carrega uma página de 100 (o máximo do back): acima de 100 elegíveis, a lista fica incompleta (o front mostra o total) |
| `TicketDataService.getSlaSummary` (`:193`) | `GET tickets/sla-summary` | `Tickets/GetSlaSummary.cs:13` (`tickets:manage`) | só `serverNowUtc` | Chamada só para ler a hora do servidor; os contadores são descartados (FE-10, FE-23) |
| `TicketDataService.getMineSummary` (`:198`) | `GET tickets/mine/summary` | `Tickets/GetOwnSummary.cs:13` (`tickets:view-own`) | priority, status, count | — (somas por grupo no front, A-18) |
| `TicketDataService.getStatusCounts` (`:203`) | `GET tickets/stats/status` | `Tickets/GetStatusCounts.cs:14` (usuário ativo, RN-35) | totais por status | Não sai com as flags atuais (quem atende usa o agregado; para os demais, `companyStatsForAll=false`); o comentário do Store diz que exige `tickets:manage` (`tickets/application/ticket-summary.store.ts:68-72`) (FE-24) |
| `TicketDataService.getVolume` (`:212`) | `GET tickets/stats/volume?groupBy=Day, Week ou Month` (mais recorte e `createdFrom`/`createdTo`) | `Tickets/GetVolume.cs:19` (usuário ativo; recorte exige `tickets:manage`) | periodStart, count, resolvedCount | Regra dos 366 pontos repetida no front (FE-25); quem não atende a fila não recebe o volume da empresa por causa da flag (FE-24) |
| `TicketMessageDataService.getAll` (`tickets/infrastructure/ticket-message.data.service.ts:31`) | `GET tickets/{id}/messages` (cursor) | `TicketMessages/GetAll.cs:14` | id, authorUserId, content, createdAtUtc, editedAtUtc | Sem nome do autor: o front infere "Você", o solicitante, o responsável atual ou "Equipe" (FE-20) |
| `TicketMessageDataService.post` (`:36`) | `POST tickets/{id}/messages` `{content}` | `TicketMessages/Post.cs:18` | id (descartado) | — (o front recarrega mensagens, histórico e SLA) |
| `TicketMessageDataService.edit` (`:41`) | `PUT ticket-messages/{id}` `{content}` | `TicketMessages/Edit.cs:18` | — | — |
| `TicketMessageDataService.delete` (`:46`) | `DELETE ticket-messages/{id}` | `TicketMessages/Delete.cs:13` | — | — (nem o back nem o front conferem chamado ativo: F-29 e Q-02 da 049) |
| `TicketAttachmentDataService.getAll` (`tickets/infrastructure/ticket-attachment.data.service.ts:30`) | `GET tickets/{id}/attachments` (cursor) | `TicketAttachments/GetAll.cs:14` | id, uploadedByUserId, fileName, fileSizeBytes, uploadedAtUtc | — |
| `TicketAttachmentDataService.getPolicy` (`:35`) | `GET ticket-attachments/policy` | `TicketAttachments/GetPolicy.cs:13` | maxFileSizeBytes, allowedTypes[].contentType | — (modelo a seguir, A-39); `maxAttachmentsPerTicket` e `maxTotalBytesPerTicket` sem uso (C-09); as extensões não são conferidas no front |
| `TicketAttachmentDataService.upload` (`:42`) | `POST tickets/{id}/attachments` (multipart `file`) | `TicketAttachments/Upload.cs:20` (limite `attachment-upload`) | id (descartado) | — |
| `TicketAttachmentDataService.download` (`:47`) | `GET ticket-attachments/{id}/download` (blob) | `TicketAttachments/Download.cs:13` | conteúdo | — (o nome vem da lista, não do `Content-Disposition`; o arquivo passa inteiro pela memória do navegador, porque o Bearer impede link direto) |
| `TicketAttachmentDataService.delete` (`:52`) | `DELETE ticket-attachments/{id}` | `TicketAttachments/Delete.cs:13` (limite `attachment-delete`) | — | — |
| `AuthDataService.login` (`shared/infrastructure/auth.data.service.ts:20`) | `POST users/login` `{email, password}` | `Users/Login.cs:16` (anônima; `authentication`) | accessToken, refreshToken | Tokens no `localStorage` (spec 046). 403 `Users.PasswordChangeRequired` comparado por `title` (`shared/application/login.store.ts:15-23`) |
| `AuthDataService.changeRequiredPassword` (`:25`) | `POST users/login/new-password` `{email, currentPassword, newPassword}` | `Users/ChangeRequiredPassword.cs:16` | tokens | Exige a senha provisória: o front a guarda em memória entre as telas (FE-26). 409 `Users.PasswordChangeNotRequired` por `title` |
| `AuthDataService.activateAccount` (`:30`) | `POST users/activation` `{token, newPassword}` | `Users/ActivateAccount.cs:19` | — | — (400 `Users.InvalidActivationToken` por `title`) |
| `AuthDataService.register` (`:35`) | `POST users/register` `{email, firstName, lastName}` | `Users/Register.cs:18` (`users:create`) | id | Seguido de `PUT users/{id}/roles`, porque o cadastro não aceita funções: duas chamadas, sem atomicidade (FE-13) |
| `AuthDataService.refresh` (`:40`) | `POST users/refresh-token` `{refreshToken}` | `Users/Refresh.cs:16` | tokens | Spec 046 (refresh em cookie) |
| `AuthDataService.getCurrentUser` (`:45`) | `GET users/me` | `Users/GetCurrent.cs:13` (token válido) | id, email, firstName, lastName, roles[].name, permissions | Usuário desativado recebe 403 `Users.Deactivated`; fora dos guards, o front descarta esse resultado (FE-08) |
| `AuthDataService.logout` (`:50`) | `POST users/logout` `{refreshToken}` | `Users/Logout.cs:15` (token válido) | — | Com o access token vencido, o 401 não renova (é rota de autenticação) e a sessão não é revogada no servidor (DEC-6 da 046) |
| `UserDataService.getStatusCounts` (`users/infrastructure/user.data.service.ts:21`) | `GET users/stats/status` | `Users/GetStatusCounts.cs:13` (`users:read`) | 4 totais | — |
| `UserDataService.getById` (`:26`) | `GET users/{id}` (id lido do `sub` do JWT) | `Users/GetById.cs:13` (o próprio ou `users:read`) | id, email, firstName, lastName | — (setor e convite sem uso) |
| `UserDataService.updateProfile` (`:31`) | `PUT users/{id}/profile` `{firstName, lastName}` | `Users/UpdateProfile.cs:15` | — | Mínimo de 2 caracteres só no front |
| `UserDataService.changePassword` (`:36`) | `PUT users/me/password` `{currentPassword, newPassword}` | `Users/ChangePassword.cs:15` (`authentication`) | — | O back revoga também a sessão de quem trocou; o front diz "suas demais sessões" e mantém os tokens revogados (FE-07) |
| `UserDataService.getAll` (`:58`) | `GET users?page&pageSize&status=All, Active ou Inactive&search&createdFrom&createdTo` | `Users/GetAll.cs:14` (`users:read`) | id, email, firstName, lastName, roles, isActive, createdAtUtc, deactivatedAtUtc, pendingActivation, invitationExpiresAtUtc | Sem uso: `sectorId`, `sectorName`, `departmentName`, `sectorIsActive` e os filtros `roleName`, `withoutRole`, `sortBy`, `sortDirection` (spec 018). Também serve a busca de responsável de departamento, que passa a exigir `users:read` (FE-16) |
| `UserDataService.assignRoles` (`:63`) | `PUT users/{id}/roles` `{roleNames}` | `Users/AssignRoles.cs:16` (`users:assign-roles`) | — | Funções por nome (a 036 passa a id, com transição) |
| `UserDataService.deactivate` (`:68`) | `PUT users/{id}/deactivate` | `Users/Deactivate.cs:13` (`users:deactivate`) | — | Lote: uma requisição por pessoa, até 100, contra o limite global de 100 por minuto (FE-03) |
| `UserDataService.countOpenAssignedTickets` (`:85`) | `GET tickets?assignedToUserId&status=1&status=2&page=1&pageSize=1` | `Tickets/GetAll.cs:17` (`tickets:manage`) | `totalCount` | Rota de chamados dentro do Data Service de usuários; uma por pessoa; exige uma permissão que a tela não exige; status como números literais (FE-03) |
| `UserDataService.resendInvitation` (`:91`) | `POST users/{id}/invitation` | `Users/ResendInvitation.cs:13` (`users:create`) | — | — |
| `UserDataService.activate` (`:96`) | `PUT users/{id}/activate` | `Users/Activate.cs:13` (`users:deactivate`) | — | O "Desfazer" do lote repete o fan-out (FE-03) |
| `DepartmentDataService.getAll` (`departments/infrastructure/department.data.service.ts:17`) | `GET departments` | `Departments/GetAll.cs:13` (usuário ativo) | id, name, description, responsibleUserName, sectors[] | Sem `responsibleUserId`: editar exige o detalhe (spec 023, PR #52) |
| `DepartmentDataService.getById` (`:22`) | `GET departments/{id}` | `Departments/GetById.cs:13` | responsibleUserId | Chamada só para saber o id do responsável (023); `isActive` sem uso |
| `DepartmentDataService.create` (`:27`) | `POST departments` `{name, description, responsibleUserId?}` | `Departments/Create.cs:20` (`departments:manage`) | id | — |
| `DepartmentDataService.update` (`:32`) | `PUT departments/{id}` (mesmo corpo) | `Departments/Update.cs:20` | — | Substituição completa: sem `responsibleUserId`, o responsável é apagado (por isso o detalhe) |
| `DepartmentDataService.delete` (`:37`) | `DELETE departments/{id}` | `Departments/Deactivate.cs:13` (desativa, com os setores) | — | O front diz "Excluir… Essa ação não pode ser desfeita" (FE-21) |
| `SectorDataService.create` (`departments/infrastructure/sector.data.service.ts:17`) | `POST sectors` `{name, description?, departmentId}` | `Sectors/Create.cs:20` (`sectors:manage`) | id | — |
| `SectorDataService.update` (`:22`) | `PUT sectors/{id}` `{name, description?}` | `Sectors/Update.cs:19` | — | — |
| `SectorDataService.delete` (`:27`) | `DELETE sectors/{id}` | `Sectors/Deactivate.cs:13` (desativa) | — | Idem departamento (FE-21) |
| `RoleDataService.getAll` (`roles/infrastructure/role.data.service.ts:15`) | `GET roles` | `Roles/GetAll.cs:13` (`roles:read`) | name, permissionCodes, isDefault, isAdministrator | — (a rota da tela exige `roles:manage`, A-21) |
| `RoleDataService.assignPermissions` (`:20`) | `PUT roles/{roleName}/permissions` `{permissionCodes}` | `Roles/AssignPermissions.cs:19` (`roles:manage`) | — | Função pelo nome (036) |
| `PermissionDataService.getAll` (`roles/infrastructure/permission.data.service.ts:15`) | `GET permissions` | `Permissions/GetAll.cs:13` (usuário ativo) | code, name, description, group, isAdministrative | — |

Os caminhos curtos da terceira coluna são de `Web.Api/Endpoints/`.

**Rotas do back sem chamada no front** (7 de 61, mais `GET health`): `POST tickets/{id}/close` (deveria ser usada, FE-01); `GET users/by-email`; `GET users/attendants` (mesma regra de elegibilidade de `GET tickets/assignees`, RN-28, com outra permissão e outro formato); `PUT users/{id}/sector` (sem tela: os usuários ficam sem setor, e a prioridade por setor dos atendentes não tem efeito, C-10); `GET sectors` e `GET sectors/{id}` (os setores vêm aninhados em `GET departments`); `GET audit-logs/{entityName}/{entityId}`. Duas estão declaradas e nunca disparam: `DELETE tickets/{id}` e, com as flags atuais, `GET tickets/stats/status`.

**Composição das telas.** Nenhum comando devolve o estado novo (204 ou só o id), então cada mutação é seguida das leituras que a tela mostra.

| Tela | Chamadas na carga | Depois de uma ação | Observação |
|---|---|---|---|
| Login e Início | `POST users/login`, `GET users/me` | — | primeiro acesso: `POST users/login/new-password` com a senha guardada (FE-26) |
| Início, quem atende a fila | `GET departments`, `GET tickets/assignees` (100), `GET tickets/stats/overview` duas vezes (período e anterior), `GET tickets/stats/times`, `GET tickets/stats/volume` (empresa), `GET tickets` duas vezes (vencidos e em risco) e `GET tickets/sla-summary` (relógio) | a cada 2 min, as mesmas, menos departamentos e responsáveis (`dashboard/dashboard-auto-refresh.ts:10`) | 8 a 9 requisições; o departamento do filtro vira a lista dos setores ativos (FE-12) |
| Início, quem abre chamados | `GET tickets/mine/summary` | — | os números da empresa ficam desligados pela flag, embora a RN-35 já os libere (FE-24) |
| Meus Chamados | `GET tickets/mine` (e a sondagem `pageSize=1`), `GET departments` | editar ou cancelar: `PUT tickets/{id}` e a lista; reabrir: `POST …/reopen` e a lista | cancelar reenvia a descrição da lista (FE-02) |
| Detalhe | `GET tickets/{id}`, `…/history`, `…/attachments`, `…/messages`, `GET ticket-attachments/policy` | mensagem: mensagens, histórico e `GET tickets/{id}/sla`; status: detalhe e histórico | 5 chamadas na abertura |
| Monitorar | `GET tickets`, `GET tickets/stats/overview` (recorte sem status, SLA nem busca), `GET departments`, `GET tickets/assignees`; painel: `GET tickets/{id}` | atribuir: a lista | o painel lê o detalhe inteiro para a descrição (FE-28) |
| Usuários | `GET users`, `GET users/stats/status`, `GET roles` (só com `roles:read`) | desativar: `GET tickets` e `PUT …/deactivate` por pessoa; cadastrar: `POST users/register` e `PUT users/{id}/roles` | um lote de 100 chega a 300 requisições com o "Desfazer" (FE-03) |
| Departamentos | `GET departments`; editar: `GET departments/{id}`; responsável: `GET users?search` | mutação: a lista | detalhe só pelo id do responsável (023) |
| Funções | `GET roles`, `GET permissions` | `PUT roles/{roleName}/permissions` e a lista | por nome até a 036 |
| Perfil | `GET users/{id}` (id do JWT) | `PUT users/{id}/profile`; `PUT users/me/password` | a troca de senha derruba a própria sessão depois (FE-07) |

**Sessão, erros e novas tentativas (como funciona hoje).**
- **Tokens:** login, troca obrigatória e renovação devolvem `{accessToken, refreshToken}` sem expiração; o front grava os dois no `localStorage` (`shared/services/session.service.ts:85-93`), lê `sub` e `email` do JWT (`:46-60`), aplica nesta aba os tokens gravados por outra pelo evento `storage` (A-01) e recarrega a página se a conta mudou (A-35).
- **401:** o interceptor renova uma vez por aba (as requisições que vencem juntas compartilham a renovação) e repete; se o token já mudou enquanto a requisição estava em voo, repete sem renovar (A-03); recusa da renovação limpa a sessão e vai ao login; falha passageira (0, 429, 5xx) mantém a sessão e repassa o erro da renovação (A-43) (`shared/interceptors/auth.interceptor.ts:25-53`).
- **403 fora das rotas de autenticação:** recarrega `GET users/me`, porque as permissões podem ter mudado; o resultado não é usado (`:55-59`).
- **`GET users/me`:** novas tentativas em 1, 2, 4 e 8 s numa falha passageira (o back não manda `Retry-After`: `Web.Api/Extensions/RateLimitingExtensions.cs:26-32` não tem `OnRejected`); persistindo, `/sessao-indisponivel` (A-42).
- **Mensagens de erro:** `extractErrorMessage` junta todas as `errors[].description` numa frase, senão usa `detail`, senão `title` (`shared/utils/http-error.util.ts:20-28`). O `title` é comparado com códigos em quatro pontos (`login.store.ts:15-23`, `activate-account.store.ts:9,38`, `ticket-detail.store.ts:264`).
- **Logout:** `POST users/logout` com o refresh token, que exige access token válido; com ou sem sucesso, a sessão local é apagada (`shared/application/login.store.ts:143-159`).

### 2.13.3 Regras de negócio duplicadas no front

Situação: **espelho fiel** (mesma regra do back, só para decidir o que mostrar), **espelho divergente** (a regra do front é outra), **só no front** (não existe no back), **apresentação** (usa números ou datas do servidor; não decide nada) e **modelo** (o front lê a regra do back).

| # | Regra | Onde no front | Fonte no back | Situação | Destino |
|---|---|---|---|---|---|
| 1 | Transições de status | `tickets/entities/ticket-status.ts:38-44` | `Domain/Tickets/Ticket.cs:376-389` (`CanTransitionTo`) e as rotas `PUT tickets/{id}`, `POST …/close`, `POST …/reopen` | espelho divergente: a tabela está certa, mas não diz a rota, e Fechado vai ao `PUT` e falha | 047 (DEC-3 dela); DEC-60 |
| 2 | Status ativos (Aberto e Em andamento) | `tickets/entities/ticket-status.ts:29`; `users/infrastructure/user.data.service.ts:78-84` (números 1 e 2) | `Ticket.cs:48` (`IsActive`) | espelho fiel; números literais no Data Service de usuários | 047; 034 (contagem) |
| 3 | Editar a descrição | `tickets/ui/ticket-action-permissions.ts:22-25` | RN-06; `Application/Tickets/Update/UpdateTicketCommandHandler.cs:35-54` | espelho fiel | 047 |
| 4 | Reabrir | `ticket-action-permissions.ts:28-31` | RN-06; `ReopenTicketCommandHandler.cs` com `TicketAccessGuard` | espelho fiel | 047 |
| 5 | Atribuir a si, a outra pessoa e remover o responsável | `ticket-action-permissions.ts:34-38`; `feature-ticket-detail/ticket-detail.component.ts:412-419`; `feature-ticket-monitor/ticket-monitor.component.ts:582-600` | RN-22, RN-23; `AssignTicketCommandHandler.cs:37,65` | espelho fiel | 047 |
| 6 | Mudar status só com `tickets:manage` (exceto cancelar) | `ticket-detail.component.ts:439-450` | RN-06; `UpdateTicketCommandHandler.cs:66-69` | espelho fiel | 047 |
| 7 | Janela de cancelamento do dono | `tickets/ui/ticket-cancel-window.ts:13-27` | RN-32; `canCancel` e `ownerCancelDeadlineUtc` | apresentação: o relógio só esconde o botão quando a janela vence (A-13) | manter |
| 8 | Editar e excluir mensagem; excluir anexo | `ticket-detail.component.ts:207-220` | RN-08; exclusão de anexo por quem enviou ou `tickets:manage` (4.3.1) | espelho fiel; nenhum dos dois lados confere chamado ativo nem reconfere o acesso | 047 (DEC-5 dela); Q-01 e Q-02 da 049 |
| 9 | Situação de SLA "ao vivo" entre recargas | `feature-ticket-monitor/ticket-monitor.component.ts:557-569`; `dashboard/dashboard-insights.component.ts:92-112` | `Ticket.cs:403-426` (`GetSlaStatus`) | apresentação ancorada no `serverNowUtc` (A-14), mas parcial: só promove a "Atrasado" pelo prazo, não a "Em risco" pelo aviso (`slaWarningAtUtc` vem e não é usado); e a hora vem de outra rota | DEC-66 |
| 10 | Prazo relativo do detalhe sem o painel de SLA | `ticket-detail.component.ts:302-310` | — | usa o relógio do navegador e o prazo do 1º ciclo (A-31) | 024 e 048; `serverNowUtc` do bloco `sla` |
| 11 | Ciclo vigente nas listas | `feature-ticket-list/ticket-list-due.ts:68-80`; `tickets/entities/ticket.ts:22-29` | cronômetros por ciclo | fallback à espera da 024 | 024 e 048 |
| 12 | Limite de pontos do gráfico (366) e janelas | `tickets/application/ticket-dashboard.store.ts:17-40`; `dashboard/dashboard-overview.ts:156` (`bucketRange`) | `Application/Tickets/GetVolume/TicketVolumePeriods.cs:15-40,62-69` | espelho divergente: a semana é contada por `Math.ceil(dias / 7) + 1`, e o back trunca na segunda-feira | registro (FE-25) |
| 13 | Recorte por departamento | `dashboard/dashboard-filters.ts:106-130` | não existe: os filtros só aceitam `sectorId` | só no front, com efeito nos números (setores desativados ficam fora) | DEC-67 |
| 14 | "Hoje" e períodos em São Paulo | `shared/utils/iso-date.util.ts:8-11`; `dashboard/dashboard-filters.ts:99-104` | fuso fixo `America/Sao_Paulo` (C-13) | espelho fiel | manter, como constante do produto |
| 15 | Limites de texto | `feature-ticket-create/ticket-create.component.ts:35-36`; `ui/ticket-edit-dialog/ticket-edit-dialog.component.ts:47`; `tickets/entities/ticket-message.ts:12`; `ui/ticket-reopen-dialog/ticket-reopen-dialog.component.ts:6`; `departments/feature-department-list/department-list.component.ts:195-202`; `users/entities/user-profile.ts:2`; `users/feature-user-list/user-list.component.ts:121-129` | só no banco desde a 042 (`Infrastructure/Tickets/TicketConfiguration.cs:20-30`, `Departments/DepartmentConfiguration.cs:15-23`, `Users/UserConfiguration.cs:15-24`); o motivo vale o limite da mensagem, 4000 (RN-33) | espelho fiel, menos o motivo (500) e o mínimo de 2 no nome, que só o front exige | DEC-62 (limites no contrato); T-512 |
| 16 | Política de senha | `shared/utils/password.validators.ts:4-29` | RN-13 (validators de senha) | espelho fiel (A-22) | manter |
| 17 | Política de anexos | `ticket-detail.component.ts:566-585` | `GET ticket-attachments/policy` | modelo: o front lê a política do back | manter |
| 18 | Não tirar a função de Administrador da própria conta | `user-list.component.ts:546-555,565-567` | não existe: o back só protege o último administrador (`Application/Users/AssignRoles/AssignUserRolesCommandHandler.cs:69-80`, RN-18) | só no front | Q-64 |
| 19 | Chamados continuam atribuídos a quem é desativado (aviso) | `user-list.component.ts:626-662`; `users/application/users.store.ts:123-151` | o back não devolve os chamados à fila; não há contagem por pessoa | só no front, com uma chamada por pessoa | DEC-64; 034 |
| 20 | "Persona" inferida das permissões | `dashboard/dashboard.component.ts:149-168` | permissões de `GET users/me` | apresentação; o menu e as rotas não seguem a mesma regra (FE-14) | T-512 |
| 21 | Funções padrão no cadastro | `user-list.component.ts:412` (`isDefault`) | RN-30 (o cadastro já dá Colaborador) | espelho reaplicado numa segunda chamada | DEC-64 |
| 22 | Grupos de Meus Chamados somados do resumo | `tickets/application/my-tickets-summary.store.ts:46-71` | `OwnTicketStatusGroup` | apresentação (A-18) | manter |
| 23 | Função Administrador só leitura; aviso de permissão administrativa | `roles/feature-role-list/role-list.component.ts:44-45,188-194` | `RoleResponse.IsAdministrator`, `PermissionResponse.IsAdministrative`; `RoleErrors.AdministratorRoleIsManaged` | modelo: a decisão vem do back | manter |

### 2.13.5 Modelo-alvo da integração front ↔ API

Princípios:
1. **A API decide; o front mostra.** Toda regra de negócio que hoje o front espelha passa a vir na resposta (ações e próximos status, ciclo vigente, hora do servidor, políticas). O front fica com apresentação: relógio "ao vivo" ancorado no servidor, agrupamentos de exibição e validação de experiência com limites vindos do contrato. A política de anexos (`GET ticket-attachments/policy`) e as flags `isAdministrator`/`isAdministrative` são os modelos que já existem.
2. **Comando por intenção, sem regravar estado.** Cada ação usa a rota que a executa (`close`, `reopen`, atribuição); o `PUT tickets/{id}` aceita só o campo que muda. Versão do chamado numa spec de contrato.
3. **Leitura composta no servidor.** Contagens, lotes e recortes que hoje o front monta com várias chamadas passam a uma consulta ou a um comando.
4. **Contrato de erro estável.** `title` = código, `detail` = texto pt-BR sem ids técnicos, `errors[].propertyName`, código também nas recusas de autorização, `Retry-After` no 429, catálogo dos códigos testado.
5. **Contrato verificável.** Snapshot do OpenAPI no back; teste de Data Service para cada chamada no front. Enums continuam numéricos (os dois lados concordam hoje em todos os valores).

| Tema | Hoje | Alvo | Onde |
|---|---|---|---|
| Ações e transições do chamado | espelho no front, sem a rota de cada transição | a API informa ações e próximos status; o front mapeia ação → rota | 047; DEC-60 |
| Mudança do chamado | `PUT` com estado completo, sem versão | `PUT` parcial; versão depois | DEC-61 |
| SLA nas listas | 1º ciclo; hora do servidor só em outras rotas | ciclo vigente e `serverNowUtc` na própria resposta | 024, 048, DEC-66 |
| Erros | `title` com código ou texto; `errors[]` sem campo; 403 de rota sem código; 429 sem `Retry-After`; textos com GUID | ProblemDetails uniforme e catálogo testado | DEC-62; Q-05 da 049 |
| Leituras compostas | contagem por pessoa, lote, departamento → setores, relógio por outra rota | consulta agregada (034), lote, `departmentId`, hora na listagem | DEC-64, DEC-66, DEC-67 |
| Sessão | tokens no `localStorage`; JWT decodificado; troca de senha derruba a sessão sem aviso; logout exige access token | cookie `HttpOnly`; `userId` de `users/me`; troca de senha com sessão nova; logout pelo cookie | 046; DEC-63 |
| Validação | limites repetidos no front; o banco recusa sem dizer o campo | limites publicados no contrato a partir da mesma constante | DEC-62 |
| Permissões na tela | parte das telas esconde ações sem permissão | toda ação visível só com a permissão da rota; regras por recurso pela API | T-512; 047; Q-64 |
| Rotas | 7 sem consumidor, 2 declaradas sem disparo, 2 pares sobrepostos | obsoletas marcadas e removidas numa limpeza | DEC-69 |
| Verificação | tipos escritos à mão; 8 de 55 chamadas testadas | snapshot do OpenAPI e Data Services testados | DEC-68 |

### 2.13.6 Não determinado

- **Corpo das respostas 401 e 403 da autorização de rota** (não executado): se é ProblemDetails, não traz código de qualquer forma (FE-08). Já registrado na 2.2.
- **Mudanças não commitadas do front** (`auth.interceptor.ts`, `current-user.service.ts`, `department.data.service.ts`, `dialog.component.ts` e outras): não lidas; podem já alterar o FE-08 ou o FE-16.
- **Outro consumidor das rotas sem uso no front** (`users/attendants`, `users/by-email`, `sectors`): o front é o único cliente conhecido; a confirmação é do responsável (DEC-69).
- **Volume real:** quantos responsáveis elegíveis existem (o filtro carrega 100) e quantos usuários se desativam de uma vez. O limite global e o fan-out do FE-03 são fato; a frequência, não.
- **Frequência do FE-02:** depende de edição concorrente; a regravação é comprovada pela lógica, sem medição.
- **`POST …/reopen` com corpo `null` pelo `fetch` do Angular:** o back aceita sem corpo (spec 016), mas o front não tem teste de Data Service dessa chamada.
- **`Jwt:ExpirationInMinutes` em produção:** define quanto tempo depois da troca de senha a sessão cai (FE-07); só o valor de Development (60) está no repositório.

### 2.13.7 Fases da área

- **Fase FE-0 — Correções só no front, sem depender do back:** T-500, a parte de T-504 que não depende da DEC-62 (código atual e conta desativada), T-512, T-514 e T-517. Sem dependência; pode correr em paralelo com a fase 1 da 049.
- **Fase FE-1 — Contrato aditivo do back:** T-501, T-503, T-505, T-508, T-513 e T-516. Depende das DEC-61, DEC-62, DEC-63, DEC-65, DEC-68 e DEC-69; não tira campo nem rota, então o front atual continua funcionando. T-503 é a spec da Q-05 da 049; T-505 anda com a 046.
- **Fase FE-2 — Leituras e comandos compostos no servidor:** T-509 (dentro da 048 e da 024), T-510 (dentro da 043p1), a consulta da 034 e T-506 (com a 036). Depende da ordem da DEC-4 da 049 e dessas specs.
- **Fase FE-3 — Front consumindo o contrato novo:** T-502, T-507, T-511 e o restante de T-504. Depende das fases FE-1 e FE-2 publicadas.
- **Fase FE-4 — Specs já planejadas que mudam o front:** 047 (T-515), 046 (sessão), 036 (funções por id), 024 e 048 (ciclo vigente), 044 e 037. Cada uma traz a sua rodada do front, depois da entrega do back.
