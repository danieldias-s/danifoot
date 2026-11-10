# Elifoot 98 Multiplayer V3 — Etapa 02B

Projeto: D:/elifoot_98. Branch main. Base preservada: ced4e43d649470188ebcc0e59e62dbc0414a6ea9. Nova autorização aplicada em 10/11/2026, America/Sao_Paulo.

**Validação técnica: PASS — 997 casos .NET distintos, incluindo as 31 simulações, 504 testes Flutter e analyze sem problemas. A política dos pedidos antigos foi aprovada e implementada. O registro do commit local está em andamento.**

## 1. Resumo executivo

A autorização de correção definitiva substitui as regras conflitantes anteriores. A implementação anterior foi preservada e ampliada: renovação voluntária e pedido espontâneo agora são eventos distintos, inclusive na UI. Desistir de uma prévia de renovação não altera o mundo; recusar um pedido espontâneo válido produz leilão obrigatório com mínimo zero e exatamente uma rodada.

Também foram mantidas a prévia autoritativa de salário na contratação, a confirmação de termos, a segurança financeira e a recuperação/idempotência da primeira implementação 02B. A entrega não altera fórmulas de salário-base, valor, inflação, partidas, força, táticas, estádio ou carreira.

## 2. Estado inicial e auditoria

A main estava em ced4e43, com os 34 arquivos da implementação 02B anterior ainda sem commit. Seus hashes, diff e inventário foram registrados em artifacts/v3-stage02b-rules antes das novas alterações. O relatório anterior foi preservado em previous-result-report.md. Os checkpoints d0489d3, 2818cb3, 2402e99 e ced4e43 permanecem intactos.

A nova autorização foi lida integralmente. A proposta e os relatórios anteriores foram tratados como contexto; regras conflitantes foram substituídas pela instrução atual, sem descartar funcionalidades já implementadas. Arquivos não rastreados preexistentes continuam fora do escopo desta entrega.

## 3. Como começa a temporada

Na modalidade sazonal vigente, não ter renovação ativa não significa estar sem clube. O início de uma nova temporada limpa RenewedForSeason e a proteção contratual anterior, zera o contador sazonal e mantém proprietário, salário e histórico. Nenhuma dessas operações libera um atleta, transfere propriedade ou exige renovação.

A renovação anterior deixa de bloquear a venda. O treinador pode manter o jogador sem renovar, tentar uma renovação ou vendê-lo conforme os demais validadores de mercado. O teste NewWorldAndNewSeasonKeepOwnershipWagesAndHistoryWithoutActiveRenewals confere o elenco inteiro, os salários, o histórico e a repetição da virada.

## 4. Renovação voluntária

A consulta GET /api/worlds/{worldId}/players/{playerId}/renew-preview é iniciada pelo treinador e não cria pedido, recibo financeiro, contrato ou leilão. Retorna salário atual, salário exigido, luvas, temporada, rodada, versão e fingerprint dos termos.

A exigência reaproveita RequestedSalary, cálculo já existente, limitada inferiormente ao salário atual. Não foi criada outra curva econômica. Se a referência não exige aumento, o jogador aceita o salário atual; se exige aumento, o valor é mostrado antes da confirmação. Luvas e validade continuam usando a regra sazonal existente.

O backend exige confirmação da exigência maior e revalida o fingerprint sob o lock da sala. Aceitar aplica o salário confirmado, registra a renovação, debita as luvas uma vez, fecha anúncios/propostas incompatíveis e bloqueia a venda na temporada. Não existe limite de renovações por rodada.

## 5. Por que desistir não gera leilão

O diálogo RENOVAÇÃO VOLUNTÁRIA permite DESISTIR, voltar ou fechar a consulta. Sem confirmação, nenhuma chamada de renovação é enviada. O servidor não mantém uma negociação pendente que precise ser recusada ou expirada. Jogador, salário, propriedade, elegibilidade de venda e histórico permanecem como antes da tentativa.

