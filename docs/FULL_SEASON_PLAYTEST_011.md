# FULL_SEASON_PLAYTEST_011 — Temporada Completa + Multiplayer 2/4/6/8 (Backend Real)

## 1. Como foi executado

- **Script**: `scripts/run_playtest_011.ps1` (Windows PowerShell 5.1).
- **Backend**: API `.NET` local DEV em `http://127.0.0.1:5089` (SQLite local, sem produção).
- **Saída**: `docs/evidence/gameplay_011/live-playtest-bundle-011.json` (respostas reais da API usadas pelo harness visual `tool/qa/gameplay_visual_011_test.dart`).
- **Data da execução**: 30/10/2026.

> [!NOTE]
> O PowerShell 5.1 serializa arrays dentro de hashtables como `{ "value": [...], "Count": N }`. O replay visual (`Replay011ApiClient._list`) desembrulha esse formato; os dados em si não foram alterados.

## 2. Temporada completa (22 rodadas) — treinador humano

- Mundo: `278e2101-1b75-4d00-8451-81eb2cb1fca6` (128 clubes, seed fixa).
- Treinador: `Treinador 011` — clube **Atlético Paulistano** (1ª Divisão).
- Operações de mercado/finanças antes da Rodada 1:
  - Proposta de R$ 1.661.687 por Fábio Oliveira;
  - Lance de leilão de R$ 699.668 por Mateus Pereira*;
  - Empréstimo bancário de R$ 300.000 (10 rodadas).
- Pausa global + substituição + troca tática executadas na primeira partida do clube (Rodada 3, minuto 25).

| Rodada | Competição | Formação enviada | Resultado do clube |
|---|---|---|---|
| 1 | Taça - Preliminar 1 | 4-4-2 | Sem jogo (fase distrital) |
| 2 | Taça - Preliminar 2 | 3-5-2 | Sem jogo (fase distrital) |
| 3 | Campeonato Nacional | 4-3-3 | Atlético Paulistano 1 x 3 Grêmio Guanabara |
| 4 | Campeonato Nacional | 3-5-2 | Palestra Verde 1 x 1 Atlético Paulistano |
| 5 | Taça - 32-avos | 4-4-2 | Comercial de Arapiraca 0 x 2 Atlético Paulistano |
| 6 | Campeonato Nacional | 4-3-3 | Atlético Paulistano 3 x 0 Sport Club Atlântico |
| 7 | Campeonato Nacional | 4-4-2 | Real Pampulha 1 x 1 Atlético Paulistano |
| 8 | Taça - 16-avos | 3-5-2 | Atlético Paulistano 6 x 0 Real de Ponta Grossa |
| 9 | Campeonato Nacional | 4-3-3 | Atlético Paulistano 0 x 0 Internacional do Sul |
| 10 | Campeonato Nacional | 3-5-2 | Estrela Solitária 0 x 2 Atlético Paulistano |
| 11 | Taça - Oitavas | 4-4-2 | Internacional do Sul 1 x 0 Atlético Paulistano (eliminado) |
| 12 | Campeonato Nacional | 4-3-3 | Atlético Paulistano 0 x 0 Esporte Clube Litoral |
| 13 | Campeonato Nacional | 4-4-2 | Grêmio Guanabara 0 x 1 Atlético Paulistano |
| 14 | Taça - Quartas | 3-5-2 | Sem jogo (eliminado) |
| 15 | Campeonato Nacional | 4-3-3 | Atlético Paulistano 2 x 1 Palestra Verde |
| 16 | Campeonato Nacional | 3-5-2 | Sport Club Atlântico 1 x 0 Atlético Paulistano |
| 17 | Taça - Semifinal | 4-4-2 | Sem jogo (eliminado) |
| 18 | Campeonato Nacional | 4-3-3 | Atlético Paulistano 1 x 0 Real Pampulha |
| 19 | Campeonato Nacional | 4-4-2 | Internacional do Sul 1 x 0 Atlético Paulistano |
| 20 | Taça - Grande Final | 3-5-2 | Sem jogo (eliminado) |
| 21 | Campeonato Nacional | 4-3-3 | Atlético Paulistano 1 x 0 Estrela Solitária |
| 22 | Campeonato Nacional | 4-4-2 | Esporte Clube Litoral 3 x 0 Atlético Paulistano |

**Classificação final (1ª Divisão)**: 2º lugar — 14 J, 6 V, 4 E, 4 D, 13 GP, 11 GC, SG +2, **22 pts** (Permanência).

**Taça Nacional**: 127 confrontos registrados (preliminares distritais + chave de 64). Final: Grêmio Guanabara 0 x 1 **Internacional do Sul (campeão)**. Todos os 127 confrontos ficam acessíveis na UI (correção do `.take(24)`).

**Virada de temporada**: após a Rodada 22, `next-round` levou o mundo à **Temporada 2, Rodada 1 (PREPARING)**, e a premiação de artilheiros da Temporada 1 ficou disponível (`season-top-scorers`).

**Treinador desempregado**: em um mundo separado, o treinador entrou sem clube e avançou 4 rodadas; não recebeu convites nesse intervalo (comportamento da IA de convites, inalterada). A tela `TREINADOR SEM CLUBE` foi capturada a partir desse estado real.

## 3. Multiplayer — 2, 4, 6 e 8 treinadores humanos

Cada sala foi criada com 128 clubes. As Rodadas 1 e 2 (preliminares distritais, sem jogos para clubes de divisão) foram jogadas por completo; a validação de pausa/substituição ocorreu na **Rodada 3**, a primeira com partida para todos os clubes humanos.

| Humanos | Contador inicial | Rodada iniciou após | Estado após pausa | Pausa pedida por | Substituição na pausa | Retomou após | Avançou para |
|---|---|---|---|---|---|---|---|
| 2 | TÁTICAS ENVIADAS: 0/2 | 2/2 | GLOBAL_PAUSED | Tecnico MP 1 | Sim | 2/2 | Rodada 4 |
| 4 | TÁTICAS ENVIADAS: 0/4 | 4/4 | GLOBAL_PAUSED | Tecnico MP 1 | Sim | 4/4 | Rodada 4 |
| 6 | TÁTICAS ENVIADAS: 0/6 | 6/6 | GLOBAL_PAUSED | Tecnico MP 1 | Sim | 6/6 | Rodada 4 |
| 8 | TÁTICAS ENVIADAS: 0/8 | 8/8 | GLOBAL_PAUSED | Tecnico MP 1 | Sim | 8/8 | Rodada 4 |

Conclusões:
- A rodada só começa quando **todos** os N humanos enviam a tática (nunca antes).
- A pausa global congela todas as partidas da sala, e a substituição é aceita durante a pausa.
- A partida só é retomada quando **todos** os N humanos confirmam `CONTINUAR`.

## 4. Correção feita no script durante esta tarefa

A primeira execução falhou com `400` em `POST /pause` na validação multiplayer, porque a pausa era pedida na Rodada 1, em que clubes de divisão não jogam (apenas distritais). A regra da API está correta e **não foi alterada**; o script passou a jogar as Rodadas 1–2 antes de validar a pausa na Rodada 3.

## 5. Limitação observada

- Alguns textos gerados pelo próprio script (ex.: `Sem jogo do clube nesta fase da TaÃ§a`) aparecem com mojibake no bundle porque o PowerShell 5.1 lê o `.ps1` UTF-8 sem BOM como ANSI. Os dados vindos da API estão corretos. O script foi regravado com BOM para as próximas execuções.
