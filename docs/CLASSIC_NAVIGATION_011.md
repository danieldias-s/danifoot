# NAVEGAÇÃO CLÁSSICA E PAINEL PRINCIPAL DO TREINADOR — TASK 011

- **Task**: `ELIFOOT_COMPLETE_GAMEPLAY_UI_011`
- **Projeto**: `D:\elifoot_98`
- **Arquivos Principais**:
  - `client/elifoot_flutter/lib/main.dart`
  - `client/elifoot_flutter/lib/shell_actions.dart`
  - `client/elifoot_flutter/lib/player_rankings.dart`

---

## 1. Filosofia de Navegação Clássica (Elifoot 98)

A navegação do Elifoot 98 Multiplayer foi projetada para ser **direta, rápida e plana** (máximo de 1 clique para alternar entre qualquer área funcional do jogo), evitando submenus profundos ou barras laterais pesadas:

1. **Cabeçalho Sincronizado Persistente (`_buildTopSynchronizedHeader`)**:
   - Sempre visível no topo da tela em todas as abas.
   - Exibe:
     - Sala / Mundo, Temporada atual, Rodada atual e Competição (`Campeonato Nacional` ou fase da `Taça`).
     - Identificação do Treinador, Clube atual (ou `TREINADOR SEM CLUBE`) e Saldo em Caixa formatado (`R$ X.XXX.XXX`).
     - Indicador colorido de Moral (`MORAL: X/100 — VERDE / AMARELA / VERMELHA`).
     - Contador sincronizado de preparação (`TÁTICAS ENVIADAS: X/N`) com pílulas individuais `[OK]` / `[..]` por treinador humano.
     - Botão primário contextual da rodada:
       - Em `PREPARING`: `ENVIAR TÁTICA` (ou `PRONTO PARA ACOMPANHAR` quando sem clube) / `RETIRAR PRONTO`.
       - Em partida (`RUNNING_*`): Placar de cotas (`MINUTO XX' | SUBSTITUIÇÕES X/5 | PARADAS X/3`) e botão `PEDIR PARADA`.
       - Em pausa (`GLOBAL_PAUSED` / `HALFTIME_PAUSED`): Faixa de pausa global com botão `CONTINUAR` / `PRONTO PARA O 2º TEMPO`.
       - Em `RESULTS`: Botão `AVANÇAR PRÓXIMA RODADA`.
     - Botão `TROCAR SALA` para retornar imediatamente ao lobby.

2. **Barra Clássica de 9 Abas (`_buildClassicNavigationMenu`)**:
   - Rolável horizontalmente em telas estreitas (`320px..430px`) e totalmente visível em telas desktop (`1280px..1920px`).
   - Sem ícones dependentes de fonte externa (evitando caixas de glifo ausente `□`).
   - Abas numeradas de 1 a 9:
     1. `1. PLANTEL & TÁTICAS`
     2. `2. JOGOS AO VIVO`
     3. `3. DIVISÕES & TAÇA`
     4. `4. MERCADO & LEILÃO`
     5. `5. FINANÇAS & ESTÁDIO`
     6. `6. RANKING & CARREIRA`
     7. `7. EDITOR & PACOTES`
     8. `8. CALENDÁRIO & RESULTADOS`
     9. `9. RANKINGS DE JOGADORES`

---

## 2. Painel Principal do Treinador (`_buildCoachMainPanel`)

Ao entrar em uma sala, a Aba 1 (`1. PLANTEL & TÁTICAS`) apresenta imediatamente no topo o **Painel Principal do Treinador**:

### 2.1. Modo Empregado (`coach-main-panel`)
Exibe de forma compacta (`PlayerStatisticRow` + barra de ações rápidas):
- **TREINADOR**: Apelido do técnico humano.
- **CLUBE ATUAL**: Nome do clube em destaque amarelo (`#FFD700`).
- **DIVISÃO**: Divisão atual do clube (`1ª Divisão` a `4ª Divisão`).
- **POSIÇÃO**: Posição atual na tabela e pontuação (`Xº (Y pts)`).
- **PRÓXIMO ADVERSÁRIO**: Clube adversário e mando de campo (`CASA` / `FORA`).
- **PRÓXIMA PARTIDA**: Confronto da rodada atual ou placar em andamento.
- **SALDO EM CAIXA**: Valor monetário formatado (`R$ X.XXX.XXX`).
- **PREPARAÇÃO**: Estado atual (`TÁTICA ENVIADA (PRONTO)`, `ESCALADO (FALTA ENVIAR)`, `ESCALAÇÃO INCOMPLETA (X/11)`).
- **MORAL DO CARGO**: Indicador colorido (`VERDE`, `AMARELA`, `VERMELHA`).
- **ALERTA DE CONVITES**: Caso existam convites pendentes, exibe `[!] X CONVITE(S) PENDENTE(S) — CLIQUE PARA VER`.
- **AÇÕES RÁPIDAS (`ClassicActionButton`)**:
  - `ESCALAÇÃO` (`quick-action-escalacao`): Executa auto-escalação inteligente respeitando os setores do esquema tático.
  - `TÁTICAS (X-X-X)` (`quick-action-taticas`): Alterna rapidamente entre os 10 esquemas táticos oficiais.
  - `MERCADO` (`quick-action-mercado`): Navega direto para `4. MERCADO & LEILÃO`.
  - `CLASSIFICAÇÃO` (`quick-action-classificacao`): Navega direto para `3. DIVISÕES & TAÇA`.
  - `PRÓXIMA PARTIDA` (`quick-action-proxima-partida`): Navega direto para `2. JOGOS AO VIVO`.

### 2.2. Modo Desempregado (`coach-unemployed-panel`)
Quando o treinador entra sem clube ou é demitido (`_isUnemployed == true`):
- Exibe o título **`TREINADOR SEM CLUBE`** e subtítulo explicativo.
- Mostra indicadores de carreira: `TREINADOR`, `SITUAÇÃO: TREINADOR SEM CLUBE`, `RANKING`, `TÍTULOS`, `MORAL`, `RODADA ATUAL` e `CONVITES`.
- Exibe o **Histórico de Clubes na Carreira** e a lista interativa de **Convites de Clubes Disponíveis** com botões diretos `ACEITAR CONVITE` e `RECUSAR`.
- Disponibiliza o botão `AVANÇAR RODADA (SEM CLUBE)` (`btn-avancar-sem-clube`) e oculta todas as ações exclusivas de clube (escalação, obras no estádio, venda de jogadores).