Essa tentativa não entra em SalaryRaiseRequest nem consome o limite dos pedidos espontâneos. Se o jogador já possui um pedido espontâneo pendente, renovar ou vender não pode resolver esse pedido por outro caminho: o responsável deve tomar a decisão explícita correspondente.

## 6. Pedido espontâneo de aumento

A geração mantém os gatilhos e critérios existentes de evolução/promoção, a escolha determinística e o cálculo do salário solicitado. O limite continua sendo no máximo um pedido por clube e rodada, com uma pendência por clube; negociações voluntárias não entram nessa contagem.

A modal obrigatória mostra nome, força, salário atual e exigido, ACEITAR e RECUSAR. Não possui botão de desistência/adiamento e bloqueia retorno enquanto a decisão estiver pendente. Fica disponível entre partidas nas páginas da sala, incluindo resultados e preparação.

Aceitar altera o salário uma vez, registra a decisão e mantém o jogador no clube. Essa aceitação não equivale a uma renovação voluntária nem cria o bloqueio sazonal de venda. Recusar não aplica aumento: mantém a consequência já aprovada de atleta livre e leilão salarial, respeitado o elenco mínimo e os demais controles da 02A. O ex-clube não recebe o arremate.

## 7. Três cenários de leilão

| Evento | Mínimo inicial | Duração | Consequência |
| --- | --- | --- | --- |
| Leilão voluntário | Padrão R$ 500.000; configuração existente preservada | 1 a 3 rodadas, padrão 2 | Usa a validação/liq. da 02A |
| Recusa de pedido espontâneo | R$ 0 | Exatamente uma rodada oficial | Leilão obrigatório, uma consequência idempotente |
| Renovação voluntária não aceita | Não aplicável | Não aplicável | Não cria leilão nem punição |

Uma recusa em PREPARING da rodada 7 vence depois da conclusão oficial da rodada 7; em RESULTS da rodada 7, depois da rodada 8. Na última rodada, RESULTS/FINISHED encaminha o vencimento à primeira rodada da temporada seguinte. A liquidação exige rodada financeira concluída; consultas e jogos parciais não encerram o leilão.

Leilões já existentes em saves conservam identidade e prazo; não são recriados retroativamente. Os testes verificam separadamente os três cenários, incluindo limites de temporada, mínimo zero, replay e falha na criação.

## 8. Venda, renovação e virada

RenewedForSeason continua sendo a fonte do bloqueio sazonal. Todos os caminhos de venda abrangidos pela 02A revalidam essa proteção no servidor. Renovar um jogador não pode contornar um pedido espontâneo pendente. A insolvência também não escolhe esse jogador para decidir silenciosamente uma pendência humana.

A pré-validação agora protege todo avanço de rodada, além do fechamento da temporada e do início via pronto/táticas. Enquanto houver pedido espontâneo pendente, o servidor mantém rodada, resultados e estado de pronto e retorna SALARY_PENDING_TRANSITION. Após a decisão, a preparação pode continuar. Um humano que já estava pronto em um save antigo ainda pode responder ao pedido sem ficar preso pela própria guarda.

A repetição da virada usa a temporada esperada e não repete histórico, prêmios ou transição. O retry depois de falha de gravação persiste o estado completo já aplicado, sem reaplicar efeitos.

## 9. Recuperação de pedidos antigos

A autorização atual permite recuperar pedidos espontâneos válidos de temporadas anteriores. São mantidos identificador, temporada/rodada original, histórico, salário e proprietário até a resposta. Não existe mais a rejeição automática SALARY_PENDING_POLICY_REQUIRED apenas por diferença de temporada.

Humanos precisam aceitar ou recusar expressamente. A IA usa sua estratégia já existente e o mesmo RespondSalary; não toma decisões por clubes humanos. Uma recusa atual cria o leilão no momento da decisão, com uma rodada futura jogável, sem leilões retroativos. Tentativas canceladas de renovação não são convertidas em pedidos espontâneos.

