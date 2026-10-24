# SPEC-03 — Gerenciamento de anexos em chamados

> **Fonte da spec 011.** Backlog de Rafael Santos ("Gerenciamento de Anexos em Tickets"), refinado no chat em 2026-10-24 contra a `origin/main` `5ca764b`. Cópia da aba "SPEC-03" do Claude Doc https://claude.ai/artifact/9JGaAcgbr31TqC7mmWuJx9. O que vale para o desenvolvimento, e com quais decisões, está na spec 011.

Versão 2.0. **O backend já envia, lista, baixa e exclui anexos, com limite de 10 MB e lista de tipos permitidos.** As lacunas reais são: o arquivo físico nunca é apagado, o conteúdo não é inspecionado, o download de arquivo ausente vira 500, e a causa da "exclusão sem efeito" não está no backend testado, o que aponta para o front.

## Diagnóstico do que já existe

| Requisito do backlog | Situação | Evidência no código |
| --- | --- | --- |
| Exclusão no backend | Implementado | `DELETE ticket-attachments/{id}`; `DeleteTicketAttachmentCommandHandler` marca `DeletedAtUtc`, grava histórico `AttachmentRemoved`; 15 testes em `TicketAttachmentsTests.cs` |
| Quem exclui | Implementado | Quem enviou o anexo ou quem tem `tickets:manage` (`TicketAccessGuard.EnsureOwnerOrManagerAsync`) |
| Remoção do arquivo físico | Não implementado | O arquivo fica no disco (GAP-08, Q-08) |
| Confirmação antes de excluir | Inconclusivo | Front em outro repositório, não analisado |
| "Exclusão sem efeito" | Inconclusivo | Backend testado responde 204; a causa provável está no front (rota, id, estado da lista) — ver RF-01.8 |
| Limite de tamanho no backend | Implementado | 10 MB no validator (`TicketAttachmentLimits.MaxFileSizeBytes`); corpo da requisição limitado a 11 MB no endpoint |
| Limite no front | Inconclusivo | Não analisado |
| Limite no proxy reverso | Inconclusivo | Não há proxy no repositório; o nginx, por exemplo, recusa acima de 1 MB por padrão (Q-11) |
| Tipos permitidos | Parcial | Lista de `content-type` com extensão correspondente; não confere o conteúdo real do arquivo (GAP-09) |
| Download seguro | Parcial | `X-Content-Type-Options: nosniff`; arquivo ausente no disco gera 500 |
| Anexo em chamado encerrado | Não tratado | Permitido em qualquer status (Q-07) |
| Limite por chamado | Não implementado | Sem limite de quantidade nem de volume |

## 1. Escopo

**Dentro do escopo:** confirmação e feedback da exclusão no front; correção da exclusão sem efeito; limite de tamanho aplicado e explicado em todas as camadas; inspeção do conteúdo do arquivo; download de arquivo ausente; limite por chamado; remoção física após retenção.

**Fora do escopo:** antivírus (D-04), armazenamento compartilhado entre instâncias (GAP-09), restrição por status do chamado (Q-07, decisão de produto separada).

## 2. Requisitos funcionais

### RF-01 — Exclusão

- RF-01.1: O front DEVE pedir confirmação antes de excluir: título "Excluir anexo", texto "Tem certeza de que deseja excluir o anexo [nome do arquivo]? Ele deixará de aparecer no chamado.", botões "Excluir" e "Cancelar". Cancelar, fechar ou apertar Esc NÃO DEVE enviar requisição.
- RF-01.2: Ao confirmar, o front DEVE chamar `DELETE ticket-attachments/{id}`, mostrar carregamento no item e desabilitar a confirmação e o botão de excluir daquele item até a resposta.
- RF-01.3: O front DEVE remover o anexo da lista só depois de 204, sem recarregar a página, e atualizar o histórico do chamado.
- RF-01.4: Resposta 404 na exclusão DEVE ser tratada como já excluído: o front remove o item e avisa "Este anexo já havia sido removido.".
- RF-01.5: Em 403, o anexo DEVE continuar visível com a mensagem "Você não tem permissão para excluir este anexo."; em falha de rede ou 5xx, "Não foi possível excluir o anexo. Tente novamente.".
- RF-01.6: O botão de excluir DEVERIA aparecer só para quem enviou o anexo ou tem `tickets:manage`; o backend continua sendo a verificação.
- RF-01.7: O backend DEVE manter a exclusão lógica, o histórico `AttachmentRemoved` e o acesso atual. Anexo excluído NÃO DEVE aparecer na listagem nem ser baixado (404).
- RF-01.8: Antes de alterar o front, a equipe DEVE reproduzir a "exclusão sem efeito" e registrar a causa conferindo, nesta ordem: método e rota da chamada; id enviado (do anexo, não do chamado); status HTTP recebido; tratamento do 204 sem corpo pelo cliente HTTP; atualização do estado da lista. A causa DEVE ser coberta por teste do front.

### RF-02 — Remoção física

