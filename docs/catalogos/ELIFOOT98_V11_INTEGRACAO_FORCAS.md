# ELIFOOT 98 MULTIPLAYER — INTEGRAÇÃO DE FORÇAS E CRIAÇÃO DE MUNDOS V1.1

**Projeto:** Elifoot 98 Multiplayer (`D:\elifoot_98`)  
**Branch:** `main`  
**Referências:** Catálogo V1.0 (`3f4adf6`), Catálogo V1.1 (`aa5130b`)

**Revisão documental:** implementação conferida diretamente no HEAD `f1764fb`, em 12/11/2026. A integração inicial foi realizada em `bf43cd4`; esta revisão não altera implementação, probabilidades, seeds ou saves. Especificação de referência: [Regras de força](ELIFOOT98_REGRAS_FORCAS_E_INTEGRACAO_V11.md).

---

## 1. Objetivo e Escopo

Este documento registra a integração oficial das regras já aprovadas de força inicial dos clubes (`016B`) e dos jogadores (`017B` + calibrador determinístico distrital) ao fluxo do **Catálogo Histórico Offline V1.1**, habilitando a criação completa, determinística e persistente de mundos de **128 clubes** (`8 + 8 + 8 + 8 + 96`) a partir de arquivos JSON `schemaVersion = "1.1"`, sem criar fórmulas novas e mantendo 100% de compatibilidade com catálogos V1.0 e saves existentes.

---

## 2. Regras Existentes Preservadas (Sem Alteração de Fórmulas)

Nenhuma fórmula, probabilidade, limite ou constante existente foi alterada:

1. **Força Coletiva do Clube (`016B` — Escala `1..20`):**
   - Implementada em `src/Elifoot.Domain/Catalog/WorldCatalogRules016B.cs` e `src/Elifoot.Contracts/Catalog/OfflineCatalogV11Dtos.cs` (`teamStrength`).
   - Serve exclusivamente para ordenar a classificação inicial dos clubes (`OfflineCatalogRanking.Order`) e registrar `ClubInitialPower` (`1..20`) no clube criado.
   - Não é multiplicada diretamente para produzir forças individuais `1..50`.

2. **Força Inicial Individual nas 4 Divisões Principais (`017B` — Escala `1..50`):**
   - Implementada em `src/Elifoot.Domain/Catalog/InitialSquadStrength017B.cs`, usando a tabela de `src/Elifoot.Domain/ChampionshipStrengthLimits.cs`.
   - Faixas oficiais por divisão:
     - **1ª Divisão:** `39..50` (posições `1..3` com faixa `[47..50]` e garantia determinística de pelo menos um atleta com força `47`, `48`, `49` e `50`; posições `4..8` com subfaixas `[45..47]`, `[43..45]`, `[41..43]`, `[39..41]`, `[39..40]`).
     - **2ª Divisão:** `26..38` (posição `1` com subfaixa `[37..38]`, até posição `8` com `[26..28]`).
     - **3ª Divisão:** `13..25` (posição `1` com subfaixa `[24..25]`, até posição `8` com `[13..15]`).
     - **4ª Divisão:** `1..12` (posição `1` com subfaixa `[11..12]`, até posição `8` com `[1..2]`). As 32 bandas completas estão na seção 3 do documento de referência e em `docs/INITIAL_STRENGTH_RULES_017B.md`.

3. **Força Inicial Individual dos 96 Clubes Distritais (`Division == 5` — Escala `1..50`):**
   - Reutiliza integralmente `InitialPlayerStrength.Apply` (`src/Elifoot.Domain/Catalog/InitialPlayerStrength.cs`, método `"relative-v1"`), o mesmo mecanismo já aprovado e utilizado por `WorldCreatorEngine.Preview` (`src/Elifoot.Domain/Catalog/WorldCreatorEngine.cs`) para os 96 clubes distritais de catálogos históricos locais.
   - Calcula forças determinísticas na escala `1..50` a partir do percentil do ranking inicial do clube (`Rank` `1..128`) e de `SHA256("relative-v1|{seed}|{canonicalPlayerId}")`.

