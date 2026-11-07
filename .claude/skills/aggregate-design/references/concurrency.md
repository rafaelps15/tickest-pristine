# Modelos de concorrência

O token fica na raiz, em `src/Domain/{Feature}/`, e na configuração dela, em `src/Infrastructure/{Feature}/`.

## Token na raiz

```csharp
builder.Property(p => p.Version).IsConcurrencyToken();
```

```csharp
_maintainers.Remove(maintainer);
Version++;
```

- O token de concorrência só é conferido quando o save atualiza aquela linha; mudar só os filhos não atualiza a raiz. Todo método que muda o agregado incrementa o `Version`, então duas mudanças simultâneas no mesmo agregado não são gravadas as duas.
- O segundo save falha com `DbUpdateConcurrencyException`. O tratador global de exceções o converte em `409 Conflict`; um projeto cujo tratador ainda responde `500` para ele ganha essa conversão uma vez. O cliente lê de novo e repete a requisição.

## Ponto de serialização para uma regra entre agregados

Quando uma regra lê vários agregados e precisa valer no instante da mudança, as duas operações concorrentes alteram uma mesma linha: em geral, a do agregado dono do número que a regra lê.

```csharp
/// <summary>
/// Conta mais um projeto do usuário; falha sem mudar nada quando ele já tem o limite.
/// </summary>
public Result AddOwnedProject()
{
    Result rule = CheckRule(new UserCannotOwnMoreThanFiveProjectsRule(OwnedProjects));

    if (rule.IsFailure)
    {
        return rule;
    }

    OwnedProjects++;

    return Result.Success();
}
```

```csharp
builder.Property(u => u.OwnedProjects).IsConcurrencyToken();
```

- O número que a regra lê é o token: duas criações que leram a mesma contagem não são gravadas as duas.
- O handler muda o usuário e inclui o projeto no mesmo `SaveChangesAsync`; a regra continua num método do agregado.
- Quando a regra pode ser reparada depois, reaja a um evento de domínio, e mantenha os agregados separados.

## Trava pessimista

Só quando um token não expressa o conflito: uma transação explícita e uma leitura com trava (`FOR UPDATE`) da linha comum. Ela fica fora destas convenções, que não usam unidade de trabalho nem transação no handler.