- RF-02.1: O sistema DEVE apagar do armazenamento os arquivos de anexos excluídos há mais de 30 dias (D-01), por rotina diária, e marcar `PurgedAtUtc` na linha.
- RF-02.2: A rotina DEVE ser idempotente: arquivo já ausente conta como removido e não é erro.
- RF-02.3: A rotina DEVE registrar no log quantos arquivos removeu e quais falharam, sem interromper os demais.

### RF-03 — Tamanho

- RF-03.1: O limite é 10 MB (10.485.760 bytes) por arquivo, mantido em `TicketAttachmentLimits.MaxFileSizeBytes` como fonte única (D-02).
- RF-03.2: A API DEVE expor o limite e os tipos aceitos em `GET ticket-attachments/policy` (`{ maxFileSizeBytes, allowedTypes[] }`), e o front DEVE usá-lo em vez de repetir o número.
- RF-03.3: O front DEVE recusar antes do envio o arquivo acima do limite, com "O arquivo excede o tamanho máximo permitido para anexos. O limite é de 10 MB.".
- RF-03.4: O backend DEVE validar o tamanho real lido do corpo, não o declarado pelo cliente.
- RF-03.5: Corpo acima do limite da requisição (11 MB) DEVE ser respondido com 413 em Problem Details com a mesma mensagem; o front DEVE tratar 413 vindo da API ou do proxy com essa mensagem, mesmo sem corpo.
- RF-03.6: O proxy reverso de produção DEVE aceitar corpos de pelo menos 11 MB na rota de upload (ex.: `client_max_body_size 11m` no nginx).
- RF-03.7: Um chamado DEVE aceitar no máximo 20 anexos ativos e 100 MB somados (D-03); acima disso, 400 `TicketAttachments.TicketLimitReached`.

### RF-04 — Validação do arquivo

- RF-04.1: A lista de tipos permitidos atual DEVE ser mantida como política e documentada no `SYSTEM_SPECIFICATION.md`; nenhum tipo novo DEVE ser restringido sem decisão de produto.
- RF-04.2: O backend DEVE conferir a assinatura do conteúdo (magic bytes) contra o tipo declarado, para os tipos binários da lista (imagens, PDF e formatos Office); divergência → 400 `TicketAttachments.ContentMismatch`, "O conteúdo do arquivo não corresponde ao tipo informado.".
- RF-04.3: O nome original DEVE ser guardado só como metadado, sem caminhos (`/`, `\`, `..`) e sem caracteres de controle, com até 260 caracteres; o nome no disco continua gerado pelo sistema.
- RF-04.4: Falha ao gravar a linha depois de gravar o arquivo DEVE apagar o arquivo (comportamento atual mantido e testado).
- RF-04.5: Download de anexo cuja linha existe e o arquivo não DEVE responder 404 `TicketAttachments.FileMissing` e gerar log de erro, em vez de 500.
- RF-04.6: O download DEVE usar `Content-Disposition: attachment` para todos os tipos, além do `nosniff` atual.

## 3. Matriz de permissões

| Operação | Solicitante do chamado | Responsável | Quem enviou | `tickets:manage` | Outros |
| --- | --- | --- | --- | --- | --- |
| Listar e baixar anexos | Sim | Sim | Sim, se ainda participante | Sim | Não (404) |
| Enviar anexo | Sim | Sim | — | Sim | Não (404) |
| Excluir anexo | Só os que enviou | Só os que enviou | Sim | Sim | Não (403/404) |
| Consultar a política | Qualquer usuário ativo | | | | |

Anexo excluído é imutável: não há restauração pela API. Registro de histórico e de auditoria são imutáveis.

## 4. Requisitos não funcionais

- RNF-01 Auditoria: envio, exclusão e remoção física DEVEM aparecer no histórico do chamado (envio e exclusão, já existentes) e na auditoria (remoção física, nova).
- RNF-02 Segurança (OWASP, upload de arquivos): tamanho real, tipo por conteúdo, nome sanitizado, armazenamento fora da pasta pública (já é), download só por rota autenticada (já é).
- RNF-03 Rate limit: upload limitado a 30 por usuário por 10 minutos; exclusão, a 60 por usuário por minuto.
- RNF-04 Idempotência: exclusão repetida responde 404 sem efeito colateral, e o front trata como sucesso (RF-01.4).
- RNF-05 LGPD: anexos podem conter dados pessoais; a remoção física após a retenção (RF-02) é a política de descarte, até decisão do jurídico (Q-08).

## 5. Decisões pendentes

| ID | Questão | Opção adotada nesta versão | Alternativa |
| --- | --- | --- | --- |
| D-01 | Retenção antes da remoção física (Q-08) | 30 dias | Apagar o arquivo na hora da exclusão |
| D-02 | Limite por arquivo | Manter 10 MB | Outro valor, mudando validator, endpoint e proxy juntos |
| D-03 | Limite por chamado | 20 anexos e 100 MB | Sem limite |
| D-04 | Antivírus (ClamAV) no envio | Fora desta spec | Incluir agora |
| D-05 | Bloquear anexos em chamados encerrados (Q-07) | Fora desta spec | Bloquear em Resolvido, Fechado e Cancelado |
