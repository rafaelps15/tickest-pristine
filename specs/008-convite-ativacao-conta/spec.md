---
id: 008
titulo: Convite por e-mail e ativação da conta pelo próprio usuário
status: Rascunho        # Rascunho | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Cancelada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em:
branch: feat/SPEC-008-user-invitation-activation
backlog:
---

# 008 — Convite por e-mail e ativação da conta pelo próprio usuário

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat, em 2026-10-24: "agora esse arquivo vai ser uma spec do projeto tickest - Insira na pasta das specs seguindo o modelo para desenvolvimento ok", sobre o documento "Especificação de Requisitos — Gestão de Acessos, Onboarding e Autenticação"; e, em seguida, "Essa documentação pode ficar lá como fonte dentro da pasta fonte para essa spec. Ok ?".
- **Fonte:** `specs/_fontes/gestao-acessos-onboarding.md` (gravado em 2026-10-24, SHA-256 `b1984631232f9615a91e55c46e7d7f204a9b9eae71caa75bddd788ed0d4f81eb`). Os identificadores RF-xx, RNF-xx, N-xx e D-xx citados nesta spec são dessa fonte.
- **Recorte desta spec (DEC-1):** a fonte é uma especificação de produto inteira (pré-cadastro com CPF e matrícula, perfil RH, convite, ativação, login por matrícula, recuperação de senha, perfil do colaborador, ciclo de vida, LGPD, MFA). Ela não cabe numa spec só. Esta spec cobre o núcleo de maior risco: **nenhuma senha definida por terceiros** (R-02, L-01). O administrador cadastra o usuário sem senha, o sistema envia um convite por e-mail com link de uso único, e o próprio usuário define a senha (RF-01.6, RF-02.1 a RF-02.5, RF-03, N-01).
- **Referências:**
  - `SYSTEM_SPECIFICATION.md`: 2.6 (RN-12, RN-16, RN-19), 2.7 (C-01 "não há convite por e-mail", C-21 "não há mecanismo de envio"), 4.3.1 (rotas de `users`), 4.6 (senhas e tempo constante no login), 7.1 (GAP-05: eventos em memória, sem outbox).
  - `specs/BACKLOG.md`: BL-015 (outbox, ainda sem spec) e BL-021 (revogação do access token).
  - Código: `src/TickestPristine.Application/Users/Register/*` (cadastro atual, com senha definida por quem cadastra), `Users/UserCredentialsVerifier.cs` (login, bloqueio por tentativas), `Users/UserPasswords.cs`, `Users/RefreshTokenHasher.cs` (modelo do hash do token), `Users/Activate/*` e `Users/Deactivate/*`, `Domain/Users/UserCredential.cs` e `RefreshToken.cs`, `Infrastructure/Audit/AuditLoggingInterceptor.cs` (`ExcludedProperties`), `Infrastructure/Database/Seeding/AdminUserSeeder.cs`, `Web.Api/Endpoints/Users/Register.cs` e `Login.cs`, `Web.Api/Infrastructure/RateLimitingPolicies.cs`.
- **Fatos do repositório que afetam a fonte:**
  - O cadastro já não é público: `POST users/register` exige `users:create` (GAP-03 resolvido). Atende R-01.
  - O cadastro recebe a senha no corpo e a grava; quem cadastra conhece a senha do usuário. É exatamente o risco L-01 da fonte.
  - Não existe envio de e-mail no sistema (C-21), nem outbox (GAP-05, BL-015). A fonte pede outbox (RF-01.6, RNF-IDP-02): ver DEC-3.
  - Não existe estado "pendente de ativação". O usuário tem só `IsActive` e `DeactivatedAtUtc`; a credencial fica em `UserCredential`, com `MustChangePassword` usado hoje só pelo administrador inicial (RN-16).
  - Quando o usuário existe mas não tem credencial, `UserCredentialsVerifier` devolve `InvalidCredentials` **sem** gastar o tempo de um hash. Hoje isso nunca acontece; com contas pendentes, o tempo de resposta revelaria quem foi convidado (RF-04.3). Tratado no REQ-9.
  - O bloqueio por tentativas (5 em 15 min) já existe (RF-05.1). O login já distingue `LockedOut`, `Deactivated` e `PasswordChangeRequired` depois da senha correta, ao contrário da RF-04.3: fica fora desta spec (DEC-1).
  - O `AuditLoggingInterceptor` audita entidades `IAuditable` e já exclui `PasswordHash` da serialização. O hash do token de convite precisa da mesma exclusão (REQ-12).
  - Não há role RH: as roles fixas são Administrador, Atendente e Colaborador (`RoleNames.cs`). Quem tem `users:create` cadastra.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | `POST users/register` (permissão `users:create`) passa a receber só e-mail, nome e sobrenome, sem senha. Cria o usuário ativo (`IsActive = true`), com a role Colaborador, **sem** `UserCredential`, e um convite de ativação. A conta fica pendente de ativação enquanto não tiver credencial. | pedido (RF-01.6, R-02, L-01); DEC-4; DEC-5 |
