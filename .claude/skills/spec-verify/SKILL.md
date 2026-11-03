---
name: spec-verify
description: Verifica se uma spec implementada atende aos critérios de aceitação — roda o build e os testes por impacto, reaproveitando os resultados ainda válidos, confere que cada critério é coberto por testes que exercitam o comportamento descrito e passaram numa execução ainda válida, executa a revisão de convenções e atualiza a documentação de estado afetada. Use depois de implementar uma spec ou quando o usuário pedir para verificar uma spec.
argument-hint: '<número ou pasta da spec, ex.: "001">'
---

# Verificar uma spec implementada

> **Contexto do projeto primeiro.** Antes de agir, leia a seção "Contexto do projeto" do `CLAUDE.md` do
> repositório. Os caminhos, namespaces, nomes e exemplos desta skill vêm do modelo de referência (Todos) e
> são ilustrativos. Quando o contexto do projeto declarar caminhos, referências vivas ou desvios, eles
> prevalecem. Se essa seção não existir, inspecione o repositório, siga o slice real mais parecido e
> pergunte antes de assumir uma convenção.

Esta skill não altera código de produção nem testes. Ela só atualiza a spec e as seções afetadas do documento de estado do sistema. Os estados e as transições da spec seguem [../spec-new/references/spec-status.md](../spec-new/references/spec-status.md).

## Fluxo

1. **Localize a spec.** Se o status não for `Em implementação`, pare e informe.
2. **Build, testes e auditoria, por impacto:** rode o conjunto que o critério de impacto do contexto do projeto seleciona para os arquivos alterados pela spec, no lugar da suíte completa, que fica para o CI, de forma que o resultado de cada teste fique visível (por exemplo, um relatório por teste ou uma execução filtrada pelos nomes da rastreabilidade). Siga a regra de validade do contexto do projeto: reaproveite o último build válido e só faça outro, sem avisos, se algum arquivo que afeta o build mudou depois dele; rode, sem recompilar, só os testes selecionados que ainda não têm resultado válido. Os testes de integração rodam uma única vez, aqui; na nova verificação de um ciclo de correção, repita só os de integração afetados pela correção. A auditoria de conformidade `python scripts/auditoria_sdd.py .` (score e código de saída) roda uma vez por verificação e, nos ciclos de correção, é reaproveitada conforme a regra de validade. Registre na seção "Evidências de verificação" as contagens (avisos, erros, testes aprovados e com falha), o score da auditoria, a data de cada execução e quais resultados foram reaproveitados.
3. **Cobertura de cada critério:** para cada AC, abra cada teste listado na rastreabilidade e confirme, lendo o código do teste, que:
   - o cenário preparado corresponde ao "Dado";
   - a ação executada corresponde ao "Quando";
   - as asserções conferem o resultado observável do "Então" (status, estado gravado, registro produzido), e não só a ausência de erro;
   - o teste aparece como aprovado numa execução ainda válida (passo 2).

   Preencha "Como o teste cobre o critério" com o que o teste afirma e "Resultado" com o resultado e a data dessa execução.
4. **Revisão:** execute a skill de revisão de convenções do projeto (por exemplo, `ca-review`) sobre as mudanças da spec e registre o veredito e os achados bloqueantes.
5. **Documentação:** atualize só as seções do documento de estado do sistema que a mudança afetou, citando a spec, e registre quais foram. Faça este passo só se o veredito do passo 6 for aprovar; se for reprovar, não altere o documento de estado e registre no histórico que a documentação fica para a próxima verificação.
6. **Veredito:**
   - build e testes verdes, auditoria com código de saída 0, todo AC coberto conforme o passo 3 e nenhum achado bloqueante → status `Verificada`, com a linha no histórico;
   - caso contrário → o status continua `Em implementação`; registre no histórico a verificação reprovada e o que falta.
7. **Apresente o resultado:** evidências, limitações, riscos e pendências, com o diff da spec e da documentação e a mensagem de commit proposta no formato `tipo(escopo): descrição [SPEC-NNN]`, com o trailer de coautoria do agente. Quando a verificação for chamada pela `spec-implement`, esta apresentação é a parada final única dela, com o diff total. Stage, commit e push são do responsável.
8. **Depois que o PR for aberto:** preencha o campo `pr:` e a linha "Entrega" das evidências com o número do PR e mostre o diff; o commit, na mesma branch, é do responsável. A spec continua `Verificada`; o `/spec-status` passa a mostrá-la como `Entregue` quando o responsável mergear o PR.

## Regras

- Não corrija código nem testes para fazer a verificação passar; relate o que falhou. Quando a verificação for chamada pela `spec-implement`, a reprovação volta para a correção dela, em até 2 ciclos.
- Um AC não conta como atendido se não tiver teste, se o teste não tiver resultado aprovado numa execução ainda válida, ou se o teste não exercitar o comportamento descrito, mesmo que exista e passe. A mera existência do arquivo e do método de teste não basta.
- Uma evidência só vale pela regra de validade do contexto do projeto: execução feita nesta sessão, sem mudança posterior no que ela cobre. Na dúvida, rode de novo.
- Não faça stage, commit nem push. O responsável executa essas operações utilizando a mensagem de commit proposta. `Verificada` não é `Entregue`: a spec só fica `Entregue` quando o responsável mergear o PR.
