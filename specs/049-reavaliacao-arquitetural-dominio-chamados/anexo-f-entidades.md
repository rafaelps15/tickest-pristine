# Anexo F — Diretriz das entidades (REQ-29, DEC-77)

Inventário e plano da diretriz das entidades, pedida por Rafael Santos em 2026-11-06. O `spec.md` tem só o REQ-29, o AC-33, a DEC-77 e a T-19; o conteúdo fica aqui, pela DEC-77.

## F.1 O pedido, nas palavras do responsável (2026-11-06)

> Atualize a **spec 049** com o seguinte direcionamento rigoroso e inegociável:
>
> 1. **Espelho da Skill `add-entity`:** Todas as entidades do projeto devem ser um reflexo exato, rigoroso e literal do modelo definido na skill `add-entity`. O que divergir ou estiver fora desse padrão deve ser **removido imediatamente**.
> 2. **Decisão Arquitetural (Kamil Grzybek & Milan Jovanović):** Para qualquer trecho de código ou situação onde haja dúvida sobre como proceder após a remoção ou ajuste, analise exatamente como **Kamil Grzybek** ou **Milan Jovanović** estruturam, modelam e resolvem esse cenário em arquiteturas limpas e monólitos modulares. O código resultante deve seguir estritamente a semântica, os padrões e a forma exata como eles arquitetam esse tipo de componente.

Respostas do questionário de 2026-11-06 (DEC-77): a diretriz é critério de toda etapa, e não uma etapa nova; a fase 2 da G3-a foi entregue antes; os nomes das duas referências ficam escritos na spec; o conteúdo fica neste anexo.

## F.2 O modelo

Critérios da skill `add-entity` (`SKILL.md`, passos 1 a 5, e `references/rich-entity.md`), com o padrão 7 da `domain-modeling` para o filho criado pelo agregado:

| # | Critério |
|---|---|
| C1 | `sealed`, herda `Entity`, `Guid Id` |
| C2 | um único construtor privado sem parâmetros; o `Create` monta a entidade com inicializador de objeto e é o único lugar em que ele aparece |
| C3 | `private set` em toda propriedade; coleção num campo privado, exposta como `IReadOnlyCollection<T>` |
| C4 | `Create` simples quando a criação não pode falhar e `Result<T>` quando uma regra pode recusá-la, disparando o evento `Created`; o filho criado pelo agregado tem `internal static Create`, e o método do agregado devolve o filho (`Result<TChild>` quando pode falhar) e dispara o evento |
| C5 | os métodos de comportamento mudam o estado, disparam o próprio evento e devolvem `Result`, `bool` ou `void`; a hora atual é parâmetro |
| C6 | erros em `{Entity}Errors`, com códigos `{FeaturePlural}.{Reason}` |
| C7 | eventos `{Entity}{PastTenseVerb}DomainEvent`, records com ids (e o valor anterior numa alteração), sem `OccurredOnUtc` |
| C8 | configuração do EF com a chave, relacionamentos no estilo shadow, `ValueGeneratedNever` no `Id` gerado pelo código e o tamanho só na configuração |
| C9 | `DbSet` no `IApplicationDbContext`, no `ApplicationDbContext` e no `TestDbContext` |
| C10 | nenhum handler ou classe estática atribui as propriedades da entidade nem dispara os eventos dela |

Pela diretriz, o que o modelo não tem sai: membros estáticos públicos além do `Create`, constantes públicas e retornos fora de `Result`, `bool`, `void`, da própria entidade ou do filho.

## F.3 Inventário (2026-11-06)

Conferido no código da branch depois do commit `21144e1`. As configurações do EF já estão no estilo shadow, sem propriedade de navegação (C8).

