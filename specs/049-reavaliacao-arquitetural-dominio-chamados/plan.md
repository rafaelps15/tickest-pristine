# Plano — SPEC-049

Este arquivo é o índice curto do plano técnico da SPEC-049. A definição canônica permanece em [`spec.md`](spec.md), seção 7 (plano técnico), especialmente 7.7 (chamados com EF), 7.9 (estratégia de testes), 7.15 (plano por etapas) e 7.16 (dependências). O inventário, decisões e evidências detalhadas ficam nos anexos A a F.

## Etapa atual: G3-a, fase 3

1. Incorporar os cronômetros ao agregado `Ticket` e adaptar persistência e seeder (T-7).
2. Migrar os handlers de abertura, atualização, reabertura, exclusão e mensagem para a API do agregado (T-8).
3. Renomear o command do monitor e retirar o prazo da fila por `UPDATE` condicional, sem token global. O agregado e a auditoria são salvos num único `SaveChangesAsync`; o interceptor padrão publica eventos depois do save bem-sucedido (T-9).
4. Confirmar as fronteiras com testes de arquitetura e revisar a qualidade (T-10).
5. Atualizar os fatos afetados em `SYSTEM_SPECIFICATION.md` (T-11).
6. Verificar os critérios da fase, testes, auditoria e documentação (T-12).

O escopo, os critérios de aceite, as dependências e o plano completo por entregas continuam definidos em `spec.md`; este índice não substitui nem altera esses contratos.