| REQ-2 | O convite guarda só o hash SHA-256 de um token aleatório de 32 bytes (256 bits) gerado por CSPRNG; o token em claro só existe no e-mail. Vale 72 horas a partir da emissão e é de uso único. Um usuário tem no máximo um convite utilizável: emitir um novo revoga os anteriores. | pedido (RF-03.1, RF-03.2, RF-02.2); DEC-7 |
| REQ-3 | Depois de gravar o cadastro, o sistema envia o e-mail de convite (N-01) ao e-mail do usuário, com saudação pelo nome, o código do usuário, o e-mail de login, o link de ativação com o token no fragmento (`#token=`), a data e a hora de expiração no horário de Brasília e a orientação de ignorar se não reconhecer. O e-mail não contém senha. | pedido (RF-02.1, RF-03.3, N-01); DEC-2; DEC-8 |
| REQ-4 | Falha no envio do e-mail não desfaz o cadastro: é registrada no log com nível `Error` (sem o token) e o cadastro responde com sucesso. O convite continua válido no banco, e o administrador pode reenviá-lo (REQ-5). | DEC-3 |
| REQ-5 | `POST users/{id}/invitation` (permissão `users:create`) reemite o convite de um usuário ativo e pendente de ativação, revogando os anteriores, e envia novo e-mail. Recusa usuário inexistente (404), usuário que já tem credencial (409), usuário desativado (409) e o sexto convite emitido para o mesmo usuário em 24 horas (409). Respeita o `PrivilegeEscalationGuard`. | pedido (RF-02.2, RF-02.3); DEC-10 |
| REQ-6 | `POST users/activation` (anônima, rate limit `authentication`) recebe o token e a nova senha. Com token válido, cria a credencial (hash da senha, novo `SecurityStamp`, `MustChangePassword = false`), marca o convite como usado e não abre sessão: devolve 204 e o usuário faz login em seguida. A senha segue a política atual (`StrongPassword`). | pedido (RF-03.5, RF-03.6, RF-03.7); DEC-6 |
| REQ-7 | Token inexistente, expirado, já usado ou revogado, ou de usuário desativado ou que já tem credencial, recebe sempre a mesma falha: `Users.InvalidActivationToken`, status 400, mensagem "Link inválido ou expirado. Solicite um novo convite ao administrador." | pedido (RF-03.4) |
| REQ-8 | Duas ativações simultâneas com o mesmo token resultam em exatamente uma credencial; a outra requisição não recebe 2xx. | pedido (RF-03.8, RNF-IDP-04) |
| REQ-9 | O login de um usuário pendente de ativação falha como credenciais inválidas (`Users.InvalidCredentials`) e gasta o tempo de uma conferência de hash, como já acontece com e-mail inexistente. | pedido (RF-04.2, RF-04.3); `SYSTEM_SPECIFICATION.md` 4.6 |
| REQ-10 | Desativar um usuário pendente revoga os convites dele. Reativar (`PUT users/{id}/activate`) um usuário que nunca ativou a conta o deixa pendente e sem convite utilizável: o administrador precisa reenviar (REQ-5). | pedido (RF-02.5) |
| REQ-11 | `GET users/{id}` e `GET users` passam a informar se a conta está pendente de ativação (`pendingActivation`) e, quando estiver, a expiração do último convite utilizável (`invitationExpiresAtUtc`, nulo se expirado ou revogado). | pedido (RF-02.7) |
| REQ-12 | Emissão, uso e revogação de convite passam pela auditoria existente, sem gravar o hash do token. O token em claro não aparece em log, URL, resposta da API nem auditoria. | pedido (RNF-AUD, RNF-SEG-05) |
| REQ-13 | O administrador inicial continua como hoje: criado pelo seed com senha provisória e troca obrigatória em `POST users/login/new-password` (RN-16). | DEC-9 |

