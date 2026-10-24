---
id: 011
titulo: Anexos — conteúdo verificado, limites explícitos, arquivo ausente e remoção física
status: Rascunho        # Rascunho | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Cancelada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em:
branch: feat/SPEC-011-attachment-hardening
backlog:
---

# 011 — Anexos: conteúdo verificado, limites explícitos, arquivo ausente e remoção física

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat, em 2026-10-24: "AGORA COLOQUE AS ESPCS DENTRO DO PROJETO E PRONTAS PARA O DESENVOLVIMENTO", sobre o backlog "Gerenciamento de Anexos em Tickets" refinado no mesmo dia.
- **Fonte:** `specs/_fontes/anexos-chamados.md` (o diagnóstico do que já existe está lá, com evidências).
- **Referências:** `SYSTEM_SPECIFICATION.md` 5.6 (anexos), 7.1 (GAP-08: arquivo não é removido; GAP-09: sem inspeção de conteúdo, disco local), 9.5 (Q-07 status do chamado, Q-08 retenção, Q-11 implantação).
- **Código:** `Application/TicketAttachments/TicketAttachmentLimits.cs` (10 MB; 11 tipos permitidos com extensão correspondente: PNG, JPEG, GIF, WebP, PDF, DOC, DOCX, XLS, XLSX, TXT, ZIP), `TicketAttachments/Upload/*`, `TicketAttachments/Delete/DeleteTicketAttachmentCommandHandler.cs` (exclusão lógica, histórico `AttachmentRemoved`, quem enviou ou `tickets:manage`), `TicketAttachments/Download/GetTicketAttachmentContentQueryHandler.cs` (abre o arquivo sem tratar ausência), `Abstractions/Storage/IFileStorage.cs` (`DeleteAsync` já existe e tolera chave inexistente), `Infrastructure/Storage/LocalFileStorage.cs`, `Web.Api/Endpoints/TicketAttachments/Upload.cs` (limite de corpo de 11 MB) e `Download.cs` (`nosniff`), `Domain/Tickets/TicketAttachment.cs`.
- **Fatos do repositório que afetam a fonte:**
  - A exclusão funciona no backend e tem testes de integração; a "exclusão sem efeito" relatada não foi reproduzida aqui e fica para investigação no front (seção 5).
  - Não há nenhum serviço em segundo plano (`BackgroundService`) na solução; uma rotina de limpeza seria a primeira, e o modelo de implantação (uma ou várias instâncias) ainda não está decidido (Q-11). Ver DEC-1.
  - DOCX e XLSX são ZIP por dentro: a assinatura sozinha não distingue um do outro nem de um ZIP comum.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | O backend confere a assinatura do conteúdo (primeiros bytes) contra o tipo declarado: PNG, JPEG, GIF, WebP, PDF, DOC e XLS (OLE2) pela assinatura própria; DOCX, XLSX e ZIP pela assinatura ZIP, e DOCX e XLSX também pela presença de `word/` ou `xl/` no índice do pacote; TXT por não conter byte nulo nos primeiros 8 KB. Divergência → 400 `TicketAttachments.ContentMismatch`, "O conteúdo do arquivo não corresponde ao tipo informado.", sem gravar arquivo nem linha. | RF-04.2 |
