---
id: 010
titulo: Redefinição de senha pelo Administrador com senha provisória
status: Rascunho        # Rascunho | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Cancelada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em:
branch: feat/SPEC-010-admin-password-reset
backlog:
---

# 010 — Redefinição de senha pelo Administrador com senha provisória

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat, em 2026-10-24: "AGORA COLOQUE AS ESPCS DENTRO DO PROJETO E PRONTAS PARA O DESENVOLVIMENTO", sobre a "SPEC-02 — Redefinição Administrativa de Senha de Usuários" refinada no mesmo dia.
- **Fonte:** `specs/_fontes/redefinicao-senha-admin.md`.
- **Referências:** `SYSTEM_SPECIFICATION.md` 2.6 (RN-16: senha provisória do administrador inicial), 4.6 (hash PBKDF2-SHA512, tempo constante), 5.1 (primeiro acesso), 7.1 (GAP-14: access token não revogável); spec 008 (convite e ativação, em Rascunho).
- **Código:** `Domain/Users/UserCredential.cs` (`MustChangePassword`, `SecurityStamp`, `FailedLoginAttempts`, `LockoutEndsAtUtc`; o comentário de `MustChangePassword` exige revogar os refresh tokens de quem receber a marcação), `Application/Users/UserCredentialsVerifier.cs`, `Users/UserPasswords.cs`, `Users/UserSessions.cs` (`RevokeAllAsync`), `Users/ChangeRequiredPassword/*` (troca em `POST users/login/new-password`), `Users/Login/LoginUserCommandHandler.cs`, `Authorization/PrivilegeEscalationGuard.cs`, `Permissions/PermissionCatalog.cs`, `Infrastructure/Roles/RolePermissionConfiguration.cs`, `Infrastructure/Audit/AuditLoggingInterceptor.cs` (já exclui `PasswordHash`), `Infrastructure/Database/Seeding/AdminUserSeeder.cs`.
- **Fatos do repositório que afetam a fonte:**
  - O fluxo de troca obrigatória já funciona: login com `MustChangePassword` responde 403 `Users.PasswordChangeRequired`, e `POST users/login/new-password` troca a senha, limpa a marcação, renova o `SecurityStamp`, revoga as sessões e devolve tokens.
  - Não existe gerador de senha: só `RandomNumberGenerator` no hasher e no provedor de tokens.
  - Não existe validade para senha provisória; a do administrador inicial vale até ser trocada.
  - **Conflito de princípio com a spec 008:** lá, ninguém além do titular conhece a senha; aqui, o Administrador conhece a provisória. Ver DEC-1.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | `POST users/{id}/password-reset` (sem corpo) exige a nova permissão `users:reset-password`, administrativa, concedida pela migration só à role Administrador. | RF-01.1; DEC-3 |
| REQ-2 | Recusas, nesta ordem: id vazio → 400; alvo inexistente → 404 `Users.NotFound`; alvo = executor → 400 `Users.CannotResetOwnPassword`; alvo com permissão administrativa que o executor não tem → 403 `Users.CannotManagePrivilegedUser` (`PrivilegeEscalationGuard`); alvo desativado → 400 `Users.PasswordResetInactiveUser` ("Não é possível redefinir a senha de um usuário desativado."); alvo sem credencial (pendente de ativação, depois da spec 008) → 400 `Users.PasswordResetPendingActivation`. Em toda recusa nada muda no banco. | RF-01.2 a RF-01.6 |
| REQ-3 | A senha provisória tem 16 caracteres, gerados com `RandomNumberGenerator` de um alfabeto sem `0 O o 1 l I`, com ao menos uma maiúscula, uma minúscula, um dígito e um caractere especial; sempre passa na regra `StrongPassword`. | RF-02.1 |
| REQ-4 | Num único `SaveChangesAsync`: grava só o hash (`IPasswordHasher`), marca `MustChangePassword = true`, grava `TemporaryPasswordExpiresAtUtc` = agora + 24 h, gera novo `SecurityStamp`, revoga todos os refresh tokens do alvo e zera `FailedLoginAttempts` e `LockoutEndsAtUtc`. Dispara `UserPasswordResetDomainEvent(UserId, ResetByUserId)`, sem a senha. | RF-02.2, RF-02.3, RF-02.5, RF-02.6, RF-02.7, RF-03.3; DEC-2 |
| REQ-5 | Sucesso responde 200 com `{ temporaryPassword, expiresAtUtc }` e os cabeçalhos `Cache-Control: no-store` e `Pragma: no-cache`. A senha não aparece em log, auditoria, histórico, telemetria nem mensagem de erro. | RF-03.1 a RF-03.3 |
| REQ-6 | Depois de `TemporaryPasswordExpiresAtUtc`, o login e a troca em `POST users/login/new-password` com a senha provisória falham como `Users.InvalidCredentials` (conferido depois do hash, para não mudar o tempo de resposta). Senha provisória sem validade gravada (administrador inicial) continua sem expiração. | RF-02.3; RN-16 |
| REQ-7 | A troca em `POST users/login/new-password` limpa também `TemporaryPasswordExpiresAtUtc` e continua exigindo senha nova diferente da atual. | RF-02.4 |
| REQ-8 | Rate limit: no máximo 5 redefinições do mesmo alvo por hora e 30 do mesmo executor por hora; acima disso, 429 com `Retry-After`, sem efeito no banco. | RNF-03 |