## 3. Critérios de aceitação
"Pendente" = usuário ativo sem `UserCredential`. "E-mail capturado" = mensagem recebida pelo `IEmailSender` substituto dos testes.

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | administrador autenticado | `POST users/register` com e-mail, nome e sobrenome, sem senha | 200 com o Id; o usuário existe com `IsActive = true` e role Colaborador; não existe `UserCredential` dele; existe um convite com `ExpiresAtUtc` = criação + 72 h |
| AC-2 | REQ-1 | administrador autenticado | `POST users/register` com o campo `password` no corpo | o campo é ignorado (contrato sem senha); nenhuma credencial é criada |
| AC-3 | REQ-2, REQ-12 | um convite recém-emitido | lê a tabela de convites e a auditoria | a coluna do token tem 64 caracteres hexadecimais (SHA-256) e não contém o token do e-mail; nenhum registro de auditoria contém o hash nem o token |
| AC-4 | REQ-3 | administrador autenticado | cadastra um usuário | exatamente um e-mail capturado, para o e-mail cadastrado, contendo o nome, o código `usr_…`, um link que termina em `#token=<token>` e a expiração em horário de Brasília; o corpo não contém a palavra "senha:" seguida de valor nem o hash |
| AC-5 | REQ-4 | o `IEmailSender` dos testes configurado para lançar exceção | cadastra um usuário | 200 com o Id; o usuário e o convite estão gravados; o log tem um evento `Error` do envio que não contém o token |
| AC-6 | REQ-5 | usuário pendente com um convite | `POST users/{id}/invitation` | 204; o convite anterior fica revogado; existe um novo convite válido; um novo e-mail capturado; ativar com o token antigo dá AC-10 |
| AC-7 | REQ-5 | usuário que já ativou a conta; usuário desativado; id inexistente | `POST users/{id}/invitation` em cada um | 409 `Users.AlreadyActivated`; 409 `Users.InvitationUserDeactivated`; 404 |
| AC-8 | REQ-5 | usuário pendente com 5 convites emitidos nas últimas 24 h | `POST users/{id}/invitation` | 409 `Users.InvitationLimitReached`; nenhum convite novo nem e-mail |
| AC-9 | REQ-6 | token válido | `POST users/activation` com token e senha forte | 204, sem tokens no corpo; existe `UserCredential` com `MustChangePassword = false`; o convite tem `UsedAtUtc`; `POST users/login` com a nova senha devolve 200 com tokens |
| AC-10 | REQ-7 | um token expirado (relógio além de 72 h), um já usado, um revogado, um inexistente e o de um usuário desativado | `POST users/activation` com cada um | todos: 400 com código `Users.InvalidActivationToken` e a mesma mensagem; nenhuma credencial criada |
| AC-11 | REQ-6 | token válido | `POST users/activation` com senha fraca (ex.: `abc`) | 400 de validação; o convite continua utilizável |
| AC-12 | REQ-8 | token válido | duas chamadas simultâneas a `POST users/activation` com o mesmo token | exatamente uma 204; a outra 400 ou 409; existe uma só `UserCredential` do usuário |
| AC-13 | REQ-9 | usuário pendente | `POST users/login` com o e-mail dele e qualquer senha | 400 `Users.InvalidCredentials`, igual ao de e-mail inexistente; teste unitário confirma que o hasher foi chamado (gasto de tempo equivalente) |
| AC-14 | REQ-10 | usuário pendente com convite válido | `PUT users/{id}/deactivate`; depois `PUT users/{id}/activate`; depois tenta ativar com o token antigo | o convite fica revogado na desativação; após a reativação, `pendingActivation = true` e `invitationExpiresAtUtc` nulo; a ativação dá AC-10 |
| AC-15 | REQ-11 | um usuário pendente e um com credencial | `GET users/{id}` e `GET users` | o pendente: `pendingActivation = true` e `invitationExpiresAtUtc` preenchido; o outro: `false` e nulo |
| AC-16 | REQ-12 | o banco migrado | emite, reemite e usa um convite | a auditoria tem os registros de inclusão e de alteração dos convites, sem a coluna do hash |
| AC-17 | REQ-6 | — | 11 chamadas a `POST users/activation` do mesmo IP dentro da janela da policy `authentication` | a chamada que passar do limite recebe 429 (mesmo comportamento do login) |
| AC-18 | REQ-13 | banco novo com `Admin:*` configurado | login do administrador inicial | 403 `Users.PasswordChangeRequired`, como hoje; os testes de `DatabaseSeedingTests` passam sem alteração |
| AC-19 | todos | a mudança aplicada | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | 0 avisos; todos os testes passam |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Como dividir a fonte em specs? | (a) série: **esta spec** (convite e ativação); depois, uma spec para cada tema, numeradas quando forem criadas: recuperação de senha por e-mail (RF-06, N-03, N-04); login por e-mail ou código e mensagem única de falha (RF-04.1, RF-04.3); dados cadastrais do colaborador (CPF, cargo, telefone, endereço, contato de emergência, foto; RF-01, RF-08, RNF-LGPD); perfil RH e matriz da seção 5; o restante da fonte (política de senha de 12 caracteres, Argon2id, suspensão separada da desativação, idempotency-key, MFA, notificações N-05 a N-07) vira demandas do backlog; (b) uma spec só com tudo; (c) outra divisão | (a) — cada spec fica testável sozinha, e esta spec resolve o risco L-01 sem depender de CPF, RH ou LGPD | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-2 | Como enviar e-mail? | (a) SMTP com MailKit, abstraído por `IEmailSender` na Application; em Development, o Mailpit no `docker-compose.yml` para ver os e-mails; (b) API de um provedor (SendGrid, Amazon SES) já na primeira entrega | (a) — qualquer provedor aceita SMTP, e trocar depois é só configuração | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-3 | A fonte pede outbox (RF-01.6, RNF-IDP-02), que não existe (BL-015). Como enviar sem ela? | (a) o handler envia depois do `SaveChangesAsync`; falha vai para o log e o administrador reenvia (REQ-4, REQ-5); quando a BL-015 existir, o envio migra para a outbox; (b) fazer a BL-015 antes desta spec; (c) handler de evento de domínio em memória (o evento carregaria o token em claro) | (a) — o reenvio cobre a falha, e o token não passa por evento que um dia será persistido | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-4 | Como representar "pendente de ativação"? | (a) derivado: usuário ativo sem `UserCredential`; nenhuma coluna nova em `users`; o login já falha sem credencial; (b) nova coluna de status na entidade `User` com os seis estados da fonte (seção 3) | (a) — menor mudança; os estados SUSPENSA e CONVITE_EXPIRADO da fonte não entram agora (o expirado é derivado da data do convite, sem rotina) | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-5 | O cadastro atual com senha some? | (a) sim: o contrato de `POST users/register` perde o campo `password` (quebra o front Angular, que precisa mudar junto); (b) senha opcional: sem senha, convite; com senha, como hoje | (a) — com (b), o risco L-01 continua aberto | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-6 | Política de senha na ativação | (a) a atual (`StrongPassword`: 6 caracteres, maiúscula e especial); a de 12 caracteres da fonte (RNF-SEG-02) fica para outra spec e vale para todos os fluxos de uma vez; (b) já a da fonte, só na ativação | (a) — política diferente por fluxo confunde o usuário | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-7 | Validade e limite do convite | (a) 72 h e 5 convites por usuário em 24 h, como constantes no código (mesmo padrão de `UserCredentialsVerifier.LockoutDuration`); (b) configuráveis no `appsettings` | (a) | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-8 | Endereço do link de ativação (o front fica em outro repositório) | (a) configuração `Invitations:ActivationUrl` (ex.: `https://tickest.exemplo/ativar-conta`), validada na inicialização, e o token vai no fragmento `#token=`; (b) rota fixa no código | (a), com o valor padrão `/ativar-conta` documentado para o front; a URL fica na configuração, então mudar a rota no front não exige deploy da API | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-9 | O administrador inicial também passa a ser convidado? | (a) não: continua pelo seed com senha provisória (RN-16), porque não há e-mail garantido no primeiro deploy; (b) sim | (a) | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-10 | Quem reenvia e quem cancela convite | (a) reenviar exige `users:create`; cancelar é a desativação que já existe (`users:deactivate`), sem rota nova; (b) permissões novas (`users:invite`) | (a) | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-11 | Item do backlog e branch | (a) item novo no `specs/BACKLOG.md`; (b) sem backlog, branch `<tipo>/SPEC-NNN-slug` | (b): o backlog sai do repositório (decisão de Rafael Santos registrada na spec 007, fluxo spec-first). Branch `feat/SPEC-008-user-invitation-activation`; vale quando a spec 007 for entregue | Rafael Santos, 2026-10-24 (pedido da spec 007) |