A validação continua rejeitando jogador sem vínculo, pedido de outro mundo, temporada futura, valores inválidos, termos salariais alterados e conflitos de leilão. Dados inconsistentes não recebem decisões ou valores financeiros fictícios. Os testes HTTP/SQLite cobrem aceitar e recusar pedidos antigos, repetição, gravação e recarga.

## 10. Humanos e IA

Geração, decisões, renovação, venda, salário aplicado e persistência usam o mesmo núcleo. A autoridade da IA é validada contra o treinador realmente vinculado. A IA pode escolher aceitar por preservação do elenco mínimo, mas não transforma uma recusa inválida em renovação obrigatória dentro do validador. Aceitar esse aumento não cria contrato sazonal.

A recuperação de pendências da IA preserva o cálculo anterior de reserva e o horizonte da estratégia. Não repete empréstimos/quitações. O estado de recuperação que ainda precisa ser salvo é apenas um marcador em memória, ignorado no JSON: uma falha de gravação pode ser repetida sem decidir novamente. Uma pré-validação bloqueada somente por humanos permanece sem alterações no snapshot.

## 11. Segurança financeira

O diário de mutação da 02A/02B mantém o desfazimento de salário, contrato, anúncio, ofertas, propriedade, saldo, histórico, leilão e recibos quando a aplicação falha. Cobrança de luvas rejeitada não pode deixar contrato parcial. Chaves financeiras anteriores permanecem protegidas no rollback.

A persistência relacional grava snapshot e ledger pela transação existente. Falhas de gravação retornam CONTRACT_PERSISTENCE_FAILED ou MARKET_PERSISTENCE_FAILED, mantendo a chave para retry. O teste SQLite com trigger de falha real continua validando rollback durável e exatamente uma cobrança depois da recuperação.

## 12. Concorrência, confirmação e contratação

Os comandos seguem o lock por sala, propriedade do treinador, fase, versão e recibo. Foram testadas decisões opostas concorrentes, duas confirmações de renovação, compras concorrentes, reinício e retry após falha. As garantias permanecem na arquitetura existente de uma instância autoritativa por sala.

A prévia de contratação da primeira implementação 02B foi preservada: usa o mesmo cálculo da assinatura, confirma preço/termos atuais e divulga a estimativa do leilão como estimativa. A retirada concorrente do anúncio continua impedindo uma confirmação sem preço. O cliente não define salário arbitrário.

## 13. Arquivos do escopo

Inventário atual: **48 arquivos**, incluindo os 34 anteriores: 18 de produção .NET, cinco de produção Flutter, 24 de testes/fixtures e este relatório. Não há incorporação de arquivos preexistentes alheios à etapa.

Conferência final de preservação aprovada às 19:35: as 23 fontes de produção coincidem com os hashes das execuções; nenhuma alteração protegida, arquivo não rastreado inesperado ou diff fora do inventário. Os 34 arquivos anteriores continuam presentes, as fórmulas de preço/salário-base permanecem iguais ao checkpoint e os quatro commits anteriores continuam ancestrais da main. Evidência: artifacts/v3-stage02b-rules/preservation-check.json.

