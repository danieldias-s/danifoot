# Comparativo Visual Antes (009) vs Depois (010) — Tarefa 010

## 1. Localização das Evidências Visuais
- **Antes (Baseline Tarefa 009 preservado)**:
  - `docs/audit-009/live-flutter-render/` (intacto)
  - `docs/evidence/visual_010/before/` (cópia de referência)
- **Depois (Tarefa 010 com dados reais da API/SQLite)**:
  - `docs/evidence/visual_010/`

## 2. Comparação Objetiva por Tela

### 2.1. Ranking Geral — Desktop (`1280 px`)
- **Antes (`before/01_scorers_general.png`)**:
  - Tabela ocupava apenas ~420px no canto esquerdo da tela de 1280px, deixando ~800px de espaço vazio à direita.
  - Seletores de filtro exibiam quadradinhos (`□`) no lugar das setas de dropdown e do ícone do botão `ATUALIZAR`.
  - Números alinhados à esquerda.
  - Prêmio exibido sem moeda (`3.000.000`).
- **Depois (`desktop_1280_scorers_general.png`)**:
  - Tabela distribuída de forma equilibrada na largura útil do container (`maxWidth: 1160px`), com linhas pares/ímpares contrastadas e destaque sutil para o Top 3.
  - Filtros `ClassicFilter` com setas vetoriais douradas e botão `ATUALIZAR` com ícone vetorial de atualização (zero `□`).
  - Colunas numéricas (`#`, `JOGOS`, `GOLS`, `MÉDIA`) alinhadas à direita; nomes e clubes alinhados à esquerda.

### 2.2. Ranking Geral, Goleiros e Defensores — Mobile (`320 px`, `360 px`, `390 px`, `430 px`)
- **Antes (`before/09_width_390_left.png` e `before/10_width_390_scrolled.png`)**:
  - Em `09_width_390_left.png`, apenas `#`, `JOGADOR` e `CLUBE ATUAL` apareciam, cortando as estatísticas.
  - Em `10_width_390_scrolled.png`, após rolar horizontalmente para ver `JOGOS`, `GOLS` e `MÉDIA`, a coluna `JOGADOR` desaparecia completamente à esquerda, tornando impossível saber a qual jogador pertenciam os números.
- **Depois (`mobile_390_scorers_left.png`, `mobile_390_scorers_scrolled.png`, `mobile_320_scorers_scrolled.png`, `mobile_390_goalkeepers_scrolled.png`, `mobile_390_defenders_scrolled.png`)**:
  - Ao rolar horizontalmente, a coluna `#  JOGADOR` permanece fixada à esquerda com divisor dourado (`#FFD700`), exibindo posição, nome clicável em amarelo sublinhado (`Mateus Alves*`) e clube (`Guarani da Serra`), enquanto as estatísticas (`JOGOS`, `GOLS`, `MÉDIA`, `GOLS SOFRIDOS`, `AMARELOS`, `VERMELHOS`, `ÍNDICE`) ficam integralmente visíveis à direita, sem textos cortados ou sobrepostos.

### 2.3. Histórico Individual do Jogador (`Mateus Alves*`)
- **Antes (`before/03_history_mateus_alves.png`)**:
  - Exibia `ARTILHEIRO: 1 VEZES` (erro gramatical no singular).
  - Coluna `CLUBE (ID HISTÓRICO)` exibia o GUID cru `f7ca895f-a395-994f-a2e4-8b0fc7ad8fb7`.
  - Cinco linhas repetidas de `1 | TAÇA | f7ca895f-...` (uma por fase da Taça).
  - Na tabela `CONQUISTAS DE ARTILHARIA`, o prêmio aparecia como `3.000.000` e a data como `2026-10-30T12:51:52.3602031Z`.
- **Depois (`desktop_1280_player_history_mateus.png` e `mobile_390_player_history_mateus.png`)**:
  - Organizado em 5 seções clássicas claras (Identificação, Resumo de Carreira com KPIs por posição, Estatísticas por Competição, Histórico de Temporadas, Conquistas de Artilharia).
  - Exibe `ARTILHEIRO: 1 VEZ`.
  - Coluna `CLUBE` exibe `Guarani da Serra` (zero GUIDs na interface) e consolida as fases da Taça da mesma temporada/clube/cobertura em `1 | TAÇA | Guarani da Serra | 5 | 2`.
  - Na tabela `CONQUISTAS DE ARTILHARIA`, exibe `R$ 3.000.000` e `30/10/2026`.
  - Botão `FECHAR` destacado com borda dourada na barra inferior do modal.

### 2.4. Encerramento da Temporada (`Temporada 1`)
- **Antes (`before/04_season_1_awards.png`)**:
  - Modal ocupava largura excessiva com grande vazio à direita, valores sem `R$` (`3.000.000`) e botão `FECHAR` discreto sem borda.
- **Depois (`desktop_1280_season_1_awards.png` e `mobile_390_season_1_awards.png`)**:
  - Banner explícito de empate: `EMPATE NA ARTILHARIA: 2 ARTILHEIROS EMPATADOS COM 10 GOLS.`
  - Valores formatados como `R$ 3.000.000` tanto por artilheiro quanto no total creditado por clube.
  - Tabelas equilibradas no desktop e responsivas no mobile (`390px`), com botão `FECHAR` de alto contraste.
