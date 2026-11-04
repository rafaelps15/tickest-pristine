---
id: 050
titulo: Validação da CT-26 por testes — evidência do cache e protótipo do carimbo com relógio local
status: Verificada        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-11-02
branch: test/SPEC-050-ct26-stamp-cache-validation                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr:                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 050 — Validação da CT-26 por testes: evidência do cache e protótipo do carimbo com relógio local

## 1. Contexto
- **Pedido original (Rafael Santos, 2026-11-01, no chat):** "Autorizo preparar a SPEC-050, exclusivamente para a validação da CT-26, em branch e worktree independentes, partindo da `origin/main`."
  - A mesma mensagem decide o protótipo da opção (3b) "exclusivamente no projeto de testes, depois da aprovação formal da SPEC-050".
  - Decide também o relógio (i), "medição monotônica local por instância", com a condição: "o valor que contém o timestamp monotônico não pode ser compartilhado entre processos pelo Redis".
  - Para o Redis de teste, o "container genérico, aproveitando as dependências existentes".
  - Escopo: "B1–B7, R1–R3 e R7, além do protótipo da parte 3 e dos critérios C1–C6", com a R6 fora.
  - "Não transforme evidência de defeito atual em teste que passe a exigir esse comportamento no futuro."
- **Revisão pedida antes da aprovação (Rafael Santos, 2026-11-02, no chat):** decide a DEC-5, a DEC-6 e a DEC-7 (a forma da DEC-5 foi depois substituída, como mostra o item seguinte) e pede nove ajustes:
  1. comprovar que o `IMemoryCache` não é compartilhado com a instância padrão e que nenhuma operação usa o cache distribuído;
  2. incluir o cenário em que uma leitura antiga sobrescreve o valor de uma nova;
  3. corrigir a duplicidade aparente do AC-8;
  4. deixar explícito no AC-5 que a invalidação não renova a validade;
  5. reforçar o AC-10 e o AC-13;
  6. validar a idade antes da decisão no AC-16 e no AC-17;
  7. confirmar o caminho de conta ativa por `GET departments`;
  8. manter as evidências fora da suíte permanente e do CI;
  9. preservar os limites de escopo.
- **Decisões sobre o armazenamento do protótipo (Rafael Santos, 2026-11-02, no chat):**
  - Ele rejeitou a instância nomeada do HybridCache, porque "as instâncias compartilham o mesmo armazenamento de memória da aplicação", e mandou "não tente contornar isso usando classes internas, reflexão ou APIs não públicas".
  - Decidiu "abandonar o HybridCache como armazenamento do carimbo monotônico no protótipo".
  - Depois, escolheu:
    - o dicionário privado por instância (`ConcurrentDictionary`);
    - a falha fechada para a leitura que passa de V, "não repetir automaticamente a consulta";
    - a gravação não condicional, sem `AddOrUpdate`;
    - o relógio controlado nos testes permanentes, "com fronteiras exatas".
- **Confirmações (Rafael Santos, 2026-11-02, no chat):**
  - as cinco verificações da 1.1 estão "corretas, na ordem apresentada";
  - na DEC-11, "a validade é estritamente `0 ≤ idade < V`";
  - as definições do AC-26 e do AC-28 estão corretas.
- **Origem:** SPEC 049, conciliação CT-26 (sessões e tokens depois da desativação), cuja análise está no anexo C da 049 (2.10.9).
  - A 049 decidiu um carimbo de segurança conferido por requisição, com cache de validade curta (30 s).
  - Contra a aceitação de carimbos antigos, ficou com a opção (3b): o valor guarda o instante em que a leitura começou, e a idade é conferida em todo uso.
  - A decisão é condicionada à validação por execução. Esta spec valida o desenho por testes, sem código de produção.
  - A CT-26 só fecha quando a implementação de produção passar nos mesmos critérios, numa spec futura.
  - A SPEC 049 ainda não está na `main`, então as referências a ela valem quando ela entrar.
- **Base:** `origin/main` em `2804b07`.
- **Referências:**
  - **Código na base:**
    - `Infrastructure/Authorization/PermissionProvider.cs`: o conjunto de permissões fica em cache e só traz usuário ativo (`:10-23`); a conta ativa também fica em cache (`:38-42`), e a invalidação é feita em `:44-48`;
    - `PermissionCacheKeys.cs:5-7`, `ActiveUserAuthorizationHandler.cs:11-28` e `PermissionAuthorizationPolicyProvider.cs:14-18`;
    - `Infrastructure/DependencyInjection.cs`: o cache em `:51-70`, a política padrão com conta ativa em `:186-193` e os handlers de autorização em `:200-202`;
    - `Application/Users/Deactivate/DeactivateUserCommandHandler.cs:71-76`: commit e, depois, invalidação;
    - `Domain/Users/UserCredential.cs:22`: o carimbo de segurança;
    - `Web.Api/Endpoints/Departments/GetAll.cs:13-22` e `Web.Api/Endpoints/Tickets/GetOwn.cs:28`.
  - **Testes na base:**
    - `IntegrationTestWebAppFactory.cs`, `IntegrationTestCollection.cs`, `BaseIntegrationTest.cs` e `Abstractions/EfCommandCounter.cs`;
    - `Tickets/SlaMonitorBackgroundServiceTests.cs:176`: host derivado pelo `WithWebHostBuilder`.
  - **Pacotes (`Directory.Packages.props`):**
    - `Microsoft.Extensions.Caching.Hybrid` 10.11.0 (`:20`);
    - `Microsoft.Extensions.Caching.StackExchangeRedis` 10.0.14 (`:28`);
    - `Testcontainers.PostgreSql` 4.15.0 (`:52`);
    - `xunit` 2.9.3 (`:53`), que tem `CollectionDefinition(DisableParallelization = true)`.
  - **Bibliotecas, fora do repositório:**
    - HybridCache no commit `25f07afa` de `dotnet/extensions`, pasta `src/Libraries/Microsoft.Extensions.Caching.Hybrid/`;
    - `RedisCache.cs` na tag `v10.0.14` de `dotnet/aspnetcore`;
    - `ConfigureDbContext` no EF Core 10.0.14 (`EntityFrameworkServiceCollectionExtensions.cs:1059`).
  - **CI:** `.github/workflows/build.yml:51` roda `dotnet test` da solução em `ubuntu-latest`.

### 1.1 Desenho do protótipo (DEC-5, DEC-7, DEC-8, DEC-11 e DEC-12)

O protótipo é uma classe que existe só no projeto de testes. Ele não usa HybridCache, `IMemoryCache` nem Redis. O construtor só recebe três coisas: o relógio (`TimeProvider`), a função que lê o carimbo na fonte e a validade V.

**Estado de cada instância (privado e exclusivo):**
- um `ConcurrentDictionary` por usuário, criado pela própria instância e nunca registrado na injeção de dependência;
- um `Guid` próprio, criado com a instância;
- o `TimeProvider` recebido, usado só pelo instante monotônico (`GetTimestamp` e `GetElapsedTime`).

**Valor guardado:** o carimbo, o instante monotônico em que a leitura começou e o identificador da instância.

**As cinco verificações, sempre antes de qualquer decisão de autenticação, nesta ordem (confirmadas pelo responsável):**
1. o valor está no dicionário da própria instância;
2. o identificador do valor é o da instância;
3. a idade é calculada desde o instante do início da leitura que produziu o valor, pelo relógio da própria instância;
4. a idade não é negativa;
5. a idade é menor que V.

**Fluxo da decisão:**
1. O protótipo faz as cinco verificações sobre o valor do dicionário.
2. Se todas passarem, compara o carimbo do token com o do valor e decide.
3. Se alguma falhar, marca o instante e lê a fonte direto. A leitura é da própria requisição, sem se juntar a nenhuma outra.
4. Antes de gravar ou usar o resultado, aplica a ele as verificações 3, 4 e 5.
5. Se o resultado passar, grava no dicionário, sem condição (DEC-7), e decide com ele.
6. Se não passar, porque a idade do resultado está fora de `0 ≤ idade < V` (na prática, a leitura durou V ou mais), recusa a decisão sem gravar o resultado e sem repetir a leitura (DEC-11).

**Outras operações:**
- **Invalidação:** remove a entrada do usuário do dicionário e não renova a validade de nenhum valor.
- **O que o protótipo expõe para os testes (DEC-12):** o próprio identificador, a contagem de leituras da fonte, o registro dos passos de cada decisão (sem carimbos) e um método que planta uma entrada montada pelo teste.
- **Nenhum método devolve carimbo ou entrada.**

**Atendimento aos requisitos do responsável:**

| Requisito | Como o desenho cumpre | Critério |
|---|---|---|
| 1. Memória própria, sem o `IMemoryCache` da aplicação | dicionário privado da instância | AC-13 |
| 2. Relógio monotônico exclusivo da instância | `TimeProvider` próprio, e os instantes só são usados pela instância que os mediu | AC-14, AC-21 |
| 3. Nada no Redis ou em outro cache distribuído | nenhuma dependência de cache, conferido na estrutura e com um Redis isolado | AC-13 |
| 4. Carimbo de outra instância sempre recusado | verificação 2 | AC-14 |
| 5. Idade desde o início da leitura | verificação 3 | AC-15, AC-18, AC-26 |
| 6. Leitura nova quando vencido ou de outra instância | passo 3 do fluxo | AC-14, AC-15, AC-19 |
| 7. Validação antes da decisão | as cinco verificações e a ausência de método que devolva carimbo | AC-17, AC-28 |

**Por que o HybridCache saiu do protótipo (DEC-5):**
- a instância nomeada (`AddKeyedHybridCache`) é criada com o provedor de serviços da aplicação (`HybridCacheServiceExtensions.cs:100-106`);
- por isso ela usa o mesmo `IMemoryCache` da instância padrão (`Internal/DefaultHybridCache.cs:75`);
- as opções públicas não oferecem outra memória (`HybridCacheOptions.cs:21-64`).

O HybridCache continua só nas evidências (B1 a B7, R1, R2, R3 e R7), como o comportamento atual a comparar.

### 1.2 Caminho dos cenários R (conferido no código)
- **Rota:** `GET departments` só tem `RequireAuthorization()`, sem política nomeada (`Endpoints/Departments/GetAll.cs:13-22`). Por isso ela usa a política padrão, que exige usuário autenticado e conta ativa (`DependencyInjection.cs:190-193`).
- **Conferência da conta ativa:** é feita pelo `ActiveUserAuthorizationHandler` (registrado em `DependencyInjection.cs:202`), que chama `IPermissionProvider.IsActiveAsync` (`ActiveUserAuthorizationHandler.cs:24`).
- **Cache:** o `IsActiveAsync` usa o `GetOrCreateAsync` com a chave `user-active-{id}` (`PermissionProvider.cs:38-42`, `PermissionCacheKeys.cs:7`), com a consulta `AnyAsync` por id e conta ativa e com as validades padrão do cache (`DependencyInjection.cs:64-68`).
- **Desativação:** confirma a transação e só depois invalida as duas chaves do usuário (`DeactivateUserCommandHandler.cs:71-76`, `PermissionProvider.cs:44-48`).
- **Nada mais entra no caminho:** o handler da consulta de departamentos não usa o provedor de permissões (`Application/Departments/GetAll/GetDepartmentsQueryHandler.cs`), e o `PermissionAuthorizationHandler` não é chamado, porque a política padrão não tem `PermissionRequirement`.
- **Por que não `GET tickets/mine`:** essa rota usa `HasPermission` (`Endpoints/Tickets/GetOwn.cs:28`). A política nomeada dela só tem a exigência de permissão (`PermissionAuthorizationPolicyProvider.cs:14-18`) e passa pelo cache do conjunto de permissões, e não pela conta ativa. Por isso ela não substitui `GET departments` nos cenários R (DEC-10).
- **Provas, nos testes, de que o caminho foi exercitado:**
  - a requisição só termina depois que o portão libera a consulta de conta ativa;
  - a memória do host passa a ter a entrada `user-active-{id}`.

