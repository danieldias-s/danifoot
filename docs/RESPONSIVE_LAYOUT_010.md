# Layout Responsivo Desktop e Mobile — Tarefa 010 (`ELIFOOT_VISUAL_POLISH_AND_RESPONSIVE_010`)

## 1. Diagnóstico do Problema na Tarefa 009
1. **Desktop (`1280px`)**:
   - Na tarefa 009, as tabelas ficavam encolhidas à esquerda (~420px) porque `SingleChildScrollView(scrollDirection: Axis.horizontal)` contraía o `DataTable` à sua largura mínima intrínseca, deixando mais da metade direita da tela vazia.
2. **Mobile (`390px`)**:
   - Na captura `10_width_390_scrolled.png` da tarefa 009, ao rolar a tabela horizontalmente para visualizar `JOGOS`, `GOLS` e `MÉDIA`, a coluna `JOGADOR` desaparecia à esquerda, impossibilitando identificar a qual atleta pertenciam os números exibidos.

## 2. Arquitetura Responsiva Implementada

### 2.1. Aproveitamento de Espaço no Desktop e Tablet (`768px`, `1024px`, `1280px`)
- O container principal de `PlayerRankingsScreen` é centralizado e limitado a `maxWidth: 1160px`, evitando dispersão visual em monitores ultra-largos.
- `ClassicTable` utiliza `LayoutBuilder` para impor `minWidth: availableWidth - 2` no `ConstrainedBox` que envolve o `DataTable` e ajusta dinamicamente `columnSpacing` conforme o número de colunas e a largura disponível.
- Colunas numéricas (`#`, `JOGOS`, `GOLS`, `GOLS SOFRIDOS`, `AMARELOS`, `VERMELHOS`, `MÉDIA`, `ÍNDICE`, `JOGOS FALTANTES`, `TEMPORADA`, `MINUTOS`, `PRÊMIO AO CLUBE`, `TOTAL`) utilizam `DataColumn(numeric: true)` para alinhamento à direita, mantendo textos (`JOGADOR`, `CLUBE ATUAL`, `CLUBE`, `COMPETIÇÃO`, `COBERTURA`) alinhados à esquerda.

### 2.2. Solução Mobile com Coluna de Identificação Fixa na Rolagem Horizontal (`320px`, `360px`, `390px`, `430px`)
- Foi implementada a **Abordagem A/C (Tabela Clássica com Coluna de Identificação Fixa na Rolagem Horizontal)**:
  - Quando a tabela é rolada horizontalmente (`_scrollOffset > 4.0`), `ClassicTable` exibe `_PinnedPlayerColumn` fixada à esquerda (`Positioned(left: 0, top: 0, width: pinnedWidth)`) com borda divisória dourada (`1.5px solid #FFD700`).
  - Cada linha da coluna fixa exibe:
    - Posição no ranking (`#`)
    - Nome do jogador clicável em dourado sublinhado (`Mateus Alves*`)
    - Nome do clube em subtítulo compacto logo abaixo do nome (`Guarani da Serra`)
  - As colunas de estatísticas (`Col 3+`: `JOGOS`, `GOLS`, `MÉDIA`, `GOLS SOFRIDOS`, `AMARELOS`, `VERMELHOS`, `ÍNDICE`, `JOGOS FALTANTES`) têm largura mínima calculada em função da zona direita visível (`availableWidth - pinnedWidth`), garantindo que, ao rolar para a direita, **todas as colunas numéricas fiquem posicionadas exatamente à direita da coluna fixa**, sem cortar texto ao meio e sem sobreposição.
  - Quando o usuário troca de categoria (ex.: `Artilheiros` -> `Goleiros` -> `Defensores`), `ClassicTableState.didUpdateWidget` reposiciona automaticamente o scroll horizontal no início (`0.0`).

### 2.3. Adaptação da Barra de Filtros e Modais por Largura
| Largura | Comportamento dos Filtros | Comportamento das Tabelas | Comportamento dos Modais (`ClassicDialog`) |
| --- | --- | --- | --- |
| `320 px` | 1 filtro por linha (`maxWidth - 20`), botão `ATUALIZAR` compacto | Tipografia `10–11px`, coluna fixa de `124px` ao rolar | `insetPadding: 8px`, largura `304px`, rolagem interna suave |
| `360 px` | 2 filtros por linha (`~156px` cada) | Coluna fixa de `142px` ao rolar | `insetPadding: 8px`, largura `344px` |
| `390 px` | 2 filtros por linha (`~171px` cada) | Coluna fixa de `142px` ao rolar | `insetPadding: 8px`, largura `374px` |
| `430 px` | 2 filtros por linha (`~191px` cada) | Coluna fixa de `156px` ao rolar | `insetPadding: 8px`, largura `414px` |
| `768 px` | 3 filtros na primeira linha + `ATUALIZAR` | Tabela completa sem truncamento | Largura `720px` centralizada |
| `1024 px` | Todos os filtros + `ATUALIZAR` em linha única | Tabela completa equilibrada | Largura `920px` centralizada |
| `1280 px` | Todos os filtros + `ATUALIZAR` em linha única | Tabela completa (`maxWidth: 1160px`) | Largura `920px` centralizada, 5 seções visíveis |
