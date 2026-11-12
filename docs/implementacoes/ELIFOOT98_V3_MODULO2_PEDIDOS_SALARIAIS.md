# V3 · Módulo 2 · Pedidos salariais

Auditoria de 12/11/2026 no repositório `D:\elifoot_98`, a partir de `43b99bb`.

**Módulo 2 — PASS** nos cenários automatizados e nos dois clientes web de teste.

## Causas e correções

| Causa | Correção |
| --- | --- |
| O gerador consultava os índices de rodadas e as pendências. Um pedido histórico já decidido podia passar despercebido se o save não trouxesse os índices. | `SalaryContractsEngine.GenerateSalaryRequests` também consulta os registros da temporada e da rodada global. Cada clube elegível é avaliado separadamente e pode gerar até um pedido. |
| Saves com pedidos simultâneos no mesmo clube podiam manter pendências sobrepostas ou impossíveis de resolver. | `ReconcileSalaryRequests` mantém o pedido válido mais antigo ativo. Pedidos válidos de outras rodadas ficam na fila e são apresentados um por vez. Duplicatas e registros incompatíveis ficam no histórico como cancelados, com motivo e sem efeito financeiro. |
| A versão global mudava quando qualquer clube respondia. O segundo treinador podia receber erro mesmo com seu próprio pedido e seus termos intactos. | `FinancialCommandsApplicationService.RespondSalaryAsync` usa a identidade do pedido, a propriedade atual do clube e os termos persistidos. O comando mantém o lock da sala e o recibo idempotente. |
| A guarda global de salário bloqueava inclusive o envio de táticas por outros treinadores. | `MultiplayerRoundOrchestrator` valida a pendência do próprio clube ao registrar o pronto. O início e o avanço da rodada continuam condicionados à resolução das pendências válidas de toda a sala. |
| O cliente parava de tentar preparar a próxima rodada após receber `SALARY_PENDING_TRANSITION`. | `main.dart` atualiza o plantel e retoma a tentativa enquanto a rodada concluída continuar sendo a mesma. O modal é restrito ao próprio clube e a mensagem de espera coletiva é genérica. `Program.cs` e `AccountLogin.cs` conferem no servidor o vínculo do treinador ao devolver os dados salariais. |

## Contratos e compatibilidade

A geração mantém os critérios determinísticos de evolução de força e promoção, a escolha do candidato e o cálculo do valor. O marcador sazonal `RENOVADO` exclui o atleta da geração de novos pedidos naquela temporada.

A Etapa 02B é a regra vigente para o aceite espontâneo. O aceite aplica o valor persistido e registra o acordo salarial. A coluna CONTRATO continua refletindo a renovação voluntária da temporada. A recusa mantém a proteção do elenco mínimo e, quando permitida, libera o atleta para leilão obrigatório de uma rodada, com lance mínimo zero.

Os estados de fila e cancelamento são aditivos no snapshot JSON. O banco dispensa migração de esquema. A recuperação ocorre nas operações da sala, com o mesmo lock e a mesma persistência usados pelas decisões. Os saves usados na validação foram criados em bases de teste.

## Evidências

- .NET com 98 testes dirigidos aprovados (59 de domínio, 19 de aplicação e 20 de integração). Os testes cobrem geração por clube, reexecução da mesma rodada com índices ausentes, nova rodada, atleta renovado, pronto de outro clube e avanço global.
- Integração HTTP com duas contas autenticadas e SQLite. Os testes cobrem autorização cruzada, pedidos coexistentes, recuperação de fila após reinício, aceite e recusa com a mesma versão anterior da sala e avanço único.
- Flutter com 566 testes aprovados, análise estática sem ocorrências e build web Release aprovado. Os testes do modal cobrem ambos os visuais, desktop e mobile.
- Navegadores IAB e Chrome com duas contas em uma sala SQLite de teste. Cada cliente exibiu o pedido do seu próprio clube. No clássico, o aceite elevou o salário de Mateus Almeida de R$ 1.000 para R$ 1.100 por jogo. O segundo cliente recarregou a página, recuperou o pedido de Felipe Carvalho e recusou pela interface atual. A rodada 2 apareceu nos dois clientes. O snapshot registrou versão 1 nas duas decisões e um único leilão obrigatório.

As capturas `classico.jpg`, `atual.jpg`, `classico-aguardando.jpg`, `rodada-liberada.jpg` e a conferência do snapshot `resultado-navegadores.json` estão em `artifacts/module-2-salary/`.