## 5. Fora do escopo
- CPF, matrícula informada pelo RH, cargo, telefone, endereço, contato de emergência e foto (RF-01 completo, RF-08, RNF-LGPD): spec de dados cadastrais (DEC-1).
- Role RH e a matriz de permissões da seção 5 da fonte: spec do perfil RH (DEC-1). Aqui vale o modelo atual de permissões.
- Recuperação de senha por e-mail (RF-06) e notificações N-02 a N-07: spec de recuperação de senha (DEC-1). N-02 (conta ativada) fica de fora porque a ativação é feita pelo próprio titular, no mesmo instante.
- Login por código e mensagem única para todas as falhas de login (RF-04.1, RF-04.3, exceto o tempo do REQ-9): spec de login (DEC-1).
- Correção do e-mail de acesso de conta pendente (RF-02.4): hoje não há rota de alteração de e-mail; entra com a spec de dados cadastrais.
- Estados SUSPENSA e BLOQUEADA como estados da conta, bloqueio progressivo (RF-05.2), desbloqueio manual (RF-05.5).
- Política de senha da fonte (RNF-SEG-02), Argon2id (RNF-SEG-01), MFA (RNF-SEG-09), `Idempotency-Key` (RNF-IDP-01), outbox (BL-015), revogação do access token (BL-021).
- Telas: o front Angular fica em outro repositório; ele precisa da tela de ativação e de remover o campo de senha do cadastro (DEC-5).