### 1.3 Comparação com o HybridCache real

Os mesmos roteiros, com a mesma ordem de eventos e o mesmo V, rodam contra a biblioteca (evidência, sob demanda, em tempo real) e contra o protótipo (teste permanente, com relógio controlado). A comparação é da ordem dos eventos e dos limites da janela, não do mesmo cronômetro. A seção 9 registra o resultado lado a lado.

| Roteiro | HybridCache (evidência) | Protótipo (teste permanente) |
|---|---|---|
| Gravação tardia depois da invalidação | AC-1: o valor antigo vale até gravação + V | AC-18 e AC-26: vale só até início + V |
| Chamada que se junta a uma leitura em andamento | AC-4 e AC-11: recebe o valor antigo | AC-16: faz a própria leitura |
| Invalidação por tag durante a leitura | AC-5: a contagem recomeça na gravação | AC-22: a invalidação não renova |
| Duas instâncias com Redis | AC-2 e AC-10: o valor antigo passa pelo Redis | AC-13: nada passa pelo Redis |
| Relógios deslocados | AC-7: o prazo se desloca junto | AC-21: o deslocamento não muda a janela |
| Renovação por acesso | AC-6: não há | AC-20: não há |

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | **Evidência da biblioteca.** Os cenários B1 a B7 (049, 2.10.9) rodam com o HybridCache 10.11.0 e o Redis 10.0.14 reais e mostram o comportamento atual descrito nos fatos da análise. O resultado de cada um fica na seção 9. | pedido; CT-26 da 049 |
| REQ-2 | **Evidência do código atual.** Os cenários R1, R2, R3 e R7 reproduzem a corrida do repovoamento na conferência de conta ativa, sem mudar código de produção, em três montagens: uma instância, duas sem Redis e duas com Redis. Eles usam `GET departments` e provam que o caminho foi exercitado (1.2). O resultado de cada um fica na seção 9. | pedido; CT-26 da 049; DEC-10 |
| REQ-3 | **Separação das evidências (DEC-6).** Os testes de evidência (REQ-1 e REQ-2) ficam numa pasta e numa coleção próprias, sem paralelismo. Ficam ignorados por padrão e só rodam com a variável de ambiente `TICKEST_EVIDENCIA_CT26=1`. A suíte permanente, inclusive a do CI, não os executa e não passa a exigir o comportamento atual. Os testes permanentes validam só o protótipo e os critérios de segurança dele. | pedido; DEC-6 |
| REQ-4 | **Protótipo da (3b) com relógio local.** Uma classe que existe só no projeto de testes decide se o carimbo de um token vale, com validade V, pelo desenho da 1.1. Não é implementação de produção.<br>- **Armazenamento (DEC-5):** um `ConcurrentDictionary` privado e exclusivo da instância; nada de HybridCache, `IMemoryCache` ou Redis.<br>- **Identidade e relógio:** cada instância tem `Guid` e `TimeProvider` próprios.<br>- **Idade:** é sempre contada desde o início da leitura que produziu o carimbo; nenhum acesso, gravação ou invalidação a renova.<br>- **Verificações:** as cinco são feitas antes de qualquer decisão.<br>- **Leitura nova:** passa pelas verificações de idade antes de ser gravada ou usada; se a idade estiver fora de `0 ≤ idade < V`, a decisão é recusada, sem gravar e sem nova tentativa (DEC-11).<br>- **Gravação:** sempre incondicional (DEC-7).<br>- **Superfície:** nenhum método devolve carimbo sem as cinco verificações (DEC-12). | pedido; DEC-2; DEC-3; DEC-5; DEC-7; DEC-11; DEC-12 |
| REQ-5 | **Testes permanentes do protótipo.** Testam, contra o protótipo, os sete requisitos da 1.1, os critérios C1 a C5 da 049, a sobrescrita da DEC-7, a falha fechada da DEC-11 e a superfície da DEC-12.<br>- **Tempo:** relógio controlado e determinístico, com fronteiras exatas (DEC-8).<br>- **Isolamento (AC-13):** usa um host igual ao de produção e um Redis isolado, exclusivo do teste.<br>- **Execução:** numa coleção própria, sem paralelismo. | pedido; DEC-7; DEC-8; DEC-11; DEC-12 |
| REQ-6 | **Sem regressão (C6).** Com os arquivos novos, `dotnet build` termina com 0 avisos e os testes que o critério de impacto do `CLAUDE.md` seleciona passam na verificação. A suíte completa roda no CI do PR, obrigatória antes do merge (DEC-13). | `CLAUDE.md`; C6 da 049; DEC-13 |
| REQ-7 | **Infraestrutura sem dependência nova.** O Redis de teste sobe por contêiner genérico do Testcontainers. Não entra pacote novo, nem mudança no `.csproj`, no `Directory.Packages.props`, no CI, em `src/` ou nas migrations. | pedido; DEC-4 |
| REQ-8 | **Registro.** A seção 9 traz, para cada cenário e critério, as execuções, o resultado previsto e o observado, além da comparação da 1.3. A referência ao resultado na CT-26 da 049 fica pendente até a 049 ter commit; esta spec não altera a 049 (DEC-14). | pedido; DEC-14 |

## 3. Critérios de aceitação

Valem para todos os critérios:
- **Evidências em tempo real:** V é de 3 s nos testes da biblioteca. Nos hosts da API, as validades do cache caem para 6 s no total e 3 s na memória, só no host de teste. A tolerância é de 0,5 s (DEC-8).
- **Protótipo:**
  - o relógio é controlado e determinístico, com V = 3 s e fronteiras exatas, sem tolerância: com idade igual a V menos 1 tique, o valor vale; com idade igual a V, não vale (DEC-8);
  - um tique é a menor unidade do relógio controlado;
  - não há espera real.
- **Repetições:** na verificação, cada critério roda 10 vezes e só é aprovado com 10 de 10.
- **Critérios de evidência** (AC-1, AC-2, AC-3, AC-4, AC-5, AC-6, AC-7, AC-8, AC-9, AC-10 e AC-11):
  - mostram o comportamento atual da biblioteca ou do código, não um requisito da solução;
  - rodam só sob demanda (REQ-3);
  - "aprovado" quer dizer que o previsto foi observado;
  - uma divergência registrada leva à revisão da análise da CT-26 antes de qualquer implementação.
