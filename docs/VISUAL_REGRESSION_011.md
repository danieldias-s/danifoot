# AUDITORIA DE REGRESSÃO VISUAL E CONSISTÊNCIA GRÁFICA — TASK 011

- **Task**: `ELIFOOT_COMPLETE_GAMEPLAY_UI_011`
- **Projeto**: `D:\elifoot_98`
- **Evidências Visuais**: `docs/evidence/gameplay_011/` (36 capturas PNG: 18 Desktop + 18 Mobile)

---

## 1. Preservação da Identidade Clássica Elifoot 98

1. **Paleta de Cores Preservada**:
   - Fundo principal: Verde-escuro clássico (`#0A4D1C` / `#062E0F`).
   - Painéis internos: Verde intermediário (`#0E3B19`) com linhas zebradas (`#0B3515` / `#10421C`) e destaque de líder/selecionado (`#185728`).
   - Cabeçalhos e destaques: Amarelo clássico (`#FFD700`).
   - Bordas retas (`BorderRadius.zero`) de `1px` a `2px` (`#3E8450` / `#FFD700`).
   - Tipografia monoespaçada (`monospace` / `Consolas`) de alta legibilidade.

2. **Eliminação Completa de Glifos Ausentes (`□`)**:
   - Todas as ocorrências de `Icon(Icons.xxx)`, `✔`, `⏳`, `❌` e setas padrão de `DropdownButton` em `main.dart` e `shell_actions.dart` foram substituídas por:
     - Indicadores clássicos ASCII (`[OK]`, `[..]`, `[!]`, `[X]`).
     - Setas vetoriais `CustomPaint` (`_ShellDropdownArrowPainter`, `_ClassicDropdownArrowPainter`).
     - Silhueta vetorial `_PlayerSilhouettePainter` na ficha do atleta quando não há foto externa.
   - Resultado: **Zero caixas de glifo ausente (`□`)** nas 36 capturas de tela de `docs/evidence/gameplay_011/` e nas 24 capturas de `docs/evidence/visual_010/`.

---

## 2. Preservação Integral das Tarefas 008, 009 e 010

| Requisito / Componente | Status na Tarefa 011 | Verificação |
|---|---|---|
| Pluralização (`ARTILHEIRO: 1 VEZ` / `2 VEZES`) | Preservado 100% | `visual_polish_010_test.dart` |
| Resolução de nome histórico de clube (sem GUID bruto) | Preservado 100% + integrado às tabelas de divisão e elenco | `ClubNameDirectory` + `resolveHistoricalClubName` |
| Datas em `pt-BR` (`dd/MM/yyyy` / `dd/MM/yyyy HH:mm`) | Preservado 100% | `formatClassicDate` |
| Valores monetários em Reais (`R$ 3.000.000`) | Expandido para todas as telas (Caixa, Elenco, Mercado, Leilões, Estádio, Livro-Caixa, Convites) | `formatClassicMoney` |
| Craque com asterisco literal (`*`) | Preservado 100% no Elenco, Mercado, Ao Vivo, Artilharia e Rankings | `widget_test.dart` & `complete_gameplay_011_test.dart` |
| Moral do treinador (`MORAL: X/100 — VERDE/AMARELA/VERMELHA`) | Preservado 100% | `player_rankings_widget_test.dart` |
| Correção do truncamento da Taça Nacional (`.take(24)`) | Corrigido — exibe todas as fases (Preliminares até a Final) com filtro por etapa | `complete_gameplay_011_test.dart` & `14_national_cup_*.png` |