| # | Entidade | Situação | Desvios | Etapa que remove |
|---|---|---|---|---|
| 1 | `Ticket` | rica, com desvios | estáticos públicos `GetSlaStatus`, `CanBeCanceled`, `OwnerCancelDeadline` e a expressão `Active`; constante pública `OwnerCancelWindowMinutes`; `PostMessage`, `ChangeDescription`, `RecordOpening`, `AddAttachment` e as transições devolvem entradas de histórico ou records de resultado; `Resolve`, `Cancel`, `Reopen` e `Delete` `internal`, chamados pelo `TicketSlaCycleService` | G3-a, fase 3 |
| 2 | `TicketSlaTimer` | filho, com desvios | `Complete`, `Stop`, `Dequeue` e `BelongsTo` `internal`, chamados pelo serviço; mudanças de estado sem evento; `CompleteFirstResponse` recebe o `Ticket` | G3-a, fase 3 (DEC-1 (b)) |
| 3 | `TicketMessage` | filho, com desvio | o evento de publicação sai do `Create` do filho, e não do método do agregado (padrão 7) | G3-a, fase 3 |
| 4 | `TicketAttachment` | filho, com desvios | o evento de envio sai do `Create` do filho; `RecordFilePurge` muda o estado sem evento | G3-a, fase 3 |
| 5 | `TicketHistoryEntry` | filho, no modelo | — | — |
| 6 | `Department` | rica, com desvio | `Update` dispara o evento mesmo sem mudança (desvio declarado no `CLAUDE.md`) | G3-c, OA-2 (REQ-303) |
| 7 | `Sector` | rica, com desvio | o mesmo do `Department` | G3-c, OA-2 (REQ-303) |
| 8 | `User` | anêmica | 9 `{ get; set; }`, sem construtor privado, sem `Create` e sem eventos próprios | G3-b, U1 e U2 (DEC-20) |
| 9 | `UserCredential` | anêmica | 7 `{ get; set; }`; interna do `User` (DEC-20) | G3-b, U2 |
| 10 | `UserInvitation` | anêmica | 7 `{ get; set; }`; interna do `User` (DEC-20) | G3-b, U2 |
| 11 | `UserRole` | anêmica | 3 `{ get; set; }`; vínculo interno do `User` (DEC-20) | G3-b, U1 |
| 12 | `RefreshToken` | anêmica | 8 `{ get; set; }` | G3-b, U2 |
| 13 | `Role` | anêmica | 2 `{ get; set; }`; raiz com `RolePermission` interna (DEC-31) | G3-b, AZ-2 e AZ-3 |
| 14 | `RolePermission` | anêmica | 3 `{ get; set; }`; interna do `Role` (DEC-31) | G3-b, AZ-2 e AZ-3 |
| 15 | `Permission` | anêmica | 6 `{ get; set; }` | G3-b, AZ-2 |
| 16 | `SlaCalendar` | anêmica | 5 `{ get; set; }`; tabela só lida | **sem etapa** (Q-09: manter ou remover a capacidade) |
| 17 | `SlaCalendarHoliday` | anêmica | 4 `{ get; set; }`; tabela só lida | **sem etapa** (Q-09) |
| 18 | `AuditLogEntry` | anêmica | 9 `{ get; set; }`; só a Infrastructure a cria, e não é entidade de domínio | G3-c, OA-3 (DEC-47 (b): sai do Domain) |

Contagem: 1 no modelo, 6 ricas com desvios e 11 anêmicas.

## F.4 Dúvidas resolvidas pelas referências

| # | Dúvida | Kamil Grzybek | Milan Jovanović | Resolução |
|---|---|---|---|---|
| D1 | Estáticos do `Ticket` para as consultas (`GetSlaStatus`, `CanBeCanceled`, `OwnerCancelDeadline`, `Active`) | o domínio não serve as consultas: a leitura é SQL sobre views (`users.v_UserPermissions`), e a regra do agregado é de instância | regras em métodos de instância; consultas com SQL no handler | saem do `Ticket`: a regra fica em método de instância do agregado e, na leitura, no trecho de SQL (`TicketSql`), conferido contra o método de instância pelo teste de concordância; os commands que ainda filtram pelo EF escrevem o predicado na consulta. Revê as DEC-13, DEC-21 e DEC-22 da 043 nesse ponto |
| D2 | Constante pública `OwnerCancelWindowMinutes` | o valor fica dentro da regra ou do agregado, sem contrato público | idem | vira constante privada do `Ticket`; o SQL do prazo usa o mesmo valor, conferido pelo teste de concordância |
| D3 | Mudança de estado sem evento (`RecordFilePurge`, cronômetros) | toda mudança de estado do agregado dispara um evento | idem | cada mudança de estado dispara o próprio evento, mesmo sem consumidor (C5) |
| D4 | Transições `internal` e o `TicketSlaCycleService` | a raiz controla as entidades internas, sem serviço de domínio para regra de um agregado só | idem | já decidido pela DEC-1 (b): o serviço sai, e as transições ficam públicas no `Ticket` |
| D5 | Marcador `IAuditable` | sem trilha de auditoria | `IAuditable` vazio nas entidades auditadas, lido pelo interceptor | fica, na semântica do Milan (desvio de persistência já declarado no `CLAUDE.md`) |
| D6 | `AuditLogEntry` no Domain | registros técnicos fora do domínio (o `OutboxMessage` fica em `BuildingBlocks/Application/Outbox`) | o registro de auditoria nasce no interceptor da persistência | sai do Domain, como propõe a DEC-47 (b); decisão no portão da G3-c |
| D7 | Evento disparado pelo `Create` do filho | o `MeetingComment` dispara o `MeetingCommentAddedDomainEvent` no próprio construtor | — | vale a skill, que prevalece sobre as referências: o método do agregado dispara o evento (padrão 7) |

