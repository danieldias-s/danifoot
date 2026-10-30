# Relatório Técnico de Auditoria e Balanceamento — 100 Temporadas Reais (`ELIFOOT_GAMEPLAY_BALANCE_100_SEASONS_005`)

## 1. Resumo Executivo e Regra Fundamental da Simulação

Todas as simulações desta auditoria foram executadas utilizando **exclusivamente o motor real de partidas minuto a minuto** ([`MatchSimulationEngine.SimulateFullMatchReal`](file:///d:/elifoot_98/src/Elifoot.Domain/MatchEngine.cs#L872-L1035) e [`MatchSimulationEngine.SimulateSingleMinute`](file:///d:/elifoot_98/src/Elifoot.Domain/MatchEngine.cs#L183-L261)). **Nenhum** placar agregado, gerador Poisson simplificado ou atalho foi utilizado.

Cada uma das partidas simulou integralmente:
- Minutos `1..45` do 1º tempo;
- Intervalo gratuito aos `45'` (`RoundState.HALFTIME_PAUSED`) com reação tática e substituições da IA;
- Minutos `46..90` do 2º tempo com checagens táticas aos `62'`, `74'` e `83'`;
- Nos jogos eliminatórios da Taça empatados aos `90'`, abertura da prorrogação (`RoundState.EXTRA_TIME_BREAK`, liberando a 6ª substituição e a 4ª janela de parada), 1º tempo da prorrogação (`91..105`), intervalo aos `105'` (`RoundState.EXTRA_TIME_HALFTIME`), 2º tempo da prorrogação (`106..120`) e disputa de pênaltis ([`MatchSimulationEngine.ResolvePenaltyShootout`](file:///d:/elifoot_98/src/Elifoot.Domain/MatchEngine.cs#L809-L865)).

---

## 2. Experimentos Executados (`BENCHMARK_REAL_MATCH_ENGINE_005.json`)

Foram executadas **301 temporadas completas** somando os 3 cenários independentes de 100 temporadas consecutivas e o experimento de replay determinístico, totalizando **105.651 partidas reais** e **9.794.220 minutos reais simulados**:

| Identificador | WorldSeed | Cenário | Temporadas | Rodadas | Partidas Agendadas | Partidas Concluídas | Minutos Reais Simulados | Tempo (ms) | Partidas/s | Integridade |
| :--- | :---: | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| `EXP_A_100_SEASONS_AI_199801` | `199801` | **Cenário Principal (A)**: 128 clubes controlados por IA com personalidades próprias | `1..100` (100) | 2.200 | 35.100 | **35.100** | **3.253.440** | 59.769 ms | 587,3 | **PASS (0 violações)** |
| `EXP_B_100_SEASONS_8_HUMAN_AGENTS_199802` | `199802` | **Cenário B**: 8 agentes treinadores humanos automatizados com políticas distintas + 120 clubes IA | `1..100` (100) | 2.200 | 35.100 | **35.100** | **3.254.010** | 56.009 ms | 626,7 | **PASS (0 violações)** |
| `EXP_C_100_SEASONS_STRESS_ECONOMY_199803` | `199803` | **Cenário C**: Mercado e economia sob estresse (baixo caixa inicial, estádios superdimensionados/degradados) | `1..100` (100) | 2.200 | 35.100 | **35.100** | **3.254.220** | 57.620 ms | 609,2 | **PASS (0 violações)** |
| `EXP_REPLAY_1_199801` vs `EXP_REPLAY_2_199801` | `199801` | **Replay Determinístico**: Temporada completa reexecutada duas vezes | `1..1` (1x2) | 22 | 351 | **351** | **32.550** | 580 ms | 605,2 | **Bit-a-Bit Idêntico** (`733:346:367:884:154:1076:317197877,91`) |

### Invariantes de Mundo Verificadas nas 300 Temporadas
- **Clubes únicos mantidos em todas as temporadas**: sempre exatamente **128 clubes** (`8` na 1ª Divisão, `8` na 2ª Divisão, `8` na 3ª Divisão, `8` na 4ª Divisão e `96` na Divisão Distrital).
- **Duplicação ou desaparecimento de clubes**: `0` ocorrências em 300 temporadas.
- **Clubes duplicados na mesma fase da Taça**: `0` ocorrências em 6.600 rodadas.
- **Elencos abaixo de 11 ou acima de 24 jogadores**: `0` ocorrências.
- **Venda do único goleiro**: `0` ocorrências.
- **Escalações pré-jogo inválidas**: `0` ocorrências nas 210.600 escalações auditadas.

---

## 3. Validação e Caracterização do Motor de Partidas (`MatchEngine.cs`)

A fórmula defensiva e setorial em [`MatchSimulationEngine.ComputeSectorBreakdown`](file:///d:/elifoot_98/src/Elifoot.Domain/MatchEngine.cs#L26-L142) foi **preservada integralmente sem alterações**:

$$\text{EffectiveDefense} = (\text{DefPower} + 0{,}45 \times \text{MidPower}) \times \text{HomeMod} \times \text{IdentityDefMod} \times 0{,}93^{\text{MissingPlayers}}$$

$$\text{EffectiveAttack} = (\text{AttPower} + 0{,}50 \times \text{MidPower}) \times \text{HomeMod} \times \text{IdentityAttMod} \times 0{,}93^{\text{MissingPlayers}} \times \text{TransitionMod}$$

- **Participação do Meio-Campo**: colabora simultaneamente com `45%` na defesa e `50%` no ataque, equilibrando esquemas de meio-campo povoado (`3-5-2`, `4-5-1`, `3-6-1`) contra esquemas de 3 ou 4 atacantes (`4-3-3`, `3-4-3`, `4-2-4`).
- **Formação Clássica `5-0-5`**: aplica `Classic505TransitionFactor = 0.82` no ataque efetivo pela ausência de meio-campo de transição.
- **Influência da Estrela `*`**: multiplicador auditável de `+18%` para atacantes (`1.18`), `+15%` para meias (`1.15`) e `+10%` para defensores/goleiros (`1.10`).
- **Comportamento Disciplinar**: estritamente monotônico (`FairPlay = 0.0 < Cordeirinho = 0.35 < Cavalheiro = 0.75 < Caneleiro = 1.65 < Caceteiro = 3.10 < Sarrafeiro = 5.20`), sem bônus oculto de desarme para atletas agressivos.

---

## 4. Estatísticas Reais das Partidas (`MATCH_STATISTICS_005.json`)

### 4.1 Indicadores Globais (Comparativo entre os 3 Cenários de 100 Temporadas)

| Indicador | Cenário A (`199801`) | Cenário B (`199802` - 8 Agentes) | Cenário C (`199803` - Estresse) |
| :--- | :---: | :---: | :---: |
| **Total de Partidas Reais** | 35.100 | 35.100 | 35.100 |
| **Total de Gols** | 88.241 | 83.565 | 85.256 |
| **Média de Gols por Partida** | **2,514** | **2,381** | **2,429** |
| **Gols no 1º Tempo (`1'..45'`)** | 43.470 (49,26%) | 41.068 (49,14%) | 41.986 (49,25%) |
| **Gols no 2º Tempo (`46'..90'`)** | 42.633 (48,31%) | 40.280 (48,20%) | 41.073 (48,18%) |
| **Gols na Prorrogação (`91'..120'`)** | 2.138 (2,42%) | 2.217 (2,65%) | 2.197 (2,58%) |
| **Vitórias do Mandante** | 14.959 (**42,62%**) | 14.689 (**41,85%**) | 14.812 (**42,20%**) |
| **Empates (no tempo regulamentar/ET)** | 7.515 (**21,41%**) | 7.938 (**22,62%**) | 7.741 (**22,05%**) |
| **Vitórias do Visitante** | 12.626 (**35,97%**) | 12.473 (**35,54%**) | 12.547 (**35,75%**) |
| **Viradas (`Comebacks`)** | 3.792 (**10,80%**) | 3.362 (**9,58%**) | 3.541 (**10,09%**) |
| **Gols de Pênalti (bola rolando)** | 9.055 (0,258/jogo) | 8.618 (0,246/jogo) | 8.812 (0,251/jogo) |
| **Pênaltis Perdidos (bola rolando)** | 3.144 (taxa conv. **74,23%**) | 3.027 (taxa conv. **74,01%**) | 3.095 (taxa conv. **74,01%**) |
| **Cartões Amarelos** | 89.757 (**2,557/jogo**) | 89.764 (**2,557/jogo**) | 89.610 (**2,553/jogo**) |
| **Cartões Vermelhos (Direto + 2º Amarelo)** | 16.808 (8.665 + 8.143 = **0,479/jogo**) | 16.309 (**0,465/jogo**) | 16.590 (**0,473/jogo**) |
| **Lesões** | 11.787 (**0,336/jogo**) | 11.617 (**0,331/jogo**) | 11.704 (**0,333/jogo**) |
| **Substituições Executadas** | 87.894 (**2,504/jogo**) | 83.825 (**2,388/jogo**) | 86.412 (**2,462/jogo**) |
| **Jogos com Prorrogação (Taça)** | 3.148 (24,79% da Taça) | 3.167 (24,94% da Taça) | 3.155 (24,84% da Taça) |
| **Disputas de Pênaltis (Taça)** | 1.745 (13,74% da Taça) | 1.749 (13,77% da Taça) | 1.741 (13,71% da Taça) |

### 4.2 Gols por Divisão e Competição (`WorldSeed = 199801`)
- **1ª Divisão**: `5.600` jogos | `15.290` gols | **2,730 gols/jogo**
- **2ª Divisão**: `5.600` jogos | `14.618` gols | **2,610 gols/jogo**
- **3ª Divisão**: `5.600` jogos | `13.382` gols | **2,390 gols/jogo**
- **4ª Divisão**: `5.600` jogos | `12.391` gols | **2,213 gols/jogo**
- **Taça Nacional (incluindo Preliminares Distritais)**: `12.700` jogos | `32.560` gols | **2,564 gols/jogo**

### 4.3 Distribuição de Placares (`WorldSeed = 199801` — 35.100 Jogos)
- `1-0` ou `0-1`: **8.431 jogos (24,02%)** (`1-0`: 4.482 | `0-1`: 3.949)
- `2-1` ou `1-2`: **5.104 jogos (14,54%)** (`2-1`: 2.712 | `1-2`: 2.392)
- `2-0` ou `0-2`: **4.718 jogos (13,44%)** (`2-0`: 2.641 | `0-2`: 2.077)
- `1-1`: **3.165 jogos (9,02%)**
- `0-0`: **2.998 jogos (8,54%)**
- `3-0` ou `0-3`: **2.290 jogos (6,52%)**
- `3-1` ou `1-3`: **2.121 jogos (6,04%)**
- Placares com 4+ gols somados (`2-2`, `3-1`, `3-2`, `4-0`, etc.): **24,61%**

### 4.4 Desempenho por Formação Tática Inicial (`WorldSeed = 199801` e `199802`)

| Formação | Aparições (`199801`) | % Vitórias | % Empates | % Derrotas | Gols Pró/J | Gols Contra/J | Perfil Tático Observado |
| :---: | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| `4-3-3` | 19.878 | 41,71% | 19,92% | 38,37% | 1,425 | 1,337 | Ofensiva equilibrada e consistente |
| `4-4-2` | 18.620 | 36,41% | 21,90% | 41,69% | 1,159 | 1,308 | Formação padrão versátil |
| `3-4-3` | 7.615 | 47,80% | 18,75% | 33,45% | 1,689 | 1,299 | Alta produção ofensiva para elencos fortes |
| `5-3-2` | 6.216 | 34,46% | 23,15% | 42,39% | 0,998 | 1,175 | Contenção sólida para elencos defensivos |
| `5-4-1` | 6.144 | 34,64% | 27,73% | 37,63% | 0,912 | 0,965 | Menor média de gols sofridos (`0,965/J`) e maior taxa de empates (`27,73%`) |
| `4-5-1` | 5.851 | 33,00% | 25,23% | 41,77% | 0,940 | 1,152 | Controle de meio-campo e baixa exposição |
| `3-5-2` | 5.543 | 43,53% | 20,58% | 35,88% | 1,351 | 1,215 | Forte domínio de meio-campo com dupla de ataque |
| `3-6-1` | 173 | 37,57% | 26,01% | 36,42% | 1,116 | 1,162 | Especialista em posse de bola e congestionamento central |
| `4-2-4` | 160 | 40,62% | 11,88% | 47,50% | 1,556 | 1,706 | Alto risco / alta recompensa (menor índice de empates: `11,88%`) |
| `5-0-5` (`199802`) | 232 | 32,33% | 29,74% | 37,93% | 1,082 | 1,181 | Formação clássica experimental com penalidade de transição (`0.82`) |

---

## 5. Inteligência Artificial dos Treinadores (`AI_MANAGER_BALANCE_005.json`)

A auditoria individual dos **128 treinadores IA** (`199801`) e dos **8 agentes treinadores humanos + 120 treinadores IA** (`199802`) comprovou:
- **100% dos treinadores ativos (`128/128`) adaptaram a formação** para a 2ª ou 3ª preferência quando lesões, suspensões, falta de orçamento ou força do adversário inviabilizaram a 1ª formação preferida.
- **75 treinadores mudaram de clube** ao longo das 100 temporadas (até 16 clubes distintos dirigidos pelo mesmo técnico), levando consigo sua identidade (`Ofensivo`, `Equilibrado`, `Defensivo`) e suas 3 formações preferidas (`PlayableEconomyRegressionTests.ManagerKeepsPersonalityAndPreferencesWhenSeasonRotatesClubs`).
- **100% dos treinadores utilizaram reservas** em substituições durante as partidas (`1.300` a `1.865` substituições por treinador ao longo de 100 temporadas).
- **Zero escalações inválidas (`0/210.600`)** após a correção do `BUG-004`.

---

## 6. Mercado de Transferências e Economia (`MARKET_ECONOMY_005.json`)

### 6.1 Curva Convexa `FairValue` e Salário por Rodada
| Força (`Strength`) | `FairValue` Normal | `FairValue` Craque `*` | Salário/Rodada Normal | Salário/Rodada Craque `*` |
| :---: | :---: | :---: | :---: | :---: |
| **10** | R$ 51.462 | R$ 67.929 | R$ 452 | R$ 542 |
| **20** | R$ 252.090 | R$ 332.759 | R$ 1.143 | R$ 1.372 |
| **30** | R$ 712.321 | R$ 940.263 | R$ 2.343 | R$ 2.812 |
| **40** | R$ 1.511.367 | R$ 1.995.004 | R$ 4.068 | R$ 4.881 |
| **50** | R$ 2.719.280 | R$ 3.589.450 | R$ 6.330 | R$ 7.596 |

### 6.2 Volume de Mercado em 100 Temporadas
- **Cenário A (`199801`)**: `8.800` compras diretas/bilaterais + `23.698` arremates em leilão = **32.498 contratações** (média de `325` negociações por temporada entre os clubes), com preço médio de **R$ 509.413**, mínimo de **R$ 45.140** e máximo de **R$ 4.016.168**.
- **Cenário B (`199802` com 8 Agentes Humanos)**: `8.800` transferências (incluindo **942 transferências envolvendo clubes de agentes humanos**) + `23.694` leilões concluídos.
- **Equilíbrio Inflacionário**: a força média dos jogadores de divisão manteve-se estável entre `27,6` e `30,3` da Temporada 1 à Temporada 100 (`AvgPlayerFairValue` entre R$ 618 mil e R$ 768 mil), sem hiperinflação de preços ou colapso de liquidez.

---

## 7. Estádios (`STADIUM_BALANCE_005.json`)

Durante as 100 temporadas do Cenário A (`199801`):
- **Manutenções preventivas pagas**: `2.382` eventos (R$ 463,85M investidos).
- **Obras de reforma estrutural**: `2.468` reformas executadas quando a condição caiu abaixo de `82%` (R$ 192,98M investidos).
- **Obras de ampliação por ROI positivo**: `263` ampliações de `+5.000` lugares (R$ 111,77M investidos), elevando a capacidade construída média das 4 divisões de `21.721` (T1) para `42.336` lugares (T100), com condição estrutural média preservada em **89,61%**.
- **Cenário C (Estresse Econômico `199803`)**: clubes da 3ª e 4ª divisões iniciados com estádios de `42.000` lugares degradados (`68%` de condição) e caixa curto (`R$ 140.000`) precisaram vender excedentes e aguardar recuperação financeira antes de reformar, validando o risco real de estádios superdimensionados.

---

## 8. Evolução Competitiva e Mobilidade Distrital (`COMPETITION_EVOLUTION_005.json`)

Ao longo das 100 temporadas do Cenário A (`199801`):
- **19 clubes distintos** conquistaram o título da 1ª Divisão (maior sequência consecutiva: `4` títulos).
- **27 clubes distintos** conquistaram a Taça Nacional.
- **1.007 zebras na Taça Nacional** (vitórias eliminatórias de clubes de divisão inferior ou Distrital sobre clubes de divisão superior).
- **Mobilidade Vertical Completa**:
  - `200` promoções do Distrital para a 4ª Divisão ao longo das 100 temporadas;
  - Dos 96 clubes que iniciaram no Distrital na Temporada 1, **32 alcançaram a 3ª Divisão**, **25 alcançaram a 2ª Divisão** e **6 chegaram até a 1ª Divisão**!
  - Todos os **8 clubes fundadores da 1ª Divisão** sofreram pelo menos um rebaixamento ao longo dos 100 anos, e **2 deles chegaram a cair até o Distrital**.
- No **Cenário B (`199802`)**, houve **12 demissões automáticas de agentes humanos por rebaixamento da 4ª Divisão para o Distrital**, seguidas de **12 contratações via convites de clubes (`JobInvitations`)**, mantendo `0` humanos no Distrital em 100% do tempo.

---

## 9. Problemas Encontrados, Classificação e Correções Aplicadas

### `BUG-001` — Suspensão por 3º Cartão Amarelo Decrementada no Fim da Própria Partida
- **Evidência**: em [`MarketAndEconomyEngine.SettleMatchAndRoundFinancials`](file:///d:/elifoot_98/src/Elifoot.Domain/MarketAndEconomyEngine.cs#L315-L325), o decremento de `SuspensionRoundsRemaining` verificava apenas `HomeSentOffIds`/`AwaySentOffIds`, limpando imediatamente a suspensão (`1 -> 0`) do atleta que acabara de receber o 3º amarelo na partida.
- **Correção**: adicionadas as listas `HomeSuspendedByYellowInMatchIds` e `AwaySuspendedByYellowInMatchIds` em [`MatchFixture`](file:///d:/elifoot_98/src/Elifoot.Domain/Entities.cs#L251-L260), populadas em [`MatchSimulationEngine.ProcessTeamDisciplineTick`](file:///d:/elifoot_98/src/Elifoot.Domain/MatchEngine.cs#L363-L370) e respeitadas em `SettleMatchAndRoundFinancials`.
- **Teste de Regressão**: `DomainEngineAndRulesTests.MatchEngine_Characterization_All10Formations_DefensiveFormula_DeterministicReplay_AndBugRegressions`.

### `BUG-002` — Cobrança de Folha Salarial e Desgaste Contratual em Rodadas sem Jogo
- **Evidência**: clubes das 4 divisões pagavam folha salarial e consumiam 1 rodada de contrato nas rodadas `CupPreliminary1` e `CupPreliminary2` (exclusivas de clubes distritais) e nas fases finais da Taça após eliminação. Além disso, visitantes em jogos únicos da Taça não recebiam participação na bilheteria.
- **Correção**: condicionada a cobrança de salários e contagem de contratos a `playedThisRound` em [`MarketAndEconomyEngine.SettleMatchAndRoundFinancials`](file:///d:/elifoot_98/src/Elifoot.Domain/MarketAndEconomyEngine.cs#L266-L275) e instituída a divisão clássica de bilheteria na Taça (`75%` mandante / `25%` visitante) mais cotas de classificação por divisão em [`CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance`](file:///d:/elifoot_98/src/Elifoot.Domain/CompetitionAndSeasonEngine.cs#L498-L506).

### `BUG-003` — Limiar de Força Incorreto na Contratação de Reservas pela IA
- **Evidência**: em [`MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch`](file:///d:/elifoot_98/src/Elifoot.Domain/MarketAndEconomyEngine.cs#L611-L633), quando o clube já possuía os titulares da posição (ex.: 3 atacantes no `4-3-3`) e buscava um reserva, `minStrengthNeeded` exigia `currentInPos.First().Strength + 2` (mais forte que o melhor titular do elenco), travando a reposição de banco.
- **Correção**: separadas as 3 faixas de necessidade (`Titular urgente`, `Reserva tático` e `Melhoria titular` comparada ao titular mais fraco do setor).

### `BUG-004` — Preenchimento Improvisado de Linha com 2º/3º Goleiro em `BuildStartersAndBench`
- **Evidência**: a simulação de 100 temporadas detectou `4.276` casos em `35.100` jogos nos quais elencos com desfalques num setor de linha selecionavam o goleiro reserva (`PlayerPosition.G`) como jogador de linha improvisado porque `BuildStartersAndBench` ordenava `remaining` apenas por `SelectionScore`.
- **Correção**: [`AiManagerBrain.BuildStartersAndBench`](file:///d:/elifoot_98/src/Elifoot.Domain/AiManagerBrain.cs#L266-L320) foi corrigido para selecionar estritamente `1` goleiro (`G`) e preencher as `10` vagas de linha exclusivamente com atletas `D/M/A`, reduzindo as violações de escalação de `4.276` para **zero (`0`)** nas 300 temporadas.

### `BALANCEAMENTO-001` — Evolução Sazonal Determinística de Força dos Atletas
- **Evidência**: em 100 temporadas, a força estática impedia que clubes promovidos do Distrital desenvolvessem seus destaques.
- **Ajuste**: implementado [`CompetitionAndSeasonEngine.EvolvePlayersAtSeasonEnd`](file:///d:/elifoot_98/src/Elifoot.Domain/CompetitionAndSeasonEngine.cs#L641-L688) com progressão determinística (`-1` a `+2`, limitada a `10..50`) baseada em gols, jogos disputados e equilíbrio por divisão.

### `BALANCEAMENTO-002` — Preservação da Identidade Tática nas Reações durante a Partida
- **Evidência**: a telemetria inicial mostrou que todos os técnicos mudavam para `5-3-2` aos 74' quando venciam e para `4-3-3` aos 60' quando perdiam, distorcendo a taxa de vitórias por formação final.
- **Ajuste**: [`AiManagerBrain.EvaluateAiInMatchReaction`](file:///d:/elifoot_98/src/Elifoot.Domain/AiManagerBrain.cs#L416-L442) passou a respeitar `Adaptability >= 65` e a identidade do treinador, recorrendo à 2ª formação preferida da própria filosofia do técnico.
