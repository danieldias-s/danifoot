# Acabamento Visual e Identidade Clássica — Tarefa 010 (`ELIFOOT_VISUAL_POLISH_AND_RESPONSIVE_010`)

## 1. Resumo Executivo
Esta entrega executa o acabamento visual, correções de formatação e padronização de componentes clássicos das telas de **Rankings de Jogadores**, **Histórico Individual do Jogador** e **Premiação de Artilheiros da Temporada** no cliente Flutter (`client/elifoot_flutter/lib/player_rankings.dart`) e no contrato/leitura de histórico do domínio (`src/Elifoot.Contracts/PlayerRankingsDtos.cs`, `src/Elifoot.Domain/PlayerStatisticsEntities.cs`, `src/Elifoot.Domain/PlayerStatisticsEngine.cs`), preservando integralmente as regras esportivas, econômicas e os artefatos das tarefas 007, 008 e 009.

## 2. Identidade Visual Clássica Preservada
- **Paleta Clássica Elifoot 98**:
  - Fundo principal: `#0A4D1C` (verde-gramado escuro).
  - Cabeçalhos e barras de título: `#062E0F` com borda lateral/inferior dourada (`#FFD700`).
  - Áreas de conteúdo e tabelas: `#0E3B19`, `#0B3515` (linhas pares), `#10421C` (linhas ímpares) e `#185728` (destaque discreto para o Top 3 dos rankings).
  - Tipografia: `monospace` retrô de alta densidade informacional, cabeçalhos em amarelo (`#FFD700`) e links clicáveis sublinhados em dourado.
- **Ausência de Descaracterização**:
  - Nenhuma introdução de dashboards modernos, cartões gigantes, animações pesadas ou gradientes.

## 3. Correções Obrigatórias Implementadas

### 3.1. Pluralização de Conquistas de Artilharia (`formatTopScorerTitles`)
- **Problema na Tarefa 009**: A ficha do jogador exibia `ARTILHEIRO: 1 VEZES` de forma fixa.
- **Solução**: Helper reutilizável `formatTopScorerTitles(dynamic value)`:
  - `0` -> `ARTILHEIRO: 0 VEZES`
  - `1` -> `ARTILHEIRO: 1 VEZ`
  - `2` -> `ARTILHEIRO: 2 VEZES`
  - `5` -> `ARTILHEIRO: 5 VEZES`
  - `10` -> `ARTILHEIRO: 10 VEZES`
- A contagem real persistida em `seasonTopScorerTitles` permanece inalterada.

### 3.2. Resolução do Nome Histórico do Clube (`resolveHistoricalClubName`)
- **Problema na Tarefa 009**: A tabela `HISTÓRICO DE TEMPORADAS` exibia o GUID interno bruto do clube (ex.: `f7ca895f-a395-994f-a2e4-8b0fc7ad8fb7`).
- **Solução em Duas Camadas (Domínio + Interface)**:
  1. **Domínio (`PlayerStatisticsEngine` / `PlayerCompetitionStatistics` / `PlayerStatisticsRowDto`)**:
     - `PlayerCompetitionStatistics` passa a persistir `ClubName` no momento da consolidação da partida (`ApplyOne`), preservando o nome histórico do clube na época em que a partida foi disputada mesmo que o clube seja renomeado posteriormente.
     - `PlayerStatisticsEngine.ResolveHistoricalClubName` resolve na ordem: nome histórico gravado na linha -> catálogo `w.Clubs` pelo `ClubId` -> `w.SeasonTopScorerAwards` (`ClubNameAtClose`) -> `EmploymentHistory` de técnicos -> fallback amigável `"Clube histórico indisponível"`.
  2. **Interface (`resolveHistoricalClubName` + `ClubNameDirectory`)**:
     - Prioriza `row['clubName']` retornado pelo backend.
     - Para snapshots legados onde `clubName` não estava preenchido, resolve exclusivamente por correspondência exata de `clubId` em premiações (`clubId -> clubNameAtClose`) e no diretório de clubes observados na sessão (`ClubNameDirectory`).
     - **Nunca utiliza indiscriminadamente o clube atual do jogador** para um `clubId` diferente.
     - Quando um GUID histórico não pode ser resolvido, registra log técnico (`[Elifoot010] Clube histórico indisponível para clubId=...`) e exibe `Clube histórico indisponível` (jamais expondo GUID cru na UI).
     - Fases da Taça disputadas na mesma temporada pelo mesmo clube e mesma cobertura são consolidadas em uma única linha de temporada para leitura clara.

### 3.3. Formatação de Datas (`formatClassicDate`)
- **Problema na Tarefa 009**: Datas de premiação apareciam em ISO bruto (`2026-10-30T12:51:52.3602031Z`).
- **Solução**: Helper reutilizável `formatClassicDate(dynamic value, {bool includeTime = false})`:
  - Normaliza frações de segundo de 7–8 dígitos geradas pelo `.NET DateTimeOffset`.
  - Converte de forma determinística (componentes UTC do instante registrado) para o formato brasileiro `dd/MM/yyyy` (`30/10/2026`) ou `dd/MM/yyyy HH:mm` (`30/10/2026 12:51` quando `includeTime: true`).
  - O valor ISO original no backend permanece intacto.

### 3.4. Formatação de Valores Financeiros (`formatClassicMoney`)
- **Problema na Tarefa 009**: Valores apareciam sem unidade monetária (`3.000.000`).
- **Solução**: Helper `formatClassicMoney(dynamic value)` seguindo o padrão monetário existente do jogo (`R$` em `main.dart`):
  - `3000000` -> `R$ 3.000.000`
  - `6000000` -> `R$ 6.000.000`
  - `0` -> `R$ 0`

### 3.5. Filtros Clássicos Sem Glifos Ausentes (`ClassicFilter` & `ClassicActionButton`)
- **Problema na Tarefa 009**: Os ícones `Icons.arrow_drop_down` e `Icons.refresh` dependiam da fonte Material Icons e apareciam como quadradinhos (`□`) na renderização com fonte monoespaçada local.
- **Solução**:
  - `ClassicFilter` utiliza `_ClassicDropdownArrowPainter` (`CustomPainter` vetorial em dourado `#FFD700`), bordas retas (`BorderRadius.zero`), rótulo em destaque e foco visível.
  - `ClassicActionButton` utiliza `_ClassicRefreshIconPainter` e `_ClassicCloseIconPainter` vetoriais para os botões `ATUALIZAR`, `TENTAR NOVAMENTE` e `FECHAR`.

### 3.6. Componentes Clássicos Compartilhados (Seção 17)
Implementados e reutilizados em `client/elifoot_flutter/lib/player_rankings.dart`:
- `ClassicSectionTitle`
- `ClassicTableHeader`
- `ClassicFilter`
- `ClassicActionButton`
- `ClassicTable`
- `PlayerStatisticRow`
- `ClassicDialog`
