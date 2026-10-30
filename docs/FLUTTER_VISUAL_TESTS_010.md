# Testes Automatizados Flutter e Backend — Tarefa 010 (`ELIFOOT_VISUAL_POLISH_AND_RESPONSIVE_010`)

## 1. Sumário de Execução

| Suíte | Comando | Resultado | Total |
| --- | --- | --- | --- |
| Análise Estática Flutter | `flutter analyze` | `PASS` (0 problemas) | — |
| Testes Herméticos Flutter (`test/`) | `flutter test` | `PASS` | 42/42 |
| Runner Visual QA 010 (`tool/qa/`) | `flutter test tool/qa/player_rankings_visual_010_test.dart` | `PASS` | 2/2 (24 PNGs gerados) |
| Testes de Domínio `.NET` | `dotnet test tests/Elifoot.Domain.Tests -c Release` | `PASS` | 196/196 |
| Testes de Aplicação `.NET` | `dotnet test tests/Elifoot.Application.Tests -c Release` | `PASS` | 5/5 |
| Testes de Integração `.NET` | `dotnet test tests/Elifoot.Integration.Tests -c Release` | `PASS` | 29/29 |
| Testes de Simulação `.NET` | `dotnet test tests/Elifoot.Simulation.Tests -c Release` | `PASS` | 8/8 |

## 2. Cobertura dos Cenários Obrigatórios da Seção 19 (`test/visual_polish_010_test.dart`)

1. **Singular (`ARTILHEIRO: 1 VEZ`)**: Validado em unit test (`formatTopScorerTitles(1)`) e widget test no modal de histórico.
2. **Plural (`ARTILHEIRO: 2 VEZES`)**: Validado em unit test e widget test.
3. **Plural com zero (`ARTILHEIRO: 0 VEZES`)**: Validado em unit test e widget test.
4. **Plural com 5 e 10 (`ARTILHEIRO: 5 VEZES`, `ARTILHEIRO: 10 VEZES`)**: Validado em unit test e widget test.
5. **Nome histórico do clube**: Validado na tabela `player-season-history` sem exposição de GUID cru (`Guarani da Serra`).
6. **Jogador que mudou de clube e divisão**: Validado em `visual_polish_010_test.dart` e `PlayerRankings009Tests.History_ResolvesHistoricalClubNames_AcrossTransfersSameSeasonAndLaterRenaming`.
7. **Jogador em dois clubes na mesma temporada**: Validado em `visual_polish_010_test.dart` (Temporada 1 com `Guarani da Serra` na 2ª divisão e `Real Pampulha` na 1ª divisão) e no domínio `.NET`.
8. **Fallback para GUID histórico desconhecido**: Validado que um GUID órfão (`deadbeef-dead-4eef-8eef-deadbeef0000`) exibe `Clube histórico indisponível` e registra log técnico sem mostrar o GUID cru.
9. **Formatação de data sem horário (`30/10/2026`)**: Validado com timestamp ISO de 8 dígitos fracionários (`2026-10-30T12:51:52.36020317`).
10. **Formatação de data com horário (`30/10/2026 12:51`)**: Validado via `formatClassicDate(..., includeTime: true)`.
11. **Formatação de valor financeiro (`R$ 3.000.000`, `R$ 6.000.000`, `R$ 0`)**: Validado nas tabelas `player-award-history`, `season-scorer-awards` e `season-club-awards`.
12. **Filtros de categoria, competição e período**: Validados para `Artilheiros`, `Goleiros`, `Defensores`, `Geral`, `1ª` a `4ª divisão`, `Taça`, `Carreira inteira`, `Temporada atual` e `Temporada selecionada`.
13. **Goleiros e Defensores (`>= 20` jogos e `< 20` jogos)**: Validados tanto na tabela principal quanto na seção `AINDA NÃO ELEGÍVEIS` com `JOGOS FALTANTES`.
14. **Abertura e fechamento do histórico do jogador**: Validado por clique no nome do jogador (tanto na tabela normal quanto na coluna fixa após scroll mobile), fechamento pelo botão `FECHAR` e pela tecla `Escape`.
15. **Premiação de artilheiros empatados e modal de encerramento**: Validado o destaque de empate (`EMPATE NA ARTILHARIA`), prêmio individual de `R$ 3.000.000` e acumulado de `R$ 6.000.000`.
16. **Responsividade e ausência de `RenderFlex overflow` nas 7 larguras obrigatórias**:
    - `320 px`, `360 px`, `390 px`, `430 px`, `768 px`, `1024 px`, `1280 px`, incluindo nomes longos de atletas e clubes, rolagem horizontal e verificação de que o nome do jogador permanece visível e clicável após o scroll horizontal.