## F.5 Pontos para a aprovação

Pontos que as referências não resolvem e que dependem do responsável; a spec não volta para `Aprovada` sem eles.

1. **Dois filhos no mesmo método.** O `PostMessage` e o `AddAttachment` criam o filho e a entrada do histórico. O padrão 7 devolve um filho só, e as duas referências não têm histórico como registro: o Kamil projetaria os eventos, o que a DEC-73 (2A) descartou. Opções para a fase 3: (a) o histórico vira coleção interna do `Ticket` (campo privado e `IReadOnlyCollection`, C3), gravado com ele, e os métodos devolvem só o filho; (b) dois métodos, um para o filho e outro para a entrada; (c) manter os records de resultado como desvio declarado.
2. **`SlaCalendar` e `SlaCalendarHoliday`.** Não têm etapa: a Q-09 deixou aberto manter ou remover a capacidade. Com a diretriz, ou as duas entram numa etapa (G3-a fase 3, junto do SLA, ou G3-c), ou a capacidade sai.

## F.6 Critério por etapa (AC-33, T-19)

- A T-19 cria um teste de arquitetura que confere C1 a C5 por reflexão em todo tipo que herda `Entity`: `sealed`, um único construtor privado sem parâmetros, nenhum `set` público ou interno, um `Create` estático, nenhum membro estático público além dele e nenhuma constante pública.
- O teste começa com a lista de exceções da F.3, uma entrada por entidade e desvio, e cada etapa tira as suas; ao fim do programa, a lista está vazia.
- Nenhuma etapa termina com entrada dela na lista (REQ-29).

## F.7 Portão da G3-b

Situação em 2026-11-06, pela 7.15.1:

| # | Item | Situação |
|---|---|---|
| 1 | G2 entregue (043 parte 1) | feito (PR #77) |
| 2 | DEC-20, DEC-21, DEC-22, DEC-29, DEC-31, DEC-32, DEC-59 e DEC-70 | decididas |
| 3 | DEC-23, DEC-26, DEC-35 e DEC-69 | **pendentes** |
| 4 | DEC-24, DEC-25, DEC-27, DEC-28, DEC-30 e DEC-39 | **recomendação a confirmar no portão** |
| 5 | DEC-76 (view de permissões efetivas) | **pendente no portão**, recomendada (a) |
| 6 | Spec 036, revista pela `spec-new` e entregue antes da U1 | **não está na `main`** |
| 7 | Spec 037, revista pela `spec-new` e entregue antes da U2 | **não está na `main`** |
| 8 | Skill de desenho de agregados, antes da AZ-3 e da U2 | **não existe**; precisa de spec própria e de autorização para mudar skills |
| 9 | REQ-29: as 8 entidades de identidade (8 a 15 da F.3) terminam a G3-b no modelo | critério novo (DEC-77) |
| 10 | T-19 ativa antes da primeira tarefa da G3-b | a fazer |

O primeiro passo da trilha de funções (AZ-2: T-205, T-206 e a T-207 com a DEC-9 da 043) começa depois dos itens 3 a 5 e da T-19; a 036, a 037 e a skill de agregados seguem a ordem da 7.15.4.
