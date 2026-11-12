# AUDITORIA DAS REGRAS JÁ IMPLEMENTADAS DE FORÇA DOS JOGADORES E CLUBES

**Projeto:** Elifoot 98 Multiplayer (`D:\elifoot_98`)  
**Branch:** `main`  
**HEAD Auditado:** `aa5130b9220967d363c66c631f4770ce01783ecc` (`feat(elifoot98): add offline catalog v1.1 and behavior classifier`)  
**Tipo da execução original:** auditoria somente leitura, sem alterações de implementação, JSON ou saves.

**Errata documental — 12/11/2026:** as regras de força foram conferidas diretamente nos fontes de `f1764fb`. O HEAD `aa5130b`, o estado de bloqueio e os resultados da auditoria original continuam registrados como históricos; `bf43cd4` integrou depois a criação V1.1. A descrição vigente de evolução, modos, fórmulas e bandas está em [ELIFOOT98_REGRAS_FORCAS_E_INTEGRACAO_V11.md](ELIFOOT98_REGRAS_FORCAS_E_INTEGRACAO_V11.md). Esta errata modifica apenas documentação; não é uma nova execução das suítes ou das simulações citadas.

---

## 1. Resumo Executivo

A presente auditoria investigou integralmente o código-fonte, a documentação técnica aprovada e a suíte de testes automatizados do projeto **Elifoot 98 Multiplayer** para mapear exatamente como o sistema implementa:
1. **Força inicial dos clubes** (`ClubInitialPower` / `teamStrength`, escala `1..20`);
2. **Força individual inicial dos jogadores** (`Player.Strength`, escala `1..50`);
3. **Aumento da força individual** durante as temporadas;
4. **Diminuição da força individual** (por participação em partida de Liga e por lesão diária);
5. **Evolução e regressão entre rodadas e na virada de temporada**.

### Conclusões Principais Confirmadas no Código

1. **Distinção Estrita entre Força do Clube (`1..20`), Ranking do Clube e Força Individual (`1..50`):**
   - **Força do Clube (`ClubInitialPower` na regra `016B` e `teamStrength` no Catálogo Offline `V1.1`):** usa escala inteira **`1..20`**. No sistema atual, esse valor **não calcula diretamente** a força individual `1..50` dos atletas; ele é usado exclusivamente como **critério primário de ordenação/classificação dos clubes** (`WorldCatalogRules016B.Order` e `OfflineCatalogRanking.Order`) para distribuir os 128 clubes nas divisões iniciais (`1ª Divisão`: 8 clubes; `2ª Divisão`: 8 clubes; `3ª Divisão`: 8 clubes; `4ª Divisão`: 8 clubes; `Distrital`: 96 clubes).
   - **Ranking do Clube (`WorldRank` / `HistoricalRanking` / `TieBreakPriority`):** funciona como critério de desempate entre clubes de mesma força `1..20` (ou para calcular `ClubInitialPower` automático a partir da posição `zeroBasedRank` na regra `016B`: `Math.Max(1, 20 - zeroBasedRank / 2)`).
   - **Força Individual (`Player.Strength`):** usa escala inteira **`1..50`**. Na regra ativa **`017B`** (`InitialSquadStrength017B`), a força inicial `1..50` de cada jogador nos 32 clubes das divisões `1..4` é gerada **a partir da divisão (`1..4`) e da posição inicial do clube na divisão (`1..8`)**, usando a tabela compartilhada `ChampionshipStrengthLimits.For(division, position)` e sorteio determinístico por `SHA256(seed, clubKey, playerKey)` na faixa `[ref - 1, ref + 1]` ajustada aos limites da divisão (ou `[47, 50]` com garantia dos 4 valores para o Top 3 da 1ª Divisão).
