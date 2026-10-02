# Plano: Onboarding de funcionário (role RH) — back-end

Roteiro do que precisa ser desenvolvido para suportar o cadastro de funcionário (nome, sobrenome,
e-mail, senha, roles e setor) por quem tiver a role **RH**, sem dar a RH poder de promover
ninguém a Admin. Não é código pronto — é o que cada parte precisa fazer; a implementação (nomes
de classe, assinatura exata, etc.) segue as convenções já usadas em `ChangePassword`,
`UpdateProfile` e `AssignRoles`.

---

## 1. Permissão nova

Adicionar `users:onboard` em `PermissionCodes.Users`. É a permissão que vai proteger o endpoint
de cadastro de funcionário — distinta de `users:manage-roles` (que continua exclusiva do fluxo
administrativo de reatribuição de roles).

## 2. Vínculo User → Sector

`User` não tem hoje nenhum campo de setor/departamento. Precisa:
- Campo opcional de `SectorId` na entidade `User`, com um método para alterá-lo.
- Configuração de FK na camada de Infra (mesmo padrão que `Sector` já tem para `Department`).
- Uma migration nova para a coluna.

Fica opcional/nulo de propósito: o cadastro público (`/users/register`, usado pelo Requester que
se autocadastra) continua sem setor; só o fluxo de onboarding (abaixo) vai preenchê-lo.

## 3. Novo caso de uso: cadastro de funcionário

Um comando novo (ex. `OnboardUserCommand`) que recebe: e-mail, nome, sobrenome, senha, a lista de
roles a atribuir e, opcionalmente, o setor. Regras de negócio do handler:

- E-mail não pode já existir (mesmo erro que o registro público já usa).
- Todas as roles informadas precisam existir (mesma checagem que `AssignUserRolesCommandHandler` já
  faz).
- **Nenhuma das roles informadas pode ser a role Admin** — essa é a regra que mantém RH sem
  poder de promover ninguém a admin. Precisa de um erro de domínio novo para esse caso.
- Se um `SectorId` for informado, ele precisa existir.
- Cria o usuário, a credencial (senha com hash, mesmo `IPasswordHasher` já usado em todo lugar),
  associa as roles e (se informado) o setor.
- Invalida o cache de permissões do usuário criado, mesmo padrão que `AssignUserRolesCommandHandler`
  já faz ao final.
- Retorna o Id do usuário criado (mesmo formato de retorno que `RegisterUserCommandHandler`).

Precisa também do validator (as mesmas regras de senha de `RegisterUserCommandValidator`, repetidas
no próprio validator, nome/sobrenome/e-mail obrigatórios, lista de roles não vazia) e do endpoint HTTP
(`POST users`, por exemplo) protegido por `.HasPermission(PermissionCodes.Users.Onboard)`.

## 4. Role "RH" (dado, não código)

Não precisa de código para a role em si — `Role`/`RolePermission` já são dinâmicos e o admin já
tem telas para criar role e atribuir permissões. Só decidir o pacote de permissões da RH:
`users:onboard` (cadastrar) + `users:manage` (listar/ver usuários existentes) — **sem**
`users:manage-roles`, `roles:manage`, `departments:manage` nem `sectors:manage`.

Quem for RH continua também podendo ter a role Agent (ou qualquer outra) ao mesmo tempo — isso já
funciona hoje, `UserRole` é N:N.

## 5. Testes a cobrir

Seguindo o padrão de testes já existente no projeto (handler + validator + integração):
- Onboarding com sucesso (com e sem setor).
- E-mail duplicado → falha.
- Role inexistente → falha.
- Tentativa de incluir a role Admin → falha com o erro novo.
- Setor inexistente → falha.
- Usuário sem `users:onboard` tentando chamar o endpoint → 403.

---

## Fora de escopo deste plano (decidir depois)

- Editar o setor de um usuário já existente (hoje só é definido na criação).
- Expor o setor no `GetAll`/`GetById` de usuários para a tela de gestão de usuários mostrar.