| Arquivo | Papel |
| --- | --- |
| [client/elifoot_flutter/lib/api_client.dart](D:/elifoot_98/client/elifoot_flutter/lib/api_client.dart) | Produção Flutter |
| [client/elifoot_flutter/lib/main.dart](D:/elifoot_98/client/elifoot_flutter/lib/main.dart) | Produção Flutter |
| [client/elifoot_flutter/lib/market_screen.dart](D:/elifoot_98/client/elifoot_flutter/lib/market_screen.dart) | Produção Flutter |
| [client/elifoot_flutter/lib/salary_request_modal.dart](D:/elifoot_98/client/elifoot_flutter/lib/salary_request_modal.dart) | Produção Flutter |
| [client/elifoot_flutter/lib/voluntary_renewal_dialog.dart](D:/elifoot_98/client/elifoot_flutter/lib/voluntary_renewal_dialog.dart) | Produção Flutter |
| [client/elifoot_flutter/test/complete_gameplay_011_test.dart](D:/elifoot_98/client/elifoot_flutter/test/complete_gameplay_011_test.dart) | Teste ou fixture |
| [client/elifoot_flutter/test/match_market_015a_test.dart](D:/elifoot_98/client/elifoot_flutter/test/match_market_015a_test.dart) | Teste ou fixture |
| [client/elifoot_flutter/test/salary_contracts_v3_stage02b_test.dart](D:/elifoot_98/client/elifoot_flutter/test/salary_contracts_v3_stage02b_test.dart) | Teste ou fixture |
| [client/elifoot_flutter/test/salary_request_modal_test.dart](D:/elifoot_98/client/elifoot_flutter/test/salary_request_modal_test.dart) | Teste ou fixture |
| [client/elifoot_flutter/test/salary_rules_correction_test.dart](D:/elifoot_98/client/elifoot_flutter/test/salary_rules_correction_test.dart) | Teste ou fixture |
| [docs/implementacoes/ELIFOOT98_V3_ETAPA02B_RESULTADO.md](D:/elifoot_98/docs/implementacoes/ELIFOOT98_V3_ETAPA02B_RESULTADO.md) | Relatório |
| [src/Elifoot.Api/Program.cs](D:/elifoot_98/src/Elifoot.Api/Program.cs) | Produção .NET |
| [src/Elifoot.Application/ClubAndMarketApplicationService.cs](D:/elifoot_98/src/Elifoot.Application/ClubAndMarketApplicationService.cs) | Produção .NET |
| [src/Elifoot.Application/FinancialCommandsApplicationService.cs](D:/elifoot_98/src/Elifoot.Application/FinancialCommandsApplicationService.cs) | Produção .NET |
| [src/Elifoot.Application/MultiplayerRoundOrchestrator.cs](D:/elifoot_98/src/Elifoot.Application/MultiplayerRoundOrchestrator.cs) | Produção .NET |
| [src/Elifoot.Application/TransferContractPreviewQuery.cs](D:/elifoot_98/src/Elifoot.Application/TransferContractPreviewQuery.cs) | Produção .NET |
| [src/Elifoot.Contracts/RenewalContractPreviewDto.cs](D:/elifoot_98/src/Elifoot.Contracts/RenewalContractPreviewDto.cs) | Produção .NET |
| [src/Elifoot.Contracts/TransferContractPreviewDto.cs](D:/elifoot_98/src/Elifoot.Contracts/TransferContractPreviewDto.cs) | Produção .NET |
| [src/Elifoot.Domain/ClassicEconomyState.cs](D:/elifoot_98/src/Elifoot.Domain/ClassicEconomyState.cs) | Produção .NET |
| [src/Elifoot.Domain/CompetitionAndSeasonEngine.cs](D:/elifoot_98/src/Elifoot.Domain/CompetitionAndSeasonEngine.cs) | Produção .NET |
| [src/Elifoot.Domain/Entities.cs](D:/elifoot_98/src/Elifoot.Domain/Entities.cs) | Produção .NET |
| [src/Elifoot.Domain/FinancialInsolvencyEngine.cs](D:/elifoot_98/src/Elifoot.Domain/FinancialInsolvencyEngine.cs) | Produção .NET |
| [src/Elifoot.Domain/MarketAndEconomyEngine.cs](D:/elifoot_98/src/Elifoot.Domain/MarketAndEconomyEngine.cs) | Produção .NET |
| [src/Elifoot.Domain/MarketContractTermsEngine.cs](D:/elifoot_98/src/Elifoot.Domain/MarketContractTermsEngine.cs) | Produção .NET |
| [src/Elifoot.Domain/MarketTransferMutation.cs](D:/elifoot_98/src/Elifoot.Domain/MarketTransferMutation.cs) | Produção .NET |
| [src/Elifoot.Domain/MarketValidationEngine.cs](D:/elifoot_98/src/Elifoot.Domain/MarketValidationEngine.cs) | Produção .NET |
| [src/Elifoot.Domain/SalaryContractsEngine.cs](D:/elifoot_98/src/Elifoot.Domain/SalaryContractsEngine.cs) | Produção .NET |
| [src/Elifoot.Domain/SalarySeasonTransitionEngine.cs](D:/elifoot_98/src/Elifoot.Domain/SalarySeasonTransitionEngine.cs) | Produção .NET |
| [src/Elifoot.Domain/VoluntaryRenewalEngine.cs](D:/elifoot_98/src/Elifoot.Domain/VoluntaryRenewalEngine.cs) | Produção .NET |
| [tests/Elifoot.Application.Tests/MarketValidationV3Stage02ATests.cs](D:/elifoot_98/tests/Elifoot.Application.Tests/MarketValidationV3Stage02ATests.cs) | Teste ou fixture |
| [tests/Elifoot.Application.Tests/MultiplayerSynchronizationAndPauseTests.cs](D:/elifoot_98/tests/Elifoot.Application.Tests/MultiplayerSynchronizationAndPauseTests.cs) | Teste ou fixture |
| [tests/Elifoot.Application.Tests/SalaryContractsV3Stage02BTests.cs](D:/elifoot_98/tests/Elifoot.Application.Tests/SalaryContractsV3Stage02BTests.cs) | Teste ou fixture |
| [tests/Elifoot.Domain.Tests/ClassicEconomy012DTests.cs](D:/elifoot_98/tests/Elifoot.Domain.Tests/ClassicEconomy012DTests.cs) | Teste ou fixture |
| [tests/Elifoot.Domain.Tests/SalaryContractsV3Stage02BTests.cs](D:/elifoot_98/tests/Elifoot.Domain.Tests/SalaryContractsV3Stage02BTests.cs) | Teste ou fixture |
| [tests/Elifoot.Domain.Tests/SalaryRequestSafetyTests.cs](D:/elifoot_98/tests/Elifoot.Domain.Tests/SalaryRequestSafetyTests.cs) | Teste ou fixture |
| [tests/Elifoot.Domain.Tests/SalaryRulesCorrectionTests.cs](D:/elifoot_98/tests/Elifoot.Domain.Tests/SalaryRulesCorrectionTests.cs) | Teste ou fixture |
| [tests/Elifoot.Domain.Tests/SeasonContractsAndDebtTests.cs](D:/elifoot_98/tests/Elifoot.Domain.Tests/SeasonContractsAndDebtTests.cs) | Teste ou fixture |
| [tests/Elifoot.Integration.Tests/AccountLoginTests.cs](D:/elifoot_98/tests/Elifoot.Integration.Tests/AccountLoginTests.cs) | Teste ou fixture |
| [tests/Elifoot.Integration.Tests/ApiAndSignalREndToEndTests.cs](D:/elifoot_98/tests/Elifoot.Integration.Tests/ApiAndSignalREndToEndTests.cs) | Teste ou fixture |
| [tests/Elifoot.Integration.Tests/FinancialPersistence007Tests.cs](D:/elifoot_98/tests/Elifoot.Integration.Tests/FinancialPersistence007Tests.cs) | Teste ou fixture |
| [tests/Elifoot.Integration.Tests/Integration014ATests.cs](D:/elifoot_98/tests/Elifoot.Integration.Tests/Integration014ATests.cs) | Teste ou fixture |
| [tests/Elifoot.Integration.Tests/MarketValidationV3Stage02AIntegrationTests.cs](D:/elifoot_98/tests/Elifoot.Integration.Tests/MarketValidationV3Stage02AIntegrationTests.cs) | Teste ou fixture |
| [tests/Elifoot.Integration.Tests/MatchMarket015ATests.cs](D:/elifoot_98/tests/Elifoot.Integration.Tests/MatchMarket015ATests.cs) | Teste ou fixture |
| [tests/Elifoot.Integration.Tests/PlayableWorld013CIntegrationTests.cs](D:/elifoot_98/tests/Elifoot.Integration.Tests/PlayableWorld013CIntegrationTests.cs) | Teste ou fixture |
| [tests/Elifoot.Integration.Tests/SalaryContractsV3Stage02BIntegrationTests.cs](D:/elifoot_98/tests/Elifoot.Integration.Tests/SalaryContractsV3Stage02BIntegrationTests.cs) | Teste ou fixture |
| [tests/Elifoot.Integration.Tests/VoluntaryAuctionIntegrationTests.cs](D:/elifoot_98/tests/Elifoot.Integration.Tests/VoluntaryAuctionIntegrationTests.cs) | Teste ou fixture |
| [tests/Elifoot.Simulation.Tests/CoachCareer008SimulationTests.cs](D:/elifoot_98/tests/Elifoot.Simulation.Tests/CoachCareer008SimulationTests.cs) | Teste ou fixture |
| [tests/Elifoot.Simulation.Tests/MultiSeasonAndBenchmarkTests.cs](D:/elifoot_98/tests/Elifoot.Simulation.Tests/MultiSeasonAndBenchmarkTests.cs) | Teste ou fixture |