2. **Evolução e Regressão Ativa por Rodada (`ClassicForceEvolutionEngine` — Regra `CLASSIC_ADAPTED_FORCE_EVOLUTION`, Versão `1`):**
   - **Gatilho:** executada **uma única vez ao final de cada rodada oficial de Liga (`CompetitionType.League`)** via `CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers` -> `ClassicForceEvolutionEngine.ApplyFinishedRound`.
   - **Rodadas da Taça (`CompetitionType != CompetitionType.League`):** **não** geram ganho de reserva (`RESERVE_PROGRESS`) nem perda por participação (`PLAYED_MATCH_DECLINE`). Apenas lesões ocorridas em campo e o avanço do calendário virtual de lesões (`INJURY_DAILY_LOSS`) ocorrem.
   - **Aumento de Força (`RESERVE_PROGRESS`):** todo jogador vinculado a um clube das divisões `1..4` que **não entrou em campo** na rodada da Liga (`!appeared.Contains(p.Id)` — inclui reservas não utilizados, não relacionados, suspensos e lesionados que não atuaram na partida) ganha **deterministicamente `+1` ponto de força** (`100%` de probabilidade) se `Strength < Ceiling`, onde `Ceiling = ChampionshipStrengthLimits.For(club.DivisionTier, currentLeaguePosition)`. Nunca ultrapassa o teto da posição atual do clube nem `50`. Clubes do `Distrital` e atletas sem clube têm `Ceiling = 1` e nunca crescem.
   - **Redução por Participação (`PLAYED_MATCH_DECLINE`):** todo jogador que **atuou na rodada da Liga** (`appeared.Contains(p.Id)` — titular ou reserva que entrou em qualquer minuto, inclusive aos 90') sofre sorteio determinístico com **probabilidade de `50%` (`PlayedDeclineProbability = 0.50`)** para perder **`-1` ponto de força** (ou `50%` para manter a força inalterada), respeitando o piso mínimo `1` e desde que não tenha sofrido nova lesão na própria partida.
   - **Redução Diária por Lesão (`INJURY_DAILY_LOSS`):** quando `ForceEvolutionMode == ClassicAdapted`, uma lesão em partida sorteia duração `D` entre `2..15` dias (pesos `[26, 26, 24, 24, 22, 20, 18, 12, 8, 6, 5, 4, 3, 2]` em `200`) e perda total `TotalLoss` uniforme em `0..MaximumLoss(force, D)`, onde `MaximumLoss(force, D) = Math.Min(force - 1, force * D / 30)` (até `50%` da força inicial em lesão de 15 dias). A perda é descontada gradualmente dia a dia conforme o calendário virtual avança (`ScheduledCumulativeLoss(totalLoss, duration, day) = totalLoss * day / duration`), com piso `1`. A recuperação zera `InjuryDaysRemaining`, mas **não restaura** automaticamente a força perdida.
3. **Motor Sazonal Legado Desativado (`PlayerStrengthEvolutionEngine`):**
   - O método real é `PlayerStrengthEvolutionEngine.ApplySeasonEnd`. Sua política de caracterização usa gols, aparições, alvos de divisão e sorteios, não faixas etárias nem `Age++`. Tem guarda `if (ClassicForceEvolutionEngine.IsActive(world)) return;` e **não possui chamada de produção em `src/`**. O fechamento futuro da Liga aplica a regra atual de participação também aos saves `LegacySeasonal`; estes conservam suas regras de lesão. Não confundir o nome do modo com execução automática da rotina sazonal.
4. **Bloqueio histórico do Catálogo Offline V1.1 em `aa5130b` (`OfflineCatalogRanking.ForceBlock`):**
   - O Catálogo Offline `V1.0` traz `players[].strength` literal (`1..50`) no JSON e funciona em `OfflineWorldFactory`.
   - O Catálogo Offline `V1.1` define `teamStrength` (`1..20`) no clube e proíbe `players[].strength`. Na auditoria de `aa5130b`, `OfflineWorldFactory` ainda não conectava a geração e bloqueava a criação com `ForceBlock`. **Estado posterior, em `bf43cd4`:** gera por `InitialPlayerStrength.Apply`, substitui as forças dos 32 clubes principais com 017B e conserva as estimativas nos 96 distritais. Não introduz conversão aritmética direta de teamStrength para força individual nem altera a evolução posterior.

---

## 2. Estado histórico do Repositório na auditoria original

- **Diretório:** `D:\elifoot_98`
- **Branch ativa:** `main`
- **Commit `HEAD`:** `aa5130b9220967d363c66c631f4770ce01783ecc` (`feat(elifoot98): add offline catalog v1.1 and behavior classifier`)
- **Checkpoints históricos preservados na árvore Git:**
  - Etapa 01: `d0489d3`, `2818cb3`
  - Etapa 02A: `2402e99`, `ced4e43`
  - Etapa 02B: `8df9b0c`, `b3fc9e3`
  - Etapa 02C: `82bdadb`, `f678e85`, `2492e95`
  - Etapa 02D: `ac4e867`, `5594e56`, `383abbc`
  - Catálogo Offline V1.1: `aa5130b`
- **Integridade da auditoria:** Nenhum arquivo de código, JSON ou save foi alterado durante esta auditoria.

---

## 3. Histórico de Commits Relevantes (Força dos Clubes e Jogadores)

| Commit | Mensagem | Escopo / Regra Introduzida ou Alterada |
| :--- | :--- | :--- |
| `a2fcbd5` | `Record baseline initial squad strengths before generator change` | Registro de baseline das forças iniciais antes da regra `017B`. |
| `18a1f31` | `Use classic starting strength distributions for newly invented clubs` | Aplicação das faixas iniciais para clubes inventados/fictícios. |
| `3662106` | `Record divisional strength audit and real-engine season regression` | Auditoria de força por divisão e regressão em temporada completa. |
| `6e0473f` | `Apply and verify season-one initial squad strength rules for 017B` | Implementação de `InitialSquadStrength017B` (referência por divisão `1..4` e posição `1..8`, banda `ref-1..ref+1` e `[47..50]` para Top 3 da 1ª Divisão). |
| `8e9ee25` | `Evolve player strength once per official round using league position` | Substituição da fórmula probabilística antiga da `012C` pela evolução determinística `+1` para não-participantes até o teto da posição na Liga e `50%` de queda `-1` para participantes. |
| `89b762c` | `Verify settled saves and record force evolution regressions` | Proteção de saves já liquidados e testes de regressão da evolução de força. |
| `9af02bc` | `Alternate league pairs with an all-club cup and restrict strength evolution to league rounds` | Restrição estrita de `RESERVE_PROGRESS` e `PLAYED_MATCH_DECLINE` exclusivamente às rodadas `CompetitionType.League` (Taça preserva forças). |
| `2ef9c09` | `Record verified cup calendar and league-only force regressions` | Evidências e testes de regressão confirmando que a Taça não altera força por participação/reserva. |
| `451a916` | `merge: preserve superseded initial strength audit history` | Preservação do histórico documental de auditorias anteriores de força inicial. |
| `aa5130b` | `feat(elifoot98): add offline catalog v1.1 and behavior classifier` | Adição do schema `1.1` com `teamStrength` (`1..20`), ordenação `OfflineCatalogRanking` e bloqueio explícito `OfflineCatalogRanking.ForceBlock` até definição da geração de força `V1.1`. |

---

## 4. Arquivos Analisados

### Código de Domínio (`src/Elifoot.Domain`)
- [`src/Elifoot.Domain/ChampionshipStrengthLimits.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/ChampionshipStrengthLimits.cs#L1-L18) — Tabela compartilhada de tetos/referências `1..50` por divisão (`1..4`) e posição (`1..8`).
- [`src/Elifoot.Domain/ClassicForceEvolutionEngine.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/ClassicForceEvolutionEngine.cs#L1-L297) — Motor ativo de evolução/regressão de força por rodada da Liga e perda diária por lesão.
- [`src/Elifoot.Domain/ClassicForceState.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/ClassicForceState.cs#L1-L43) — Estado persistido de calendário virtual, guardas de rodada, posições concluídas na Liga, lesões de força e snapshots de turno.
- [`src/Elifoot.Domain/PlayerStrengthEvolutionEngine.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/PlayerStrengthEvolutionEngine.cs#L1-L74) — Motor sazonal legado (`LEGACY_SEASONAL_005_SCALE_FIX_012`), hoje inativo quando `ClassicForceEvolutionEngine.IsActive(world)` é verdadeiro.
- [`src/Elifoot.Domain/WorldSeedFactory.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/WorldSeedFactory.cs#L1-L350) — Fábrica de mundo rápido com sorteio clássico por faixa de divisão e suporte opcional a `InitialSquadStrength017B.Apply`.
- [`src/Elifoot.Domain/Catalog/InitialSquadStrength017B.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/InitialSquadStrength017B.cs#L1-L67) — Gerador ativo da força inicial dos elencos na temporada 1 (regra `017B`).
- [`src/Elifoot.Domain/Catalog/InitialPlayerStrength.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/InitialPlayerStrength.cs#L1-L50) — Estimador `relative-v1` de força `1..50` para atletas importados de provedor externo.
- [`src/Elifoot.Domain/Catalog/WorldCatalogRules016B.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/WorldCatalogRules016B.cs#L1-L59) — Cálculo de `ClubInitialPower` (`1..20`), `SquadSizeAt` (`17..23`), classificação e seleção balanceada de elencos.
- [`src/Elifoot.Domain/Catalog/PlayableWorldPolicy.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/PlayableWorldPolicy.cs#L1-L140) — Preparação de clubes jogáveis e geração de atletas fictícios de preenchimento.
- [`src/Elifoot.Domain/Catalog/HistoricalCatalogPolicy.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/HistoricalCatalogPolicy.cs#L1-L79) — Política de importação histórica (`1998`) e estimativa de força quando `Strength <= 0`.
- [`src/Elifoot.Domain/Catalog/InitialRosterPolicy.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/InitialRosterPolicy.cs#L1-L43) — Política legada de corte de elenco (`target = 18`).
- [`src/Elifoot.Domain/Catalog/WorldCreatorEngine.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/WorldCreatorEngine.cs#L1-L395) — Criador de mundos a partir do catálogo, aplicando `WorldCatalogRules016B` e `InitialSquadStrength017B`.
- [`src/Elifoot.Domain/Catalog/OfflineCatalogRanking.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/OfflineCatalogRanking.cs#L1-L53) — Ordenação de clubes `V1.1` por `teamStrength` (`1..20`) e bloqueio `ForceBlock`.
- [`src/Elifoot.Domain/Catalog/OfflineCatalogRules.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/OfflineCatalogRules.cs#L1-L56) — Validação de contratos dos catálogos offline `V1.0` e `V1.1`.
- [`src/Elifoot.Domain/Catalog/OfflineWorldFactory.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/OfflineWorldFactory.cs#L1-L164) — Fábrica de mundos offline (`V1.0` funcional com força literal; `V1.1` bloqueado no `Preview`/`Create`).
- [`src/Elifoot.Domain/CompetitionAndSeasonEngine.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/CompetitionAndSeasonEngine.cs#L1-L753) — Orquestração de calendário, fim de rodada (`ClassicForceEvolutionEngine.ApplyFinishedRound`) e virada de temporada (`FinalizeSeasonAndAdvance`).
- [`src/Elifoot.Domain/LineupSelectionEngine.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/LineupSelectionEngine.cs#L1-L73) — Escalação `Best` (`Melhores`) vs `Automatic` (`Automático` com bônus temporário de mérito `0..10`).
- [`src/Elifoot.Domain/MatchEngine.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/MatchEngine.cs#L1-L350) — Influência de `Player.Strength` e `IsStar` no poder dos setores (`G`, `D`, `M`, `A`) durante a simulação da partida.
- [`src/Elifoot.Domain/AiManagerBrain.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/AiManagerBrain.cs#L1-L350) — Planejamento de elenco da IA (`GetPlanningScore`, `myAvgStrength`, lacunas táticas e substituições).
- [`src/Elifoot.Domain/MarketAndEconomyEngine.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/MarketAndEconomyEngine.cs#L1-L1057) — Impacto de `Strength` em valor justo calibrado, salário, renovação e mercado da IA.
- [`src/Elifoot.Domain/ClassicPlayerEconomyEngine.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/ClassicPlayerEconomyEngine.cs#L1-L300) — Fórmulas econômicas clássicas do Elifoot 98 (`F^1.04` no salário-base e no valor de mercado).

### Contratos (`src/Elifoot.Contracts`)
- [`src/Elifoot.Contracts/Enums.cs`](file:///d:/elifoot_98/src/Elifoot.Contracts/Enums.cs#L1-L120) — `DivisionTier`, `PlayerPosition`, `PlayerBehavior`, `ForceEvolutionMode`, `LineupSelectionMode`.
- [`src/Elifoot.Contracts/WorldCatalogDtos.cs`](file:///d:/elifoot_98/src/Elifoot.Contracts/WorldCatalogDtos.cs#L1-L120) — `ClubInitialProfile016B`, `InitialSquadReference017B`.
- [`src/Elifoot.Contracts/OfflineCatalogV11Dtos.cs`](file:///d:/elifoot_98/src/Elifoot.Contracts/OfflineCatalogV11Dtos.cs#L1-L80) — `OfflineClubV11` (`TeamStrength` `1..20`), `OfflinePlayerV11` (sem campo `Strength`).
- [src/Elifoot.Contracts/PlayerStrengthDtos.cs](../../src/Elifoot.Contracts/PlayerStrengthDtos.cs) — `PlayerStrengthChangeDto`.

### Documentação Analisada (`docs/`)
- [`docs/CLUB_INITIAL_POWER_016B.md`](file:///d:/elifoot_98/docs/CLUB_INITIAL_POWER_016B.md)
- [`docs/INITIAL_STRENGTH_RULES_017B.md`](file:///d:/elifoot_98/docs/INITIAL_STRENGTH_RULES_017B.md)
- [`docs/evidence-017b/INITIAL_SQUAD_STRENGTH_017B.md`](file:///d:/elifoot_98/docs/evidence-017b/INITIAL_SQUAD_STRENGTH_017B.md)
- [`docs/CLASSIFICATION_FORCE_EVOLUTION.md`](file:///d:/elifoot_98/docs/CLASSIFICATION_FORCE_EVOLUTION.md)
- [`docs/CLASSIC_FORCE_EVOLUTION_012C.md`](file:///d:/elifoot_98/docs/CLASSIC_FORCE_EVOLUTION_012C.md) *(documento histórico da etapa 012C, posteriormente substituído em `8e9ee25` e `9af02bc` por `CLASSIFICATION_FORCE_EVOLUTION.md`)*
- [`docs/catalogos/ELIFOOT98_CATALOGO_V11_RESULTADO.md`](file:///d:/elifoot_98/docs/catalogos/ELIFOOT98_CATALOGO_V11_RESULTADO.md)
- [`docs/catalogos/ELIFOOT98_ESTRUTURA_CATALOGOS.md`](file:///d:/elifoot_98/docs/catalogos/ELIFOOT98_ESTRUTURA_CATALOGOS.md)

---

## 5. Classes e Métodos Encontrados

| Conceito Procurado | Classe / Arquivo Real no Projeto | Métodos Principais e Linhas | Status |
| :--- | :--- | :--- | :--- |
| `ChampionshipStrengthLimits` | [`ChampionshipStrengthLimits`](file:///d:/elifoot_98/src/Elifoot.Domain/ChampionshipStrengthLimits.cs#L5-L17) | `For(DivisionTier tier, int position)` (L15-16) | **ATIVO** |
| `ClubInitialPower` (`1..20`) | [WorldCatalogRules016B](../../src/Elifoot.Domain/Catalog/WorldCatalogRules016B.cs) | `PowerAt` 7, `SquadSizeAt` 8, `Classify` 9, `Order` 22, `SelectRoster` 26, `Prepare` 49 | **ATIVO** |
| `teamStrength` (`1..20` V1.1) | [OfflineCatalogRanking](../../src/Elifoot.Domain/Catalog/OfflineCatalogRanking.cs) | `IsV11`, `Order`, `Warnings`, `BoundaryErrors` | **ATIVO; bloqueio existia apenas antes de bf43cd4** |
| `InitialStrength` / `PlayerGeneration` (Regra `017B`) | [`InitialSquadStrength017B`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/InitialSquadStrength017B.cs#L8-L66) | `Reference(int division, int position)` (L14-21), `Hash(int seed, string clubKey, string playerKey)` (L23-25), `Draw(...)` (L27-46), `Apply(...)` (L48-65) | **ATIVO** |
| `InitialPlayerStrength` (Importação Provedor) | [InitialPlayerStrength](../../src/Elifoot.Domain/Catalog/InitialPlayerStrength.cs) | `Unit`, `Percentile`, `Apply` (linha 18) | **ATIVO (pré-017B / Distrital)** |
| `PlayerGeneration` (Fábrica Rápida / Seed) | [WorldSeedFactory](../../src/Elifoot.Domain/WorldSeedFactory.cs) | `CreateSeededWorld`; usa `InitialSquadStrength017B.Reference`/`Draw` quando solicitado | **ATIVO** |
| `Evolution` / `Regression` (Por Rodada e Lesão Diária) | [ClassicForceEvolutionEngine](../../src/Elifoot.Domain/ClassicForceEvolutionEngine.cs) | Linhas de `f1764fb`: `GrowthCeiling` 20, `DurationFromRoll` 23, `MaximumLoss` 35, `ScheduledCumulativeLoss` 36, `InitializeSeason` 39, `PrepareRound` 60, `RegisterInjury` 135, `AdvanceToDate` 98, `ParticipationMinutes` 161, `ApplyFinishedRound` 186 | **ATIVO** |
| `PlayerProgression` / `SeasonTransition` (Legado) | [PlayerStrengthEvolutionEngine](../../src/Elifoot.Domain/PlayerStrengthEvolutionEngine.cs) | `ApplySeasonEnd(GameWorld world)` (linha 14) | **CARACTERIZAÇÃO LEGADA; SEM CHAMADOR DE PRODUÇÃO** |
| `Training` | *Inexistente no código* | Não existe sistema de treino manual ou automático de força no projeto. | **NÃO EXISTE** |

---

## 6. Regra de Força Inicial dos Clubes

### 6.1. Distinção Obrigatória dos Três Conceitos

| Conceito | Escala | Onde é Definido | Papel Real no Sistema Atual |
| :--- | :--- | :--- | :--- |
| **FORÇA DO CLUBE** (`ClubInitialPower` / `teamStrength`) | **`1..20`** | [`WorldCatalogRules016B.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/WorldCatalogRules016B.cs#L7) / [`OfflineClubV11`](file:///d:/elifoot_98/src/Elifoot.Contracts/OfflineCatalogV11Dtos.cs#L28) | Define a **ordem de classificação inicial dos clubes** para alocação nas divisões (`1ª a 4ª Divisão` e `Distrital`). **Não gera diretamente a força `1..50` dos jogadores.** |
| **RANKING DO CLUBE** (`WorldRank` / `Score` / `HistoricalRanking`) | Contínua (`Score`/`Rating`) ou Posição (`1..N`) | [`WorldCatalogRules016B.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/WorldCatalogRules016B.cs#L10-L34) / [`OfflineCatalogRanking.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/OfflineCatalogRanking.cs#L19-L37) | Calcula `ClubInitialPower` quando não há override manual (`016B`) e **desempata clubes** com mesma força `1..20`. |
| **FORÇA INDIVIDUAL** (`Player.Strength`) | **`1..50`** | [`InitialSquadStrength017B.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/InitialSquadStrength017B.cs#L14-L65) / [`WorldSeedFactory.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/WorldSeedFactory.cs#L168-L283) | Atributo individual de cada jogador usado na partida (`MatchSimulationEngine`), escalação (`LineupSelectionEngine`) e economia (`ClassicPlayerEconomyEngine` / `MarketAndEconomyEngine`). |

### 6.2. Regra 016B conferida no código

Fonte: [WorldCatalogRules016B.cs](../../src/Elifoot.Domain/Catalog/WorldCatalogRules016B.cs).

1. `Classify` ordena por `SourceRanking.Score` ou `RankingInfo.Score` decrescente, depois por `Rank` crescente e `Key` ordinal. Não existe o método `Score` transcrito na primeira versão deste relatório.
2. `PowerAt(r) = max(1, 20 - floor(max(0,r)/2))`: pares de posições recebem 20, 19, ... até 1. Se `InitialProfile.ManualPower` estiver ativo, `Classify` preserva o valor manual existente; não aplica o `Math.Clamp` anteriormente atribuído a esse trecho.
3. `Order` usa `InitialProfile.ClubInitialPower DESC`, `WorldRankingScore DESC`, `WorldRank ASC`, `Key ASC`. Não são os nomes do clube nem `LeagueOrder` que compõem esse comparador.
4. `SquadSizeAt(r) = 23 - floor((max(0,r) mod 14)/2)`, ciclo `[23,23,22,22,21,21,20,20,19,19,18,18,17,17]`. `Prepare` usa alvo 23 para elite com `WorldRank > 0`, `RankingConfidence > 0` e `WorldRank <= eliteCutoff`; fora disso usa o ciclo. Não há mínimo 22 determinado apenas por força coletiva 20/19.

Esses procedimentos de preparo não completam o catálogo offline V1.1. Seu elenco é o fornecido no JSON, com 11..23 atletas para READY. Força coletiva, ranking e força individual são atributos distintos; a evolução posterior usa a classificação esportiva atual.

### 6.3. Regra no Catálogo Offline `V1.1` (`OfflineCatalogRanking`)
Em [`src/Elifoot.Domain/Catalog/OfflineCatalogRanking.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/OfflineCatalogRanking.cs#L19-L51):
- Cada clube anual `V1.1` declara explicitamente `teamStrength` inteiro de **`1` a `20`** ([`OfflineCatalogRules.cs:26`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/OfflineCatalogRules.cs#L26)).
- `OfflineCatalogRanking.Order` (L19-25) ordena os clubes candidatos por:
  1. `teamStrength` decrescente (`1..20`);
  2. `ComparableRating` decrescente (`historicalRanking.rating`, apenas quando todos os clubes empatados na mesma força compartilham a mesma `historicalRanking.source` e `referenceDate`);
  3. `tieBreakPriority` crescente;
  4. `CountryCode`, `Name`, `Key`.
- Os 128 clubes selecionados são distribuídos em:
  - Posições `1..8`: `1ª Divisão`
  - Posições `9..16`: `2ª Divisão`
  - Posições `17..24`: `3ª Divisão`
  - Posições `25..32`: `4ª Divisão`
  - Posições `33..128`: `Distrital`
- **Observação:** Um clube com `teamStrength = 20` que fique na 9ª colocação geral entra na `2ª Divisão` mantendo `teamStrength = 20` (comprovado pelo teste `NineForce20RankByComparableRatingAndNinthKeeps20InSecondDivision` em [`OfflineCatalogV11Tests.cs:161-172`](file:///d:/elifoot_98/tests/Elifoot.Integration.Tests/OfflineCatalogV11Tests.cs#L161-L172)).
- **Diferença entre IA e Humano:** Nenhuma. A força inicial do clube e a geração do elenco inicial independem de o clube vir a ser controlado por humano ou IA.

---

## 7. Regra de Geração da Força dos Jogadores

Não existe um único gerador no projeto; existem **4 mecanismos implementados** conforme o fluxo de criação de mundo. **Nenhum deles converte diretamente `ClubInitialPower (1..20)` em `Player.Strength (1..50)` por fórmula aritmética direta**: no fluxo principal (`017B`), `ClubInitialPower (1..20)` define a posição do clube nas divisões, e a **posição na divisão (`Division 1..4`, `Slot 1..8`)** define a força inicial `1..50` dos jogadores.

### 7.1. Mecanismo Ativo Principal — Regra `017B` (`InitialSquadStrength017B`)
- **Arquivos e Métodos:**
  - [`src/Elifoot.Domain/Catalog/InitialSquadStrength017B.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/InitialSquadStrength017B.cs#L8-L66) (`Reference`, `Hash`, `Draw`, `Apply`)
  - [`src/Elifoot.Domain/ChampionshipStrengthLimits.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/ChampionshipStrengthLimits.cs#L7-L17) (`For`)
  - Chamado em [`WorldCreatorEngine.Preview`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/WorldCreatorEngine.cs#L150-L152), [`WorldCreatorEngine.Create`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/WorldCreatorEngine.cs#L169-L170) e [`WorldSeedFactory.CreateSeededWorld`](file:///d:/elifoot_98/src/Elifoot.Domain/WorldSeedFactory.cs#L310-L335).
- **Tabela de Referência (`ChampionshipStrengthLimits.Limits`) e Faixas por Divisão e Posição (`InitialSquadStrength017B.Reference`, L14-21):**

| Posição Inicial na Divisão | 1ª Divisão (Faixa `39..50`) | 2ª Divisão (Faixa `26..38`) | 3ª Divisão (Faixa `13..25`) | 4ª Divisão (Faixa `1..12`) |
| :---: | :---: | :---: | :---: | :---: |
| **1º** | Ref `50` $\rightarrow$ **`[47..50]`** *(garante 47, 48, 49, 50)* | Ref `38` $\rightarrow$ **`[37..38]`** | Ref `25` $\rightarrow$ **`[24..25]`** | Ref `12` $\rightarrow$ **`[11..12]`** |
| **2º** | Ref `50` $\rightarrow$ **`[47..50]`** *(garante 47, 48, 49, 50)* | Ref `37` $\rightarrow$ **`[36..38]`** | Ref `24` $\rightarrow$ **`[23..25]`** | Ref `11` $\rightarrow$ **`[10..12]`** |
| **3º** | Ref `50` $\rightarrow$ **`[47..50]`** *(garante 47, 48, 49, 50)* | Ref `36` $\rightarrow$ **`[35..37]`** | Ref `23` $\rightarrow$ **`[22..24]`** | Ref `10` $\rightarrow$ **`[9..11]`** |
| **4º** | Ref `46` $\rightarrow$ **`[45..47]`** | Ref `34` $\rightarrow$ **`[33..35]`** | Ref `21` $\rightarrow$ **`[20..22]`** | Ref `8` $\rightarrow$ **`[7..9]`** |
| **5º** | Ref `44` $\rightarrow$ **`[43..45]`** | Ref `32` $\rightarrow$ **`[31..33]`** | Ref `19` $\rightarrow$ **`[18..20]`** | Ref `6` $\rightarrow$ **`[5..7]`** |
| **6º** | Ref `42` $\rightarrow$ **`[41..43]`** | Ref `30` $\rightarrow$ **`[29..31]`** | Ref `17` $\rightarrow$ **`[16..18]`** | Ref `4` $\rightarrow$ **`[3..5]`** |
| **7º** | Ref `40` $\rightarrow$ **`[39..41]`** | Ref `28` $\rightarrow$ **`[27..29]`** | Ref `15` $\rightarrow$ **`[14..16]`** | Ref `2` $\rightarrow$ **`[1..3]`** |
| **8º** | Ref `39` $\rightarrow$ **`[39..40]`** | Ref `27` $\rightarrow$ **`[26..28]`** | Ref `14` $\rightarrow$ **`[13..15]`** | Ref `1` $\rightarrow$ **`[1..2]`** |

- **Fórmula Exata do Sorteio Determinístico (`InitialSquadStrength017B.Draw`, L23-46):**
  1. Para cada atleta com chave estável `playerKey` no clube `clubKey` com `seed` do mundo:
     $$\text{hash} = \text{ReadUInt64LittleEndian}\left(\text{SHA256}\left(\text{UTF8}(\texttt{\{"Rule":"017B","seed":seed,"clubKey":clubKey,"playerKey":playerKey\}})\right)\right)$$
  2. Sendo $\text{width} = \text{Maximum} - \text{Minimum} + 1$:
     $$\text{Strength}_i = \text{Minimum} + (\text{hash}_i \bmod \text{width})$$
  3. **Regra Especial do Top 3 da 1ª Divisão (`[47..50]` com $\ge 4$ atletas, L35-44):**
     - Calcula $\text{offset} = \text{Hash}(\text{seed}, \text{clubKey}, \texttt{""}) \bmod 4$.
     - Seleciona os 4 atletas do elenco com os menores valores de $\text{hash}_i$ (`OrderBy(i => hashes[i]).ThenBy(i => i).Take(4)`) e atribui a eles `47 + offset`, incrementando `offset = (offset + 1) % 4`. Isso garante que todo clube entre os 3 primeiros da 1ª Divisão possua pelo menos um jogador de força `47`, um `48`, um `49` e um `50`.
- **Influência de outros atributos em `InitialSquadStrength017B`:**
  - **Posição (`G`, `D`, `M`, `A`):** **Nenhuma** na geração da força `017B` (todos os atletas do clube são sorteados na mesma banda `[Minimum, Maximum]`).
  - **Titulares vs Reservas:** `InitialSquadStrength017B` sorteia as forças de todo o elenco na mesma banda; em seguida, em `WorldSeedFactory.CreateSeededWorld` (L330), `AiManagerBrain.BuildStartersAndBench` seleciona os 11 titulares escolhendo os de maior `Score`/`Strength` em cada posição. Portanto, titulares tendem a ter `ref + 1` e reservas `ref - 1` ou `ref` por efeito da escalação pós-sorteio, e não por sorteio separado.
  - **Idade, Asterisco (`IsStar`), Comportamento (`Behavior`), Nacionalidade:** **Nenhuma influência** sobre a força sorteada na `017B`.
  - **Clubes do Distrital (`Division == 5`):** `InitialSquadStrength017B.Apply` ignora `entry.Division == 5` (L54: `if (entry.Division == 5) return entry;`), mantendo nos clubes distritais a força oriunda da etapa anterior (`PlayableWorldPolicy` / `InitialPlayerStrength` ou `WorldSeedFactory`).

### 7.2. Mecanismo da Fábrica Clássica Rápida (`WorldSeedFactory.CreateSeededWorld` quando `applyInitialSquadStrength == false`)
- **Arquivo e Linhas:** [`src/Elifoot.Domain/WorldSeedFactory.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/WorldSeedFactory.cs#L168-L283).
- Quando `applyInitialSquadStrength` é `false` (padrão do parâmetro opcional em `CreateSeededWorld`, L25, embora o endpoint de criação rápida e os testes `017B`/`ClassificationForce` passem `applyInitialSquadStrength: true`), a força de cada jogador é sorteada uniformemente via `rng.NextInt(minStr, maxStr + 1)` por divisão (L168-175 e L252):
  - **1ª Divisão:** `34..48`
  - **2ª Divisão:** `27..39`
  - **3ª Divisão:** `21..32`
  - **4ª Divisão:** `16..26`
  - **Distrital:** `10..21`

### 7.3. Estimativa de catálogo e preenchimento fictício

Fontes: [InitialPlayerStrength.cs](../../src/Elifoot.Domain/Catalog/InitialPlayerStrength.cs), [PlayableWorldPolicy.cs](../../src/Elifoot.Domain/Catalog/PlayableWorldPolicy.cs).

`InitialPlayerStrength.Apply` processa atletas cujo método de estimativa começa por `relative-v1`. A coorte usa gênero, temporada, posição e escala de rating. Usa percentil de rating quando há pelo menos três amostras; senão, percentil de minutos com pelo menos três amostras; senão, mantém estimativa individual neutra. O percentil do clube vem de `Rank`, não de uma conversão direta de teamStrength.

Com `noise = Unit(chave,seed)*2-1` e `c = clubPercentile ?? 0.5`, o código usa:

```text
raw = confidence > 0.2
    ? 12 + 37*individual + 2*(c-0.5) + noise
    : 26 + 16*(c-0.5) + 7*noise
strength = clamp(round(raw, AwayFromZero), 1, 50)
```

A chave canônica/seed alimenta SHA-256; o método grava a proveniência da estimativa. A combinação de pesos 0.58/0.37/0.05 e potência 1.08 transcrita na versão original deste relatório não corresponde a esse código e foi corrigida documentalmente, sem alterar a implementação.

`PlayableWorldPolicy.Prepare` é um caminho separado para preenchimento fictício consentido, usando nível do clube e variação determinística. Não é chamado pelo criador offline para inventar atletas faltantes. No fluxo 017B, as forças dos clubes das divisões 1..4 são substituídas por suas bandas; a estimativa anterior permanece nos distritais. A integração offline de `bf43cd4` conectou `InitialPlayerStrength.Apply` e 017B, preservando todos os atletas cadastrados e congelando suas forças na prévia.

### 7.4. Mecanismo do Catálogo Offline (`OfflineWorldFactory`)
- **Arquivo e Linhas:** [`src/Elifoot.Domain/Catalog/OfflineWorldFactory.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/OfflineWorldFactory.cs#L18-L20), [`L62`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/OfflineWorldFactory.cs#L62), [`L75`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/OfflineWorldFactory.cs#L75), [`L130-L137`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/OfflineWorldFactory.cs#L130-L137).
- **Catálogo V1.0 (`schemaVersion == "1.0"`):** lê diretamente `p.Strength!.Value` (`1..50`) gravado no arquivo JSON de cada clube e copia sem alteração para `CatalogAthlete` (L19) e `Player.Strength` (L133).
- **Catálogo V1.1 na auditoria histórica de `aa5130b` (`schemaVersion == "1.1"`):** os jogadores não possuem `strength` no JSON (`OfflinePlayerV11`), e `OfflineWorldFactory.Eligible` cria `Players = []` (L18). Tanto `Preview` (L62) quanto `Create` (L75) bloqueiam a operação com `OfflineCatalogRanking.ForceBlock`.

---

## 8. Regra de Aumento de Força

### 8.1. Motor Ativo: `ClassicForceEvolutionEngine.ApplyFinishedRound`
- **Arquivo:** [`src/Elifoot.Domain/ClassicForceEvolutionEngine.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/ClassicForceEvolutionEngine.cs#L186-L251).
- **Ponto de Chamada:** [`src/Elifoot.Domain/CompetitionAndSeasonEngine.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/CompetitionAndSeasonEngine.cs#L405-L406) ao concluir todas as partidas oficiais da rodada atual (`ApplyFinishedRoundResultsToStandingsAndCareers`).
- **Guarda de Idempotência por Rodada (`L192-194`):** retorna se `GuardSeason > SeasonNumber` ou se a temporada é igual e `GuardRound >= RoundNumber`. Após processar a Liga, grava as duas marcas. O evento usa chave `$"{w.Id}:S{w.SeasonNumber}:R{w.RoundNumber}:P{p.Id}:{reason}"` (`L245`). Não é a existência de um evento que permite reprocessar a rodada: as marcas persistidas protegem também decisões sem mudança de força.
- **Condições Estritas para Ganho de Força (`RESERVE_PROGRESS`, L196-240):**
  1. **Partidas da Liga (`L196-197`):** filtra `matches.Where(m => m.CompetitionType == CompetitionType.League && m.CurrentMinute > 0)` e retorna se a lista ficar vazia. Rodadas exclusivamente da Taça não recebem ganho/perda por participação. Lesões seguem pelo calendário.
  2. **Não Ter Entrado em Campo na Rodada (`L226`, `L239`):** `!appeared.Contains(p.Id)`.
     - `appeared` é montado diretamente em `ApplyFinishedRound` (`L204-218`): titulares de `StatisticsInitialLineup` e entradas por `Substitution.PrimaryPlayerId`, inclusive aos 90 minutos sem minuto completo decorrido. Não depende de `ParticipationMinutes > 0`; há fallback das listas de participantes para fixtures antigos sem captura inicial.
     - Quem **não** está em `appeared` (e portanto é elegível ao ganho `+1`)?
       - Reservas no banco que não entraram na partida;
       - Jogadores do elenco fora do banco (não relacionados);
       - Jogadores cumprindo suspensão (`SuspensionMatchesRemaining > 0`);
       - Jogadores já lesionados que não entraram em campo na rodada.
  3. **Estar Abaixo do Teto da Posição Atual do Clube na Divisão (`L239`):**
     $$\text{ Se } \neg\text{appeared}(p) \;\land\; \text{Strength}_{\text{before}} < \text{Ceiling}(\text{DivisionTier}, \text{LeagueRank}) \implies \text{Strength}_{\text{after}} = \text{Strength}_{\text{before}} + 1$$
  4. **Probabilidade:** **100% determinístico** (não há sorteio de RNG para o ganho na versão ativa desde o commit `8e9ee25`).
  5. **Teto (`Ceiling`) por Divisão e Posição na Tabela Atualizada da Liga (`ChampionshipStrengthLimits.For`, [`ChampionshipStrengthLimits.cs:7-17`](file:///d:/elifoot_98/src/Elifoot.Domain/ChampionshipStrengthLimits.cs#L7-L17)):**
     - **1ª Divisão (1º ao 8º):** `50, 50, 50, 46, 44, 42, 40, 39`
     - **2ª Divisão (1º ao 8º):** `38, 37, 36, 34, 32, 30, 28, 27`
     - **3ª Divisão (1º ao 8º):** `25, 24, 23, 21, 19, 17, 15, 14`
     - **4ª Divisão (1º ao 8º):** `12, 11, 10, 8, 6, 4, 2, 1`
     - **Distrital ou Sem Clube (`null`):** `1` (nunca ganham força).
  6. **Ordem de Cálculo da Posição na Tabela (`CompetitionAndSeasonEngine.cs:360-406` e `ClassicForceEvolutionEngine.cs:200-206`):**
     - Primeiro os placares da rodada da Liga atualizam pontos, vitórias e saldo de gols de todos os clubes (`L361-391`);
     - Em seguida, o laço de `ApplyFinishedRound` (`L199-202`) lê `CompetitionAndSeasonEngine.GetDivisionStandings(w, tier)` com a classificação **já atualizada pela rodada encerrada** e preenche `CompletedLeaguePositions`. Não existe método separado `UpdateCompletedLeaguePositions` nesse fluxo. Sem posição concluída, `ClubContext` usa referência provisória de 4º lugar.
  7. **Jogadores Acima do Teto:** Se um jogador já possui `Strength >= Ceiling` (por exemplo, um atleta de força `48` cujo clube caiu para 8º lugar da 1ª Divisão com teto `39`, ou foi transferido para a 4ª Divisão com teto `12`), ele **não perde força pelo teto** (o teto jamais reduz ou "clampa" a força existente; apenas impede novos ganhos `RESERVE_PROGRESS`, conforme comprovado em `CeilingNeverClampsAStrongNonparticipant`, [`ClassicForce012CTests.cs:135-141`](file:///d:/elifoot_98/tests/Elifoot.Domain.Tests/ClassicForce012CTests.cs#L135-L141)).

---

## 9. Regra de Redução de Força

Existem **duas** formas ativas de redução de força individual em `ClassicForceEvolutionEngine`:

### 9.1. Redução por Participação em Partida da Liga (`PLAYED_MATCH_DECLINE`)
- **Arquivo e Linhas:** [`src/Elifoot.Domain/ClassicForceEvolutionEngine.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/ClassicForceEvolutionEngine.cs#L15), [`L226-L238`](file:///d:/elifoot_98/src/Elifoot.Domain/ClassicForceEvolutionEngine.cs#L226-L238).
- **Regra Exata:**
  - Aplica-se somente em rodadas da **Liga** (`CompetitionType.League`).
  - Aplica-se a todo atleta que **entrou em campo** na rodada (`appeared.Contains(p.Id)` — titular ou substituto utilizado).
  - Exceção de precedência de lesão (`L232`): se o jogador sofreu uma nova lesão na própria partida (`injured.Contains(p.Id)` quando `classicInjuries` está ativo), ele não sofre `PLAYED_MATCH_DECLINE` porque já estará sujeito à perda diária por lesão (`INJURY_DAILY_LOSS`).
  - Se `before > 1`, executa o sorteio determinístico:
    ```csharp
    bool decline = DeterministicRng.FromContext(
        w.Seed, w.SeasonNumber, w.RoundNumber, PlayerToken(p.Id), 0, 12042)
        .NextBool(PlayedDeclineProbability); // PlayedDeclineProbability = 0.50
    ```
    onde `PlayerToken(Guid id)` (linha 296) lê os primeiros quatro bytes em little-endian e aplica `& 0x3fffffff`, produzindo inteiro entre 0 e 1073741823.
  - **Resultado:**
    - Com **50% de probabilidade**: `p.Strength = Math.Max(1, before - 1)` (`ChangeReason = "PLAYED_MATCH_DECLINE"`).
    - Com **50% de probabilidade**: `p.Strength` permanece inalterado (`before`).
  - **Piso Mínimo:** `1` (um jogador com força `1` nunca cai para `0`).
  - **Independência:** A probabilidade de `50%` **não depende** de idade, posição, gols, assistências, cartões, resultado da partida, força atual ou teto do clube.

### 9.2. Redução Gradual Diária por Lesão (`INJURY_DAILY_LOSS`)
- **Arquivo e Linhas:** [`src/Elifoot.Domain/ClassicForceEvolutionEngine.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/ClassicForceEvolutionEngine.cs#L16-L18), [`L32-L53`](file:///d:/elifoot_98/src/Elifoot.Domain/ClassicForceEvolutionEngine.cs#L32-L53), [`L98-L152`](file:///d:/elifoot_98/src/Elifoot.Domain/ClassicForceEvolutionEngine.cs#L98-L152).
- **Quando ocorre uma lesão (`MatchSimulationEngine.ProcessInjuryTick`, `MatchEngine.cs:439-454` → `ClassicForceEvolutionEngine.RegisterInjury`, linha 135):**
  - O limiar é `0.0018`, ou **0,18% por equipe/minuto**, quando há atletas ativos (`MatchEngine.cs:442`). Não é taxa por jogador. Uma tentativa que selecione atleta já marcado como lesionado na mesma partida não gera novo episódio.
  - Quando `ClassicForceEvolutionEngine.IsActive(world)` (`ForceEvolutionMode == ClassicAdapted`):
    1. **Sorteio da Duração em Dias (`DurationFromRoll`, L32-43):**
        - Sorteia `roll = rng.NextInt(0, 200)` com `DeterministicRng.FromContext(worldSeed, match.SeasonNumber, match.RoundNumber, match.DeterministicMatchIndex, match.CurrentMinute, 12031 + PlayerToken(p.Id))` (`RegisterInjury`, linhas 139-142).
       - Pesos para `2..15` dias (`DurationWeights`, soma = `200`):
         - `2 dias`: 26/200 (`13,0%`)
         - `3 dias`: 26/200 (`13,0%`)
         - `4 dias`: 24/200 (`12,0%`)
         - `5 dias`: 24/200 (`12,0%`)
         - `6 dias`: 22/200 (`11,0%`)
         - `7 dias`: 20/200 (`10,0%`)
         - `8 dias`: 18/200 (`9,0%`) — *(acumulado de 2 a 8 dias = 160/200 = `80,0%`)*
         - `9 dias`: 12/200 (`6,0%`)
         - `10 dias`: 8/200 (`4,0%`)
         - `11 dias`: 6/200 (`3,0%`)
         - `12 dias`: 5/200 (`2,5%`)
         - `13 dias`: 4/200 (`2,0%`)
         - `14 dias`: 3/200 (`1,5%`)
         - `15 dias`: 2/200 (`1,0%`)
    2. **Sorteio da Perda Total Planejada (`MaximumLoss`, L45-49 e `RegisterInjury`, L109):**
       $$\text{MaximumLoss}(\text{force}, \text{duration}) = \min\left(\text{force} - 1,\; \left\lfloor \frac{\text{force} \times \text{duration}}{30} \right\rfloor\right)$$
       $$\text{TotalLoss} = \text{rng.NextInt}(0,\; \text{MaximumLoss}(\text{force}, \text{duration}) + 1)$$
       - Para `duration = 15` dias, `MaximumLoss` chega a $\lfloor \text{force} \times 15 / 30 \rfloor = \lfloor \text{force} / 2 \rfloor$ (até **50% da força inicial**; ex.: força `50` $\rightarrow$ perda máxima de `25` pontos).
       - Como o sorteio começa em `0`, uma lesão também pode ter `TotalLoss = 0` ou perda parcial.
    3. **Desconto Diário pelo Calendário Virtual (`AdvanceToDate`, L125-152):**
       - O estado padrão inicia em **01/08/1998** (`ClassicForceState.SeasonStartDate`/`GameDate`). `RoundDate` (`L54-57`) usa `gaps=max(0,round-1)` e deslocamento `floor(gaps/2)*7 + (gaps ímpar ? 4 : 0)`: intervalos normais **4 e 3 dias alternados**, com possíveis adiamentos em `PrepareRound`. Não existe constante `DaysPerRound = 3` no código conferido. A data de saves existentes é preservada.
       - Para cada dia $d \in \{1, \dots, \text{duration}\}$ alcançado pelo calendário virtual:
         $$\text{ScheduledCumulativeLoss}(\text{TotalLoss}, \text{duration}, d) = \left\lfloor \frac{\text{TotalLoss} \times d}{\text{duration}} \right\rfloor$$
         $$\text{DailyLoss}_d = \text{ScheduledCumulativeLoss}(d) - \text{ScheduledCumulativeLoss}(d - 1)$$
         $$\text{Strength}_{\text{after}} = \max(1,\; \text{Strength}_{\text{before}} - \text{DailyLoss}_d)$$
       - Cada redução real grava um `PlayerStrengthChangeDto` com `ChangeReason = "INJURY_DAILY_LOSS"`, data e episódio, recalcula o valor de mercado existente e preserva o salário contratado. A parcela pode ser zero ou maior que um; não é desconto fixo por rodada. Novas lesões e perdas diárias também ocorrem em datas da Taça.
    4. **Recuperação da Lesão (`L135`, `L150`):**
       - Ao atingir `EndDate`, com `ProcessedDays == Duration`, `AdvanceToDate` zera `InjuryDaysRemaining`. Esse método não zera `InjuryRoundsRemaining`, que pertence à política legada, nem remove suspensões. A disponibilidade depende de todos esses impedimentos. **A força perdida não é restaurada automaticamente**; o atleta pode receber ganhos de reserva elegíveis na Liga.

---

## 10. Evolução na Virada de Temporada

Em [`CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance`](file:///d:/elifoot_98/src/Elifoot.Domain/CompetitionAndSeasonEngine.cs#L474-L655):
1. **Promoção e Rebaixamento (`L536-586`):**
   - Os 2 últimos de cada divisão caem (`1ª -> 2ª -> 3ª -> 4ª -> Distrital`) e os 2 primeiros sobem (`Distrital -> 4ª -> 3ª -> 2ª -> 1ª`).
   - **Impacto na Força:** A mudança de divisão **não altera** a força imediata dos jogadores do clube promovido ou rebaixado, mas altera os futuros tetos de crescimento (`ChampionshipStrengthLimits`) a partir da temporada seguinte.
2. **Limpeza das Posições Concluídas da Liga (`L591`):**
   - `world.ClassicForce.CompletedLeaguePositions.Clear();` — limpa o cache de posições da temporada encerrada para que a posição da divisão antiga não sirva de referência na nova temporada.
3. **Avanço de 21 Dias de Pré-Temporada no Calendário Virtual (`L637-638`):**
   - `if (ClassicForceEvolutionEngine.IsActive(world)) ClassicForceEvolutionEngine.AdvanceToDate(world, world.ClassicForce.GameDate.AddDays(21));`
   - Como os episódios clássicos duram no máximo `15` dias, os `21` dias liquidam suas parcelas restantes e zeram `InjuryDaysRemaining`. Isso não equivale a restaurar força ou eliminar impedimentos de outras políticas.
4. **Inicialização da Nova Temporada (`InitializeSeasonFixtures`, L139-140):**
   - Chama `ClassicForceEvolutionEngine.InitializeSeason(world)` e `ClassicForceEvolutionEngine.PrepareRound(world)`.
   - `InitializeSeason` limpa `CompletedLeaguePositions`; até a primeira classificação concluída, `ClubContext` usa a referência provisória de 4º lugar (`L253-256`), não uma posição deduzida do `DeterministicIndex`.
5. **Não há Envelhecimento ou Regressão Sazonal no Modo Ativo:**
   - `PlayerStrengthEvolutionEngine.ApplySeasonEnd` **não é chamado** em `FinalizeSeasonAndAdvance`; se chamado diretamente para um mundo ClassicAdapted, retorna na guarda da linha 16. `ClassicForce.SeasonStartingStrengths` serve aos snapshots; não impõe piso de força inicial menos três.

---

## 11. Relação com Idade

- **Na Geração Inicial (`WorldSeedFactory.cs:256`, `PlayableWorldPolicy.cs:112`):**
  - `Player.Age` é inicializado (ex.: `rng.NextInt(18, 34)` em `WorldSeedFactory` ou `18 + (index + slot) % 15` em `PlayableWorldPolicy`), mas **não entra no cálculo da força inicial** na regra `017B` nem na `WorldSeedFactory`.
  - No Catálogo Offline `V1.1` (`OfflinePlayerV11`), o campo `age` sequer existe no schema JSON.
- **Na Evolução e Regressão Ativa (`ClassicForceEvolutionEngine`):**
  - **Nenhuma relação.** A idade (`Player.Age`) não afeta o ganho `+1` no banco (`RESERVE_PROGRESS`), não afeta a chance de `50%` de queda `-1` ao jogar (`PLAYED_MATCH_DECLINE`) e não afeta a perda por lesão (`INJURY_DAILY_LOSS`). Além disso, `Player.Age` não é incrementado na virada de temporada quando `ClassicForceEvolutionEngine` está ativo.
- **No Motor Legado Inativo (`PlayerStrengthEvolutionEngine.cs:31-52`):**
  - `ApplySeasonEnd` não incrementa idade nem contém essas faixas etárias. `GameWorld.ForceEvolutionMode` sem valor explícito assume zero (`LegacySeasonal`); o pedido de criação da API, por sua vez, tem padrão ClassicAdapted. Saves antigos sem campo permanecem legados. Não há chamada de produção da rotina sazonal; a regra de participação atual vale nos fechamentos futuros da Liga também para o legado.

---

## 12. Relação com Posição (`G`, `D`, `M`, `A`)

- **Na Geração da Força Inicial (`017B` e `WorldSeedFactory`):**
  - **Nenhuma diferença de faixa de força por posição.** Goleiros (`G`), defensores (`D`), meias (`M`) e atacantes (`A`) de um mesmo clube recebem forças sorteadas exatamente no mesmo intervalo `[Minimum, Maximum]`.
- **Na Evolução e Regressão (`ClassicForceEvolutionEngine`):**
  - **Nenhuma diferença nas fórmulas de evolução/regressão.** A mesma regra (`+1` se não jogou na Liga e `< Ceiling`; `50%` de `-1` se jogou na Liga) vale para `G`, `D`, `M` e `A`.
  - *Efeito indireto de rotatividade:* Como um time usa apenas `1` goleiro titular por partida e raramente substitui o goleiro (exceto por lesão/expulsão), o goleiro titular atua em quase todas as rodadas de Liga (sofrendo o sorteio de `50%` de `-1`) enquanto o goleiro reserva quase nunca entra (recebendo `+1` até o teto), fazendo com que os goleiros se alternem na titularidade quando a seleção automática/melhores reavalia quem tem maior força.
- **Na Simulação da Partida (`MatchSimulationEngine.ComputeSectorBreakdown`, [`MatchEngine.cs:44-161`](file:///d:/elifoot_98/src/Elifoot.Domain/MatchEngine.cs#L44-L161)):**
  - Jogador escalado na sua própria posição recebe `100%` (`posMult = 1.0`) da sua `Strength`.
  - Jogador improvisado fora de posição sofre penalidade multiplicativa temporária na partida: `0.35` se improvisado no gol (ou goleiro na linha) e `0.65` se jogador de linha improvisado em outro setor de linha (`MatchEngine.cs:156-159`).

---

## 13. Relação com Asterisco (`IsStar`)

- **Definição e Restrição de Posição:**
  - No Catálogo Offline `V1.1` ([`OfflineCatalogRules.cs:40`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/OfflineCatalogRules.cs#L40)) e na derivação clássica por nome ([`ClassicPlayerEconomyEngine.cs:316-322`](file:///d:/elifoot_98/src/Elifoot.Domain/ClassicPlayerEconomyEngine.cs#L316-L322)), `IsStar` (`*`) é restrito exclusivamente às posições **`M` (Meio-campo)** e **`A` (Ataque)** (`G` e `D` nunca recebem asterisco no clássico/V1.1).
- **Efeito na Força Nominal (`Player.Strength`):**
  - **Nenhum.** `IsStar` não altera a força inicial `017B` nem altera a velocidade/probabilidade de evolução ou regressão em `ClassicForceEvolutionEngine`.
- **Efeito Multiplicativo no Desempenho em Partida ([`MatchEngine.cs:16-18, 147-154`](file:///d:/elifoot_98/src/Elifoot.Domain/MatchEngine.cs#L16-L18)):**
  - `StarBonusAttacker = 1.18` (`+18%` na força efetiva de ataque se atuar no setor `A`);
  - `StarBonusMidfielder = 1.15` (`+15%` na força efetiva se atuar no setor `M`);
  - `StarBonusDefenderOrGk = 1.10` (`+10%` caso exista em `D`/`G` em mundos fictícios legados).
  - Bônus adicional de escolha de finalizador/cobrador de pênalti (`1.25x` em `PickWeightedScorer`, `MatchEngine.cs:596`).
- **Efeito Econômico e na IA:**
  - Na economia clássica (`ClassicPlayerEconomyEngine`), o asterisco **não altera** o salário-base nem o valor de mercado base (`SEG03:7FF4` e `SEG09:371A` usam `F^1.04` sem multiplicar por `IsStar`), mas multiplica por **`1.10` (`+10%`)** o fator de interesse de contratação da IA (`CalculateAiHiringInterestFactor`, `ClassicPlayerEconomyEngine.cs:544`).
  - No planejamento interno da IA (`AiManagerBrain.GetPlanningScore`, `AiManagerBrain.cs:279`), soma `+5.5` pontos de prioridade para `A`/`M` (ou `+3.5` para `D`/`G`), sem alterar a prioridade de escalação de `LineupSelectionEngine`.

---

## 14. Relação com Comportamento (`PlayerBehavior`)

- **Escala:** `0 = FairPlay`, `1 = Cordeirinho`, `2 = Cavalheiro`, `3 = Caneleiro`, `4 = Caceteiro`, `5 = Sarrafeiro` ([`Enums.cs:27-35`](file:///d:/elifoot_98/src/Elifoot.Contracts/Enums.cs#L27-L35)).
- **Efeito na Força Nominal (`Player.Strength`) e na Evolução/Regressão:**
  - **Nenhum efeito direto.** O comportamento não altera a força inicial sorteada nem a regra `+1` / `-1` da `ClassicForceEvolutionEngine`.
  - **Efeito indireto via Suspensões:** Jogadores mais agressivos (`Caceteiro`, `Sarrafeiro`) têm maior peso de faltas e cartões em [`MatchSimulationEngine.GetBehaviorFoulWeight`](file:///d:/elifoot_98/src/Elifoot.Domain/MatchEngine.cs#L168-L177) (`0.0` para `FairPlay` até `3.60` para `Sarrafeiro`). Quando suspensos por cartões, ficam fora da partida da Liga (`!appeared.Contains(p.Id)`) e, portanto, recebem `+1` de `RESERVE_PROGRESS` enquanto cumprem suspensão se estiverem abaixo do teto do clube (comprovado pelo teste `FourthDivisionFifthPlaceGrowsExactlyOnceWithUnusedAndSuspendedPlayers` em [`ClassicForce012CTests.cs:40-52`](file:///d:/elifoot_98/tests/Elifoot.Domain.Tests/ClassicForce012CTests.cs#L40-L52)).
- **Efeito no Valor de Mercado:**
  - Na fórmula clássica (`ClassicPlayerEconomyEngine.CalculateClassicalMarketValue`, [`ClassicPlayerEconomyEngine.cs:224-243`](file:///d:/elifoot_98/src/Elifoot.Domain/ClassicPlayerEconomyEngine.cs#L224-L243)), o código de comportamento $A \in \{0..5\}$ reduz o fator individual em `-0.04 * A` ($1 - 0.04A - 0.02B + 0.02D$).

---

## 15. Impactos Econômicos e Táticos da Força (`Player.Strength`)

1. **Salário-Base Calculado (`ClassicPlayerEconomyEngine.CalculateClassicalBaseSalary`, [`ClassicPlayerEconomyEngine.cs:168-192`](file:///d:/elifoot_98/src/Elifoot.Domain/ClassicPlayerEconomyEngine.cs#L168-L192)):**
   $$S = 50 \cdot C \cdot \text{Round}\left(\frac{35 \cdot I \cdot \sqrt{R} \cdot F^{1.04} \cdot 1000^{0.04}}{50}\right)$$
   limitado a $[50C,\; 50000C]$.
   - **Regra Crítica de Preservação do Salário Contratado:** Quando a força $F$ de um jogador muda após uma rodada ou lesão (`ClassicForceEvolutionEngine.cs:143, 241`), **`p.SalaryPerRound` NÃO muda automaticamente**. O salário contratado é preservado até que ocorra um evento contratual explícito (renovação, pedido salarial espontâneo aceito, transferência ou leilão). Apenas `p.ReferenceSalaryPerRound` reflete dinamicamente a nova força (`Entities.cs:167`).
2. **Valor de Mercado (`ClassicPlayerEconomyEngine.CalculateClassicalMarketValue`, [`ClassicPlayerEconomyEngine.cs:224-264`](file:///d:/elifoot_98/src/Elifoot.Domain/ClassicPlayerEconomyEngine.cs#L224-L264)):**
   $$V = \text{Round}\left(2000 \cdot C \cdot I \cdot F^{1.04} \cdot (1 - 0.04A - 0.02B + 0.02D)\right)$$
   - Ao contrário do salário contratado, **`p.MarketPrice` é recalculado imediatamente** sempre que `p.Strength` sobe ou desce (`ClassicForceEvolutionEngine.cs:143, 241`).
3. **Renovação Contratual e Pedidos Salariais ([`VoluntaryRenewalEngine.cs:10-20`](file:///d:/elifoot_98/src/Elifoot.Domain/VoluntaryRenewalEngine.cs#L10-L20), [`OperatingEconomyEngine.cs`](file:///d:/elifoot_98/src/Elifoot.Domain/OperatingEconomyEngine.cs)):**
   - Na renovação preventiva/voluntária, o salário exigido é `Math.Max(p.SalaryPerRound, RequestedSalary(w, c, p))`, que usa a força atual $F$ do jogador. Se o jogador ganhou força na reserva, exigirá salário maior na renovação; se perdeu força jogando ou por lesão, o piso `p.SalaryPerRound` impede redução salarial na renovação.
4. **Escalação `Melhores` vs `Automático` ([`LineupSelectionEngine.cs:14-29`](file:///d:/elifoot_98/src/Elifoot.Domain/LineupSelectionEngine.cs#L14-L29)):**
   - Modo `Best` (`Melhores`): ordena estritamente por `player.Strength` nominal (`L17`).
   - Modo `Automatic` (`Automático`): calcula uma pontuação temporária de escalação:
     $$\text{Score} = \text{player.Strength} + \min(10,\; \text{merit})$$
     onde $\text{merit}$ vale `goals + 0.5 * assists` para `A`, `0.5 * goals + assists` para `M`, `0.5 * goals + 0.5 * assists` para `D`, e `0` para `G`.
   - **Importante:** Esse bônus de até `+10` pontos no modo `Automático` **é exclusivamente prioridade de escalação** (`Selection priority only: never changes nominal strength, tactics or match physics`, `LineupSelectionEngine.cs:4`). Ele **jamais altera** `player.Strength`.

---

## 16. Integração com Catálogo V1.1

### O que o Catálogo Offline V1.1 já fornece hoje:
- Em [`OfflineClubV11`](file:///d:/elifoot_98/src/Elifoot.Contracts/OfflineCatalogV11Dtos.cs#L23-L35) e nos arquivos `data/catalogos/{pais}/{ano}/clubes/*.json` (ex.: `brasil/1998`, `espanha/1998`, `inglaterra/1998`, `italia/1998`):
  - Cada clube possui `teamStrength` inteiro de `1` a `20`;
  - Cada clube possui `historicalRanking` (`rating`, `source`, `referenceDate`) e `tieBreakPriority` opcional;
  - Cada jogador possui `id`, `name`, `position` (`G`, `D`, `M`, `A`), `nationality`, `behavior` (`0..5`) e `isStar` (`true`/`false`, restrito a `M`/`A`);
  - **Não existe campo `strength` nos jogadores V1.1** (`InvalidV11PlayersAreRejected` rejeita qualquer JSON V1.1 que contenha `strength` no jogador, [`OfflineCatalogV11Tests.cs:98-99`](file:///d:/elifoot_98/tests/Elifoot.Integration.Tests/OfflineCatalogV11Tests.cs#L98-L99)).

### Como `OfflineCatalogRanking` e `OfflineWorldFactory` tratam o V1.1 hoje:
1. `OfflineCatalogRanking.Order` ([`OfflineCatalogRanking.cs:19-25`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/OfflineCatalogRanking.cs#L19-L25)) já ordena perfeitamente os clubes V1.1 por `teamStrength` (`1..20`), `ComparableRating` e `tieBreakPriority`, distribuindo os 128 clubes nas 4 divisões (`8` por divisão) e no Distrital (`96` clubes).
2. Porém, em [`OfflineWorldFactory.Eligible`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/OfflineWorldFactory.cs#L18), quando `source.V11 != null`, a lista `EligibleCatalogClub.Players` é inicializada vazia (`[]`).
3. Em [`OfflineWorldFactory.Preview`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/OfflineWorldFactory.cs#L62) e [`OfflineWorldFactory.Create`](file:///d:/elifoot_98/src/Elifoot.Domain/Catalog/OfflineWorldFactory.cs#L75), há um bloqueio explícito:
   ```csharp
   if (selected.Any(c => c.OfflineSource?.V11 != null))
       errors.Add(OfflineCatalogRanking.ForceBlock);
   ```
   onde `OfflineCatalogRanking.ForceBlock` (`OfflineCatalogRanking.cs:7-8`) é:
   > `"Criação de mundo V1.1 bloqueada: a geração de força individual dos atletas a partir de teamStrength ainda não foi aprovada."`

### O que já está pronto vs. o que falta conectar para o V1.1:
- **Já existe e está pronto:**
  - Leitura, validação e cache de identidades e clubes V1.1 (`OfflineCatalogLoader`);
  - Classificação dos clubes V1.1 por `teamStrength` (`1..20`) e alocação nas divisões `1..5` (`OfflineCatalogRanking`);
  - Gerador de força inicial por slot de divisão `017B` (`InitialSquadStrength017B`) para os 32 clubes das divisões `1..4`.
- **Lacuna exata que motiva o bloqueio atual (`ForceBlock`):**
  1. `OfflineWorldFactory.Eligible` ainda não converte `source.V11.Players` (`OfflinePlayerV11`) em `CatalogAthlete` (atualmente retorna `[]` na linha 18);
  2. `InitialSquadStrength017B.Apply` gera forças apenas para os **32 clubes das divisões `1..4`** e ignora `Division == 5` (os **96 clubes do Distrital**), porque no fluxo `016B`/`017B` os clubes do Distrital já vinham com força prévia de `InitialPlayerStrength` ou `PlayableWorldPolicy`. No Catálogo `V1.1`, como os atletas não têm força no JSON, os 96 clubes do Distrital (e/ou uma regra direta baseada em `teamStrength 1..20`) precisariam de definição de força inicial (por exemplo, a partir de `teamStrength` ou de uma faixa distrital) antes de remover `OfflineCatalogRanking.ForceBlock`.

---

## 17. Exemplos Numéricos Reais e Reprodutíveis

### 17.1. Geração Inicial de Elenco para Clubes com Força `20`, `15`, `10` e `5` (`seed = 199801`)

Na arquitetura atual (`016B` + `017B`), um clube com `ClubInitialPower` (`1..20`) é posicionado no ranking inicial (`zeroBasedRank`), alocado em sua divisão (`1..4`) e posição (`1..8`), recebe tamanho de elenco `SquadSizeAt(zeroBasedRank)` e tem as forças individuais sorteadas por `InitialSquadStrength017B.Draw(seed, clubKey, playerKeys, reference)`.

Abaixo estão os resultados exatos reproduzidos pelo algoritmo real `InitialSquadStrength017B.Draw` para **`seed = 199801`**:

| Força do Clube (`1..20`) | Rank Geral (`016B`) | Divisão e Posição Inicial | Tamanho do Elenco (`SquadSizeAt`) | Referência `017B` e Faixa `[Min..Max]` | Forças Individuais Sorteadas (`seed = 199801`) | Mín / Máx / Média Real |
| :---: | :---: | :---: | :---: | :---: | :--- | :---: |
| **Clube Força `20`** | 1º (`rank = 0`) | **1ª Divisão — 1º lugar** | `23` atletas | Ref `50` $\rightarrow$ **`[47..50]`** *(garante 47..50)* | `[49, 47, 48, 50, 48, 48, 48, 50, 47, 48, 49, 47, 47, 49, 47, 47, 49, 50, 49, 49, 48, 47, 49]` | Mín: **47** \| Máx: **50** \| Média: **48,26** |
| **Clube Força `15`** | 11º (`rank = 10`) | **2ª Divisão — 3º lugar** | `20` atletas | Ref `36` $\rightarrow$ **`[35..37]`** | `[36, 36, 36, 36, 36, 35, 35, 37, 37, 36, 35, 37, 36, 37, 37, 35, 36, 37, 37, 35]` | Mín: **35** \| Máx: **37** \| Média: **36,10** |
| **Clube Força `10`** | 21º (`rank = 20`) | **3ª Divisão — 5º lugar** | `18` atletas | Ref `19` $\rightarrow$ **`[18..20]`** | `[19, 18, 19, 18, 20, 19, 19, 20, 19, 20, 19, 19, 19, 18, 19, 20, 18, 18]` | Mín: **18** \| Máx: **20** \| Média: **18,94** |
| **Clube Força `5`** | 31º (`rank = 30`) | **4ª Divisão — 7º lugar** | `22` atletas | Ref `2` $\rightarrow$ **`[1..3]`** | `[1, 3, 3, 2, 2, 1, 2, 2, 2, 1, 1, 2, 3, 3, 1, 1, 3, 1, 1, 1, 2, 3]` | Mín: **1** \| Máx: **3** \| Média: **1,86** |

*(Nota: se o mundo for criado pelo modo antigo da fábrica `WorldSeedFactory.CreateSeededWorld(applyInitialSquadStrength: false)`, as faixas sorteadas por divisão são: 1ª Div `34..48`, 2ª Div `27..39`, 3ª Div `21..32`, 4ª Div `16..26`, Distrital `10..21`.)*

### 17.2. Exemplo Numérico — Jogador A (Crescimento / Evolução)
- **Cenário:** Jogador A pertence ao clube em **5º lugar da 3ª Divisão** (cujo teto em `ChampionshipStrengthLimits` é **`19`**) e inicia a temporada com `Strength = 17`.
- **Rodada 1 (`CompetitionType.League`):** Jogador A fica no banco de reservas e não entra em campo (`!appeared.Contains(p.Id)`). Como `17 < 19`, recebe `RESERVE_PROGRESS`:
  $$\text{Strength}: 17 \rightarrow 18 \quad (+1)$$
- **Rodada 2 (`CompetitionType.League`):** Jogador A novamente não entra em campo. O clube sobe para **4º lugar da 3ª Divisão** (novo teto = **`21`**). Como `18 < 21`, recebe `RESERVE_PROGRESS`:
  $$\text{Strength}: 18 \rightarrow 19 \quad (+1)$$
- **Rodada 3 (`CompetitionType.CupRoundOf64` — Taça):** Jogador A fica no banco. Como a rodada **não é da Liga** (`isLeagueRound == false`), **não há evolução**:
  $$\text{Strength}: 19 \rightarrow 19 \quad (0)$$
- **Rodadas 4 e 5 (`CompetitionType.League`):** Jogador A permanece sem entrar em campo com o clube em 4º lugar da 3ª Divisão (teto `21`):
  - Rodada 4: $19 \rightarrow 20$ (`+1`)
  - Rodada 5: $20 \rightarrow 21$ (`+1`, atingiu o teto `21`)
- **Rodada 6 (`CompetitionType.League`):** Jogador A permanece no banco e o clube continua em 4º lugar (teto `21`). Como `before (21) < Ceiling (21)` é **falso**, ele **para de crescer**:
  $$\text{Strength}: 21 \rightarrow 21 \quad (0)$$

### 17.3. Exemplo Numérico — Jogador B (Regressão por Jogo e por Lesão)
- **Caso B1 — Regressão por Participação em Jogo da Liga (`PLAYED_MATCH_DECLINE`):**
  - Jogador B tem `Strength = 36` (2ª Divisão) e atua como titular (ou entra aos 85 minutos) nas Rodadas 1 e 2 da Liga:
  - Em cada rodada da Liga em que atua, `DeterministicRng.FromContext(seed, season, round, PlayerToken(p.Id), 0, 12042).NextBool(0.50)` é avaliado:
    - Se `NextBool(0.50) == true` (50%): `Strength` cai de $36 \rightarrow 35$ (`-1`, motivo `PLAYED_MATCH_DECLINE`).
    - Se `NextBool(0.50) == false` (50%): `Strength` mantém $36 \rightarrow 36$ (`0`).
  - Na Rodada 3 (Taça Nacional), Jogador B joga os 90 minutos: **0% de chance de perda por participação**, pois a Taça não aplica `PLAYED_MATCH_DECLINE`.
  - Ao longo de 14 rodadas de Liga jogando todas sem se lesionar, a perda esperada é de $14 \times 0,50 = 7,0$ pontos de força.
- **Caso B2 — Regressão Gradual Diária por Lesão (`INJURY_DAILY_LOSS`):**
  - Suponha força `50` e lesão de `duration = 15` dias; o máximo permitido é:
    $$\text{MaximumLoss}(50, 15) = \min\left(49,\; \left\lfloor \frac{50 \times 15}{30} \right\rfloor\right) = 25$$
  - Caso o sorteio `rng.NextInt(0, 26)` retorne `TotalLoss = 15` pontos ao longo de `15` dias:
    - Neste sorteio **parcial específico**, `floor(15*d/15)=d`: exatamente um ponto por dia. Não é a regra geral nem um desconto por rodada. Sem ganhos intermediários, dias 3/6/9/12/15 resultam em forças 47/44/41/38/35; são marcos de dias, não cinco rodadas fixas. Os intervalos esportivos normais alternam 4/3 dias e podem ser adiados.
    - Se o sorteio fosse máximo, `TotalLoss=25`, as parcelas seriam `1,2,2,1,2,2,1,2,2,1,2,2,1,2,2`, total 25 e força final 25 sem outros ganhos. Recuperar-se não devolve os pontos. O atleta ainda pode receber crescimento de reserva elegível em fechamentos da Liga durante o episódio.

---

## 18. Testes Automatizados Existentes

| Arquivo de Teste | Testes Principais | O que Validam |
| :--- | :--- | :--- |
| [`tests/Elifoot.Domain.Tests/InitialSquadStrength017BTests.cs`](file:///d:/elifoot_98/tests/Elifoot.Domain.Tests/InitialSquadStrength017BTests.cs#L6-L81) | `CatalogGenerationPersistsAll32ReferenceSlotsAndReplays`, `QuickCreationUsesSameRulesAndKeepsHistoricFactoryProfileAvailable` | Valida todas as 32 referências `017B` (divisões `1..4`, posições `1..8`), bandas `[min, max]`, garantia dos 4 valores `47, 48, 49, 50` nos 3 primeiros da 1ª Divisão e determinismo por `seed`. |
| [`tests/Elifoot.Domain.Tests/WorldCatalogRules016BTests.cs`](file:///d:/elifoot_98/tests/Elifoot.Domain.Tests/WorldCatalogRules016BTests.cs#L4-L116) | `GlobalPowerUsesPairsKeepsDetailedOrderAndMinimumBeyondForty`, `ManualTiesRemainAndAutomaticOrderUsesScore`, `SquadCycleRepeatsAfterFourteenAndEliteHasPriority`, `SelectionBalancesPositionsDeduplicatesAndDoesNotChangeStrength`, `OrderedDivisionSwapsKeepRankedSquadsAndRecomputeEligibleDistricts` | Valida `ClubInitialPower` (`1..20`) em pares (`20 - rank/2`), ciclo de tamanho de elenco `23..17`, prioridade de elite (`>=22` atletas) e preservação de elencos em trocas manuais de divisão. |
| [`tests/Elifoot.Domain.Tests/ClassicForce012CTests.cs`](file:///d:/elifoot_98/tests/Elifoot.Domain.Tests/ClassicForce012CTests.cs#L8-L543) | `ApprovedCeilingsForAllEightPlaces`, `DurationHasExactCumulativeBoundariesAndLossMaximum`, `FourthDivisionFifthPlaceGrowsExactlyOnceWithUnusedAndSuspendedPlayers`, `DailyLossIsGradualExactAndRecoversOnVirtualDate`, `PartialAndZeroInjuryLossDoNotBecomeMaximumOrAutomaticRestoration`, `StrengthOneNeverFallsBelowOneEvenAcrossAnInterruptedSave`, `NonParticipantCanReachApprovedCeiling`, `CeilingNeverClampsAStrongNonparticipant`, `EveryCupStagePreservesStrengthForStartersUsedAndUnusedBenchAndEliminatedClubs`, `LeagueParticipantCanMaintainOrLoseOneButNeverReceivesReserveGain`, `ActualMinutesDistinguishUnusedBenchLateEntryAndZeroMinuteEntry`, `LeagueUsesNewPositionAfterAllScoresAreApplied` | Valida tetos `1..50` nas 4 divisões, ganho `+1` determinístico para reservas/suspensos na Liga, preservação na Taça quanto à participação (lesões seguem o calendário), queda de `50%` (`440..560` em 1000) para participantes da Liga, perda gradual por lesão e piso `1`. |
| [`tests/Elifoot.Integration.Tests/ClassificationForcePostgresTests.cs`](file:///d:/elifoot_98/tests/Elifoot.Integration.Tests/ClassificationForcePostgresTests.cs#L14-L112) | `CupPreservesStrengthAcrossRefreshReconnectAndPostgresApiRestart` | Valida em PostgreSQL real + SignalR que rodadas da Taça não alteram força nem salário após refresh, reconexão e restart da API. |
| [`tests/Elifoot.Integration.Tests/OfflineCatalogV11Tests.cs`](file:///d:/elifoot_98/tests/Elifoot.Integration.Tests/OfflineCatalogV11Tests.cs#L9-L247) | `NineForce20RankByComparableRatingAndNinthKeeps20InSecondDivision`, `InvalidV11PlayersAreRejected`, `TeamStrengthRange` | Valida `teamStrength` `1..20`, proibição de `strength` individual no JSON V1.1, ordenação por `teamStrength` + `historicalRanking` e bloqueio `OfflineCatalogRanking.ForceBlock`. |
| [`tests/Elifoot.Simulation.Tests/ClassificationForceSimulationTests.cs`](file:///d:/elifoot_98/tests/Elifoot.Simulation.Tests/ClassificationForceSimulationTests.cs#L9-L121) | `ClassificationForce_100SeasonsOn017BWorld` | Simula **100 temporadas completas** (`35.100` partidas) num mundo `017B` com `ClassicAdapted`, validando invariantes de força `1..50`, zero ganhos/perdas de participação na Taça e equilíbrio de longo prazo entre divisões. |

---

## 19. Registro histórico de divergências da auditoria em `aa5130b`

Os estados PASS/PARTIAL desta tabela pertencem à auditoria original. Em especial, a linha 3 descreve o bloqueio anterior a `bf43cd4`, não um bloqueio atual. Evolução, calendário e bandas foram corrigidos no corpo deste documento e confrontados com `f1764fb`.

| Item Auditado | Regra Documentada / Esperada | Implementação Real no Código | Classificação | Observação Técnica |
| :--- | :--- | :--- | :---: | :--- |
| **1. Força inicial do clube (`1..20`)** | Escala `1..20` (`ClubInitialPower` na `016B` e `teamStrength` no Catálogo `V1.1`). | Implementado em `WorldCatalogRules016B.PowerAt` (`1..20`) e `OfflineClubV11.TeamStrength` (`1..20`). | **PASS** | Usado exclusivamente para ordenar clubes e definir divisões iniciais. |
| **2. Geração de força individual (`1..50`) nas divisões `1..4`** | Regra `017B` baseada na divisão (`1..4`) e posição inicial (`1..8`), faixa `ref-1..ref+1` e `[47..50]` para Top 3 da 1ª Divisão. | Implementado em `InitialSquadStrength017B.Reference`, `Draw` e `Apply`. | **PASS** | Totalmente determinístico por `SHA256(seed, clubKey, playerKey)`. |
| **3. Geração de força individual no Catálogo `V1.1`** | Catálogo `V1.1` não armazena `strength` por jogador no JSON e requer geração a partir da força/classificação do clube. | `OfflineWorldFactory` bloqueia mundos `V1.1` com `OfflineCatalogRanking.ForceBlock`; `InitialSquadStrength017B` cobre apenas divisões `1..4` (32 clubes) e ignora `Division == 5` (96 distritais). | **PARTIAL** | Bloqueio intencional e documentado em `ELIFOOT98_CATALOGO_V11_RESULTADO.md` até aprovação da regra de força para `V1.1` (incluindo os 96 clubes distritais). |
| **4. Padrão de `applyInitialSquadStrength` em `WorldSeedFactory.CreateSeededWorld`** | Criação rápida deve usar a regra `017B` quando solicitada e manter perfil histórico disponível. | O parâmetro `bool applyInitialSquadStrength = false` em `WorldSeedFactory.CreateSeededWorld` (`WorldSeedFactory.cs:25`) tem default `false` (usando faixas antigas `34..48`, `27..39`, `21..32`, `16..26`, `10..21`), enquanto `WorldCreatorEngine` aplica `017B` sempre. | **PARTIAL** | Intencional para retrocompatibilidade de testes antigos (`QuickCreationUsesSameRulesAndKeepsHistoricFactoryProfileAvailable`), mas chamadores diretos de `CreateSeededWorld()` precisam passar `applyInitialSquadStrength: true` se quiserem a regra `017B`. |
| **5. Evolução por não-participação (`RESERVE_PROGRESS`)** | `+1` por rodada oficial da Liga para quem não entrou em campo, limitado ao teto da posição atual na Liga (`ChampionshipStrengthLimits`). | Implementado em `ClassicForceEvolutionEngine.ApplyFinishedRound` (`L239`). | **PASS** | Documentado em `docs/CLASSIFICATION_FORCE_EVOLUTION.md`. |
| **6. Regressão por participação (`PLAYED_MATCH_DECLINE`)** | `50%` de chance de perder `-1` por rodada oficial da Liga para quem entrou em campo (piso `1`). | Implementado em `ClassicForceEvolutionEngine.ApplyFinishedRound` (`L15, L232-238`). | **PASS** | Documentado em `docs/CLASSIFICATION_FORCE_EVOLUTION.md`. |
| **7. Documentação histórica `docs/CLASSIC_FORCE_EVOLUTION_012C.md` vs Código Atual** | `CLASSIC_FORCE_EVOLUTION_012C.md` descrevia chance probabilística `0.15..0.85` de ganho e `15%` de queda em qualquer partida oficial. | Os commits `8e9ee25` e `9af02bc` substituíram essa regra por `+1` determinístico na reserva e `50%` de queda para quem joga, restrito à Liga (`docs/CLASSIFICATION_FORCE_EVOLUTION.md`). | **PASS (Histórico Documental)** | `docs/CLASSIFICATION_FORCE_EVOLUTION.md` é o documento vigente do código atual; `docs/CLASSIC_FORCE_EVOLUTION_012C.md` permanece apenas como registro histórico da etapa `012C` (para a parte de lesões diárias, continua válido). |

---

## 20. Conclusão histórica da auditoria original

- **Status Geral da Auditoria:** **`PARTIAL`** (com **`PASS`** integral em todas as regras implementadas de força inicial `016B`/`017B` e evolução/regressão `ClassicForceEvolutionEngine`, e **`PARTIAL`** exclusivamente porque o Catálogo Offline `V1.1` permanece intencionalmente bloqueado por `OfflineCatalogRanking.ForceBlock` aguardando conexão/definição da geração de força inicial dos atletas V1.1 — em especial para os 96 clubes do Distrital que `InitialSquadStrength017B` não sobrescreve).
- A execução original foi somente leitura. As transcrições incorretas de métodos, calendário e fórmulas foram objeto da errata documental posterior; não devem ser tomadas como código literal de `f1764fb`.

## 21. Resultado da conferência documental em `f1764fb`

As regras efetivas são +1 por rodada da Liga sem participação abaixo do teto da posição atual; sorteio determinístico de 50% de -1 para quem entrou na Liga, piso 1; exceção de nova lesão na partida clássica; perdas diárias de episódios de 2..15 dias; intervalos normais 4/3 dias com adiamentos; Taça sem ganho/queda por participação, preservando lesões pelo calendário. As bandas 017B da seção 7.1 foram conferidas contra `Reference` e `ChampionshipStrengthLimits.For`.

As descrições de cinco/dez jogos, piso relativo à força inicial e perda fixa por rodada lesionado em outros documentos eram erros documentais. Não foram comprovadas como regras anteriormente implementadas. Os seis arquivos de evolução, limites, 017B, rotina sazonal, fechamento e orquestração têm os mesmos blobs entre `bf43cd4^` (`73ae459`) e `f1764fb`. O desbloqueio V1.1 alterou geração inicial, não essas regras. Registros e resultados antigos permanecem históricos; esta revisão não executou testes, simulações ou operações de banco.