4. **Evolução e Regressão Pós-Criação (`ClassicForceEvolutionEngine`):**
   - Mantida integralmente em `src/Elifoot.Domain/ClassicForceEvolutionEngine.cs`:
     - `RESERVE_PROGRESS`: `+1` ao fechar cada rodada oficial da Liga para quem **não entrou em campo**, se `Strength < ChampionshipStrengthLimits.For(divisão, posição atual do clube)`. Inclui banco não utilizado, não relacionados, suspensos e lesionados que não atuaram. O teto limita ganhos; não reduz quem já está acima dele. Sem posição concluída, a referência provisória é o 4º lugar; distritais e atletas sem clube têm teto 1.
     - `PLAYED_MATCH_DECLINE`: sorteio determinístico de **50% para perder 1**, piso 1, para quem foi titular ou entrou por substituição na Liga, inclusive entrada registrada no último minuto sem minuto completo decorrido. Uma nova lesão na própria partida, no modo clássico, não acumula essa perda de participação. Não existe contador de titularidades nem piso relativo à força inicial da temporada.
     - `INJURY_DAILY_LOSS`, no modo `ClassicAdapted`: duração `D` de 2..15 dias virtuais; `MaximumLoss(F,D) = min(F-1, floor(F*D/30))`; `TotalLoss` sorteado entre zero e esse máximo, inclusive. A perda acumulada no dia `d` é `floor(TotalLoss*clamp(d,0,D)/D)`; cada parcela é a diferença dos acumulados consecutivos, com piso de força 1. A recuperação não restaura pontos. A perda diária pode ser zero ou maior que um.
     - **Calendário:** intervalos normais de 4 e 3 dias alternados, possíveis adiamentos por elenco lesionado e mais 21 dias na virada da temporada. O relógio virtual não avança durante partida em andamento.
     - **Taça:** sem ganho por reserva e sem queda por participação; novas lesões e perdas diárias por lesão continuam conforme o calendário virtual.
     - **Ocorrência de lesão:** `MatchSimulationEngine.ProcessInjuryTick` usa limiar `0.0018`, ou **0,18% por equipe/minuto**, quando há atletas ativos. Isso não é uma taxa por jogador nem uma perda de força por rodada.

---

## 3. Integração no Fluxo do Catálogo V1.1 (`OfflineWorldFactory`)

### 3.1. Prévia (`OfflineWorldFactory.Preview`)
Arquivo: `src/Elifoot.Domain/Catalog/OfflineWorldFactory.cs`

Quando os clubes selecionados pertencem ao schema `1.1` (`OfflineCatalogRanking.IsV11`):
1. Os candidatos são ordenados por `OfflineCatalogRanking.Order(available)`:
   - `teamStrength` (`1..20`) descendente;
   - `historicalRanking.rating` descendente (apenas quando todo o grupo de mesma `teamStrength` possui `source` e `referenceDate` idênticos);
   - `tieBreakPriority` ascendente;
   - `Key` ascendente apenas para apresentação estável de empates internos à mesma divisão.
2. Para cada clube selecionado na ordem classificada (`rank = i + 1`):
   - Preserva `InitialProfile` com `ClubInitialPower = v11.TeamStrength.Value` (`1..20`).
   - Mapeia os atletas de `OfflineClub.Players` (`11..23` jogadores sem `strength` no JSON V1.1) para `CatalogAthlete` com `CanonicalPlayerId = "offline:{country}:{year}:{catalogClubId}:{playerId}"` e `StrengthEstimate` inicial `"relative-v1"`.
3. Aplica `InitialPlayerStrength.Apply(clubs, request.Seed)` sobre os clubes selecionados, gerando forças determinísticas `1..50` que permanecem nos **96 clubes distritais** (`Division == 5`).
4. Após distribuir os 128 clubes nas divisões `1..5` (`8 + 8 + 8 + 8 + 96`) e validar eventuais empates de fronteira (`OfflineCatalogRanking.BoundaryErrors`), aplica `InitialSquadStrength017B.Apply(assigned, request.Seed)` sobre os **32 clubes das divisões 1 a 4**, gravando `InitialSquadReference` e as forças `017B` (`39..50`, `26..38`, `13..25`, `1..12`) em cada atleta da prévia.
5. Congela todas as forças, identidades, cores, escudos, treinadores e referências no `DataHash` (`SHA256`) da prévia.

### 3.2. Caso do 9º Clube com `teamStrength = 20`
- Se houver 9 clubes com `teamStrength = 20`, os 8 primeiros ocupam a 1ª Divisão (`[39..50]`) e o 9º clube entra na **1ª posição da 2ª Divisão**.
- O 9º clube **mantém `teamStrength = 20`** (`OfflineSource.V11.TeamStrength == 20`, `InitialProfile.ClubInitialPower == 20` e `Club.ClubInitialPower == 20` no mundo criado).
- Seus jogadores recebem a faixa individual correspondente à **1ª posição da 2ª Divisão** segundo `017B` (`37..38`, dentro de `26..38`).

### 3.3. Criação do Mundo (`OfflineWorldFactory.Create`)
Arquivo: `src/Elifoot.Domain/Catalog/OfflineWorldFactory.cs`