## 6. Plano técnico
- **Arquivos a criar ou alterar:**
  - Domain, `src/TickestPristine.Domain/Users/`: `UserInvitation.cs` (`Entity`, `IAuditable`; `Id`, `UserId`, `TokenHash`, `CreatedAtUtc`, `ExpiresAtUtc`, `UsedAtUtc`, `RevokedAtUtc`); eventos `UserInvitationIssuedDomainEvent`, `UserInvitationAcceptedDomainEvent`; `UserErrors` ganha `InvalidActivationToken`, `AlreadyActivated`, `InvitationUserDeactivated`, `InvitationLimitReached`.
  - Application, `src/TickestPristine.Application/Users/`:
    - `UserInvitations.cs` (estática, no padrão de `UserPasswords`/`UserSessions`): gera o token (32 bytes de `RandomNumberGenerator`, Base64Url), grava só o hash (reuso de `RefreshTokenHasher` ou um `TokenHasher` comum), revoga os anteriores, aplica validade e limite (DEC-7) e envia o e-mail tratando a falha (REQ-4).
    - `Register/*`: command, validator e handler sem senha; cria o convite e envia.
    - `ResendInvitation/*` (command, validator, handler) com `PrivilegeEscalationGuard`.
    - `ActivateAccount/*` (command `Token` + `NewPassword`, validator com `StrongPassword`, handler): consumo condicional do convite (`ExecuteUpdateAsync` com `UsedAtUtc IS NULL AND RevokedAtUtc IS NULL AND ExpiresAtUtc > agora`) dentro de transação, mais o índice único já existente em `user_credentials.user_id` como segunda barreira (REQ-8).
    - `Deactivate/DeactivateUserCommandHandler.cs`: revoga convites (REQ-10).
    - `UserCredentialsVerifier.cs`: hash fictício quando não há credencial (REQ-9).
    - `GetById/UserResponse.cs`, `GetAll/UserSummaryResponse.cs` e handlers: `PendingActivation`, `InvitationExpiresAtUtc` (REQ-11).
    - `Abstractions/Notifications/IEmailSender.cs` e `EmailMessage.cs`; modelo do e-mail N-01 (HTML e texto) num lugar só.
    - `IApplicationDbContext` ganha `UserInvitations`.
  - Infrastructure: `Users/UserInvitationConfiguration.cs` (tabela `user_invitations`; índice único em `token_hash`; índice em `(user_id, created_at_utc)`; FK para `users`); `Notifications/SmtpEmailSender.cs` e `EmailOptions.cs` (MailKit, DEC-2) e `InvitationOptions` (`ActivationUrl`, DEC-8), com validação na inicialização; `Audit/AuditLoggingInterceptor.cs` exclui `UserInvitation.TokenHash`; registro na DI; `ApplicationDbContext`.
  - Migration `Add_User_Invitations` (só estrutura).
  - Web.Api, `Endpoints/Users/`: `Register.cs` (Request sem senha), `ResendInvitation.cs` (`POST users/{id}/invitation`, `users:create`), `ActivateAccount.cs` (`POST users/activation`, `AllowAnonymous`, `RateLimitingPolicies.Authentication`).
  - `docker-compose.yml`: serviço `mailpit` (SMTP 1025, interface 8025), se a DEC-2 for (a). `appsettings.Development.json`: `Email` e `Invitations`.
  - `Directory.Packages.props`: `MailKit`, se a DEC-2 for (a).
  - Testes: `tests/TickestPristine.Application.UnitTests/Users/` (`RegisterUserCommandHandlerTests` revisto, `ResendInvitationCommandHandlerTests`, `ActivateAccountCommandHandlerTests`, validators, `UserCredentialsVerifierTests`); `tests/TickestPristine.IntegrationTests/Users/` (`UserInvitationsTests.cs`, com um `IEmailSender` substituto no `IntegrationTestWebAppFactory`); ajustes nos testes que hoje cadastram usuário com senha (helpers dos testes de integração passam a cadastrar e ativar).
