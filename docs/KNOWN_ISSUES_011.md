# PROBLEMAS CONHECIDOS E OBSERVAÇÕES TÉCNICAS — TASK 011

- **Task**: `ELIFOOT_COMPLETE_GAMEPLAY_UI_011`
- **Projeto**: `D:\elifoot_98`

---

## 1. Problemas Corrigidos na Tarefa 011

1. **Truncamento da Taça Nacional em 24 Jogos (`main.dart` linha 1625)**:
   - **Causa**: A listagem da Taça utilizava `_cupFixtures.take(24)`, ocultando todas as fases a partir da 1ª Eliminatória Distrital (que sozinha possui 32 confrontos).
   - **Solução**: Substituído por navegação de fases (`TODAS AS FASES`, `1ª Eliminatória`, `2ª Eliminatória`, `32-avos`, `16-avos`, `Oitavas`, `Quartas`, `Semifinal`, `Final`), banner de `CAMPEÃO DA TAÇA NACIONAL` e tabela `ClassicTable` sem truncamento.
2. **Caixas de Glifo Ausente (`□`) nas Telas Principais**:
   - **Causa**: Uso de `Icon(Icons.xxx)`, `✔`, `⏳`, `❌` e setas padrão de `DropdownButton` em `main.dart` e `shell_actions.dart`.
   - **Solução**: Substituídos por indicadores clássicos (`[OK]`, `[..]`, `[!]`, `[X]`) e `CustomPaint` vetoriais.
3. **Valores Financeiros sem Formatação de Milhar nas Telas Principais**:
   - **Causa**: Uso de `toStringAsFixed(0)` bruto no cabeçalho, seleção de clube, elenco, mercado, estádio, livro-caixa e convites.
   - **Solução**: Aplicação universal de `formatClassicMoney` (`R$ 1.850.000`).
4. **Overflows Horizontais em `320px` nos Seletores de Troca e Substituição**:
   - **Causa**: `DropdownButton` sem restrição de largura máxima e `Row` rígida na barra de esquema tático.
   - **Solução**: Aplicação de `ConstrainedBox(maxWidth: 230)` + `isExpanded: true` + `TextOverflow.ellipsis` e `Wrap` responsivo.

---

## 2. Limitações Conhecidas Fora do Escopo de Regra/Backend

1. **Primeiras 2 Rodadas da Temporada (Preliminares Distritais da Taça)**:
   - Por regra oficial do calendário de 22 rodadas (validada nas tarefas 005 a 008), as Rodadas 1 e 2 da temporada são dedicadas exclusivamente às fases `Preliminar 1 (96 -> 48)` e `Preliminar 2 (48 -> 32)` entre clubes da Divisão Distrital. Clubes das Divisões 1ª a 4ª estreiam na Rodada 3 (Campeonato Nacional) e entram na Taça Nacional na Rodada 5 (`32-avos de Final`). A interface exibe claramente a competição da rodada no topo e no calendário.
2. **Persistência de Sessão em Testes Herméticos**:
   - Em ambiente Web (`LOCAL_DEV`), a sessão utiliza `localStorage` (`session_store_web.dart`), enquanto nos testes de widget em VM utiliza armazenamento em memória (`session_store_stub.dart`).
3. **Repositório Git Local**:
   - Conforme determinação de segurança da Tarefa 011, o repositório Git foi inicializado exclusivamente de forma **local e privada** (`D:\elifoot_98\.git`), sem qualquer remoto configurado (`git remote -v` vazio) e sem publicação externa.