## 14. Banco, migrations e saves

Nenhuma migration EF/SQL foi adicionada e nenhum banco ou save real foi alterado. O campo opcional de confirmação de propostas da primeira implementação continua compatível com JSON antigo. Os novos DTOs de prévia não mudam o esquema; o marcador de recuperação da IA possui JsonIgnore.

As integrações completas usaram o cluster temporário 02B em artifacts/v3-stage02b/postgres/data, loopback 55442, usuário v3stage02b, com prefixos de bancos exclusivos desta revisão. Dois casos finais afetados por uma falha de alocação do WAL foram reexecutados em um cluster novo e separado em C:/Users/Daniel/AppData/Local/Temp/elifoot98-v3-stage02b-rules-pg-20261110-bf9e44ef/data, na mesma porta após encerrar o anterior. Não houve movimentação, exclusão ou reset dos dados anteriores.

Ambos os clusters temporários foram encerrados com segurança, com dados preservados, em 10/11/2026; o último às 18:16. Evidências: test-infrastructure-recheck.json, infrastructure-disk-stop.json e infrastructure-stopped.json. Nenhum outro serviço foi alterado. O input histórico de compatibilidade é somente de leitura. O input de temporada real é um dado de entrada já produzido, e seu teste consumidor foi executado novamente; não reutiliza aprovação de outra revisão.

