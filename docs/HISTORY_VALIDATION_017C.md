# Validação da missão ELIFOOT_CLUB_PLAYER_HISTORY_017C

Worktree `D:/elifoot_98_017C`, branch `feature/club-player-history-017c`.
O checkpoint é o commit que inclui este relatório. A base `c9a91a8` contém a
missão de pedido salarial e descende dos checkpoints 017A e 017B.

| Item | Resultado e evidência |
| --- | --- |
| CLUB_HISTORY, SEASON_RECORDS | Temporadas, placares reais, finanças e recordes de clubes humanos e de IA. O clube novo começa sem partidas ou conquistas. |
| TITLES, PROMOTIONS, RELEGATIONS | Classificação antes da troca de divisão. Campeões e vices por competição, incluindo final da Taça efetivamente disputada. |
| COACH_HISTORY | Vínculos existentes e treinador registrado no início de cada partida. Teste de troca/demissão e regressão de carreira. |
| TRANSFER_HISTORY, PLAYER_CAREER | ID do jogador preservado em passagens por múltiplos clubes, inclusive após recarregar. Um registro por execução, com reaproveitamento de leilão antigo. |
| PLAYER_HISTORY, PLAYER_AWARDS | Carreira, temporadas e totais por clube e competição. Artilharias registradas por divisão histórica, Taça e geral. Títulos vinculados à participação oficial. |
| PLAYER_STRENGTH_HISTORY | Força inicial/final observada, com ausência identificada em saves incompletos. |
| CURRENT_UI, CLASSIC_UI | Entradas pelo clube, classificação, plantel, mercado e rankings. Tabelas no computador e cartões em telas estreitas. Aprovação visual final da clássica pendente. |
| POSTGRESQL | Estado curto e estado de 100 temporadas gravados e recuperados após reiniciar a API. O estado longo foi gravado novamente. |
| OLD_SAVES_PRESERVED | Testes com snapshots sem a extensão recuperaram os fatos preservados e identificaram cobertura parcial. A validação usou bancos separados. |
| MULTIPLAYER | Dois treinadores na mesma sala, um por aparência, receberam o mesmo JSON ao consultar Palestra Verde. |
| DOTNET_TESTS | 507 Domain, 31 Application e 275 casos distintos de Integration aprovados. Testes adicionais com PostgreSQL confirmaram recuperação e consultas do repositório. |
| FLUTTER_TESTS | 395 aprovados na suíte completa. Após os ajustes de fonte e legenda, 5 casos de histórico foram repetidos. Análise estática sem problemas e build Web release concluído. |
| NODE_TESTS | 2 testes da suíte Node de catálogo passaram. Navegação real exercitada com Playwright/Chrome. |
| LONG_SIMULATIONS | 100 temporadas com 64 clubes e 28.700 partidas concluídas pelo motor. Validação nos marcos de 1, 10 e 100 temporadas. |
| BROWSER_TEST, DESKTOP, MOBILE | Chrome em 1366×1000 e 390×1000. História do clube e do jogador aberta nas duas aparências. Entradas da classificação, ranking e mercado conferidas. |
| KNOWN_ISSUES | [Limites conhecidos](KNOWN_ISSUES_017C.md). |

| Temporadas fechadas | Partidas arquivadas | Transferências registradas | Campeões e vices |
| ---: | ---: | ---: | ---: |
| 1 | 287 | 84 | 10 |
| 10 | 2.870 | 804 | 100 |
| 100 | 28.700 | 7.995 | 1.000 |

O cenário longo totalizou 2.617.710 minutos simulados. Após a poda restaram
798 fixtures completas, com os 28.700 placares no histórico. Os totais de
jogos, gols e assistências foram comparados com as somas anuais. O teste de
reprocessamento e de reinício manteve a identidade e os números registrados.

A primeira execução de Integration passou 261 de 275 casos. Os demais
exigiam variáveis de bancos isolados e snapshots aprovados de missões
anteriores. Após preparar as dependências, os 14 casos foram reexecutados e
passaram. A reexecução por método também incluiu casos da mesma teoria já
aprovados na primeira passagem. `scripts/test-history-regression.ps1`
documenta a configuração e aceita filtros para repetir cenários específicos.

Os testes de domínio incluem 20 verificações de hashes congelados do motor de
partidas. Nenhum arquivo do motor foi alterado. Os testes de regressão também
abrangem mercado, leilão, compra direta, contratos, economia, banco, táticas,
multiplayer, criador, força inicial, evolução, classificação e Taça.

Na sala curta de QA, a partida Atlético Paulistano 1 × 2 Grêmio Guanabara
gerou um jogo em cada histórico de clube. Nos rankings de ambas as aparências,
Pedro Vieira abriu a carreira com 1 jogo, 1 gol e 90 minutos. Os dois
treinadores consultaram o Atlético Paulistano e receberam o mesmo JSON.
Nenhuma exceção de página foi capturada nesse roteiro. A sala longa também
abriu a temporada 100 na interface atual.

Evidências de [simulação](history-017c/simulation.json), [PostgreSQL](history-017c/postgres.json)
e [navegador](history-017c/browser.json).

| Aparência | Clube | Jogador |
| --- | --- | --- |
| Atual | [Computador](history-017c/clube-atual-qa-web.png) · [Celular](history-017c/clube-atual-qa-mobile.png) | [Computador](history-017c/jogador-atual-qa-web.png) · [Celular](history-017c/jogador-atual-qa-mobile.png) |
| Clássica | [Computador](history-017c/clube-classico-qa-web.png) · [Celular](history-017c/clube-classico-qa-mobile.png) | [Computador](history-017c/jogador-classico-qa-web.png) · [Celular](history-017c/jogador-classico-qa-mobile.png) |
