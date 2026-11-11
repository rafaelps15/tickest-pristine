# Plano — SPEC-049

Este arquivo é o índice curto do plano técnico da SPEC-049. A definição canônica permanece em [`spec.md`](spec.md), seção 7 (plano técnico), especialmente 7.15 (etapas e portões) e 7.16 (dependências). O inventário, decisões e evidências detalhadas ficam nos anexos A a F.

## Etapa concluída: G3-b, AZ-3 — funções e permissões

1. Migrar `Role` para agregado rico, manter `RolePermission` como entidade interna e carregar as permissões pela consulta nomeada do command (T-208).
2. Mover o catálogo de permissões para o domínio, ler a tabela com Dapper e comparar os dados persistidos com o catálogo (T-210).
3. Conferir build, testes unitários, arquitetura, integrações de roles/permissões/migrations e auditoria SDD; registrar os resultados na rastreabilidade e no anexo B.18.

A AZ-3 está concluída. O AC-23 global e a T-14 continuam abertos até a cobertura de carga dos demais agregados; a próxima entrega da trilha de funções é a spec 036. O escopo, os critérios e as dependências completos continuam definidos em `spec.md`.