| REQ-2 | O tamanho validado é o do conteúdo recebido, e não um valor declarado pelo cliente. | RF-03.4 |
| REQ-3 | Corpo acima do limite da requisição de upload responde 413 em Problem Details, com a mensagem "O arquivo excede o tamanho máximo permitido para anexos. O limite é de 10 MB.". | RF-03.5 |
| REQ-4 | `GET ticket-attachments/policy` (usuário ativo) devolve `{ maxFileSizeBytes, maxAttachmentsPerTicket, maxTotalBytesPerTicket, allowedTypes: [{ contentType, extensions[] }] }`, lido das mesmas constantes usadas na validação. | RF-03.2 |
| REQ-5 | Um chamado aceita no máximo 20 anexos não excluídos e 100 MB somados; o envio que passar disso → 400 `TicketAttachments.TicketLimitReached`, sem gravar arquivo nem linha. | RF-03.7; DEC-2 |
| REQ-6 | O nome original é recusado (400) se tiver `/`, `\`, `..` ou caracteres de controle; o limite de 260 caracteres se mantém. | RF-04.3 |
| REQ-7 | Download de anexo cuja linha existe e cujo arquivo não existe → 404 `TicketAttachments.FileMissing`, com log de erro contendo o id do anexo, em vez de 500. | RF-04.5 |
| REQ-8 | O download sempre envia `Content-Disposition: attachment` com o nome original, além do `nosniff` atual. | RF-04.6 |
| REQ-9 | Remoção física do arquivo de anexos excluídos, conforme DEC-1; a linha guarda `PurgedAtUtc`; arquivo já ausente não é erro; falha de remoção é registrada em log e não desfaz a exclusão lógica. | RF-02; DEC-1 |
| REQ-10 | Comportamentos atuais mantidos: exclusão lógica com histórico `AttachmentRemoved`; quem pode excluir; anexo excluído some da listagem e o download dele dá 404; exclusão repetida dá 404 sem efeito; falha ao gravar a linha apaga o arquivo recém-gravado. | RF-01.7, RF-04.4, RNF-04 |
| REQ-11 | Rate limit: upload, 30 por usuário a cada 10 minutos; exclusão, 60 por usuário por minuto. | RNF-03 |

## 3. Critérios de aceitação
| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | participante do chamado | envia um executável renomeado `foto.png` com `image/png`; um PNG real; um ZIP renomeado `planilha.xlsx`; um XLSX real; um `notas.txt` com bytes nulos | 400 `TicketAttachments.ContentMismatch`; 200; 400; 200; 400. Nos 400, nenhum arquivo no armazenamento e nenhuma linha nova |
| AC-2 | REQ-1 | — | testes unitários do verificador com um exemplo válido e um inválido de cada um dos 11 tipos | todos classificados corretamente |
| AC-3 | REQ-2 | — | envio de 10 MB + 1 byte direto à API | 400 com a mensagem de tamanho; nada gravado |
| AC-4 | REQ-3 | — | envio com corpo de 12 MB | 413 em Problem Details com a mensagem do REQ-3 |
| AC-5 | REQ-4 | usuário ativo | `GET ticket-attachments/policy` | 200 com 10485760, 20, 104857600 e os 11 tipos com suas extensões |
| AC-6 | REQ-5 | chamado com 20 anexos; outro com 95 MB em anexos | envia mais um anexo de 1 MB em cada; depois exclui um anexo do primeiro e envia de novo | 400 `TicketAttachments.TicketLimitReached` nos dois; depois da exclusão, 200 |
| AC-7 | REQ-6 | — | envio com nome `../../a.png` e com nome contendo `\u0007` | 400 nos dois |
| AC-8 | REQ-7 | anexo com a linha no banco e o arquivo apagado do armazenamento | o participante baixa o anexo | 404 `TicketAttachments.FileMissing`; log de erro com o id do anexo |
| AC-9 | REQ-8 | um anexo PDF | o participante baixa | cabeçalhos `Content-Disposition: attachment; filename=...` e `X-Content-Type-Options: nosniff` |
| AC-10 | REQ-9 | anexo excluído que cumpre a condição da DEC-1 | a remoção física roda | o arquivo não existe mais no armazenamento; `PurgedAtUtc` preenchido; rodar de novo não gera erro |
| AC-11 | REQ-9 | `IFileStorage.DeleteAsync` do teste lança exceção | a remoção física roda | a exclusão lógica continua; `PurgedAtUtc` continua nulo; log de erro |
| AC-12 | REQ-10 | anexo enviado pelo Colaborador A | B (sem `tickets:manage`) exclui; A exclui; A exclui de novo; A lista os anexos; A baixa o excluído | 403; 204 e histórico `AttachmentRemoved`; 404; lista sem ele; 404 |
| AC-13 | REQ-11 | — | 31 uploads do mesmo usuário em 10 minutos; 61 exclusões em 1 minuto | o 31º e a 61ª recebem 429 |
| AC-14 | todos | a mudança aplicada | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | 0 avisos; todos os testes passam, inclusive os 15 de `TicketAttachmentsTests` |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Quando apagar o arquivo físico (Q-08)? | (a) na hora da exclusão, depois do commit, pelo `IFileStorage.DeleteAsync` que já existe; sem rotina; (b) depois de 30 dias, por uma rotina diária (`BackgroundService`, a primeira da solução, que exige decidir antes a execução única em várias instâncias, Q-11); (c) não apagar por enquanto | (a): resolve o GAP-08 sem infraestrutura nova; a linha excluída continua para o histórico e a auditoria. A fonte sugeria 30 dias (b) | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-2 | Limite por chamado | (a) 20 anexos e 100 MB; (b) sem limite; (c) outros valores | (a) | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-3 | Limite por arquivo | (a) manter 10 MB; (b) outro valor | (a) | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-4 | Antivírus no envio | (a) fora desta spec; (b) incluir ClamAV agora | (a) | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-5 | Bloquear anexos em chamados Resolvidos, Fechados ou Cancelados (Q-07) | (a) fora desta spec; (b) incluir | (a) | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |

## 5. Fora do escopo
- Front Angular (RF-01.1 a RF-01.6 e RF-03.3 da fonte): confirmação antes de excluir, carregamento, remoção da lista só depois do 204, 404 tratado como "já removido", mensagens, uso do `GET ticket-attachments/policy` e validação de tamanho antes do envio.
- Investigação da "exclusão sem efeito" (RF-01.8 da fonte): o backend responde corretamente nos testes (AC-12); a reprodução e a correção são do repositório Angular.
- Configuração do proxy reverso de produção (RF-03.6 da fonte, `client_max_body_size 11m`): depende do modelo de implantação (Q-11).
- Antivírus (DEC-4), status do chamado (DEC-5), armazenamento compartilhado entre instâncias (GAP-09).

## 6. Plano técnico
- **Arquivos a criar ou alterar:**
  - Domain: `Tickets/TicketAttachment.cs` (`PurgedAtUtc`); `Tickets/TicketAttachmentErrors.cs` (`ContentMismatch`, `TicketLimitReached`, `FileMissing`).
  - Application: `TicketAttachments/TicketAttachmentContentInspector.cs` (assinaturas e verificação de pacote Office com `System.IO.Compression.ZipArchive`); `TicketAttachmentLimits.cs` (limites por chamado); `Upload/UploadTicketAttachmentCommandHandler.cs` (inspeção antes de gravar, contagem e soma do chamado); `Upload/UploadTicketAttachmentCommandValidator.cs` (nome do arquivo); `Download/GetTicketAttachmentContentQueryHandler.cs` (arquivo ausente); `Delete/DeleteTicketAttachmentCommandHandler.cs` (remoção física, se DEC-1 = a); `GetPolicy/*` (query e resposta).
  - Infrastructure: `Storage/LocalFileStorage.cs` (`OpenReadAsync` sinaliza ausência de forma tratável); configuração de `PurgedAtUtc`; migration `Add_Ticket_Attachment_Purged_At`.
  - Web.Api: `Endpoints/TicketAttachments/Upload.cs` (413 em Problem Details; rate limit), `Download.cs` (`Content-Disposition`), `Delete.cs` (rate limit), `GetPolicy.cs` (nova rota); políticas de rate limit novas.
  - Testes: `tests/TickestPristine.Application.UnitTests/TicketAttachments/` (inspetor, upload, download, exclusão); `tests/TickestPristine.IntegrationTests/TicketAttachments/TicketAttachmentsTests.cs` (amostras de arquivo em `TestData/`).
- **Contratos:** `GET ticket-attachments/policy` (nova, 200); `POST tickets/{id}/attachments` ganha 400 `TicketAttachments.ContentMismatch` e `TicketAttachments.TicketLimitReached`, 413 em Problem Details e 429; `GET ticket-attachments/{id}/download` ganha 404 `TicketAttachments.FileMissing` e `Content-Disposition: attachment`; `DELETE ticket-attachments/{id}` ganha 429.
- **Mudança no banco:** sim. Coluna `ticket_attachments.purged_at_utc` (nula).
- **Riscos:**
  - R-1: arquivos legítimos com assinatura incomum (ex.: TXT em UTF-16 com bytes nulos) passam a ser recusados; o REQ-1 documenta a regra e o AC-2 a fixa.
  - R-2: anexos que já passam do limite por chamado continuam lá; o limite vale só para envios novos.
  - R-3: com DEC-1 = (a), um arquivo apagado não volta, mesmo que a exclusão lógica seja revertida no banco à mão.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | `PurgedAtUtc`, erros novos, migration | AC-10 | add-entity | [ ] |
| T-2 | `TicketAttachmentContentInspector` e uso no upload; validação do nome; tamanho real | AC-1, AC-2, AC-3, AC-7 | add-feature | [ ] |
| T-3 | Limites por chamado e `GET ticket-attachments/policy` | AC-5, AC-6 | add-feature | [ ] |
| T-4 | 413 em Problem Details no upload | AC-4 | add-feature | [ ] |
| T-5 | Download: arquivo ausente e `Content-Disposition` | AC-8, AC-9 | add-feature | [ ] |
| T-6 | Remoção física conforme DEC-1 | AC-10, AC-11 | add-feature | [ ] |
| T-7 | Rate limit de upload e exclusão | AC-13 | add-feature | [ ] |
| T-8 | Testes unitários e de integração dos ACs, com amostras de arquivo | AC-1 a AC-13 | add-tests | [ ] |
| T-9 | Build e testes da solução | AC-12, AC-14 | add-tests | [ ] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | | | |
| AC-2 | | | |
| AC-3 | | | |
| AC-4 | | | |
| AC-5 | | | |
| AC-6 | | | |
| AC-7 | | | |
| AC-8 | | | |
| AC-9 | | | |
| AC-10 | | | |
| AC-11 | | | |
| AC-12 | | | |
| AC-13 | | | |
| AC-14 | | | |

## 9. Evidências de verificação
- **Build:**
- **Testes:**
- **Revisão:**
- **Documentação atualizada:** a atualizar pela `spec-verify`: `SYSTEM_SPECIFICATION.md` 5.6 (anexos), 7.1 (GAP-08 e GAP-09), 4.3.1 (rota nova), 9.5 (Q-08, conforme DEC-1).
- **CI:**
- **Entrega:**

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-24 | Rascunho | Criada a partir de `specs/_fontes/anexos-chamados.md`, a pedido de Rafael Santos no chat. DEC-1 a DEC-5 em aberto, com recomendação; na DEC-1 a recomendação difere da fonte (apagar na hora em vez de 30 dias), pelo motivo registrado na decisão. |
| 2026-10-24 | Rascunho | DEC-1 a DEC-5 decididas pela opção recomendada: Rafael Santos delegou a escolha no chat ("encontre as melhores soluções"). DEC-1: arquivo apagado na hora da exclusão. Checklist de completude atendido; aguarda aprovação explícita. |