- **Contratos** (rotas, payloads, erros, eventos):
  - `POST users/register` — Request `{ email, firstName, lastName }` (sem `password`); resposta 200 com o Id, como hoje.
  - `POST users/{id}/invitation` — sem corpo; 204; 404, 409 (`Users.AlreadyActivated`, `Users.InvitationUserDeactivated`, `Users.InvitationLimitReached`), 403.
  - `POST users/activation` — Request `{ token, newPassword }`; 204; 400 (`Users.InvalidActivationToken`, validação); 429.
  - `GET users/{id}`, `GET users` — novos campos `pendingActivation` e `invitationExpiresAtUtc`.
  - Eventos de domínio novos: `UserInvitationIssuedDomainEvent(UserId)` e `UserInvitationAcceptedDomainEvent(UserId)`, sem o token.
- **Mudança no banco:** sim. Tabela `user_invitations` com índices. Usuários existentes não mudam: todos têm credencial, logo nenhum vira pendente.
- **Riscos:**
  - R-1: a mudança de contrato do cadastro quebra o front até ele ser atualizado (DEC-5).
  - R-2: sem outbox, um e-mail pode se perder; mitigado pelo reenvio e pelo `pendingActivation` na listagem (DEC-3).
  - R-3: o link depende da URL do front configurada; URL errada gera convites inúteis. A validação na inicialização exige URL absoluta HTTPS fora de Development.
  - R-4: os testes de integração que criam usuários pelo cadastro com senha precisam de um helper novo (cadastrar e ativar), o que mexe em vários arquivos de teste.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Entidade `UserInvitation`, eventos e erros; configuração EF com índices; `DbSet`; exclusão do `TokenHash` na auditoria; migration `Add_User_Invitations` | AC-3, AC-16 | add-entity | [ ] |
