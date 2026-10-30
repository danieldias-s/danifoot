# ELIFOOT_CLASSIC_BANK_CONTRACTS_ECONOMY_012D

STATUS: PARTIAL — implementação e auditoria técnica concluídas; equilíbrio monetário de longo prazo ainda pendente.

HISTORICAL_BANK_RESEARCH: documentação histórica e relatos classificados em `CLASSIC_BANK_RESEARCH_012D.md`; fórmula de crédito e valores exatos não comprovados historicamente.
LOAN_PRINCIPAL: R$ 500.000 por nova operação.
INTEREST_PER_MATCH: R$ 25.000 por empréstimo ativo e partida oficial efetivamente disputada, sem capitalizar juros vencidos.
DIV4_CREDIT_LIMIT: 1–2, individual por clube.
DIV3_CREDIT_LIMIT: 1–3, adaptado.
DIV2_CREDIT_LIMIT: 2–4, adaptado.
DIV1_CREDIT_LIMIT: 2–5, adaptado.
CREDIT_ELIGIBILITY: capacidade utilizável × R$ 50; limiares versionados; caixa emprestado não amplia crédito; dívidas antigas preservadas no rebaixamento.
LOAN_REPAYMENT: integral com caixa disponível real, principal e juros separados; legados conservam taxa, prestação e dívida residual.
BANK_IDEMPOTENCE: recibos persistentes, versão/fase da sala, semáforo compartilhado, IDs de partidas cobradas e replay após SQLite.
SALARY_RAISE_REQUESTS: força/promoção; PENDING/ACCEPTED/REFUSED persistentes; salário negociado separado da referência.
ONE_REQUEST_PER_ROUND: uma solicitação por clube/rodada, determinística.
SEASON_RENEWAL: negociação vigente protegida; renovação preventiva preserva salário e registra compromisso.
REFUSED_RENEWAL_AUCTION: saída somente se legal, livre, lance inicial zero, sem receita ao ex-clube.
MANDATORY_RENEWAL: piso do elenco impõe renovação, sem criar caixa.
MINIMUM_SQUAD: 11 registrados, um goleiro e dez de linha; indisponibilidade temporária permanece separada.
LAST_GOALKEEPER_PROTECTION: domínio, API, IA e liquidação.
INSOLVENCY: caixa negativo ou juros vencidos; dívida persistente, recuperação por receitas reais.
FORCED_AUCTION: vínculo até venda; pagamento ao vendedor; renovados elegíveis; resultado persistente.
PLAYER_EXIT_PRIORITY: força, salário, ID estável.
AUCTION_WITHOUT_BIDS: AUCTION_UNSOLD, sem liberação por dívida, intervalo de duas rodadas; sem elegível não entra em loop.
INFLATION: nenhum indexador temporal; controles equivalentes constantes; crescimento excessivo de caixa medido e pendente de recalibração.
SALARY_EVOLUTION: 5,057,147 verificações preservando salário entre novos contratos.
PLAYER_MARKET_VALUES: curva preservada e seis faixas de força auditadas por temporada.
100_SEASONS: 100, 35.100 partidas, 3,254,370 minutos reais; todas as divisões.
BANK_LOANS_TOTAL: 5 no cenário com sondas declaradas.
BANK_INTEREST_TOTAL: R$ 1.275.000,00, reconciliados.
FORCED_AUCTIONS_TOTAL: 1 no cenário; demais casos cobertos em testes controlados.
INSOLVENT_CLUBS: 0 no fechamento T100.
RECOVERED_CLUBS: 1 episódios no cenário.
DOTNET_TESTS: solução completa 408/408; revisão 338 domínio, seis aplicação, 45 integração; 410 testes distintos aprovados em Release.
FLUTTER_TESTS: 61/61; análise limpa.
MULTIPLAYER: 2/4/6/8 em HTTP/SignalR; empréstimos e decisões repetidos; reserva de caixa; IA/humanos disputando o mesmo atleta.
DETERMINISTIC_REPLAY: duas temporadas clássicas na regressão e liquidação bancária/salarial após serialização.
KNOWN_ISSUES: `KNOWN_ISSUES_012D.md`, sobretudo crescimento de caixa e limites da evidência histórica.

