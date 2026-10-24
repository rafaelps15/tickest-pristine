# SPEC-02 — Redefinição administrativa de senha

> **Fonte da spec 010.** Especificação de Rafael Santos ("SPEC-02 — Redefinição Administrativa de Senha de Usuários"), refinada no chat em 2026-10-24 contra a `origin/main` `5ca764b`. Cópia da aba "SPEC-02" do Claude Doc https://claude.ai/artifact/9JGaAcgbr31TqC7mmWuJx9. O que vale para o desenvolvimento, e com quais decisões, está na spec 010. No documento refinado, "spec 007" e "spec 008" se referem às specs que no repositório são a 008 (convite e ativação) e a futura spec de recuperação de senha.

Versão 2.0. O sistema **já tem o mecanismo de troca obrigatória** (`UserCredential.MustChangePassword`, login respondendo 403 `Users.PasswordChangeRequired` e troca em `POST users/login/new-password`), hoje usado só pelo administrador inicial. **Não há gerador de senha** nem rota de redefinição. Esta spec cria a redefinição pelo Administrador com senha provisória exibida uma única vez.

> **Atenção — conflito com a spec de convite e ativação.** Ela parte do princípio de que ninguém além do titular conhece a senha dele. Esta funcionalidade faz o Administrador conhecer a senha provisória. Ela é aceitável como solução provisória enquanto não houver envio de e-mail; quando a recuperação por link existir, esta rota DEVERIA passar a enviar o link em vez de devolver a senha (D-01).

## Lacunas e ambiguidades corrigidas

| # | Lacuna ou risco na versão 1.0 | Fato no código | Tratamento nesta versão |
| --- | --- | --- | --- |
| L-01 | Sessões abertas continuam valendo após a redefinição | A renovação de token não confere `MustChangePassword`; o próprio código avisa que quem ligar a marcação precisa revogar os refresh tokens | Revogar todas as sessões e trocar o `SecurityStamp` (RF-02.5) |
| L-02 | Access token já emitido | Não é revogável antes de expirar (GAP-14) | Risco aceito e documentado; vale até a expiração configurada (R-2) |
| L-03 | Conta bloqueada por tentativas | `FailedLoginAttempts` e `LockoutEndsAtUtc` | A redefinição zera o contador e o bloqueio (RF-02.6) |
| L-04 | Permissão "administrativa adequada" vaga | Não existe permissão de redefinição | Nova permissão administrativa `users:reset-password` (RF-01.1) |
| L-05 | Escalada de privilégio | Um usuário com `users:update` poderia redefinir a senha de um Administrador e entrar como ele | `PrivilegeEscalationGuard` obrigatório (RF-01.3) |
| L-06 | Redefinir a própria senha | Contornaria a exigência da senha atual em `PUT users/me/password` | Proibido (RF-01.4) |
| L-07 | Senha provisória sem validade | Ficaria válida indefinidamente se o usuário não entrar | Validade de 24 h (RF-02.3, D-02) |
| L-08 | Clique duplo gera duas senhas | A segunda invalida a primeira e o Administrador pode copiar a errada | Bloqueio no front e limite por alvo (RF-04.4, RNF-03) |
| L-09 | Resposta guardada em cache | Proxies e navegador podem guardar o corpo com a senha | `Cache-Control: no-store` (RF-03.2) |
| L-10 | "Usuário inativo → 400" sem código | — | Código `Users.PasswordResetInactiveUser` (RF-01.5) |

## 1. Escopo e definições

**Dentro do escopo:** rota de redefinição, geração da senha provisória, troca obrigatória no próximo login, revogação de sessões, auditoria e a ação no front de Gestão de Usuários.

**Fora do escopo:** recuperação de senha pelo próprio usuário, envio da senha por e-mail ou SMS (proibido), política de senha de 12 caracteres.

**Senha provisória:** senha gerada pelo sistema, conhecida pelo Administrador, que só serve para chegar à tela de troca obrigatória e expira em 24 h.

