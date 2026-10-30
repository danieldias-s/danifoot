# RESPONSIVIDADE E ADAPTAÇÃO MULTI-VIEWPORT (WEB & ANDROID) — TASK 011

- **Task**: `ELIFOOT_COMPLETE_GAMEPLAY_UI_011`
- **Projeto**: `D:\elifoot_98`
- **Viewports Validados**: `320px`, `360px`, `390px`, `430px`, `768px`, `1024px`, `1280px`, `1920px`

---

## 1. Estratégia de Layout Responsivo Clássico

Todas as telas do Elifoot 98 Multiplayer foram ajustadas para funcionar sem qualquer erro de `RenderFlex overflow` desde telas mobile ultra-estreitas (`320px`) até monitores desktop Full HD (`1920px`):

1. **Tabelas Clássicas com Coluna Fixa no Mobile (`ClassicTable`)**:
   - Reaproveita o componente `ClassicTable` introduzido na Tarefa 010 agora aplicado em **todas** as tabelas densas do jogo:
     - `squad-table` (Elenco e Escalação — fixa `POS / JOGADOR`)
     - `standings-table` (Classificação das 4 Divisões — fixa `# / CLUBE`)
     - `cup-fixtures-table` (Taça Nacional)
     - `market-listings-table` (Mercado de Transferências — fixa `POS / JOGADOR`)
     - `finances-ledger-table` (Livro-Caixa Auditável)
     - `coach-ranking-table` (Ranking de Treinadores — fixa `# / TREINADOR`)
     - `ranking-table` / `not-eligible-table` / `player-season-history` / `season-scorer-awards` (Rankings e Histórico de Jogadores)
   - Em viewports `< 640px`, ao rolar horizontalmente para consultar estatísticas à direita, a coluna de identificação permanece fixada à esquerda com divisor amarelo (`#FFD700`).

2. **Placares Simultâneos Adaptativos (`_buildLiveMatchesAndPauseScreen`)**:
   - Em desktop (`>= 560px`): Exibe minuto, mandante, placar centralizado, visitante e cotas de substituições/paradas em linha única.
   - Em mobile (`< 560px`): Empilha o cabeçalho compacto do jogo (minuto + cotas) acima da linha de confronto (`Mandante` — `Placar` — `Visitante` com `TextOverflow.ellipsis`), evitando compressão ou corte do placar.

3. **Seletores e Botões Protegidos em `320px`**:
   - Todos os `DropdownButton` com nomes de atletas (`dropdown-swap-starter`, `dropdown-swap-reserve`, `in-match-sub-out-dropdown`, `in-match-sub-in-dropdown`) utilizam `ConstrainedBox(constraints: BoxConstraints(maxWidth: 230))` + `isExpanded: true` + `TextOverflow.ellipsis`.
   - O componente compartilhado `ClassicActionButton` envolve o rótulo de texto em `Flexible(child: Text(..., maxLines: 1, overflow: TextOverflow.ellipsis))` para nunca ultrapassar a largura de contêineres internos em `320px`.
   - Diálogos modais (`ClassicDialog` e `_chooseClub`) ajustam `insetPadding` e largura dinamicamente (`screen.width - 24` no mobile vs `620px..920px` no desktop).

---

## 2. Matriz de Verificação por Resolução

Validada automaticamente no teste `Task 011: Responsive Viewport Matrix (320px..1920px) Renders Zero Overflows` (`test/complete_gameplay_011_test.dart`) e no teste `visual_polish_010_test.dart`:

| Largura | Perfil de Dispositivo | Abas 1 a 9 | Modais (Clube / Ficha / Premiação) | Overflows Detectados |
|---|---|---|---|---|
| `320px` | Android Compacto / SE | OK | OK | `0` |
| `360px` | Android Padrão | OK | OK | `0` |
| `390px` | Android / iPhone Moderno | OK | OK | `0` |
| `430px` | Android Plus / Max | OK | OK | `0` |
| `768px` | Tablet Retrato | OK | OK | `0` |
| `1024px` | Tablet Paisagem / Laptop | OK | OK | `0` |
| `1280px` | Desktop HD | OK | OK | `0` |
| `1920px` | Desktop Full HD | OK | OK | `0` |
