---
description: Auditoria arquitetural somente-leitura do ambiente IA Agêntica + Spec-Driven Development (Tickest / .NET)
allowed-tools: Read, Glob, Grep, Bash(dotnet --info), Bash(dotnet --list-sdks), Bash(dotnet build:*), Bash(dotnet test:*), Bash(dotnet list package:*), Bash(git status:*), Bash(git log:*), Bash(git branch:*), Bash(git remote:*), Bash(git ls-files:*), Bash(git config --list:*), Bash(gh auth status), Bash(gh repo view:*), Bash(gh workflow list:*), Bash(gh run list:*), Bash(docker version), Bash(docker ps:*), Bash(claude --version), Bash(claude mcp list), Bash(node --version), Bash(python --version), Bash(python auditoria_sdd.py:*), Write(C:/dev/auditorias/**)
---

# AUDITORIA ARQUITETURAL — IA AGÊNTICA + SPEC-DRIVEN DEVELOPMENT

## 0. Parâmetros (ajuste antes de rodar)

| Parâmetro | Valor |
|---|---|
| REPO_BACKEND | `C:\dev\tickest-pristine` |
| REPO_FRONTEND | `FORA DO ESCOPO` (troque pelo caminho do repositório Angular para incluí-lo) |
| RELATORIO | `C:\dev\auditorias\tickest-auditoria-<AAAA-MM-DD>.md` |
| CONFIG_GLOBAL_CLAUDE | `%USERPROFILE%\.claude` |
| FOCO_EXTRA | $ARGUMENTS |

## 1. Missão

Você vai auditar se este ambiente está pronto para desenvolvimento profissional de nível arquiteto .NET usando IA agêntica (Claude Code) e Spec-Driven Development.

A pergunta central é:

> **"Uma feature nova do Tickest pode sair de uma necessidade de negócio e chegar a código em produção passando por spec → aprovação → plano → tarefas → implementação pelo agente → testes → revisão humana, com rastreabilidade, segurança e qualidade arquitetural — e isso está COMPROVADO, não apenas declarado?"**

Você avalia três coisas, nesta ordem de importância:
1. **O processo SDD**: se ele existe, é coerente e é seguido.
2. **A plataforma agêntica**: Claude Code, skills, comandos, hooks, permissões e MCPs.
3. **A base de engenharia .NET**: arquitetura, testes, build, CI e segurança, que sustentam o agente.

## 2. Regras invioláveis

### 2.1 Somente leitura
É PROIBIDO, sem autorização explícita minha no chat:
- criar, editar, mover ou apagar arquivos (única exceção: o arquivo RELATORIO definido acima);
- instalar, atualizar ou remover pacotes, SDKs, ferramentas, extensões ou MCPs;
- alterar configurações do Claude Code, do git, do GitHub, do Docker ou do sistema;
- `git add/commit/push/checkout/switch/reset/stash/merge/rebase`, criar branches ou tags;
- `dotnet ef database update`, migrations, scripts SQL, `docker compose up`, `docker run`, `docker rm`;
- abrir PRs ou issues, disparar workflows, implementar correções, "só ajustar rapidinho".

### 2.2 Comandos de verificação permitidos
Estes comandos são permitidos porque só geram artefatos descartáveis (`bin/`, `obj/`, `TestResults/`):
- `dotnet --info`, `dotnet --list-sdks`, `dotnet build`, `dotnet test`, `dotnet list package --vulnerable --include-transitive`, `dotnet list package --outdated`
- git somente leitura: `status`, `log`, `branch`, `remote -v`, `ls-files`, `config --list`
- `gh auth status`, `gh repo view`, `gh workflow list`, `gh run list` (somente leitura)
- `docker version`, `docker ps`
- `claude --version`, `claude mcp list`, `node --version`, `python --version`
- `python auditoria_sdd.py --formato json` (se o script existir na raiz)

**Testes de integração** (Testcontainers ou banco real) só rodam se: (a) o Docker estiver ativo, (b) os testes criarem apenas containers efêmeros e (c) nenhuma connection string apontar para banco compartilhado ou de produção. Na dúvida, **não rode**: marque como NÃO VERIFICADO e explique o motivo.

Qualquer comando fora desta lista: pare e peça autorização antes.

### 2.3 Segredos
- Nunca reproduza valores de tokens, senhas, connection strings ou chaves no relatório ou no chat.
- Ao encontrar um segredo, registre **arquivo + linha + tipo** e mascare o valor (`sk-ant-****`, `Password=****`).
- Leia `.env`, `appsettings.*.json`, `.mcp.json` e `settings.json` apenas para verificar a estrutura e a exposição, e nunca copie conteúdo sensível.

### 2.4 Honestidade de evidência
- **Nunca invente** resultados, versões, saídas de comando ou conteúdo de arquivos.
- Distinga sempre: **DECLARADO** (está escrito ou configurado) × **COMPROVADO** (executado com resultado observado) × **INFERIDO** (dedução sua, marcada como hipótese).
- Se algo estiver inacessível (pasta, ferramenta, permissão, rede), registre como **NÃO VERIFICADO**, com o motivo e o que eu precisaria fazer para liberar, e **continue a auditoria**. Inacessível não é o mesmo que não conforme.
- Se um comando falhar, registre o comando, o código de saída e um trecho curto do erro. Não repita indefinidamente.

## 3. Método (siga nesta ordem)

**Fase A — Instruções primeiro.** Leia o `CLAUDE.md` do projeto, o `CLAUDE.md` global, o `README.md` e o `SYSTEM_SPECIFICATION.md`. Extraia deles o **fluxo SDD declarado** e as **regras arquiteturais declaradas**. Esse é o padrão contra o qual o resto será medido. Se não houver fluxo declarado, isso já é um achado.

**Fase B — Inventário.** Levante tudo o que existe antes de julgar qualquer coisa. Não sugira ferramenta nova sem antes verificar se algo existente já cumpre o papel.

**Fase C — Verificação.** Para cada dimensão da seção 4, colete evidências e classifique.

**Fase D — Prova de coerência (amostragem).** Escolha **2 ou 3 specs já implementadas** e trace, para cada uma: requisito → tarefa → commit(s) → código (handler/endpoint/entidade) → teste que comprova o critério de aceite. Registre onde a cadeia se rompe. Esta é a evidência mais importante da auditoria.

**Fase E — Síntese.** Matriz de conformidade, achados, plano de ação e critérios de prontidão.

## 4. Dimensões auditadas

Para cada item: status, evidência e, se não conforme, o impacto concreto no trabalho com o agente.

### D1. Ambiente e ferramentas
- SDK .NET instalado × `global.json` × `TargetFramework` dos projetos (são compatíveis?).
- Versões de Claude Code, git, gh, Docker, Node (se houver frontend) e Python.
- `gh` autenticado. Docker ativo.
- Reprodutibilidade: um clone limpo compila só com o que está documentado no README?

### D2. Plataforma Claude Code
- **Instruções:** CLAUDE.md global e de projeto. Conteúdo, tamanho, contradições entre eles, comandos de build/teste, regras de arquitetura, regra "sem código sem spec aprovada" e regra "commit só após revisão humana do diff".
- **Orçamento de contexto:** tamanho do CLAUDE.md, número de skills, comandos e MCPs carregados, e sobreposição entre eles. Excesso de contexto prejudica o agente. Aponte o que está redundante.
- **Permissões:** `allow`/`deny` em settings global, de projeto e local. Verifique bloqueio de leitura de segredos, de `git push`, de comandos destrutivos e de migrations. Aponte se algo está liberado amplo demais (ex.: `Bash(*)`).
- **Hooks:** existem? O que validam (format, build, testes)? Rodam de fato (há evidência de execução)?
- **Skills e comandos personalizados** (ex.: `/add-entity`, `/add-feature`, `/add-module`, `/add-tests`, `/ca-review`): para cada um, avalie se o objetivo é claro, se é coerente com a arquitetura real do repositório, se exige spec como entrada, se gera testes, se tem critério de pronto e se há sobreposição ou conflito com outros.
- **MCPs:** quais estão configurados (`claude mcp list`), quais estão de fato conectados, risco de credenciais inline e se cada um é necessário.
- **Isolamento:** há estratégia para o agente trabalhar sem sujar a branch principal (worktrees, branches por feature)?

### D3. Processo SDD (o mais importante)
- O ciclo completo está **definido por escrito**: descoberta → backlog → spec → aprovação → plano → tarefas → implementação → testes → verificação → revisão → merge?
- **Portões de aprovação:** onde o humano aprova? Isso está explícito e é verificável (status na spec, checklist, PR)?
- Existe **Definition of Ready** (quando uma spec está pronta para o agente) e **Definition of Done** (quando uma feature está concluída)?
- Cada etapa tem comando ou skill correspondente? Onde o fluxo depende só da memória humana?
- **Divergência spec × código:** quando o código muda, a spec é atualizada? Há specs desatualizadas? (use a amostragem da Fase D)

### D4. Qualidade das specs e do backlog
- Estrutura consistente entre as specs (template único?).
- Requisitos com ID, critérios de aceite verificáveis (Dado/Quando/Então ou EARS), escopo e fora de escopo explícitos.
- Requisitos não funcionais (performance, segurança, auditoria, multi-tenancy, SLA) presentes quando relevantes.
- Ambiguidades não resolvidas (`TBD`, `[NEEDS CLARIFICATION]`, termos vagos como "rápido" ou "adequado").
- Coerência entre `SYSTEM_SPECIFICATION.md`, backlog e specs individuais (contradições, duplicidade, órfãos).
- Rastreabilidade: requisito → tarefa → commit → teste.

### D5. Arquitetura .NET
- **Regra de dependência da Clean Architecture:** Domain não referencia Application, Infrastructure ou Persistence; Application não referencia Infrastructure. Verifique pelos `<ProjectReference>` e pelos `using`.
- **Testes de arquitetura** (NetArchTest/ArchUnitNET) existem e cobrem essas regras? São a principal barreira contra o agente quebrar camadas.
- **Consistência de padrões:** Result pattern, CQRS (commands e queries separados, um handler por caso de uso), validação (FluentValidation/pipeline behaviors), domain events, Outbox (se declarado), entidades e AuditableEntity, soft delete.
- **Governança de build:** `Directory.Build.props` (Nullable, TreatWarningsAsErrors, analyzers), `Directory.Packages.props` (Central Package Management), `.editorconfig`, `global.json`.
- **Dependências:** pacotes vulneráveis e desatualizados (`dotnet list package`).
- **Persistência:** migrations versionadas e coerentes com o modelo; configurações EF separadas por entidade; interceptors (auditoria, domain events).
- **Segurança da aplicação:** autenticação/autorização (permissões e roles), dados sensíveis em logs, validação de entrada.
- **Observabilidade:** logging estruturado (Serilog/OpenTelemetry), correlation id, health checks.
- **Decisões arquiteturais documentadas (ADRs):** existem? As decisões relevantes (ex.: Clean Architecture × Modular Monolith, separação Entity/AuditableEntity) estão registradas com contexto e consequências?

### D6. Build, testes e automação
- `dotnet build` passa **sem warnings** (registre a contagem).
- `dotnet test`: total, aprovados, falhos e ignorados, separados por unitários, integração e arquitetura.
- Cobertura, se houver coletor configurado (não instale nada para medir).
- Os testes de fato verificam os critérios de aceite das specs (use a amostragem da Fase D)?
- Se `auditoria_sdd.py` existir na raiz, rode com `--formato json` e use o resultado como **uma** fonte de evidência, sem substituir a sua análise.

### D7. Git, GitHub e CI/CD
- Padrão de commits (Conventional Commits, IDs de spec/tarefa, trailer de coautoria do agente).
- Estratégia de branches e proteção da branch principal (verifique via `gh` se possível; se não, NÃO VERIFICADO).
- Workflows de CI: build, testes, análise de segurança, gate de qualidade. Últimas execuções (`gh run list`): passando ou falhando?
- Template de PR com vínculo à spec, CODEOWNERS, revisão obrigatória.

### D8. Segurança e autonomia controlada
- Segredos versionados no histórico (`git ls-files` + padrões conhecidos). Mascare tudo.
- `.gitignore` cobrindo `.env`, `appsettings.*.local.json`, `*.pfx`, `TestResults/`.
- Nível de autonomia do agente × risco: o que ele pode fazer sem perguntar? Isso é proporcional?
- Capacidade de recuperação: se o agente errar, é fácil reverter (commits pequenos, branches, worktrees)?

### D9. Frontend (somente se REPO_FRONTEND não estiver "FORA DO ESCOPO")
- Versões de Node e Angular, build, testes e lint.
- Contrato com o backend (OpenAPI/cliente gerado?) e se as specs cobrem frontend e backend de forma coerente.

## 5. Escala de classificação

**Status:** ✅ CONFORME · 🟡 PARCIALMENTE CONFORME · ❌ NÃO CONFORME · ⚪ NÃO VERIFICADO (sempre com motivo)

**Severidade:**
- **CRÍTICA:** impede o fluxo agêntico seguro ou expõe risco real (segredo exposto, agente com push livre, build quebrado, ausência total de specs ou testes).
- **ALTA:** o fluxo funciona, mas sem garantia de qualidade ou rastreabilidade (sem testes de arquitetura, sem portão de aprovação, specs sem critério de aceite).
- **MÉDIA:** reduz a eficiência ou a consistência (contexto inchado, skills sobrepostas, falta de ADRs).
- **BAIXA:** melhoria incremental.

**Bloqueador:** um achado CRÍTICO que impede começar a próxima feature no fluxo SDD. Liste os bloqueadores separadamente.

## 6. Formato do relatório

Grave em RELATORIO (crie a pasta `C:\dev\auditorias` se não existir; é a única escrita permitida) e mostre no chat apenas o Resumo Executivo e a Próxima Ação.

```
# Auditoria Arquitetural — Tickest — <data>

## 1. Resumo executivo
- Veredito: PRONTO | PRONTO COM RESSALVAS | NÃO PRONTO
- Bloqueadores: <n> | Achados críticos/altos/médios/baixos: <n/n/n/n>
- 3 a 5 frases: estado real, maior risco, maior ponto forte.

## 2. Inventário do ambiente
Tabela: componente | versão/estado | fonte da evidência | DECLARADO/COMPROVADO

## 3. Plataforma Claude Code (D2)
## 4. Processo SDD (D3)
## 5. Specs, backlog e skills (D4 + skills de D2)
## 6. Arquitetura .NET (D5)
## 7. Build, testes e automação (D6 + D7)
## 8. Segurança e autonomia (D8)
## 9. Frontend (D9 ou "fora do escopo")

## 10. Prova de coerência (Fase D)
Para cada spec amostrada: requisito → tarefa → commit → código → teste, indicando onde a cadeia se rompe.

## 11. Matriz de conformidade
Tabela: dimensão | item | status | severidade | evidência (arquivo:linha ou comando)

## 12. Achados
Para cada um:
- ID (ex.: ARQ-03) | severidade | DECLARADO/COMPROVADO/INFERIDO
- O que foi encontrado (fato)
- Evidência
- Impacto no trabalho com o agente
- Correção recomendada (específica para este repositório, sem conselho genérico)
- Esforço estimado: P (< 1h) | M (meio dia) | G (1+ dia)

## 13. Plano de ação
Ordenado por DEPENDÊNCIA, e não só por severidade (ex.: testes de arquitetura antes de liberar mais autonomia ao agente).
Agrupe em ondas: Onda 1 (bloqueadores) → Onda 2 (garantias de qualidade) → Onda 3 (eficiência).
Marque explicitamente o que NÃO deve ser refeito porque já funciona.

## 14. Critérios objetivos de prontidão
Checklist verificável que define "pronto para SDD com IA agêntica" para este projeto (ex.: "build sem warnings", "testes de arquitetura cobrem as 4 camadas", "toda spec tem status de aprovação", "agente sem permissão de push").
Para cada critério: status atual.

## 15. Limites desta auditoria
O que não pôde ser comprovado em modo somente leitura e por quê.
Recomende uma Fase 2: piloto controlado com uma feature pequena percorrendo o ciclo completo, que é a única prova real de que o processo funciona.

## 16. Próxima ação recomendada
UMA única ação, concreta, com o motivo de ser a primeira.
```

## 7. Critérios de qualidade do seu trabalho

Antes de entregar, confira:
- [ ] Toda afirmação de conformidade ou não conformidade tem evidência (arquivo:linha ou comando + resultado).
- [ ] Nenhum segredo aparece sem máscara.
- [ ] Nenhuma recomendação é genérica: cada uma cita o arquivo, o componente ou a regra deste repositório.
- [ ] Nada que já funciona aparece no plano como "refazer".
- [ ] Hipóteses estão marcadas como INFERIDO.
- [ ] Nenhum arquivo foi alterado além do RELATORIO.

Ao terminar, pare. Não implemente nada. Pergunte quais achados eu quero tratar primeiro.