## 2. Requisitos funcionais

### RF-01 — Autorização e elegibilidade

- RF-01.1: `POST users/{id}/password-reset` DEVE exigir a nova permissão `users:reset-password`, registrada no `PermissionCatalog` como administrativa e concedida só à role Administrador.
- RF-01.2: O usuário-alvo DEVE existir (404 `Users.NotFound`).
- RF-01.3: O executor DEVE ter todas as permissões administrativas do alvo (`PrivilegeEscalationGuard.EnsureCallerCanManageUserAsync`); senão, 403 `Users.CannotManagePrivilegedUser`.
- RF-01.4: O executor NÃO PODE redefinir a própria senha: 400 `Users.CannotResetOwnPassword`.
- RF-01.5: O alvo DEVE estar ativo e ter credencial: inativo → 400 `Users.PasswordResetInactiveUser` ("Não é possível redefinir a senha de um usuário desativado."); pendente de ativação → 400 `Users.PasswordResetPendingActivation`.
- RF-01.6: Validações na ordem: id não vazio (400) → permissão (403) → alvo existe (404) → não é o próprio (400) → guard de privilégio (403) → ativo e com credencial (400).

### RF-02 — Efeito da redefinição

- RF-02.1: O sistema DEVE gerar a senha provisória com `RandomNumberGenerator`: 16 caracteres de um alfabeto sem caracteres ambíguos (sem `0 O o 1 l I`), contendo ao menos uma maiúscula, uma minúscula, um dígito e um caractere especial, de modo que atenda sempre à política `StrongPassword` vigente.
- RF-02.2: O sistema DEVE gravar só o hash (`IPasswordHasher` atual, PBKDF2-SHA512) e marcar `MustChangePassword = true`.
- RF-02.3: O sistema DEVE gravar `TemporaryPasswordExpiresAtUtc` = agora + 24 h. Após esse instante, o login com a senha provisória DEVE falhar como credenciais inválidas e só uma nova redefinição resolve.
- RF-02.4: A troca em `POST users/login/new-password` DEVE limpar `MustChangePassword` e `TemporaryPasswordExpiresAtUtc` (fluxo atual estendido) e exigir senha nova diferente da provisória.
- RF-02.5: Na mesma transação, o sistema DEVE gerar novo `SecurityStamp` e revogar todos os refresh tokens do alvo (`UserSessions.RevokeAllAsync`).
- RF-02.6: O sistema DEVE zerar `FailedLoginAttempts` e `LockoutEndsAtUtc`.
- RF-02.7: Tudo DEVE ser gravado num único `SaveChangesAsync`. A senha só é devolvida depois do commit; falha antes dele NÃO DEVE devolver senha.

### RF-03 — Resposta

- RF-03.1: Sucesso DEVE responder 200 com `ResetPasswordResponse { temporaryPassword, expiresAtUtc }`.
- RF-03.2: A resposta DEVE ter `Cache-Control: no-store` e `Pragma: no-cache`.
- RF-03.3: A senha em claro NÃO DEVE aparecer em log, telemetria (OpenTelemetry), auditoria, evento de domínio, histórico nem mensagem de erro. O evento `UserPasswordResetDomainEvent(UserId, ResetByUserId)` não carrega a senha.

### RF-04 — Front (Gestão de Usuários, Angular)

