# INTERFACE DE PARTIDA AO VIVO, PAUSA GLOBAL E SUBSTITUIÇÕES — TASK 011

- **Task**: `ELIFOOT_COMPLETE_GAMEPLAY_UI_011`
- **Projeto**: `D:\elifoot_98`
- **Arquivos Principais**:
  - `client/elifoot_flutter/lib/main.dart` (`_buildLiveMatchesAndPauseScreen`, `_buildHighlightedCoachMatchCard`, `_buildInMatchSubstitutionPanel`)

---

## 1. Estados Visuais da Partida (`_matchVisualStateLabel`)

A tela `2. JOGOS AO VIVO` diferencia explicitamente todos os estados do motor de jogo autoritativo no cabeçalho `live-match-state-banner` e no cartão da partida do treinador:

| Estado Interno (`roundState`) | Condição Adicional | Rótulo Visual na Interface |
|---|---|---|
| `PREPARING` | Antes do envio de todas as táticas | `PREPARAÇÃO DA RODADA` |
| `RUNNING_FIRST_HALF` / `RUNNING_SECOND_HALF` / `RUNNING_EXTRA_TIME` | Relógio correndo | `PARTIDA EM ANDAMENTO` |
| `GLOBAL_PAUSED` | `confirmedCount == 0` | `PAUSA SOLICITADA • PARTIDA PAUSADA` |
| `HALFTIME_PAUSED` / `EXTRA_TIME_BREAK` / `EXTRA_TIME_HALFTIME` | `confirmedCount == 0` | `PARTIDA PAUSADA (INTERVALO)` |
| `GLOBAL_PAUSED` / `HALFTIME_PAUSED` / `EXTRA_TIME_*` | `confirmedCount > 0` | `PARTIDA PAUSADA • AGUARDANDO RETOMADA` |
| `RESULTS` | Minuto 90' / 120' / Pênaltis concluídos | `PARTIDA FINALIZADA` |

---

## 2. Cartão de Jogo em Destaque do Treinador (`coach-highlighted-match-card`)

Quando o clube do treinador humano disputa a rodada atual, seu confronto é exibido em destaque no topo da tela `2. JOGOS AO VIVO`:
- **Cabeçalho do Confronto**: `SEU JOGO EM DESTAQUE • RODADA X (Competição)` + estado visual e minuto atual (`XX'`).
- **Placar Central Clássico**:
  - Mandante + Esquema Tático (`Clube A [4-4-2]`)
  - Placar de alto contraste amarelo sobre fundo preto (`2 x 1` + `(Pênaltis: X x Y)` quando aplicável)
  - Visitante + Esquema Tático (`Clube B [4-3-3]`)
- **Cronologia de Eventos da Partida**:
  - Lista os eventos cronológicos com etiquetas visuais padronizadas:
    - `[GOL]` — Gols marcados (tempo normal ou prorrogação)
    - `[CARTÃO]` — Cartões amarelos
    - `[EXPULSÃO]` — Cartões vermelhos / expulsões
    - `[LESÃO]` — Lesões em campo
    - `[SUB]` — Substituições processadas
    - `[LANCE]` — Outros eventos de partida

---

## 3. Painel Interativo de Substituições e Tática em Pausa (`in-match-substitution-panel`)

Durante qualquer janela de pausa (`GLOBAL_PAUSED`, `HALFTIME_PAUSED`, `EXTRA_TIME_BREAK`, `EXTRA_TIME_HALFTIME`), a tela exibe o painel interativo de ajustes:
1. **Controle de Cotas**:
   - Exibe `SUBS: X/5 (RESTANTES: Y) | PARADAS: W/3` (e `6/4` em prorrogação da Taça).
   - Impede submissão de nova substituição caso `RESTANTES == 0`, exibindo aviso claro no banner de status.
2. **Seleção Dupla (Dropdowns + Toque Direto nas Listas)**:
   - **Esquema Tático (`in-match-formation-dropdown`)**: Permite alterar a formação em tempo real entre as 10 opções (`4-4-2` a `5-0-5`).
   - **Lista Interativa `EM CAMPO (TOQUE PARA SELECIONAR QUEM SAI)`**: Exibe os 11 titulares com posição, nome (`*` preservado), força e indicador `[EXP]` para atletas expulsos (bloqueando seleção de atleta expulso).
   - **Lista Interativa `RESERVAS (TOQUE PARA SELECIONAR QUEM ENTRA)`**: Exibe os atletas do banco com posição, nome e força.
   - Os seletores `in-match-sub-out-dropdown` e `in-match-sub-in-dropdown` permanecem sincronizados com os toques nas pílulas de jogadores.
3. **Aplicação e Retomada**:
   - O botão `APLICAR AJUSTE NA JANELA` (`btn-aplicar-ajuste-janela`) é protegido por `_runUiCommand` contra cliques duplicados e atualiza imediatamente o snapshot da partida.
   - O botão `CONTINUAR` / `PRONTO PARA O 2º TEMPO` (`btn-confirmar-continuar`) registra a confirmação do treinador para retomada sincronizada.