## 15. Validações desta revisão

Evidências: D:/elifoot_98/artifacts/v3-stage02b-rules.

| Validação | Resultado atual |
| --- | --- |
| Build Release final | PASS, zero avisos/erros; build-final.log |
| Domínio relacionado | PASS, 88 casos; tests/domain-targeted-final.trx |
| Aplicação relacionada | PASS, 15 casos; tests/application-targeted-final.trx |
| Integrações selecionadas | PASS, seis casos; tests/integration-targeted.trx |
| Domínio completo | PASS, 616 casos; tests/domain-full.trx |
| Aplicação completa | PASS, 55 casos; tests/application-full.trx |
| Integração completa consolidada | PASS, 295 casos distintos; última execução completa 292 PASS/3 FAIL, com as três falhas resolvidas pelas reexecuções abaixo |
| Reexecução do grupo de leilão voluntário | PASS, dois casos; tests/integration-recheck-voluntary.trx |
| Reexecução dos dois casos PostgreSQL | PASS, dois casos; tests/integration-recheck-postgres.trx |
| Flutter relacionado | PASS, 25 casos; flutter-rules-targeted.log |
| Flutter completo | PASS, 504 casos; flutter-full.log |
| Flutter analyze final | PASS, sem problemas; flutter-analyze-final.log |
| Simulações completas consolidadas | PASS, 31 casos distintos; execução original 30 PASS/1 FAIL, resolvida pela reexecução de carreira |
| Reexecução de carreira/replay | PASS, um caso com duas campanhas de uma temporada e uma de 50 temporadas; tests/simulation-recheck-career.trx |
| Total .NET distinto final | PASS, 997 = 616 + 55 + 295 + 31; zero falhas pendentes, ignorados ou casos ausentes |