| T-2 | `IEmailSender`, `EmailMessage`, `SmtpEmailSender` (MailKit), opções de e-mail e de convite com validação; Mailpit no compose; modelo do e-mail N-01 | AC-4, AC-5 | add-feature | [ ] |
| T-3 | `UserInvitations` (geração, hash, revogação, validade, limite, envio com tratamento de falha) | AC-1, AC-3, AC-4, AC-5, AC-6, AC-8 | add-feature | [ ] |
| T-4 | Cadastro sem senha: command, validator, handler e endpoint `Register` | AC-1, AC-2, AC-4, AC-5 | add-feature | [ ] |
| T-5 | Reenvio de convite: slice `ResendInvitation` e endpoint | AC-6, AC-7, AC-8 | add-feature | [ ] |
| T-6 | Ativação: slice `ActivateAccount` com consumo condicional e endpoint anônimo com rate limit | AC-9, AC-10, AC-11, AC-12, AC-17 | add-feature | [ ] |
| T-7 | Desativação revoga convites; hash fictício no `UserCredentialsVerifier` sem credencial | AC-13, AC-14 | add-feature | [ ] |
| T-8 | `pendingActivation` e `invitationExpiresAtUtc` nas consultas de usuário | AC-14, AC-15 | add-feature | [ ] |
| T-9 | Testes unitários e de integração dos ACs; helper de testes "cadastrar e ativar"; ajuste dos testes existentes que cadastram com senha | AC-1 a AC-18 | add-tests | [ ] |
| T-10 | Build e testes da solução | AC-19 | add-tests | [ ] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | | | |
| AC-2 | | | |
| AC-3 | | | |
| AC-4 | | | |
| AC-5 | | | |
| AC-6 | | | |
| AC-7 | | | |
| AC-8 | | | |
| AC-9 | | | |
| AC-10 | | | |
| AC-11 | | | |
| AC-12 | | | |
| AC-13 | | | |
| AC-14 | | | |
| AC-15 | | | |
| AC-16 | | | |
| AC-17 | | | |
| AC-18 | | | |
| AC-19 | | | |

## 9. Evidências de verificação
- **Build:**
- **Testes:**
- **Revisão:**
- **Documentação atualizada:** a atualizar pela `spec-verify`: `SYSTEM_SPECIFICATION.md` 2.6 (nova regra do convite), 2.7 (C-01, C-21), 4.3.1 (rotas novas e contrato do cadastro), 5.8 (fluxo de cadastro e ativação), 4.8 (configurações `Email` e `Invitations`).
- **CI:**
- **Entrega:**

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-24 | Rascunho | Criada como 007 a partir de `specs/_fontes/gestao-acessos-onboarding.md`, a pedido de Rafael Santos no chat. Decisões DEC-1 a DEC-11 em aberto, com recomendação; checklist de completude não atendido enquanto houver DEC em aberto. |
| 2026-10-24 | Rascunho | Renumerada de 007 para 008: o número 007 ficou com a spec do fluxo spec-first. DEC-11 resolvida pela decisão de tirar o backlog do repositório; DEC-1 sem números fixos para as próximas specs. |
| 2026-10-24 | Rascunho | DEC-1 a DEC-10 decididas pela opção recomendada: Rafael Santos delegou a escolha no chat ("encontre as melhores soluções"). DEC-8 com rota padrão `/ativar-conta`. Checklist de completude atendido; aguarda aprovação explícita. |