Defeitos corrigidos: quitação por prazo, salário sobrescrito pela força, estimativa de 3,5% na interface, ausência de sincronização/recibos e modalidade bancária sem crédito individual. Não foi comprovado defeito que justificasse substituir o comportamento anterior do leilão por vencimento.

Juros e dívidas, salários e arremates foram reconciliados; 8,800 pares de venda somam zero. Histórico legado desconhecido foi marcado como tal. O checkpoint 012C é `50e9c12`; MatchEngine e núcleo disciplinar/tático preservados. Nenhuma publicação em produção.

Arquivos e evidências: `audit-012d/verification.json`, `audit-012d/independent-reconciliation.json`, os dez documentos obrigatórios e fontes/testes de economia 012D. O relatório não declara PASS global porque o caixa chegou a R$ 18.107.128.451,84 e o papel estratégico do crédito ao longo de todo o horizonte não foi demonstrado como equilibrado.

## FILES_CHANGED

- `client/elifoot_flutter/lib/api_client.dart`
- `client/elifoot_flutter/lib/main.dart`
- `client/elifoot_flutter/test/complete_gameplay_011_test.dart`
- `src/Elifoot.Api/Program.cs`
- `src/Elifoot.Application/ClubAndMarketApplicationService.cs`
- `src/Elifoot.Application/MultiplayerRoundOrchestrator.cs`
- `src/Elifoot.Contracts/Dtos.cs`
- `src/Elifoot.Contracts/Enums.cs`
- `src/Elifoot.Domain/ClassicForceEvolutionEngine.cs`
- `src/Elifoot.Domain/CompetitionAndSeasonEngine.cs`
- `src/Elifoot.Domain/Entities.cs`
- `src/Elifoot.Domain/MarketAndEconomyEngine.cs`
- `src/Elifoot.Domain/PlayerStrengthEvolutionEngine.cs`
- `src/Elifoot.Domain/WorldSeedFactory.cs`
- `src/Elifoot.Infrastructure/EfWorldRepository.cs`
- `tests/Elifoot.Domain.Tests/ClassicForce012CTests.cs`
- `tests/Elifoot.Domain.Tests/PlayerForce012Tests.cs`

## FILES_CREATED

- `client/elifoot_flutter/test/economy_012d_api_test.dart`
- `client/elifoot_flutter/test/economy_012d_widget_test.dart`
- `docs/BANK_CREDIT_RULES_012D.md`
- `docs/BANK_INTEREST_CALCULATION_012D.md`
- `docs/CLASSIC_BANK_RESEARCH_012D.md`
- `docs/ECONOMY_100_SEASONS_012D.md`
- `docs/ECONOMY_FINAL_REPORT_012D.md`
- `docs/ECONOMY_REGRESSION_012D.md`
- `docs/FINANCIAL_INSOLVENCY_012D.md`
- `docs/FORCED_AUCTIONS_012D.md`
- `docs/KNOWN_ISSUES_012D.md`
- `docs/MARKET_INFLATION_012D.md`
- `docs/SALARY_RENEWAL_RULES_012D.md`
- `scripts/report_economy_012d.py`
- `src/Elifoot.Application/FinancialCommandsApplicationService.cs`
- `src/Elifoot.Contracts/EconomyDtos.cs`
- `src/Elifoot.Domain/ClassicEconomyEngine.cs`
- `src/Elifoot.Domain/ClassicEconomyState.cs`
- `src/Elifoot.Domain/FinancialInsolvencyEngine.cs`
- `src/Elifoot.Domain/SalaryContractsEngine.cs`
- `tests/Elifoot.Application.Tests/FinancialCommandDurability012DTests.cs`
- `tests/Elifoot.Domain.Tests/ClassicEconomy012DTests.cs`
- `tests/Elifoot.Integration.Tests/ClassicEconomy012DIntegrationTests.cs`
- `tests/Elifoot.Simulation.Tests/ClassicEconomy012DSimulationTests.cs`