- **Cenários R** (AC-8, AC-9, AC-10 e AC-11): usam `GET departments` e as duas provas de caminho da 1.2.
- **Critérios do protótipo** (AC-13, AC-14, AC-15, AC-16, AC-17, AC-18, AC-19, AC-20, AC-21, AC-22, AC-26, AC-27 e AC-28): são os testes permanentes da solução.

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | B1, evidência da biblioteca: uma instância sem Redis, com a função de leitura presa por um portão | a chave é removida durante a leitura, e a leitura é liberada depois de V | a instância devolve o valor antigo, sem nova leitura, até gravação + V − 0,5 s, e chama a função de novo depois de gravação + V + 0,5 s |
| AC-2 | REQ-1 | B2, evidência da biblioteca: duas instâncias com o mesmo Redis | a A grava depois de uma leitura mais longa que V; numa segunda rodada, de uma leitura mais curta que V | na primeira rodada, a B não aceita o payload e chama a própria função; na segunda, a B aceita o valor até início + V e não depois de início + V + 0,5 s |
| AC-3 | REQ-1 | B3, evidência da biblioteca: uma leitura presa numa instância com Redis | a chave é removida e a leitura é liberada | a memória da instância e o Redis guardam o valor antigo |
| AC-4 | REQ-1 | B4, evidência da biblioteca: uma leitura presa | outra chamada com a mesma chave e as mesmas flags chega depois da remoção | ela recebe o valor antigo, e a função de leitura dela não é chamada |
| AC-5 | REQ-1 | B5, evidência da biblioteca, não requisito da solução: uma entrada com tag e a leitura presa | a tag é invalidada durante a leitura, e a leitura é liberada | a biblioteca devolve o valor antigo depois da invalidação até gravação + V, porque recomeça a contagem na gravação (fato 6 da 049). Por isso o protótipo não usa tag nem a validade da biblioteca: nele, a invalidação não renova a validade do carimbo (AC-22 e AC-26) |
| AC-6 | REQ-1 | B6, evidência da biblioteca: uma entrada gravada com Redis | ela é lida a cada 0,5 s | a releitura acontece em gravação + V ± 0,5 s, e o tempo de vida da chave no Redis não aumenta com as leituras |
| AC-7 | REQ-1 | B7, evidência da biblioteca: duas instâncias com o mesmo Redis; a A grava com o relógio do sistema, e a B lê com o relógio adiantado em 2 s (relógio de teste, só na hora de parede) (DEC-15) | a A grava, e a B lê pelo Redis | o prazo percebido pela B, medido em tempo real desde a gravação, se antecipa 2 s ± 0,5 s em relação ao de relógios iguais; o registro separa o tempo real transcorrido, o relógio da A e o relógio da B |
| AC-8 | REQ-2 | R1, evidência do defeito atual no caminho real da API (o AC-1 mostra o mesmo fenômeno só na biblioteca): uma instância sem Redis; um usuário ativo, com token; a consulta de conta ativa presa depois de executada | o administrador desativa o usuário (commit e invalidação), e a consulta é liberada | a requisição do usuário a `GET departments` só termina depois da liberação, e a memória do host tem `user-active-{id}` (provas do caminho); as requisições seguintes respondem 200 até gravação + 3 s e 403 depois de gravação + 3,5 s |
| AC-9 | REQ-2 | R2, evidência do defeito atual: duas instâncias sem Redis, com a entrada de conta ativa já gravada na B pela própria consulta dela | a desativação acontece na A | a A responde 403 na requisição seguinte; a B responde 200 até a própria entrada vencer (gravação na B + 3 s) e 403 depois de mais 0,5 s |
| AC-10 | REQ-2 | R3, evidência do defeito atual: duas instâncias com o mesmo Redis, com a consulta de conta ativa presa na A | a desativação acontece na A, e a consulta é liberada | hoje o valor antigo passa pelo Redis: a B, sem entrada própria, responde 200 só até o início da leitura na A + 6 s, sem executar a própria consulta de conta ativa enquanto aceita; a A responde 200 até gravação + 3 s. O isolamento entre instâncias e a recusa de valores do Redis são exigidos do protótipo no AC-13 |
| AC-11 | REQ-2 | R7, evidência do defeito atual: uma instância, com a consulta de conta ativa presa | depois da desativação e da invalidação, outra requisição do mesmo usuário chega durante a consulta presa | ela termina só depois da liberação, sem executar a própria consulta (a consulta de conta ativa roda uma vez), e responde 200, porque recebe o valor da consulta compartilhada |
| AC-12 | REQ-3 | o projeto de integração sem a variável `TICKEST_EVIDENCIA_CT26`, como no CI | `dotnet test tests/TickestPristine.IntegrationTests` roda, o conjunto que o critério de impacto seleciona pela infraestrutura de teste nova da T-1 (DEC-13) | os testes de evidência aparecem como ignorados, com o motivo; nenhum falha, e a coleção de evidências não sobe contêineres; com `TICKEST_EVIDENCIA_CT26=1`, eles rodam pelo filtro da seção 6, na coleção própria, sem paralelismo |
| AC-13 | REQ-4, REQ-5 | Isolamento de memória e de cache distribuído (requisitos 1 e 3): o protótipo construído só com relógio, fonte e V; um host de teste igual ao de produção, com o HybridCache da aplicação e um Redis isolado, exclusivo deste teste | o protótipo lê, decide e invalida o carimbo de um usuário; depois, valores do mesmo usuário são plantados no Redis isolado e na memória padrão da aplicação | **estrutura:** o construtor não aceita cache nenhum, e o arquivo do protótipo não referencia HybridCache, `IMemoryCache`, `IDistributedCache` nem `StackExchange.Redis`, o que a verificação confere sem reflexão; **Redis:** as estatísticas de comandos do Redis isolado (`INFO commandstats`) não mostram nenhum comando de leitura ou gravação de dados durante as operações do protótipo, e nenhuma chave do usuário existe; **memória:** o `MemoryCache.Count` da aplicação não muda; **valores plantados:** nenhum é usado, e o protótipo lê a fonte (o contador sobe) |
| AC-14 | REQ-4, REQ-5 | Valor de outra instância (requisitos 2, 4 e 6): as instâncias A e B, cada uma com `Guid` e relógio próprios; uma entrada montada pelo teste com o identificador da A e idade menor que V no relógio da B, plantada na B pelo método de teste (DEC-12), sem reflexão e sem API de produção | a B decide sobre o usuário | no registro de passos, a B recusa a entrada na verificação 2, antes de calcular qualquer idade; depois lê a fonte (o contador da B sobe 1) e decide com o valor lido; a entrada da A nunca é usada |
| AC-15 | REQ-4, REQ-5 | Leitura vencida (requisitos 5 e 6): uma entrada da própria instância cuja leitura começou em t | o protótipo decide em t + V − 1 tique, em t + V e de novo em t + V + 1 s | em t + V − 1 tique, usa a entrada sem ler a fonte; em t + V, lê a fonte uma vez e grava o valor novo, com o próprio início; em t + V + 1 s, não lê a fonte |
| AC-16 | REQ-4, REQ-5 | Sem junção: uma leitura da fonte presa por um portão, iniciada antes da troca do carimbo | outra requisição decide depois da troca | ela não espera a leitura presa e faz a própria leitura (contador 2); no registro de passos, as cinco verificações vêm antes da decisão de autenticação, e a decisão usa o carimbo novo |
| AC-17 | REQ-4, REQ-5 | Verificações antes da decisão (requisito 7): cinco situações do valor guardado, com valor ausente, de outra instância, com idade negativa, com idade igual a V e válido; e uma sexta, de injeção de falha, com a leitura nova presa enquanto o relógio de teste regride o instante (DEC-16) | o protótipo decide em cada situação | no registro de passos, toda decisão vem depois das cinco verificações; nas quatro primeiras situações, a leitura nova termina e passa pelas verificações 3, 4 e 5 antes da decisão; na situação válida, nenhuma leitura acontece; na injeção de falha, o instante do início da leitura fica guardado, a idade da leitura nova sai negativa, e a decisão é recusada na verificação 4, sem gravar no dicionário e sem nova tentativa. A injeção de falha não indica que um relógio monotônico válido possa voltar |
| AC-18 | REQ-5 | C1: a troca do carimbo em t1 e a leitura mais antiga em andamento iniciada em t0, antes de t1 | decisões com o carimbo antigo são pedidas de t1 até t0 + V, em uma instância e em duas | o carimbo antigo é aceito no máximo até t0 + V − 1 tique e é recusado a partir de t0 + V, sem tolerância |
| AC-19 | REQ-5 | C2: uma entrada gravada por outra requisição da mesma instância, com idade igual a V | uma requisição decide | ela lê a fonte antes da decisão (o contador sobe 1) e recusa o carimbo antigo |
| AC-20 | REQ-5 | C3: uma entrada lida em t | há decisões a cada 0,5 s de tempo controlado | a fonte é lida de novo exatamente na primeira decisão com idade igual ou maior que V, e não antes; os acessos não mudam o início guardado |
| AC-21 | REQ-5 | C4: duas instâncias com relógios controlados independentes, com pontos de partida diferentes e hora de parede deslocada em mais e menos 5 s | a sequência do AC-18 roda nas duas | a janela de cada uma é a do AC-18; nenhuma usa instante medido pelo relógio da outra, porque valores da outra são recusados na verificação 2 (AC-14) |
| AC-22 | REQ-5 | C5: a troca do carimbo na instância A, seguida da invalidação feita pelo protótipo, que remove a entrada do dicionário | a próxima decisão acontece na A | a A lê a fonte e recusa o carimbo antigo. A invalidação não renova a validade de nenhum valor: uma gravação atrasada depois dela continua com a idade contada do início da leitura que a produziu (AC-26). É uma otimização, e a garantia continua sendo o AC-18 |
| AC-23 | REQ-6 | os arquivos novos da seção 6 | `dotnet build TickestPristine.slnx` e `dotnet test tests/TickestPristine.IntegrationTests` rodam: o projeto de integração inteiro, que o critério de impacto seleciona pela infraestrutura de teste nova da T-1 (DEC-13) | o build termina com 0 avisos e todos os testes do projeto passam, com as evidências como ignoradas; os unitários e os de arquitetura, que esta spec não altera, ficam para a suíte completa do CI |
| AC-24 | REQ-7 | o diff da branch contra a `origin/main` | ele é conferido | só há arquivos novos em `tests/TickestPristine.IntegrationTests/` e alterações em `specs/050-validacao-ct26-carimbo-cache/`; nada em `src/`, `.csproj`, `Directory.Packages.props`, `.github/` ou nas migrations |
| AC-25 | REQ-8 | as execuções da verificação | a spec é verificada | a seção 9 traz, para cada critério de evidência e do protótipo, as 10 execuções, o previsto e o observado, além da comparação da 1.3, e a referência na CT-26 da 049 anotada como pendente até a 049 ter commit; a 049 não é alterada (DEC-14) |
| AC-26 | REQ-4, REQ-5 | DEC-7, concorrência A e B: a leitura A (antiga) começa em t0, antes da troca do carimbo, e fica presa; a leitura B (nova) começa depois da troca, termina e grava primeiro | rodada 1: a A termina em t0 + V − 1 s e grava; rodada 2: a A termina em t0 + V ou depois | **rodada 1:** a A sobrescreve o valor da B, porque a gravação é incondicional; o carimbo antigo é aceito só até t0 + V − 1 tique e é recusado a partir de t0 + V; a decisão seguinte lê a fonte e volta ao carimbo novo, então a sobrescrita nunca prolonga a janela da A.<br>**Rodada 2:** o resultado da A é recusado antes de ser gravado ou usado (DEC-11); o valor da B continua no dicionário, e nenhuma decisão aceita o carimbo antigo |
| AC-27 | REQ-4, REQ-5 | DEC-11, falha fechada: uma entrada vencida e a leitura nova presa até a idade dela chegar a V | a leitura termina | a decisão é recusada; o resultado não é gravado no dicionário nem usado; a fonte foi chamada uma única vez, sem nova tentativa; o registro de passos mostra a recusa pela idade da leitura nova |
| AC-28 | REQ-4 | DEC-12, superfície do protótipo: o código do protótipo | ele é inspecionado na verificação e exercitado pelos testes | nenhum método devolve carimbo ou entrada. As únicas operações são:<br>- a decisão, com as cinco verificações;<br>- a invalidação;<br>- o identificador da instância;<br>- o método de teste que planta uma entrada;<br>- os diagnósticos (registro de passos e contagem de leituras), que não contêm carimbos. |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Onde a validação da CT-26 roda | (A) spec derivada só de testes / (B) experimento fora do repositório | **(A)**, em branch e worktree independentes, a partir da `origin/main`, sem trocar a branch da sessão nem tocar na 035 | Rafael Santos, 2026-11-01 (chat) |
| DEC-2 | Relógio da (3b) | (i) memória local com relógio monotônico / (ii) Redis com a hora do banco / (iii) Redis com tolerância declarada | **(i)**, com a condição de que o valor com o instante monotônico não passe pelo Redis | Rafael Santos, 2026-11-01 (chat) |
| DEC-3 | Protótipo da (3b) | agora, no projeto de testes / só com a implementação de produção | **Agora**, só no projeto de testes, depois da aprovação formal desta spec; não é implementação de produção | Rafael Santos, 2026-11-01 (chat) |
| DEC-4 | Redis de teste | contêiner genérico / módulo `Testcontainers.Redis` | **Contêiner genérico**, sem pacote novo e sem mudar `.csproj`, `Directory.Packages.props` e CI. O Redis serve às evidências e ao teste de isolamento do protótipo (AC-13), nunca ao valor do carimbo | Rafael Santos, 2026-11-01 (chat) |
| DEC-5 | Armazenamento do carimbo no protótipo | (a1) HybridCache num contêiner de serviços privado / (a2) instância nomeada do HybridCache / (c1) dicionário privado por instância / (c2) `MemoryCache` privado | **(c1)**: um `ConcurrentDictionary` privado e exclusivo de cada instância, com `Guid` e `TimeProvider` próprios; nada de HybridCache, `IMemoryCache` ou Redis no armazenamento do protótipo.<br>- A (a2) foi rejeitada: compartilha o `IMemoryCache` da aplicação (1.1).<br>- Nada de classes internas, reflexão ou APIs não públicas. | Rafael Santos, 2026-11-02 (chat) |
| DEC-6 | Como separar as evidências da suíte permanente | (a) pasta e coleção próprias, sem paralelismo, ignoradas por padrão e habilitadas só pela variável de ambiente / (b) evidências fora do PR | **(a):**<br>- coleção própria, sem paralelismo (`CollectionDefinition(DisableParallelization = true)`);<br>- testes ignorados por padrão e habilitados só com `TICKEST_EVIDENCIA_CT26=1`;<br>- a fixture da coleção só sobe contêineres com a variável ligada. | Rafael Santos, 2026-11-02 (chat) |
| DEC-7 | Gravação não condicional | (a) aceitar / (b) conferir antes de gravar | **(a), mantida:** gravação não condicional, sem `AddOrUpdate` nem outra estratégia de escrita condicional nesta etapa.<br>- A idade é sempre contada do início da leitura que produziu o carimbo.<br>- Uma leitura antiga que termina depois pode sobrescrever o valor novo, mas nunca prolonga a própria janela (AC-26).<br>- A recusa da DEC-11 confere só a idade do próprio resultado, antes de gravar; ela não compara com o valor guardado e não é escrita condicional. | Rafael Santos, 2026-11-02 (chat) |
| DEC-8 | Tempos e repetições | — | **Testes permanentes do protótipo:** relógio controlado e determinístico, com V = 3 s e fronteiras exatas, sem tolerância nem espera real.<br>**Evidências que dependem de tempo real:**<br>- V = 3 s na biblioteca;<br>- 6 s no total e 3 s na memória nos hosts da API, só no host de teste, por `Configure<HybridCacheOptions>`;<br>- só nelas vale a tolerância de 0,5 s.<br>**Repetições:** 10 execuções por critério na verificação e uma execução no CI. | Rafael Santos, 2026-11-02 (chat), quanto ao relógio controlado e à tolerância; os demais valores, recomendação aprovada com a spec |
| DEC-9 | Fonte do carimbo nos testes do protótipo | fonte controlada / coluna real | **Recomendação; aprovada com a spec:** uma fonte controlada por portões, para corridas determinísticas, e um teste com o carimbo real da credencial (`UserCredential.SecurityStamp`) no PostgreSQL da coleção | |
| DEC-10 | Rota e cache dos cenários R | `GET departments`, com a política padrão (conta ativa) / uma rota com permissão (conjunto de permissões) | **Recomendação; aprovada com a spec:** `GET departments`, porque a política padrão confere a conta ativa (1.2). O cache do conjunto de permissões, também invalidado na desativação, fica fora, e `GET tickets/mine` não substitui a rota | |
| DEC-11 | Leitura nova que chega com idade igual ou maior que V | falhar fechado / aceitar o valor recém-lido | **Falhar fechado:** a validade é estritamente `0 ≤ idade < V`, então idade igual ou superior a V é vencida. A decisão é recusada antes de gravar ou usar o resultado, e a consulta não é repetida automaticamente | Rafael Santos, 2026-11-02 (chat), com a fronteira confirmada no mesmo dia |
| DEC-12 | Como os testes demonstram a recusa de valor de outra instância sem reflexão | método de teste do protótipo / reflexão / API de produção | **Recomendação; aprovada com a spec:** o protótipo tem um método só para os testes, que planta no dicionário uma entrada montada pelo teste. O método não devolve carimbo, e o dicionário continua privado. Sem reflexão e sem API de produção (AC-14 e AC-28) | |
| DEC-13 | Como comprovar a suíte inteira (REQ-6, AC-12 e AC-23) depois da SPEC-051 | (a) exceção nesta spec: suíte inteira local na verificação / (b) seguir a SPEC-051: verificação por impacto, com a suíte completa no CI | **(b):** a SPEC-051 (PR #64, mergeada em 2026-11-03) passou a verificação local para os testes por impacto e deixou a suíte completa para o CI, obrigatória antes do merge (check `build`). Pela DEC-12 dela, as regras novas valem a partir do merge, e esta spec foi aprovada antes, em 2026-11-02.<br>- Pelo critério de impacto do `CLAUDE.md`, a infraestrutura de teste nova da T-1 faz a verificação rodar o projeto de integração inteiro, e não só os testes novos.<br>- Os unitários e os de arquitetura, que esta spec não altera, ficam para o CI.<br>- Ajusta o REQ-6, o AC-12, o AC-23 e a T-6. | Rafael Santos, 2026-11-03 (chat): "Vamos seguir as novas atualizações da spec 051 !" |
| DEC-14 | Onde a CT-26 da 049 recebe a referência ao resultado (REQ-8 e AC-25) | (a) no rascunho da 049, sem commit, na worktree da SPEC-035 (`C:\dev\tickest-pristine`) / (b) adiar até a 049 ter commit | **(b):** a 049 só existe como arquivo sem commit na worktree da SPEC-035, e a aprovação desta spec proíbe alterar outras specs e mexer nessa worktree.<br>- A seção 9 registra os resultados e a referência pendente.<br>- A referência entra na CT-26 quando a 049 for para uma branch.<br>- Ajusta o REQ-8, o AC-25 e a T-6. | Rafael Santos, 2026-11-03 (chat), na opção "Adiar até a 049 ter commit", depois de saber da restrição; antes disso, tinha escolhido a (a) |
| DEC-15 | Montagem e previsão do B7 (AC-7) | (a) manter o relógio da A adiantado / (b) corrigir para o B7 original, com o relógio da B adiantado | **(b):** a A grava com o relógio do sistema, e a B lê com o relógio adiantado em 2 s; a previsão passa a ser a antecipação do prazo percebido pela B em 2 s ± 0,5 s.<br>- Motivo: com o relógio da A adiantado, a previsão estava errada, porque a memória da B e a chave no Redis valem V a partir da gravação e escondem o deslocamento (seção 9). O B7 original desloca o relógio de quem lê, e o código do HybridCache 10.11.0 mostra que isso é observável.<br>- Sem cenário independente de relógio atrasado e sem outras combinações de relógios.<br>- Ajusta o AC-7, o teste do B7, a descrição da medição e a seção 9. | Rafael Santos, 2026-11-04 (chat): "Corrija o AC-7 para reproduzir o cenário original do B7: instância B com relógio adiantado em 2 segundos, enquanto A grava com relógio normal." |
| DEC-16 | Como comprovar a recusa da leitura nova com idade negativa (REQ-4) | (a) deixar sem teste / (b) teste determinístico de injeção de falha | **(b):** um relógio de teste montado sobre o relógio controlado, só nesse teste, regride o instante durante uma leitura presa. O teste confere a recusa por idade negativa, sem gravação no dicionário e sem nova tentativa.<br>- O `ControlledTimeProvider` continua nunca voltando, e o protótipo não muda.<br>- É uma comprovação defensiva de uma condição já prevista no REQ-4: não indica que um relógio monotônico válido possa voltar.<br>- Ajusta o AC-17. | Rafael Santos, 2026-11-04 (chat): "Acrescente um teste determinístico de injeção de falha, utilizando o mecanismo de relógio de teste, para simular uma regressão artificial do timestamp durante uma leitura bloqueada." |

## 5. Fora do escopo
- **Implementação de produção da (3b):** a conferência por requisição e o carimbo no login e na renovação (U2 e T-105 da 049) ficam para uma spec futura, e a CT-26 só fecha lá.
- **R6:** fica fora, porque a (3a) não foi adotada.
- **Mudanças fora dos testes:** nada muda em `src/`, migrations, contratos, `.csproj`, `Directory.Packages.props`, CI ou configuração.
- **Cache do conjunto de permissões:** não entra como cenário R (DEC-10).
- **No armazenamento do protótipo:** HybridCache, `IMemoryCache`, Redis, tags e qualquer cache compartilhado ficam fora (DEC-5).
- **Gravação condicional:** `AddOrUpdate` e qualquer outra escrita condicional ficam fora (DEC-7).
- **Repetição automática da leitura:** fica fora (DEC-11).
- **Alternativas não adotadas na 049 (anexo C, 2.10.9):** os relógios (ii) e (iii), a biblioteca sem envoltório, a (3a), a geração de invalidação e a geração na chave.
- **`SYSTEM_SPECIFICATION.md`:** não se aplica, porque nenhum comportamento do sistema muda.

## 6. Plano técnico
- **Arquivos a criar (nenhum existente é alterado):**
  - **Evidência, na pasta `tests/TickestPristine.IntegrationTests/Evidence/Ct26/`:**
    - `Ct26EvidenceCollection.cs`: coleção própria, sem paralelismo, cuja fixture só sobe contêineres com a variável ligada (DEC-6);
    - `EvidenceFactAttribute.cs`: pula o teste sem `TICKEST_EVIDENCIA_CT26=1` (DEC-6);
    - `HybridCacheEvidenceTests.cs`: B1 a B7 (AC-1, AC-2, AC-3, AC-4, AC-5, AC-6 e AC-7);
    - `ActiveUserCacheRaceEvidenceTests.cs`: R1, R2, R3 e R7 (AC-8, AC-9, AC-10 e AC-11).
  - **Protótipo e testes permanentes, na pasta `tests/TickestPristine.IntegrationTests/Caching/StampAge/`:**
    - `StampAgePrototypeCollection.cs`: coleção própria, sem paralelismo, com as fixtures do PostgreSQL e de um Redis isolado, exclusivo desta coleção;
    - `StampAgeCachePrototype.cs`: o protótipo da 1.1, com dicionário privado, `Guid`, `TimeProvider`, o valor guardado, o registro de passos, a contagem de leituras e o método de teste da DEC-12;
    - `StampAgeCachePrototypeTests.cs`: AC-13, AC-14, AC-15, AC-16, AC-17, AC-18, AC-19, AC-20, AC-21, AC-22, AC-26, AC-27 e AC-28;
    - `ControlledStampSource.cs`: fonte do carimbo controlada por portões (DEC-9).
  - **Apoio, na pasta `tests/TickestPristine.IntegrationTests/Caching/`:**
    - `RedisContainerFixture.cs`: contêiner `redis:7.4` pelo construtor genérico do Testcontainers;
    - `ControlledTimeProvider.cs`: relógio de teste com o instante monotônico avançado à mão e a hora de parede deslocável; ele nunca volta no tempo e não depende de pacote.
  - **`tests/TickestPristine.IntegrationTests/Abstractions/EfCommandGate.cs`:** prende um comando do EF, escolhido pelo texto, até o teste liberar. É registrado por `ConfigureDbContext`, só nos hosts de teste das evidências R.
  - **Não mudam:** `IntegrationTestWebAppFactory.cs`, `IntegrationTestCollection.cs`, `BaseIntegrationTest.cs`, o `.csproj`, o `Directory.Packages.props`, o CI, `src/` e as migrations.
- **APIs públicas usadas:**
  - **No protótipo:**
    - `System.Collections.Concurrent.ConcurrentDictionary` (`TryGetValue`, atribuição e `TryRemove`);
    - `ConcurrentQueue`, para o registro de passos;
    - `System.TimeProvider` (`GetTimestamp`, `GetElapsedTime` e `TimestampFrequency`);
    - `Guid.NewGuid`.
  - **Nos testes:**
    - `TaskCompletionSource`, para os portões;
    - uma subclasse de `TimeProvider`;
    - `WebApplicationFactory.WithWebHostBuilder` e `ConfigureTestServices`, para os hosts de teste;
    - `MemoryCache.Count`, para a memória da aplicação;
    - `StackExchange.Redis` (`ConnectionMultiplexer` e `IServer.InfoAsync("commandstats")`), para o Redis isolado;
    - o construtor genérico do Testcontainers, para subir o Redis;
    - o `ApplicationDbContext`, para ler o carimbo real.
  - Tudo chega pelas referências atuais. Que o `StackExchange.Redis` e o `MemoryCache` chegam ao projeto de testes por elas só se confirma no primeiro build da tarefa.
- **Infraestrutura e dependências:**
  - **Docker:** já é exigido pelos testes de integração. As imagens são `postgres:17`, que já existe, e `redis:7.4`, que o Testcontainers baixa, sem mudança no workflow.
  - **Uso no CI:** o Redis só sobe no CI para o teste de isolamento do protótipo (AC-13). As evidências não sobem contêineres sem a variável.
  - **Duas instâncias da API (evidências R):** hosts derivados pelo `WithWebHostBuilder`, cada um com a própria memória. Os dois usam o mesmo PostgreSQL e, nas variantes com Redis, o mesmo Redis, pela `ConnectionStrings:Cache`.
  - **Duas instâncias do protótipo:** dois objetos, cada um com dicionário, `Guid` e relógio próprios, que compartilham só a fonte do carimbo. No AC-13, eles são registrados num host de teste igual ao de produção, com o Redis isolado.
  - **Ajustes só nos hosts de teste das evidências:**
    - as validades do cache, por `Configure<HybridCacheOptions>`, que vale depois do `AddHybridCache` (`HybridCacheServiceExtensions.cs:24-29`);
    - o portão do EF, por `ConfigureDbContext`.
  - **Execução das evidências:** `TICKEST_EVIDENCIA_CT26=1 dotnet test tests/TickestPristine.IntegrationTests --filter "FullyQualifiedName~Evidence.Ct26"`.
  - **Tempo:**
    - as evidências levam cerca de 12 minutos com as 10 repetições, só sob demanda;
    - os testes permanentes do protótipo não esperam tempo real, e o tempo deles fica por conta dos contêineres do AC-13 e do carimbo real.
- **Contratos** (rotas, payloads, erros, eventos): nenhum.
- **Mudança no banco:** não.
- **Riscos:**
  - **Protótipo não é produção:** ele valida o desenho, e não a integração. O token de hoje só leva `sub`, `email` e `name`, e a conferência não está no pipeline de autorização. A validação de ponta a ponta fica para a spec de implementação, e a CT-26 continua aberta.
  - **Suspensão da máquina:** em alguns sistemas operacionais, o relógio monotônico não conta o tempo em que a máquina fica suspensa. É um risco de produção, que o protótipo não testa.
  - **Leituras simultâneas:** sem junção, várias requisições do mesmo usuário com o valor vencido leem a fonte, uma cada.
  - **Memória sem limpeza:** o dicionário não descarta entradas. Em produção, isso exigiria uma limpeza ou outro armazenamento, numa spec própria.
  - **Falha fechada (DEC-11):** uma leitura que dure V ou mais recusa uma requisição legítima.
  - **Evidências em tempo real:** a tolerância de 0,5 s e as coleções sem paralelismo reduzem a flutuação. Se a máquina da verificação mostrar instabilidade, a tolerância é revista com o responsável.
  - **Portão SDD e worktree:** o portão de uma sessão avalia caminhos relativos à pasta do projeto dela. Por isso, a implementação roda numa sessão aberta nesta worktree (`C:/dev/tickest-pristine-spec050`), onde o portão vale e o `/spec-implement 050` encontra esta spec.
  - **Evidência diferente do previsto:** não reprova esta spec. É registrada, e a análise da CT-26 é revista antes de qualquer implementação.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Apoio de teste: coleções próprias sem paralelismo, contêiner Redis genérico, relógio controlado, portão de comando do EF e atributo de evidência | AC-12, AC-23, AC-24 | add-tests | [x] |
| T-2 | Evidência da biblioteca: B1 a B7, sob demanda | AC-1, AC-2, AC-3, AC-4, AC-5, AC-6, AC-7 | add-tests | [x] |
| T-3 | Evidência do código atual: R1, R2, R3 e R7, sob demanda, por `GET departments` | AC-8, AC-9, AC-10, AC-11 | add-tests | [x] |
| T-4 | Protótipo da (3b) com dicionário privado e relógio local, só no projeto de testes | AC-13, AC-14, AC-15, AC-16, AC-17, AC-26, AC-27, AC-28 | add-tests | [x] |
| T-5 | Testes permanentes do protótipo, com relógio controlado | AC-13, AC-14, AC-15, AC-16, AC-17, AC-18, AC-19, AC-20, AC-21, AC-22, AC-26, AC-27, AC-28 | add-tests | [x] |
| T-6 | Verificação: 10 execuções por critério, build e testes por impacto (o projeto de integração inteiro; a suíte completa fica para o CI, DEC-13), registro e comparação na seção 9, com a referência na CT-26 da 049 anotada como pendente (DEC-14) | AC-23, AC-25 | spec-verify | [x] |
| T-7 | Correções depois da verificação (DEC-15 e DEC-16): o B7 com o relógio da B adiantado e o teste de injeção de falha da idade negativa da leitura nova; depois, nova verificação pela T-6, com a suíte completa local a pedido do responsável | AC-7, AC-17 | add-tests | [x] |

## 8. Rastreabilidade
Caminhos a partir de `tests/TickestPristine.IntegrationTests/`.

| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | `Evidence/Ct26/HybridCacheEvidenceTests.cs` › `GetOrCreateAsync_Should_ServeLateWriteUntilWritePlusValidity_WhenKeyIsRemovedDuringRead` | Instância sem Redis com a leitura presa; a chave sai durante a leitura, liberada em V + 0,5 s; a cada 100 ms, confere o valor antigo sem nova chamada até gravação + V − 0,5 s e a nova chamada a partir de gravação + V + 0,5 s | Conforme o previsto, 10 de 10 execuções com a variável na verificação 3 (2026-11-04) |
| AC-2 | `Evidence/Ct26/HybridCacheEvidenceTests.cs` › `GetOrCreateAsync_Should_LetOtherInstanceUseRedisPayloadOnlyUntilReadStartPlusValidity` | Duas instâncias com o mesmo Redis; com o payload confirmado no Redis, a leitura de V + 1 s faz a B chamar a própria função, e a de V − 1 s faz a B aceitar o valor da A até início + V − 0,5 s e chamar a própria função a partir de início + V + 0,5 s | Conforme o previsto, 10 de 10 execuções com a variável na verificação 3 (2026-11-04) |
| AC-3 | `Evidence/Ct26/HybridCacheEvidenceTests.cs` › `GetOrCreateAsync_Should_StoreLateWriteInMemoryAndRedis_WhenKeyIsRemovedDuringRead` | Remove a chave durante a leitura presa e libera; lê só a memória da instância (Redis desligado na leitura) e o Redis por outra instância, sem memória: os dois devolvem o valor antigo sem chamar a função | Conforme o previsto, 10 de 10 execuções com a variável na verificação 3 (2026-11-04) |
| AC-4 | `Evidence/Ct26/HybridCacheEvidenceTests.cs` › `GetOrCreateAsync_Should_JoinHeldReadAndReturnOldValue_WhenSameKeyAndFlagsArriveAfterRemoval` | Com a leitura presa e a chave removida, outra chamada com a mesma chave e as mesmas flags não termina antes da liberação, recebe o valor antigo e não chama a própria função | Conforme o previsto, 10 de 10 execuções com a variável na verificação 3 (2026-11-04) |
| AC-5 | `Evidence/Ct26/HybridCacheEvidenceTests.cs` › `GetOrCreateAsync_Should_ServeValueWrittenAfterTagInvalidationUntilWritePlusValidity` | Invalida a tag durante a leitura presa e libera depois; confere o valor antigo sem nova chamada até gravação + V − 0,5 s e a nova chamada a partir de gravação + V + 0,5 s | Conforme o previsto, 10 de 10 execuções com a variável na verificação 3 (2026-11-04) |
| AC-6 | `Evidence/Ct26/HybridCacheEvidenceTests.cs` › `GetOrCreateAsync_Should_ReadSourceAgainAtWritePlusValidityWithoutExtendingRedisTimeToLive` | Com a memória valendo 1 s, lê a cada 0,5 s e mede o tempo de vida da chave no Redis: a releitura cai em gravação + V ± 0,5 s, e o tempo de vida nunca aumenta antes dela | Conforme o previsto, 10 de 10 execuções com a variável na verificação 3 (2026-11-04) |
| AC-7 | `Evidence/Ct26/HybridCacheEvidenceTests.cs` › `GetOrCreateAsync_Should_AnticipateReaderDeadline_WhenReaderClockIsTwoSecondsAhead` | A A grava sempre com o relógio do sistema; a B lê com relógios iguais e depois com o relógio adiantado em 2 s. O teste mede em tempo real (Stopwatch) quando a B chama a própria função depois da gravação e registra os relógios da A e da B na gravação e nessa chamada. Confere a diferença B − A na gravação (0 e 2 s) e a antecipação do prazo da B em 2 s ± 0,5 s | Conforme o previsto com a montagem da DEC-15, 10 de 10 na verificação 3 (2026-11-04): antecipação de 1,958 a 2,056 s. A montagem anterior, com o relógio da A, deu divergência em 10 de 10 nas verificações 1 e 2 |
| AC-8 | `Evidence/Ct26/ActiveUserCacheRaceEvidenceTests.cs` › `GetDepartments_Should_AcceptDeactivatedUserUntilLateWritePlusMemoryValidity_WhenActiveCheckEndsAfterDeactivation` | Prende a consulta de conta ativa depois de executada, desativa o usuário e libera em 1,5 s; confere que a requisição só termina depois, a entrada `user-active-{id}` na memória do host e, a cada 250 ms, 200 até gravação + 2,5 s e 403 a partir de gravação + 3,5 s | Conforme o previsto, 10 de 10 execuções com a variável na verificação 3 (2026-11-04), com as duas provas de caminho |
| AC-9 | `Evidence/Ct26/ActiveUserCacheRaceEvidenceTests.cs` › `GetDepartments_Should_AcceptDeactivatedUserOnOtherInstanceUntilItsOwnEntryExpires_WithoutRedis` | Duas instâncias sem Redis. Provas do caminho: a primeira requisição na B só termina depois que o portão libera a consulta de conta ativa dela, e a entrada `user-active-{id}` aparece na memória da B. Depois da desativação na A, a A responde 403, e a B 200 até gravação na B + 2,5 s e 403 a partir de + 3,5 s | Conforme o previsto, 10 de 10 execuções com a variável na verificação 3 (2026-11-04), com as duas provas de caminho |
| AC-10 | `Evidence/Ct26/ActiveUserCacheRaceEvidenceTests.cs` › `GetDepartments_Should_AcceptDeactivatedUserOnOtherInstanceThroughRedisUntilReadStartPlusTotalValidity` | Duas instâncias com o mesmo Redis e a consulta presa na A por 1,5 s. Provas do caminho: a requisição na A só termina depois da liberação, e a entrada aparece na memória da A. Com a chave confirmada no Redis, a A responde 200 até gravação + 2,5 s, e a B, sem consulta própria enquanto aceita, 200 até início + 5,5 s e 403 a partir de início + 6,5 s | Conforme o previsto, 10 de 10 execuções com a variável na verificação 3 (2026-11-04), com as duas provas de caminho |
| AC-11 | `Evidence/Ct26/ActiveUserCacheRaceEvidenceTests.cs` › `GetDepartments_Should_JoinHeldActiveCheckAndAccept_WhenRequestArrivesAfterDeactivation` | Com a consulta presa e o usuário já desativado: provas do caminho (a primeira requisição só termina depois da liberação, e a entrada aparece na memória); a segunda requisição também só termina depois da liberação e responde 200, e a contagem de consultas de conta ativa é 1 | Conforme o previsto, 10 de 10 execuções com a variável na verificação 3 (2026-11-04), com as duas provas de caminho |
| AC-12 | `Evidence/Ct26/EvidenceFactAttribute.cs` › `EvidenceFactAttribute` e `Evidence/Ct26/Ct26EvidenceCollection.cs` › `Ct26EvidenceFixture.InitializeAsync`, sobre os 11 testes de evidência acima | Sem a variável, o atributo marca cada teste como ignorado, com o motivo, e a fixture não cria contêiner; a verificação confere os 11 ignorados no projeto inteiro e a ausência de contêiner criado na execução só das evidências; com a variável, eles rodam na coleção própria, sem paralelismo | Aprovado na verificação 3 (2026-11-04): 11 ignorados, com o motivo, na suíte completa e na execução isolada, sem nenhum contêiner criado; com a variável, os 11 rodaram 10 vezes na coleção própria |
| AC-13 | `Caching/StampAge/StampAgeCachePrototypeTests.cs` › `PrototypeSource_Should_TakeOnlyClockSourceAndValidityAndReferenceNoCache` e `DecideAsync_Should_LeaveApplicationMemoryAndRedisUntouched_WhenHostedBesideProductionCaches` | Estrutura: o texto do arquivo do protótipo tem um único construtor, com relógio, fonte e validade, e nenhuma referência a cache, Redis, reflexão ou escrita condicional. Execução: num host igual ao de produção, com o Redis isolado (a gravação de aquecimento do cache da aplicação aparece no Redis), compara o `INFO commandstats`, lido depois de estabilizar, e o `MemoryCache.Count` antes e depois das operações, procura chaves do usuário e confirma que os valores plantados não são usados (a fonte é lida) | Aprovado, 20 de 20 na verificação 3 (2026-11-04) |
| AC-14 | `Caching/StampAge/StampAgeCachePrototypeTests.cs` › `DecideAsync_Should_RefuseEntryFromAnotherInstanceBeforeMeasuringAnyAge` | Planta na B uma entrada com o identificador da A e idade de 1 s; o registro mostra a recusa na verificação 2 sem idade medida, a leitura da B (contador 1) e a decisão com o valor lido | Aprovado, 10 de 10 na verificação 3 (2026-11-04) |
| AC-15 | `Caching/StampAge/StampAgeCachePrototypeTests.cs` › `DecideAsync_Should_UseEntryUntilOneTickBeforeValidityAndReadSourceOnceAtValidity` | Em t + V − 1 tique usa a entrada (idade exata, contador 1); em t + V lê a fonte uma vez (contador 2); em t + V + 1 s mede idade de 1 s, o que prova o novo início | Aprovado, 10 de 10 na verificação 3 (2026-11-04) |
| AC-16 | `Caching/StampAge/StampAgeCachePrototypeTests.cs` › `DecideAsync_Should_ReadSourceItselfWithoutWaiting_WhenAnotherReadIsHeld` | Com uma leitura presa iniciada antes da troca, outra decisão termina sem esperar, com o contador em 2, as conferências antes da decisão e o carimbo novo aceito (o antigo, recusado) | Aprovado, 10 de 10 na verificação 3 (2026-11-04) |
| AC-17 | `Caching/StampAge/StampAgeCachePrototypeTests.cs` › `DecideAsync_Should_FinishFiveChecksBeforeDeciding` (ausente, outra instância, idade negativa, idade igual a V e válido) e `DecideAsync_Should_FailClosedWithoutStoringOrRetrying_WhenInjectedClockRegressionMakesFreshAgeNegative` (injeção de falha, DEC-16) | Compara a sequência exata do registro de passos em cada situação do valor guardado: as conferências vêm antes da decisão; nas quatro primeiras, a leitura nova passa pelas verificações 3, 4 e 5; na válida, o contador fica em 0. Na injeção de falha, o instante regride 1 s durante a leitura presa: a idade medida da leitura nova é −1 s, a decisão é recusada na verificação 4, a fonte é lida uma vez só, e a decisão seguinte não encontra entrada, o que prova que nada foi gravado | Aprovado, 60 de 60 na verificação 3 (2026-11-04): 50 nas cinco situações do valor guardado e 10 na injeção de falha da leitura nova (DEC-16) |
| AC-18 | `Caching/StampAge/StampAgeCachePrototypeTests.cs` › `DecideAsync_Should_AcceptOldStampAtMostUntilOldReadStartPlusValidity` (1 e 2 instâncias) | Leitura antiga presa em t0, troca em t0 + 0,5 s, gravação atrasada em t0 + 1 s e decisões até t0 + V + 0,5 s: o último aceite é exatamente em t0 + V − 1 tique, e tudo a partir de t0 + V é recusado | Aprovado, 20 de 20 na verificação 3 (2026-11-04) |
| AC-19 | `Caching/StampAge/StampAgeCachePrototypeTests.cs` › `DecideAsync_Should_ReadSourceBeforeDeciding_WhenEntryFromAnotherRequestIsExactlyValidityOld` | Entrada de outra requisição com idade exatamente V: a decisão lê a fonte antes (contador + 1) e recusa o carimbo antigo | Aprovado, 10 de 10 na verificação 3 (2026-11-04) |
| AC-20 | `Caching/StampAge/StampAgeCachePrototypeTests.cs` › `DecideAsync_Should_ReadSourceExactlyAtFirstDecisionWithAgeAtValidity_WhenDecidingEveryHalfSecond` | Decisões a cada 0,5 s: contadores `[1, 1, 1, 1, 1, 2]` e idades medidas de 0,5 s a 3 s, contadas do mesmo início | Aprovado, 10 de 10 na verificação 3 (2026-11-04) |
| AC-21 | `Caching/StampAge/StampAgeCachePrototypeTests.cs` › `DecideAsync_Should_KeepEachInstanceWindow_WhenClocksStartDifferentlyAndWallTimesAreShifted` | Relógios com partidas diferentes e hora de parede a + 5 s e − 5 s (diferença de 10 s conferida): a mesma janela do AC-18 nas duas, e o valor medido pelo relógio da A é recusado na verificação 2 da B | Aprovado, 10 de 10 na verificação 3 (2026-11-04) |
| AC-22 | `Caching/StampAge/StampAgeCachePrototypeTests.cs` › `Invalidate_Should_MakeNextDecisionReadSourceAndRejectOldStamp_WhenStampChanges`, `Invalidate_Should_NotRenewLateWriteOfReadStartedBeforeIt` e `DecideAsync_Should_RejectPreviousCredentialStamp_WhenPasswordChangeRenewsItAndEntryIsInvalidated` | Depois da troca e da invalidação, a decisão lê a fonte e recusa o antigo; a gravação atrasada depois da invalidação vale só até t0 + V − 1 tique; com o carimbo real da credencial, a troca de senha pela API e a invalidação levam à recusa do carimbo anterior | Aprovado, 30 de 30 na verificação 3 (2026-11-04) |
| AC-23 | Build da solução e `dotnet test tests/TickestPristine.IntegrationTests` (projeto inteiro) | Na verificação: 0 avisos e todos os testes do projeto aprovados, com os 11 de evidência ignorados | Aprovado na verificação 3 (2026-11-04): 0 avisos em Debug e em Release; `dotnet test TickestPristine.slnx` com 1533 aprovados, 0 com falha e 11 ignorados (arquitetura 60, unitários 905, integração 568 mais os 11 ignorados), em 6 min 40 s. O CI do PR continua sendo a validação definitiva |
| AC-24 | `git diff origin/main` e `git status` da branch | Na verificação: só arquivos novos em `tests/TickestPristine.IntegrationTests/` e a spec em `specs/050-validacao-ct26-carimbo-cache/` | Aprovado (2026-11-04): só a spec e os 11 arquivos novos; nada fora do escopo |
| AC-25 | Seção 9 desta spec | Na verificação: as 10 execuções por critério, o previsto e o observado, a comparação da 1.3 e a referência pendente na CT-26 da 049 | Aprovado (2026-11-04): seção 9 com a verificação 3; referência na CT-26 pendente (DEC-14) |
| AC-26 | `Caching/StampAge/StampAgeCachePrototypeTests.cs` › `DecideAsync_Should_LetOldReadOverwriteNewerValueWithoutExtendingItsWindow_WhenItEndsBeforeValidity` e `DecideAsync_Should_RefuseOldReadResultBeforeStoringIt_WhenItEndsAtOrAfterValidity` (em t0 + V e t0 + V + 1 s) | Rodada 1: a A grava em t0 + V − 1 s sobre o valor da B (idade medida 2 s), o antigo vale até t0 + V − 1 tique e é recusado em t0 + V, e a decisão seguinte volta ao novo. Rodada 2: a A falha fechada sem gravar; a idade medida prova que o valor da B continua no dicionário, e os passos e o contador da decisão seguinte também são conferidos | Aprovado, 30 de 30 na verificação 3 (2026-11-04) |
| AC-27 | `Caching/StampAge/StampAgeCachePrototypeTests.cs` › `DecideAsync_Should_FailClosedWithoutStoringUsingOrRetrying_WhenFreshReadAgeReachesValidity` | Leitura nova presa até a idade V: decisão recusada mesmo com o carimbo do token igual ao lido, uma leitura só, registro com a recusa pela idade da leitura nova, e a decisão seguinte mede a idade da entrada antiga | Aprovado, 10 de 10 na verificação 3 (2026-11-04) |
| AC-28 | `Caching/StampAge/StampAgeCachePrototypeTests.cs` › `Prototype_Should_KeepStampsOutOfDecisionsAndDiagnostics`, e a inspeção do arquivo na verificação | Exercita a decisão, a invalidação, o identificador, o método de teste e os diagnósticos, e confere que nenhum carimbo aparece nas decisões nem no registro de passos. O teste de estrutura do AC-13 confere, pelo texto do arquivo e sem reflexão, que os membros públicos são só esses sete e que a decisão e o registro de passos não têm carimbo | Aprovado, 10 de 10 na verificação 3 (2026-11-04), com a superfície conferida também pelo teste de estrutura, mais a inspeção da seção 9 |

## 9. Evidências de verificação
- **Build:** `dotnet build TickestPristine.slnx` (Debug), depois da última alteração em `tests/` (T-7), e `dotnet build TickestPristine.slnx -c Release`, a configuração do CI, em 2026-11-04 às 01:18: 0 avisos e 0 erros nos dois. As verificações 1 e 2 tiveram o mesmo resultado nos builds delas. Nenhum resultado anterior a esta sessão foi reaproveitado.
- **Testes** (horário de Brasília; todos com `--no-build` sobre esse build, com os TRX guardados fora do repositório). **Verificação 3, a vigente** (2026-11-04, depois da T-7):
  - **suíte completa** `dotnet test TickestPristine.slnx`, sem `TICKEST_EVIDENCIA_CT26` (AC-12, AC-23 e REQ-6), das 01:18 às 01:25, em 6 min 40 s: 1533 aprovados, 0 com falha e 11 ignorados. Por projeto: arquitetura, 60 aprovados; unitários, 905 aprovados; integração, 568 aprovados e 11 ignorados (as evidências, com o motivo);
  - **só as evidências, sem a variável** (AC-12), às 01:25: 11 ignorados, com o motivo "Evidência sob demanda: rode com TICKEST_EVIDENCIA_CT26=1."; o `docker events` da janela da execução não registrou nenhum contêiner criado;
  - **protótipo, 10 execuções** de `--filter "FullyQualifiedName~Caching.StampAge"`, das 01:25 às 01:27: 240 de 240 aprovados (24 casos × 10, com o teste de injeção de falha);
  - **evidências, 10 execuções** com `TICKEST_EVIDENCIA_CT26=1` e `--filter "FullyQualifiedName~Evidence.Ct26"`, sozinhas na máquina, das 01:27 às 01:38: 110 de 110 aprovados (11 × 10), com o B7 na montagem da DEC-15;
  - **auditoria** `python scripts/auditoria_sdd.py .`, às 01:18, rodada de novo porque a spec mudou de texto: score 100%, CONFORME, 0 falhas críticas, código de saída 0;
  - **repetições:** as 10 execuções valem para os critérios com teste (evidências e protótipo). A suíte completa roda uma vez, e o AC-12, o AC-24 e o AC-25 são conferências da execução, do diff e desta seção;
  - **verificações anteriores, só como histórico** (os resultados delas não valem onde o código mudou depois):
    - verificação 1 (2026-11-03, das 23:46 de 2026-11-03 às 00:07 de 2026-11-04): projeto de integração inteiro com 567 aprovados, 0 com falha e 11 ignorados; protótipo 230 de 230; evidências com 100 aprovados e 10 com falha, todas do B7 na montagem antiga, com o relógio da A (abaixo); unitários e arquitetura não rodaram; auditoria 100%;
    - verificação 2, depois do ciclo de correção 1 (2026-11-04, das 00:15 às 00:24): sem a variável, 11 ignorados e nenhum contêiner; protótipo 230 de 230; cenários R 40 de 40.
- **Resultados das evidências, por cenário** (verificação 3: 10 execuções cada; mínimo e máximo do que os testes registraram):

  | Cenário | AC | Previsto | Observado | Resultado |
  |---|---|---|---|---|
  | B1 | AC-1 | valor antigo, sem nova leitura, até gravação + V − 0,5 s; nova chamada depois de gravação + V + 0,5 s | leitura presa por 3,500 a 3,515 s; valor antigo devolvido; nova chamada em gravação + 3,054 a 3,065 s | conforme, 10 de 10 |
  | B2 | AC-2 | leitura mais longa que V: a B não aceita o payload; mais curta: a B aceita até início + V e não depois de início + V + 0,5 s | payload no Redis nas duas rodadas; na longa, a B chamou a própria função (1 chamada); na curta, chamou em início + 3,007 a 3,103 s | conforme, 10 de 10 |
  | B3 | AC-3 | memória da instância e Redis com o valor antigo | chave no Redis; memória e Redis devolveram o valor antigo, sem chamar a função | conforme, 10 de 10 |
  | B4 | AC-4 | a chamada que chega depois da remoção recebe o valor antigo, sem chamar a própria função | não terminou antes da liberação; valor antigo; 0 chamadas | conforme, 10 de 10 |
  | B5 | AC-5 | depois da invalidação da tag, o valor antigo vale até gravação + V | valor antigo devolvido; nova chamada em gravação + 3,054 a 3,062 s | conforme, 10 de 10 |
  | B6 | AC-6 | releitura em gravação + V ± 0,5 s; o tempo de vida no Redis não aumenta | releitura em gravação + 3,251 a 3,265 s (leituras no meio de cada meio segundo); tempo de vida sempre decrescente, de 2,739–2,746 s a 0,238–0,253 s | conforme, 10 de 10 |
  | B7 | AC-7 | a A grava com o relógio do sistema, e a B lê com o relógio adiantado em 2 s: o prazo percebido pela B se antecipa 2 s ± 0,5 s | diferença B − A na gravação: 0,000 s com relógios iguais e 2,000 s com a B adiantada. Tempo real da gravação até a B chamar a própria função: 3,053 a 3,078 s com relógios iguais e 1,009 a 1,104 s com a B adiantada. Antecipação: 1,958 a 2,056 s | conforme, 10 de 10 |
  | R1 | AC-8 | a requisição só termina depois da liberação; `user-active-{id}` na memória; 200 até gravação + 3 s e 403 depois de gravação + 3,5 s | não terminou antes da liberação; 200; entrada na memória; último 200 em gravação + 2,881 a 2,918 s e primeiro 403 em gravação + 3,148 a 3,183 s | conforme, 10 de 10 |
  | R2 | AC-9 | provas do caminho; a A responde 403; a B, 200 até gravação na B + 3 s e 403 depois de mais 0,5 s | a primeira requisição na B só terminou depois da liberação, com a entrada na memória da B; A com 403; na B, último 200 em gravação + 2,858 a 2,930 s e primeiro 403 em gravação + 3,125 a 3,189 s | conforme, 10 de 10 |
  | R3 | AC-10 | provas do caminho; a B, sem consulta própria, responde 200 só até o início da leitura na A + 6 s; a A, 200 até gravação + 3 s | leitura presa por 1,507 a 1,519 s; a requisição na A só terminou depois da liberação, com a entrada na memória da A; chave no Redis; A com 200 até gravação + 2,5 s; na B, último 200 em início + 5,835 a 5,882 s e primeiro 403 em início + 6,099 a 6,150 s, com 0 consultas próprias enquanto aceitava | conforme, 10 de 10 |
  | R7 | AC-11 | provas do caminho; a segunda requisição termina só depois da liberação, sem consulta própria, e responde 200 | as duas requisições só terminaram depois da liberação; entrada na memória; 200; 1 consulta de conta ativa no total | conforme, 10 de 10 |

- **B7 (AC-7), corrigido pela DEC-15:** a montagem e a previsão seguem o B7 original. A A grava com o relógio do sistema, e a B lê com o relógio adiantado em 2 s, só na hora de parede. O tempo é medido em tempo real (`Stopwatch`), e o registro mostra também os relógios da A e da B na gravação e na chamada da função da B.
  - O mecanismo foi conferido no código do HybridCache 10.11.0, `dotnet/extensions` no commit `25f07afa51034e37b334210642d7dc5f191c142d` (o do `.nuspec` do pacote):
    - cada instância usa o `TimeProvider` da injeção de dependência (`Internal/DefaultHybridCache.cs:78`) para os carimbos (`DefaultHybridCache.TagInvalidation.cs:198`);
    - o payload leva o carimbo do início da operação, pelo relógio de quem grava, e a duração V (`DefaultHybridCache.StampedeStateT.cs:34` e `:40`; `DefaultHybridCache.L2.cs:227-228`);
    - quem lê calcula o tempo restante com o próprio relógio, `criação + duração − agora`, e recusa o payload quando ele chega a 0 (`HybridCachePayload.cs:203`, `:211` e `:238-244`);
    - a cópia em memória de quem lê dura o menor entre a validade da memória e esse tempo restante, contada em tempo real (`DefaultHybridCache.L2.cs:189-198`; `DefaultHybridCache.StampedeStateT.cs:237`);
    - a chave no Redis tem validade relativa de V, contada a partir da gravação, sem depender de nenhum dos relógios (`DefaultHybridCache.cs:123`; `DefaultHybridCache.L2.cs:230` e `:258-280`).

    Com a B adiantada em 2 s, o tempo restante que ela calcula cai para cerca de V − 2 s, e ela chama a própria função cerca de 2 s antes, como foi medido.
  - **Montagem anterior, só como histórico:** nas verificações 1 e 2, o AC-7 deslocava o relógio da A, que grava, e previa que o prazo da B se deslocaria 2 s. Nas 10 execuções, o deslocamento foi de −0,011 a +0,025 s. O tempo restante maior que a B calculava ficava escondido pela cópia em memória dela e pela chave no Redis, que valem V a partir da gravação. A previsão estava errada, e o teste, certo; a DEC-15 levou o AC-7 de volta ao B7 original.
- **Resultados do protótipo, por critério** (verificação 3: 10 execuções de cada caso, além da suíte completa; todas as asserções são exatas, sem tolerância; as verificações 1 e 2 deram os mesmos resultados nos casos que já existiam):

  | Critério | Requisito ou decisão | Casos × execuções | Resultado |
  |---|---|---|---|
  | AC-13 | requisitos 1 e 3 | 2 × 10 | 20 de 20: estrutura conferida; o cache da aplicação gravou no Redis isolado, e mesmo assim não houve nenhum comando de dados nele durante as operações do protótipo, nenhuma chave do usuário, `MemoryCache.Count` igual antes e depois, e os valores plantados não foram usados (a fonte foi lida uma vez em cada instância) |
  | AC-14 | requisitos 2, 4 e 6 | 1 × 10 | 10 de 10: recusa na verificação 2, sem idade medida, e uma leitura da B |
  | AC-15 | requisitos 5 e 6 | 1 × 10 | 10 de 10: aceite com idade V − 1 tique, leitura única em V e idade de 1 s em V + 1 s |
  | AC-16 | sem junção | 1 × 10 | 10 de 10: leitura própria com a outra presa, contador 2 |
  | AC-17 | requisito 7 e REQ-4 | 6 × 10 | 60 de 60: sequência exata das conferências nas cinco situações do valor guardado; na injeção de falha (DEC-16), idade da leitura nova de −1 s, recusa na verificação 4, uma leitura só e nada gravado |
  | AC-18 | C1 | 2 × 10 | 20 de 20: último aceite em t0 + V − 1 tique, com uma e com duas instâncias |
  | AC-19 | C2 | 1 × 10 | 10 de 10 |
  | AC-20 | C3 | 1 × 10 | 10 de 10: leituras `[1, 1, 1, 1, 1, 2]`, idades de 0,5 s a 3 s |
  | AC-21 | C4 | 1 × 10 | 10 de 10: mesma janela com partidas diferentes e hora de parede ± 5 s |
  | AC-22 | C5 | 3 × 10 | 30 de 30, inclusive com o carimbo real da credencial trocado pela API |
  | AC-26 | DEC-7 | 3 × 10 | 30 de 30: sobrescrita sem prolongar a janela; recusa em t0 + V e em t0 + V + 1 s |
  | AC-27 | DEC-11 | 1 × 10 | 10 de 10: falha fechada, sem gravar, sem usar e com uma leitura só |
  | AC-28 | DEC-12 | 1 × 10 | 10 de 10, com os sete membros públicos conferidos também pelo teste de estrutura, mais a inspeção abaixo |
  | AC-23 | C6 | 1 execução da suíte completa | 1533 aprovados, 0 com falha, 11 ignorados; build com 0 avisos em Debug e em Release |

- **Comparação com o HybridCache real (1.3):**

  | Roteiro | HybridCache (evidência, observado) | Protótipo (teste permanente) |
  |---|---|---|
  | Gravação tardia depois da invalidação | AC-1: o valor antigo valeu até gravação + 3,054–3,065 s, contado da gravação | AC-18 e AC-26: o carimbo antigo vale no máximo até t0 + V − 1 tique e é recusado em t0 + V, contado do início da leitura |
  | Chamada que se junta a uma leitura em andamento | AC-4: recebeu o valor antigo; AC-11: o usuário desativado recebeu 200 | AC-16: faz a própria leitura (contador 2) e só aceita o carimbo novo |
  | Invalidação por tag durante a leitura | AC-5: o valor antigo valeu até gravação + 3,054–3,062 s | AC-22: a invalidação não renova; a gravação atrasada vale só até t0 + V − 1 tique |
  | Duas instâncias com Redis | AC-2: a B aceitou o payload até início + V; AC-10: o valor antigo passou pelo Redis, e a B aceitou o desativado até início + 5,835–5,882 s | AC-13: nenhum comando de dados no Redis e nenhuma chave do usuário |
  | Relógios deslocados | AC-7: com o relógio de quem lê adiantado em 2 s, o prazo dele se antecipou 1,958–2,056 s; com o relógio de quem grava adiantado (montagem antiga), o deslocamento ficou escondido | AC-21: a hora de parede ± 5 s não muda a janela, e o valor medido pelo relógio da outra instância é recusado na verificação 2 |
  | Renovação por acesso | AC-6: não há; o tempo de vida só decresce, e a releitura veio em gravação + 3,251–3,265 s | AC-20: não há; as idades medidas partem do mesmo início |

- **Estrutura do protótipo (AC-13 e AC-28)**, pela leitura de `tests/TickestPristine.IntegrationTests/Caching/StampAge/StampAgeCachePrototype.cs`:
  - construtor único (`:36`), com o relógio, a fonte e a validade;
  - estado privado: o dicionário (`:28`), o registro de passos (`:29`) e o `InstanceId`, criado com a instância (`:43`); o relógio só é usado em `GetTimestamp` (`:65`) e `GetElapsedTime` (`:122`);
  - escrita só por atribuição incondicional (`:79`; `:95` no método de teste), remoção por `TryRemove` (`:89`) e leitura por `TryGetValue` (`:100`); sem `AddOrUpdate`, `GetOrAdd`, `TryAdd` ou `TryUpdate`;
  - membros públicos: `InstanceId`, `SourceReadCount`, `Steps`, `DecideAsync`, que devolve só o id da decisão e o resultado (`:56`, `:176`), `Invalidate` e `PlantForTest`, ambos `void`. Nenhum devolve carimbo ou `StampAgeEntry`, e o registro de passos (`:178`) não tem carimbo;
  - nenhuma referência a HybridCache, `IMemoryCache`, `IDistributedCache`, `StackExchange.Redis`, `IServiceProvider` ou reflexão, o que o teste de estrutura também confere;
  - no host do AC-13, as duas instâncias, criadas pelo teste com `new`, entram como instâncias prontas (`StampAgeCachePrototypeTests.cs:140-141`); o contêiner não as constrói, e o dicionário nunca é registrado.
- **Referência na CT-26 da 049 (DEC-14):** pendente até a 049 ter commit. Texto a levar para a CT-26: "Validação por testes na SPEC-050 (branch `test/SPEC-050-ct26-stamp-cache-validation`): as 11 evidências conforme a análise, nas 10 execuções. No B7, com o relógio de quem lê adiantado em 2 s, o prazo percebido por ele se antecipa cerca de 2 s. Com o relógio de quem grava adiantado, o deslocamento fica escondido, porque a cópia em memória e a chave no Redis valem V a partir da gravação. O protótipo da (3b), com dicionário privado e relógio local, passou em todos os critérios, 10 de 10, inclusive na recusa da leitura nova com idade negativa, comprovada por injeção de falha."
- **Revisão:**
  - `ca-review` sobre os 11 arquivos e a spec, 2026-11-03: nenhum achado bloqueante e nenhuma violação de convenção. Duas lacunas não bloqueantes: o ramo de idade negativa da leitura nova (`StampAgeCachePrototype.cs:125`) é defensivo e não é exercitado, porque o relógio controlado nunca volta; o auxiliar de cadastro está repetido em `ActiveUserCacheRaceEvidenceTests.cs:232` e `StampAgeCachePrototypeTests.cs:1008` (linhas atuais), porque o `BaseIntegrationTest` é preso à coleção principal e não muda nesta spec.
  - Revisão adversarial própria, antes da primeira execução: a gravação no Redis pode terminar depois da resposta, o que daria divergência falsa no B2, no B3, no B6, no B7 e no R3. Os testes passaram a esperar a chave no Redis, até 1 s, e a conferir que ela apareceu; o R1 espera a entrada na memória.
  - Revisão adversarial independente (um agente que leu os arquivos sem rodar nada), concluída em 2026-11-04:
    - nenhuma asserção errada nos testes determinísticos, conferidos à mão: sequências de passos, idades, contadores, fronteiras de V − 1 tique e a ordem dos portões;
    - o protótipo cumpre a 1.1: as cinco verificações na ordem, `0 ≤ idade < V` estrita, a falha fechada sem gravar nem repetir, a gravação incondicional, sem junção;
    - as evidências do AC-1 ao AC-6 e do AC-8 ao AC-11 batem com o código da biblioteca;
    - lacunas que levaram ao ciclo de correção 1, registrado no histórico: as provas de caminho dos cenários R, a ligação do host ao Redis no AC-13, os passos da rodada 2 do AC-26 e a superfície do AC-28;
    - ajuste de comentário: o predicado da consulta de conta ativa também pega a ativação de conta, que roda antes das contagens.
  - Os testes alterados no ciclo 1 seguem as mesmas convenções da `ca-review` acima, e só ganharam asserções.
  - `ca-review` das mudanças da T-7, 2026-11-04: nenhum achado bloqueante e nenhuma violação de convenção. A primeira lacuna acima foi fechada: o ramo de idade negativa da leitura nova agora é exercitado pela injeção de falha (DEC-16), sem mudar o protótipo nem o `ControlledTimeProvider`. A repetição do auxiliar de cadastro continua, pelo mesmo motivo.
  - Revisão adversarial própria da T-7, antes de executar: com a B adiantada, a invalidação curinga também usa o relógio da B. O código da biblioteca mostra que ela não interfere: sem invalidação gravada no Redis, o carimbo curinga é 0 (`DefaultHybridCache.L2.cs:163`), e `IsWildcardExpired` só recusa um payload com carimbo menor ou igual a ele (`DefaultHybridCache.TagInvalidation.cs:69-80`).
- **Documentação atualizada:** nenhuma. O `SYSTEM_SPECIFICATION.md` não se aplica (seção 5), porque nenhum comportamento do sistema mudou.
- **CI:** pendente. O PR ainda não existe. A suíte completa já passou localmente na verificação 3, mas o CI do PR continua sendo a validação definitiva, obrigatória antes do merge (DEC-13).
- **Entrega:** <PR #n (o mesmo do campo `pr:`); a spec fica `Entregue` quando o responsável mergear o PR na `main`>

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-11-02 | Rascunho | Spec criada a partir do pedido de Rafael Santos de 2026-11-01 (nona resposta da reavaliação da SPEC 049, CT-26), na branch `test/SPEC-050-ct26-stamp-cache-validation`, numa worktree própria (`C:/dev/tickest-pristine-spec050`) criada da `origin/main` (`2804b07`). |
| 2026-11-02 | Em descoberta | A descoberta foi feita nas rodadas da SPEC 049 (anexo C, 2.10.9). As decisões do responsável viraram a DEC-1 à DEC-4. A investigação do HybridCache 10.11.0 para as cinco condições do relógio (i) está na 1.1. A DEC-5, a DEC-6 e a DEC-7 ficam pendentes para a aprovação, e a DEC-8, a DEC-9 e a DEC-10 são recomendações. Nenhum arquivo de teste foi criado e nada foi executado. |
| 2026-11-02 | Em descoberta | Revisão pedida por Rafael Santos, sem aprovação formal:<br>- a DEC-5, a DEC-6 e a DEC-7 foram decididas;<br>- a comprovação da DEC-5 mostrou que a instância nomeada compartilha o `IMemoryCache` com a instância padrão (1.1), e a forma da instância ficou pendente;<br>- entrou o AC-26 (sobrescrita, DEC-7);<br>- foram revistos o AC-5, o AC-8, o AC-10, o AC-11, o AC-12, o AC-13, o AC-16, o AC-17 e o AC-22;<br>- o caminho dos cenários R foi conferido (1.2);<br>- as evidências ficaram em coleção própria, fora do CI.<br>Nenhum arquivo de teste foi criado e nada foi executado. |
| 2026-11-02 | Em descoberta | Decisões de Rafael Santos, sem aprovação formal:<br>- o HybridCache foi abandonado no armazenamento do protótipo, e a DEC-5 passou a (c1), dicionário privado por instância;<br>- entrou a DEC-11 (falha fechada, sem nova tentativa);<br>- a DEC-7 foi mantida sem escrita condicional;<br>- a DEC-8 passou a usar relógio controlado com fronteiras exatas no protótipo;<br>- entrou a DEC-12 (método de teste para plantar entradas, sem reflexão).<br>Ficaram reescritos a 1.1 e o REQ-4 e o REQ-5, e entrou a 1.3. Foram revistos o AC-13, o AC-14, o AC-15, o AC-16, o AC-17, o AC-18, o AC-19, o AC-20, o AC-21, o AC-22 e o AC-26, e entraram o AC-27 e o AC-28. Nenhum arquivo de teste foi criado e nada foi executado. |
| 2026-11-02 | Em descoberta | Consolidação para a aprovação formal, sem decisão nova:<br>- Rafael Santos confirmou as cinco verificações, na ordem, a validade estritamente `0 ≤ idade < V` da DEC-11 e as definições do AC-26 e do AC-28;<br>- a redação da 1.1, do REQ-4, da DEC-7 e da DEC-11 foi alinhada a essa fronteira, e a DEC-7 deixou explícito que a recusa da DEC-11 não é escrita condicional;<br>- foram conferidas a consistência da DEC-5, da DEC-7, da DEC-8, da DEC-11 e da DEC-12, a rastreabilidade e a numeração dos 28 critérios, a preservação dos doze primeiros critérios (evidências e separação) e os limites de arquivos.<br>Nenhum arquivo de teste foi criado e nada foi executado. |
| 2026-11-02 | Aprovada | Aprovação formal de Rafael Santos no chat: "Aprovo formalmente a SPEC-050 — Validação da CT-26 — Carimbo de Cache." A aprovação se baseia na versão consolidada: decisões, requisitos, os 28 critérios de aceitação, tarefas, rastreabilidade e limites de escopo.<br>**Implementação:** autorizada só por `/spec-implement 050`, numa sessão aberta na worktree `C:\dev\tickest-pristine-spec050`. O pedido cita a branch `test/SPEC-050-ct26-stamp-cache`; a branch da spec, no campo `branch:`, é `test/SPEC-050-ct26-stamp-cache-validation`.<br>**Restrições do responsável:**<br>- só os 11 arquivos novos previstos no projeto de testes e os registros de execução desta spec;<br>- nenhuma alteração em código de produção, projetos, pacotes, CI, migrations, contratos ou outras specs;<br>- sem tocar na worktree nem na branch da SPEC-035;<br>- sem stage, commit, push, PR ou merge: depois da implementação e da verificação, os resultados são apresentados, e qualquer operação Git espera nova autorização;<br>- sem ampliar o escopo nem contornar o portão SDD;<br>- diante de impedimento técnico ou de arquivo fora do escopo, a implementação para e apresenta a situação.<br>A SPEC-049 continua `Em descoberta`, e a CT-26 continua aberta até a implementação real e a validação de ponta a ponta. |
| 2026-11-03 | Aprovada | Ajustes aprovados por Rafael Santos no chat, sem mudança de estado:<br>- a branch foi atualizada com a `origin/main` por fast-forward, de `2804b07` para `b510e46` (merge do PR #64, SPEC-051), a pedido dele ("traga a main para a branch da 050"), sem commit novo;<br>- entrou a DEC-13 ("Vamos seguir as novas atualizações da spec 051 !"): a verificação segue os testes por impacto da SPEC-051, e foram ajustados o REQ-6, o AC-12, o AC-23 e a T-6;<br>- entrou a DEC-14 (opção "Adiar até a 049 ter commit"): a referência na CT-26 da 049 fica pendente, e foram ajustados o REQ-8, o AC-25, a T-6 e a seção 9.<br>Nenhum arquivo de teste foi criado e nada foi executado. |
| 2026-11-03 | Em implementação | `/spec-implement 050` iniciado por Rafael Santos, em modo contínuo, numa sessão aberta na worktree `C:/dev/tickest-pristine-spec050`, na branch `test/SPEC-050-ct26-stamp-cache-validation`, que é a do campo `branch:` (o pedido cita `test/SPEC-050-ct26-stamp-cache`).<br>Restrições repetidas no pedido: só os 11 arquivos novos do plano e os registros desta spec; nada fora do escopo; sem stage, commit, push, PR ou merge. |
| 2026-11-04 | Em implementação | Verificação 1 reprovada na conferência de cobertura (passo 3 da `spec-verify`), com os achados de uma revisão adversarial independente:<br>- os cenários R do AC-9, do AC-10 e do AC-11 não traziam as duas provas de caminho da 1.2 que a seção 3 exige;<br>- o AC-13 não provava que o cache da aplicação usava o Redis isolado;<br>- a rodada 2 do AC-26 não conferia os passos da decisão seguinte;<br>- o AC-28 conferia a superfície pública só pela inspeção.<br>Os resultados da verificação 1 estavam todos aprovados, menos a divergência do AC-7, que é registrada e não reprova a spec.<br>**Ciclo de correção 1:** só foram acrescentadas asserções, sem mudar nenhuma existente, em `ActiveUserCacheRaceEvidenceTests.cs` e `StampAgeCachePrototypeTests.cs`. Rodam de novo só essas classes. |
| 2026-11-04 | Verificada | Verificação 2 aprovada, depois do ciclo de correção 1:<br>- build com 0 avisos, em Debug e em Release;<br>- projeto de integração inteiro com 567 aprovados, 0 com falha e 11 ignorados, e as classes alteradas aprovadas de novo;<br>- protótipo com 230 de 230 em 10 execuções;<br>- evidências: 10 das 11 conforme o previsto em 10 execuções, e a divergência do AC-7 registrada, que pela seção 6 não reprova a spec;<br>- auditoria com score 100% e código de saída 0, reaproveitada no ciclo;<br>- `ca-review` sem achados bloqueantes;<br>- os 28 critérios com teste ou conferência e resultado (seção 8).<br>**Pendências:** rever o fato do B7 na CT-26 da 049 antes de qualquer implementação, a referência na CT-26 (DEC-14) e o CI do PR. Sem stage, commit, push ou PR. |
| 2026-11-04 | Em implementação | Volta de `Verificada` para `Em implementação`: haverá alteração de código depois da verificação, e a spec precisa ser verificada de novo.<br>- Decisões de Rafael Santos no chat, depois do diagnóstico do B7, do AC-17 e do AC-23: "AUTORIZO a correção e complementação da SPEC-050 nos limites abaixo."<br>- Entraram a DEC-15 (o AC-7 volta ao B7 original, com o relógio da B adiantado e a previsão de antecipação) e a DEC-16 (injeção de falha para a idade negativa da leitura nova), e foram ajustados o AC-7 e o AC-17, com a tarefa T-7.<br>- São ajustes de AC aprovados no chat, e não mudança grande: não mudam o objetivo, os REQs, os arquivos do plano, a segurança nem o contrato. Reabrem só o teste do B7, da T-2, e acrescentam um teste à T-5.<br>- Pedido também: `dotnet test TickestPristine.slnx` local, com as evidências desligadas, além do CI do PR.<br>- Branch confirmada: `test/SPEC-050-ct26-stamp-cache-validation` ("A indicação anterior da branch curta foi um equívoco meu."). |
| 2026-11-04 | Verificada | Verificação 3 aprovada, depois da T-7. Rafael Santos pediu no chat para seguir até o fim: "atue coomo especialista .net e decida por mim. vá até finalizar essa spec".<br>- build com 0 avisos, em Debug e em Release;<br>- `dotnet test TickestPristine.slnx`, sem as evidências: 1533 aprovados, 0 com falha e 11 ignorados (arquitetura 60, unitários 905, integração 568 mais os 11), em 6 min 40 s;<br>- protótipo com 240 de 240 em 10 execuções, inclusive a injeção de falha da idade negativa;<br>- evidências com 110 de 110 em 10 execuções, inclusive o B7 corrigido, com antecipação de 1,958 a 2,056 s;<br>- sem a variável, as evidências ficaram ignoradas e nenhum contêiner foi criado;<br>- auditoria com score 100% e código de saída 0, rodada de novo;<br>- `ca-review` sem achados bloqueantes;<br>- os 28 critérios conformes ou aprovados, com resultado na seção 8.<br>**Pendências:** o CI do PR, que é a validação definitiva, e a referência na CT-26 da 049 (DEC-14). Sem stage, commit, push ou PR. |