1. Valida integridade da prévia (`Errors.Count == 0`, `PendingSlots == 0`, `DataHash` idêntico, `128` clubes únicos em `8/8/8/8/96`, todos `READY`, e todos os atletas na prévia com `Strength` em `1..50`).
2. Para cada clube:
   - Copia `Name`, `ShortName`, `Abbreviation`, `PrimaryColorHex`, `SecondaryColorHex` e `CrestDataUrl` congelados de `identidades/<id>/identidade.json`.
   - Se `c.Stadium != null`, aplica nome e capacidade do estádio do catálogo; se `c.Stadium == null` (permitido em V1.1), mantém a capacidade base da fundação e define `"Estádio de {c.Name}"`.
   - Registra `ClubInitialPower = source.V11?.TeamStrength ?? entry.Club.InitialProfile?.ClubInitialPower` e `InitialSquadReference = entry.InitialSquadReference`.
   - Cria o `AiManager` com `Name`, `Nationality` e as `2` táticas `home` + `2` táticas `away` em `OfflineIdentity`.
   - Cria cada `Player` com:
     - `Strength = p.Strength ?? entry.Club.Players[j].Strength` (`p.Strength` literal para V1.0; `entry.Club.Players[j].Strength` congelado na prévia para V1.1);
     - `Behavior = (PlayerBehavior)p.Behavior.Value` (`0..5`);
     - `IsStar = p.IsStar.Value` (`true` apenas para `M` ou `A`);
     - `Nationality = p.Nationality`;
     - `SalaryPerRound = MarketAndEconomyEngine.CalculateFairSalaryPerRound(player)`;
     - `MarketPrice = MarketAndEconomyEngine.CalculateFairValue(player)`.
   - Seleciona os 11 titulares iniciais (`AiManagerBrain.PlanAndSelectLineup`) já com as forças `1..50` atribuídas.
3. Inicializa economia, calendário, carreira (`CoachCareerEngine.EnsureMigrated`) e evolução clássica de força. `ClassicForceEvolutionEngine.InitializeSeason` grava `ClassicForce.SeasonStartingStrengths`, limpa as posições concluídas e inicia o contexto da temporada. Essa coleção serve aos snapshots e não impõe piso de força. O teto posterior vem da divisão e posição atual em `ChampionshipStrengthLimits.For`; não existe campo `SeasonInitialStrength` ou `ChampionshipStrengthLimit` sendo atribuído a cada jogador por esse método. Salva `world.OfflineCatalogOrigin` com `SchemaVersion = "1.1"`.

### 3.4. Independência entre Força, Comportamento e Estrela
- `behavior` (`0..5`: Fair Play, Cordeirinho, Cavalheiro, Caneleiro, Caceteiro, Sarrafeiro) e `isStar` (`true`/`false`, restrito a `M` e `A` em V1.1) não entram em nenhum cálculo de `InitialPlayerStrength` nem de `InitialSquadStrength017B`.
- Alterar `behavior` ou `isStar` em um catálogo V1.1 mantém exatamente as mesmas forças individuais sorteadas para a mesma `Seed`.

### 3.5. Remoção do Bloqueio Temporário
- A constante `OfflineCatalogRanking.ForceBlock` foi removida de `src/Elifoot.Domain/Catalog/OfflineCatalogRanking.cs` e de `src/Elifoot.Domain/Catalog/OfflineWorldFactory.cs`.
- Textos informativos da tela `client/elifoot_flutter/lib/offline_catalog_creator.dart` foram atualizados para refletir a geração e o congelamento das forças `1..50` na prévia V1.1.

## 4. Histórico da correção documental

A versão publicada em `f1764fb` descrevia ganho a cada cinco partidas, queda a cada dez titularidades, pisos relativos à força inicial e perda fixa por rodada lesionado. Essas afirmações eram erros documentais, **não regras antigas comprovadamente implementadas**. Também continha bandas incorretas da primeira divisão e da última posição da quarta divisão e excluía indevidamente a lesão na Taça. O texto original permanece recuperável nesse commit; a descrição vigente acima foi conferida no código.

Os arquivos `ClassicForceEvolutionEngine.cs`, `ChampionshipStrengthLimits.cs`, `InitialSquadStrength017B.cs`, `PlayerStrengthEvolutionEngine.cs`, `CompetitionAndSeasonEngine.cs` e `MultiplayerRoundOrchestrator.cs` têm conteúdo Git idêntico entre `bf43cd4^` (`73ae459`) e `f1764fb`. Portanto, `bf43cd4` mudou o caminho de geração/criação V1.1, sem mudar a evolução posterior. O fluxo de fechamento está na seção 6.1 da referência oficial; os testes correspondentes estão na seção 9.