Testes direcionados e reexecuções não são somados às suítes completas como casos distintos. Os números 933/971 .NET e 485/493 Flutter são checkpoints anteriores; não substituem esta execução.

A consolidação final com consolidate-validation.ps1 -RequireComplete identifica 997 casos distintos aprovados, Complete=true, Unresolved=0 e MissingSuites vazio. A comparação por assembly/identidade mantém as tentativas anteriores e substitui apenas o resultado final do mesmo caso reexecutado. São 19 casos novos de domínio, seis de aplicação e um caso adicional na teoria de recuperação legada da integração nesta correção. O Flutter acrescenta 11 casos aos 493 da implementação anterior. Evidência por identidade do caso e tentativas: validation-consolidated.json.

## 16. Falhas investigadas e correções

Dois testes antigos de domínio exigiam salário sempre inalterado na renovação e conversão da recusa da IA em renovação obrigatória. Foram adequados às regras substituídas: confirmação do salário exigido e recusa inválida rejeitada com aceitação explícita, preservando verificações de propriedade, valores, bloqueio e despesas.

Dois casos novos de aplicação identificaram que a pré-validação estava salvando desnecessariamente um mundo bloqueado apenas por pedidos humanos, alterando seu timestamp. O código foi corrigido: só grava a recuperação da IA quando há alteração aceita ou retry de gravação pendente. As assertivas de snapshot integral foram mantidas e passaram.

O analyze inicial apontou uma indicação de chaves em um if do novo teste Flutter. A estrutura foi corrigida sem alterar assertivas; analyze final sem problemas e suíte completa com 504 aprovados.

A primeira execução completa de integração terminou com 270 aprovados e 25 falhas em 295 casos: nove em autenticação sob restrições locais e 16 em roteiros que avançavam sem responder aos pedidos. Um caso de autenticação foi reproduzido e passou com o acesso local necessário, sem alterar a implementação de autenticação. Os clientes sintéticos dos demais roteiros passaram a enviar decisões pela API antes do avanço. Nenhum veto de produção ou assertiva de resultados, finanças, leilões, reconexão ou contagem de prontos foi removido.

Uma nova execução completa, com acesso local e bancos novos, terminou com 292 aprovados e três falhas. O caso de leilão voluntário começava na rodada 7 com donos fictícios nos clubes fora do cenário: esses clubes criavam pedidos que nenhum dos dois clientes podia responder. A fixture agora representa esses outros elencos como já renovados na rodada 7; os dois participantes continuam respondendo aos próprios pedidos pela API. O grupo completo de leilões foi reexecutado: dois aprovados, mantendo e reforçando as assertivas existentes.

As duas outras falhas ocorreram quando o PostgreSQL de teste registrou PANIC ao alocar pg_wal/xlogtemp por falta de espaço e entrou em recuperação. History017C e ClassificationForce falharam com conexão interrompida/57P03, sem erro de regra salarial. O diretório foi preservado e encerrado; ambos passaram em um novo cluster isolado no disco C, sem alteração de código ou assertivas. Os dois TRX de reexecução resolvem os três casos distintos; um caso de leilão que já passava foi repetido e não é somado novamente. Todos os resultados intermediários permanecem registrados.

A execução completa das 31 simulações teve 30 aprovados e uma falha no roteiro de 50 temporadas de carreira. Esse roteiro respondia aos pedidos antes de concluir carreira/mercado e chegava à virada com uma pendência. Foi corrigido somente o roteiro: ao fim do processamento, os humanos sintéticos respondem pelo domínio, a IA usa seu resolvedor existente e uma assertiva adicional exige ausência de pendências antes de avançar. A guarda e o motor de carreira não foram alterados. A reexecução passou em 3 min 46 s, mantendo 17.550 partidas, 250 títulos, demissões, aceitação de convites pela IA, propriedade, moral e igualdade do replay. Não houve relaxamento dessas assertivas nem recontagem desse caso como teste adicional.

