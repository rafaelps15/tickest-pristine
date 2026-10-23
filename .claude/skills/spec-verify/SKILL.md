---
name: spec-verify
description: Verifica se uma spec implementada atende aos critérios de aceitação — roda build e testes, confere que cada critério é coberto por testes que exercitam o comportamento descrito e passaram na execução atual, executa a revisão de convenções e atualiza a documentação de estado afetada. Use depois de implementar uma spec ou quando o usuário pedir para verificar uma spec.
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
2. **Build e testes:** rode os comandos de validação do contexto do projeto de forma que o resultado de cada teste fique visível (por exemplo, um relatório por teste ou uma execução filtrada pelos nomes da rastreabilidade). Registre na seção "Evidências de verificação" as contagens (avisos, erros, testes aprovados e com falha) e a data da execução.
3. **Cobertura de cada critério:** para cada AC, abra cada teste listado na rastreabilidade e confirme, lendo o código do teste, que:
   - o cenário preparado corresponde ao "Dado";
   - a ação executada corresponde ao "Quando";
   - as asserções conferem o resultado observável do "Então" (status, estado gravado, registro produzido), e não só a ausência de erro;
   - o teste aparece como aprovado na execução do passo 2.

   Preencha "Como o teste cobre o critério" com o que o teste afirma e "Resultado" com o resultado e a data dessa execução.
4. **Revisão:** execute a skill de revisão de convenções do projeto (por exemplo, `ca-review`) sobre as mudanças da spec e registre o veredito e os achados bloqueantes.
5. **Documentação:** atualize só as seções do documento de estado do sistema que a mudança afetou, citando a spec, e registre quais foram.
6. **Veredito:**
   - build e testes verdes, todo AC coberto conforme o passo 3 e nenhum achado bloqueante → status `Verificada`, com a linha no histórico;
   - caso contrário → o status continua `Em implementação`; registre no histórico a verificação reprovada e o que falta.
7. **Apresente o resultado:** evidências, limitações, riscos e pendências.

## Regras

- Não corrija código nem testes para fazer a verificação passar; relate o que falhou.
- Um AC não conta como atendido se não tiver teste, se o teste não rodou ou falhou nesta execução, ou se o teste não exercitar o comportamento descrito, mesmo que exista e passe. A mera existência do arquivo e do método de teste não basta.
- Evidência de uma execução anterior não vale: a verificação usa a execução feita por ela.
- Não faça stage, commit nem push. `Verificada` não é `Entregue`: a entrega depende de autorização explícita do responsável.