## 3. Critérios de aceitação
| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | Atendente e Colaborador autenticados | `POST users/{id}/password-reset` | 403; credencial do alvo inalterada |
| AC-2 | REQ-1 | banco migrado | lê as permissões das roles | `users:reset-password` existe, é administrativa e só a role Administrador a tem; a migration é idempotente quanto ao vínculo |
| AC-3 | REQ-2 | Administrador | redefine: id inexistente; a própria conta; um usuário desativado | 404 `Users.NotFound`; 400 `Users.CannotResetOwnPassword`; 400 `Users.PasswordResetInactiveUser`; nos três, nenhuma linha muda |
| AC-4 | REQ-2 | um usuário com `users:reset-password` mas sem `roles:manage`, e um alvo com `roles:manage` | redefine a senha do alvo | 403 `Users.CannotManagePrivilegedUser` |
| AC-5 | REQ-3 | — | gera 10.000 senhas no teste unitário do gerador | todas com 16 caracteres, sem os caracteres excluídos, com as quatro classes, aprovadas pelo validator `StrongPassword` e sem repetição |
| AC-6 | REQ-4, REQ-5 | Colaborador ativo com duas sessões abertas e 3 senhas erradas seguidas | Administrador redefine | 200 com `temporaryPassword` (16 caracteres) e `expiresAtUtc` = agora + 24 h; cabeçalhos `no-store` e `no-cache`; `PasswordHash` diferente e verificável com a senha devolvida; `MustChangePassword = true`; `FailedLoginAttempts = 0`; `LockoutEndsAtUtc` nulo; `SecurityStamp` novo; os dois refresh tokens revogados (renovação devolve 400) |
| AC-7 | REQ-5 | a redefinição do AC-6 | lê os logs capturados no teste, a auditoria e o histórico | nenhum contém o valor de `temporaryPassword` |
| AC-8 | REQ-4 | a redefinição do AC-6 | o usuário faz `POST users/login` com a senha provisória | 403 `Users.PasswordChangeRequired`; depois `POST users/login/new-password` com senha nova devolve 200 com tokens, e `TemporaryPasswordExpiresAtUtc` fica nulo (REQ-7) |
| AC-9 | REQ-6 | uma senha provisória com `TemporaryPasswordExpiresAtUtc` no passado (relógio do teste) | login e troca obrigatória com ela | 400 `Users.InvalidCredentials` nos dois |
| AC-10 | REQ-6 | o administrador inicial do seed | login com a senha do `Admin:Password` | 403 `Users.PasswordChangeRequired`, como hoje (`DatabaseSeedingTests` sem alteração) |
| AC-11 | REQ-8 | 5 redefinições do mesmo alvo na última hora | a sexta | 429 com `Retry-After`; o hash não muda |
| AC-12 | todos | a mudança aplicada | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | 0 avisos; todos os testes passam |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Esta funcionalidade contraria o princípio da spec 008 (só o titular conhece a senha). Como conviver? | (a) implementar agora como solução provisória e trocar por envio de link quando existir recuperação por e-mail; (b) não implementar e esperar a recuperação por e-mail | (a), com o risco R-1 aceito por escrito | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-2 | Validade da senha provisória | (a) 24 h, constante no código, com coluna `temporary_password_expires_at_utc` em `user_credentials`; (b) configurável; (c) sem validade e sem coluna | (a) | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-3 | Permissão | (a) nova `users:reset-password`, administrativa; (b) reusar `users:update` | (a): editar nome não deveria permitir tomar a conta de alguém | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-4 | Ordem com a spec 008 | (a) independente: o REQ-2 trata "sem credencial" de forma genérica, o que funciona antes e depois da 008; (b) implementar depois da 008 | (a) | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |

## 5. Fora do escopo
- Front Angular (RF-04 da fonte: menu "Redefinir senha", confirmação, exibição única com "Copiar senha", descarte da senha ao fechar, mensagens de erro): repositório separado. Este repositório entrega o contrato da seção 6.
- Recuperação de senha pelo próprio usuário e envio de senha por e-mail ou SMS.
- Revogação imediata do access token já emitido (GAP-14): continua valendo até expirar (R-2).
- Política de senha mais forte (12 caracteres) da spec de gestão de acessos.

