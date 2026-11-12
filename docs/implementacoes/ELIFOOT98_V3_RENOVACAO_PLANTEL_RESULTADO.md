# V3 — renovação pelo jogador selecionado no plantel

Data: 11/11/2026. Branch local: `main`. Sem commit ou push.

## Comportamento

Ao selecionar um atleta na tela clássica, **Renovar contrato** aparece à direita de **Listar Jogador**. Permanece desativado para contrato já renovado na temporada e quando uma operação financeira está bloqueada pela partida ou confirmação de pronto.

O modal começa com o salário atual preenchido e editável. **Consultar jogador** faz uma prévia no servidor, sem alterar contrato, caixa ou elenco. Oferta suficiente apresenta **Proposta aceita** com salário e luvas; **Confirmar renovação** efetiva o acordo. Oferta insuficiente apresenta a contraproposta do jogador, com **Aceitar e renovar** ou **Recusar e enviar a leilão**. Cancelar qualquer prévia não produz consequência.

A renovação usa o salário confirmado, as luvas e a proteção da temporada já existentes. Atualiza a coluna CONTRATO para RENOVADO e desativa o botão. Valores com centavos são exibidos e enviados sem arredondamento para inteiro.

Recusar explicitamente a contraproposta utiliza o fluxo existente `SalaryRenewalRefused`: o jogador fica livre, entra em leilão por uma rodada com lance mínimo zero e o ex-clube não recebe o arremate. As restrições de elenco mínimo e reservas em leilão continuam valendo. A recusa não reduz o elenco se essas restrições impedirem a saída. Essa ação explícita é o comportamento novo solicitado; cancelar uma prévia e o fluxo anterior de renovação voluntária continuam sem leilão automático.

## Servidor e persistência

- A exigência salarial e as luvas continuam calculadas por `PreviewRenewal` / `RenewContract`; não foi introduzida fórmula econômica nova.
- A prévia de oferta informa a aceitação e as luvas pelo servidor. O comando revalida titularidade, disponibilidade da rodada, versão da sala, salário e token dos termos antes de qualquer mutação.
- O comando financeiro mantém chave de idempotência. Reenvios não cobram novas luvas nem criam outro leilão, inclusive após reiniciar a API. Chave reutilizada para decisão diferente é rejeitada.
- Não houve alteração no banco, migrations, saves antigos, escalação A/M, força ou simulação de gols. A renovação antiga permanece disponível nos seus pontos anteriores; salas legadas continuam no fluxo anterior.

## Arquivos deste ajuste

Produção:

- `client/elifoot_flutter/lib/classic98_shell.dart`
- `client/elifoot_flutter/lib/main.dart`
- `client/elifoot_flutter/lib/api_client.dart`
- `client/elifoot_flutter/lib/contract_negotiation_dialog.dart` (novo)
- `src/Elifoot.Contracts/RenewalContractPreviewDto.cs`
- `src/Elifoot.Contracts/RenewalNegotiationCommandDto.cs` (novo)
- `src/Elifoot.Domain/VoluntaryRenewalEngine.cs`
- `src/Elifoot.Domain/RenewalNegotiationEngine.cs` (novo)
- `src/Elifoot.Application/TransferContractPreviewQuery.cs`
- `src/Elifoot.Application/RenewalNegotiationApplicationService.cs` (novo)
- `src/Elifoot.Api/Program.cs`

Testes novos:

- `client/elifoot_flutter/test/contract_negotiation_test.dart`
- `tests/Elifoot.Domain.Tests/RenewalNegotiationTests.cs`
- `tests/Elifoot.Integration.Tests/RenewalNegotiationIntegrationTests.cs`

O inicializador local `artifacts/v3-manual-play/start-api.ps1` aponta para o build isolado da API atualizada. As alterações de interface consolidadas anteriormente foram preservadas.

## Validação

| Validação relacionada | Resultado |
| --- | --- |
| Domínio .NET: renovação, salários, economia clássica e escalação | 52 PASS |
| Integração .NET: API, persistência, reenvio e reinício | 8 PASS |
| Flutter: novo fluxo e regressões de telas, A/M, Taça, classificação e contratos | 136 PASS em 49 s |
| Flutter analyze | PASS, sem problemas, 11,5 s |
| Flutter Web release | PASS, 82,7 s; Wasm dry run aprovado |
| Publicação da API em diretório isolado | PASS |

Cobertura do novo fluxo: oferta aceita, aceite da contraproposta, recusa explícita, cancelamento, marcação de renovado, botão desativado, salário predefinido, centavos, erro de valor, cliques repetidos, falha HTTP com reutilização de chave, elenco mínimo, termos desatualizados, pedido espontâneo pendente, propriedade do jogador e pronto. Larguras de interface: 390, 768, 1024 e 1366 px. Não foram executadas suítes completas ou simulações demoradas.

Evidências: `artifacts/v3-renewal-negotiation/`, incluindo logs, TRX, backup local do banco, metadados do build e comparação de snapshots.

## Ambiente local

Build: `V3-consolidada-20261111-renovacao`. Interface: http://127.0.0.2:5242/ . API: http://127.0.0.2:5288/ . PostgreSQL de QA existente reutilizado.

Antes da troca da API foi verificada a ausência de partidas em andamento e criado um backup completo do banco local. Depois da troca, nove snapshots permaneceram byte a byte idênticos; no mundo aberto mudou apenas `updatedAtUtc` pela reconexão. Comparação semântica confirmou zero alteração de dados de jogo. Nenhum contrato foi renovado e nenhum jogador foi enviado a leilão no save do usuário durante a verificação.

Conferência final no Chrome: botão ao lado de Listar Jogador; modal preenchido com R$ 379,00 do atleta selecionado; consulta de uma oferta de R$ 1 retornou a contraproposta real de R$ 379 e luvas de R$ 682,20, com aceitar/recusar disponíveis. A consulta foi cancelada e o plantel ficou aberto para teste manual. Capturas: `renewal-button.jpg`, `renewal-form.jpg` e `renewal-counteroffer.jpg` na pasta de evidências.

Status: **PASS**. Sem pendências neste escopo. API, interface e PostgreSQL continuam executando. Nenhum commit ou push.