## 17. Simulações

O inventário da assembly atual contém 31 casos, particionados em grupos de 17, quatro, quatro e seis, com diretórios separados e nova execução sobre esta revisão. Abrange campanhas de cem temporadas masculinas/femininas, conciliação financeira, propriedade, leilões, replay, rankings, histórico, força, lesões e benchmarks de até 1.024 clubes.

| Grupo | Resultado original | Duração aproximada | Resultado consolidado |
| --- | --- | --- | --- |
| 1 | 16 PASS / 1 FAIL | 1 h 51 min | 17 PASS após reexecução do caso de carreira |
| 2 | 4 PASS | 1 h 42 min | 4 PASS |
| 3 | 4 PASS | 1 h 52 min | 4 PASS |
| 4 | 6 PASS | 1 h 43 min | 6 PASS |

Os quatro grupos foram executados em paralelo a partir de 17:34; a reexecução final terminou às 19:33. Todos os 31 casos do inventário estão aprovados na consolidação final, sem ignorados. O TRX original com a falha foi preservado; a reexecução está registrada separadamente em tests/simulation-recheck-career.trx.

Os humanos sintéticos tomam decisões salariais explícitas nos roteiros. Isso não é aceitação automática na produção. Evidências: simulation-inventory.log, simulation-partitions.json, simulation-parts/part-*/tests.log e tests/simulation-part-*.trx. Nenhum resultado de simulação da revisão anterior aprova esta correção.

## 18. Preservação das Etapas 01 e 02A

A/M, mérito, gols da Taça, formação, força e regras de partida permanecem preservados. As suítes completas incluem os casos originais dessas funcionalidades e os layouts responsivos, inclusive 1.024 px. As funções econômicas de valor e salário-base são comparadas com o checkpoint; não houve ajuste de fórmula para fazer teste passar.

O código de produção foi congelado antes da regressão completa. Ajustes posteriores de roteiro/estilo de testes não mudaram as 18 fontes .NET nem as cinco fontes Flutter. A evidência visual desta revisão é a suíte automatizada de widgets/layout; não se declara nova certificação manual em navegador ou dispositivo físico.

## 19. Compatibilidade e limites

Mundos antigos sem pedidos continuam carregáveis. Pedidos espontâneos antigos válidos podem ser respondidos pelo responsável, mantendo origem e histórico. A renovação permanece opcional. Recarga, recibos, ledger, propriedade e cenários reais HTTP/SQLite/PostgreSQL fazem parte da regressão.

Estados arbitrariamente corrompidos não são reparados com transferências, salários ou decisões fictícias. A proteção de concorrência não foi transformada em lock distribuído. Leilões que já estavam ativos conservam sua identidade e prazo persistidos.

## 20. Status e pendências

**PASS técnico, com registro do commit local em andamento.** A regra de negócio pendente da revisão anterior foi resolvida pela autorização atual e implementada. Os 997 casos .NET distintos, os 504 testes Flutter, o analyze e os builds estão aprovados, sem falhas pendentes. Os 48 arquivos do escopo foram conferidos; ambos os clusters temporários estão encerrados, com dados preservados. Não resta requisito de implementação ou teste pendente.

Nenhuma atividade das Etapas 02C ou 02D foi iniciada. Resta apenas concluir o registro dos hashes locais nesta seção e a conferência posterior ao commit.

## 21. Commit

Todas as validações exigidas e a conferência de preservação foram aprovadas. O commit local de implementação será criado com a mensagem autorizada feat(elifoot98): implement V3 stage 02B salary and contracts, adicionando somente os 48 arquivos do inventário. Seu hash será registrado no fechamento documental imediatamente após a criação.

Base mantida durante a validação: ced4e43d649470188ebcc0e59e62dbc0414a6ea9, branch main. Nenhum push, reset --hard ou reescrita dos checkpoints anteriores.