## 6. Plano técnico
- **Arquivos a criar ou alterar:**
  - Domain: `Users/UserCredential.cs` (`TemporaryPasswordExpiresAtUtc`); `Users/UserPasswordResetDomainEvent.cs`; `Users/UserErrors.cs` (`CannotResetOwnPassword`, `PasswordResetInactiveUser`, `PasswordResetPendingActivation`).
  - Application: `Users/TemporaryPasswordGenerator.cs` (estático, `RandomNumberGenerator.GetInt32` por posição e embaralhamento Fisher-Yates); `Users/ResetPassword/*` (command, validator, handler, `ResetPasswordResponse`); `Users/UserCredentialsVerifier.cs` (expiração da provisória depois do hash); `Users/UserPasswords.cs` (limpa `TemporaryPasswordExpiresAtUtc`); `Permissions/PermissionCodes.cs` e `PermissionCatalog.cs` (`users:reset-password`, "Redefinir senhas", administrativa).
  - Infrastructure: `Users/UserCredentialConfiguration.cs`; `Roles/RolePermissionConfiguration.cs`; migration `Add_Password_Reset` (coluna, permissão e vínculo com `ON CONFLICT DO NOTHING`).
  - Web.Api: `Endpoints/Users/ResetPassword.cs` (`POST users/{id}/password-reset`, `HasPermission(users:reset-password)`, cabeçalhos `no-store`); `Endpoints/Users/Permissions.cs`; política de rate limit encadeada (`PartitionedRateLimiter.CreateChained`: partição pelo id do alvo da rota e pelo id do executor).
  - Testes: `tests/TickestPristine.Application.UnitTests/Users/TemporaryPasswordGeneratorTests.cs`, `ResetPasswordCommandHandlerTests.cs`, `UserCredentialsVerifierTests.cs`; `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` (redefinição, troca obrigatória, expiração, rate limit, ausência da senha nos logs com um sink de teste do Serilog).
- **Contratos:** `POST users/{id}/password-reset` → 200 `{ temporaryPassword, expiresAtUtc }`; 400 (`Validation`, `Users.CannotResetOwnPassword`, `Users.PasswordResetInactiveUser`, `Users.PasswordResetPendingActivation`); 401; 403 (`Users.CannotManagePrivilegedUser` ou sem permissão); 404; 409; 429. Evento novo `UserPasswordResetDomainEvent(UserId, ResetByUserId)`.
- **Mudança no banco:** sim. Coluna `user_credentials.temporary_password_expires_at_utc` (nula); permissão `users:reset-password` e vínculo com a role Administrador.
- **Riscos:**
  - R-1: o Administrador conhece a senha provisória até a troca (DEC-1).
  - R-2: um access token emitido antes da redefinição continua válido até expirar (GAP-14).
  - R-3: a entrega da senha ao usuário fica fora do sistema; o canal é responsabilidade do Administrador.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Coluna `TemporaryPasswordExpiresAtUtc`, permissão `users:reset-password` no catálogo e na role Administrador, migration | AC-2 | add-entity | [ ] |
| T-2 | `TemporaryPasswordGenerator` | AC-5 | add-feature | [ ] |
| T-3 | Slice `ResetPassword` (validações, efeitos, evento) e endpoint com cabeçalhos e rate limit | AC-1, AC-3, AC-4, AC-6, AC-7, AC-11 | add-feature | [ ] |
| T-4 | Expiração da provisória no `UserCredentialsVerifier` e limpeza na troca obrigatória | AC-8, AC-9, AC-10 | add-feature | [ ] |
| T-5 | Testes unitários e de integração dos ACs | AC-1 a AC-11 | add-tests | [ ] |
| T-6 | Build e testes da solução | AC-12 | add-tests | [ ] |

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

## 9. Evidências de verificação
- **Build:**
- **Testes:**
- **Revisão:**
- **Documentação atualizada:** a atualizar pela `spec-verify`: `SYSTEM_SPECIFICATION.md` 2.2 (permissão nova), 2.6 (regra da senha provisória com validade), 4.3.1 (rota), 4.6 (senhas), 5.8 (gestão de usuários).
- **CI:**
- **Entrega:**

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-24 | Rascunho | Criada a partir de `specs/_fontes/redefinicao-senha-admin.md`, a pedido de Rafael Santos no chat. DEC-1 a DEC-4 em aberto, com recomendação. |
| 2026-10-24 | Rascunho | DEC-1 a DEC-4 decididas pela opção recomendada: Rafael Santos delegou a escolha no chat ("encontre as melhores soluções"). Risco R-1 aceito como solução provisória até existir recuperação de senha por e-mail. Checklist de completude atendido; aguarda aprovação explícita. |