- RF-04.1: A ação "Redefinir senha" DEVE aparecer no menu de cada usuário só para quem tem `users:reset-password`, e não para a própria linha nem para usuários desativados.
- RF-04.2: Antes de enviar, DEVE pedir confirmação: "Deseja redefinir a senha do usuário [Nome]? Uma nova senha provisória será gerada e as sessões dele serão encerradas."
- RF-04.3: No sucesso, DEVE exibir a senha uma única vez, com botão "Copiar senha" (copia exatamente o valor recebido), a validade em horário de Brasília e o texto: "Senha redefinida. Entregue a senha provisória ao usuário por um canal seguro. Ele deverá trocá-la no próximo acesso, em até 24 horas."
- RF-04.4: Durante a requisição, DEVE mostrar carregamento e desabilitar a confirmação, impedindo segundo envio.
- RF-04.5: Ao fechar o diálogo, a senha DEVE ser descartada da memória do componente; NÃO DEVE ir para `localStorage`, `sessionStorage`, store global, URL nem log do console.
- RF-04.6: Mensagens de erro: 403 → "Você não tem permissão para redefinir senhas."; 404 → "Usuário não encontrado."; 400 → o `detail` recebido; 429 → "Muitas redefinições em pouco tempo. Aguarde e tente novamente."; rede ou 5xx → "Não foi possível redefinir a senha. Nenhuma alteração foi confirmada."

## 3. Matriz de permissões e dados

| Operação | Administrador | Atendente | Colaborador |
| --- | --- | --- | --- |
| Redefinir senha de outro usuário sem privilégios maiores que os seus | Sim | Não (403) | Não (403) |
| Redefinir a própria senha por esta rota | Não (400) | Não | Não |
| Trocar a senha provisória no primeiro login | Titular | Titular | Titular |
| Ler a senha provisória depois do diálogo | Ninguém: não é armazenada | — | — |

| Dado | Gravado | Exposto |
| --- | --- | --- |
| Senha provisória | Nunca em claro; só o hash | Uma vez, na resposta 200 ao executor |
| `MustChangePassword`, `TemporaryPasswordExpiresAtUtc` | Sim, auditados | Não |
| `PasswordHash` | Sim | Nunca; já excluído da auditoria |

## 4. Contrato

| Rota | Sucesso | Erros |
| --- | --- | --- |
| `POST users/{id}/password-reset` (sem corpo) | 200 `{ temporaryPassword, expiresAtUtc }` | 400 (`Users.CannotResetOwnPassword`, `Users.PasswordResetInactiveUser`, `Users.PasswordResetPendingActivation`) · 401 · 403 · 404 · 409 (concorrência) · 429 |

## 5. Requisitos não funcionais

- RNF-01 Auditoria: registrar executor, alvo, data e hora UTC e resultado (sucesso ou motivo da recusa), pelo `AuditLoggingInterceptor` e por log estruturado de informação, sem a senha.
- RNF-02 Segurança: comparação e geração com APIs criptográficas; rota só em HTTPS; verificação no backend independente do front.
- RNF-03 Rate limit: até 5 redefinições por usuário-alvo por hora e 30 por executor por hora; acima disso, 429 com `Retry-After`.
- RNF-04 Idempotência: a operação não é idempotente (cada chamada gera senha nova e invalida a anterior); a proteção contra duplicidade é o RF-04.4 e o RNF-03.
- RNF-05 LGPD: a senha provisória é dado de autenticação; não pode ser retida além da exibição.

## 6. Decisões pendentes

| ID | Questão | Opção adotada nesta versão | Alternativa |
| --- | --- | --- | --- |
| D-01 | Convivência com a spec de convite, que proíbe senha conhecida por terceiros | Implementar agora como solução provisória; substituir por envio de link quando a recuperação por e-mail existir | Não implementar e esperar a recuperação por e-mail |
| D-02 | Validade da senha provisória | 24 h, constante no código | Configurável; ou sem validade |
| D-03 | Permissão | Nova `users:reset-password`, administrativa | Reusar `users:update` |
| D-04 | Mudança no banco | Coluna `temporary_password_expires_at_utc` em `user_credentials` | Sem validade, sem coluna |

## 7. Riscos

- R-1: o Administrador conhece a senha provisória até a troca (D-01).
- R-2: um access token emitido antes da redefinição continua válido até expirar (GAP-14).
- R-3: o Administrador pode entregar a senha por um canal inseguro; o texto do RF-04.3 orienta, mas não impede.
