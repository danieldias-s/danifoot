# Real Madrid 2026/27 — pesquisa para revisão do catálogo

**Situação da pesquisa: PARTIAL.** O recorte dos 23 e as propostas de estrelas, treinador, força coletiva e ranking estão disponíveis para revisão. A disciplina de 24 dos 26 candidatos exige evidência adicional ou uma coorte mais representativa. O clube e o manifest estão em `DRAFT`.

## Escopo e data de corte

- Pesquisa em `main`, HEAD `43b99bb98e63848fbd57aadf99ef6b04bc7afc30`. Ano do catálogo `2026`, temporada esportiva 2026/27, país `ES`, identidade `real-madrid`. [JSON anual](../catalogos/espanha/2026/clubes/real-madrid.json), [manifest `es-2026-principal`](../catalogos/espanha/2026/manifest.json), [identidade compartilhada](../catalogos/identidades/real-madrid/identidade.json).
- A identidade registra `#FFFFFF`, `#111111` e `crestFile: null`. O JSON anual tem 23 IDs, seis estrelas e todos os `behavior: null`. Os campos `teamStrength`, `historicalRanking`, `tieBreakPriority`, `stadium` e `coach` estão nulos.
- Corte observado em **12/11/2026**. A liga vai até Valencia 0–3 Real Madrid em 07/11, 12ª rodada. A Champions vai até AEK 2–3 Real Madrid em 04/11, quarta rodada da fase de liga. A lista de camisas do clube define os 26 candidatos. A lista UEFA informa inscrição e atuação europeia na fotografia consultada. [Lista oficial de camisas](https://www.realmadrid.com/en-US/news/football/first-team/latest-news/dorsales-del-real-madrid-para-la-temporada-2026-27-10-08-2026), [Valencia 0–3](https://www.realmadrid.com/en-US/news/football/first-team/reports/cronica-valencia-real-madrid-j12-liga-07-11-2026), [Elenco UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/), [Calendário da Champions](https://www.uefa.com/uefachampionsleague/news/02a8-2176fa83582b-d99f0b27f405-1000--champions-league-league-phase-fixtures-by-team/).

## Recorte recomendado

O elenco proposto tem **2 G, 8 D, 6 M e 7 A**. O critério prioriza cobertura de funções, minutos, titularidades e aparições de 2026/27. A convocação, a escalação oficial recente e boletins de recuperação ajudam a interpretar amostras pequenas. Os dois goleiros da lista oficial são suficientes para manter titular e reserva. Na defesa, a proposta mantém quatro zagueiros, dois laterais-direitos e dois laterais-esquerdos. Estrelas, preço e saídas disciplinares ficam fora do critério de corte.

A única troca proposta é **Dumfries por Asencio**. Dumfries somou 447 minutos em oito jogos de liga e foi titular contra o AEK. Asencio tem zero partidas nas duas competições e estava em recuperação informada em 10/11. Militão, com cinco minutos de liga, ocupou uma vaga de zagueiro no onze contra o AEK. Rodrygo estava em recuperação no mesmo boletim e a proposta mantém a vaga de ponta ao longo da temporada, com 19 jogos de liga em 2025/26 como contexto. A fotografia da UEFA e o perfil oficial de Mendy dão evidência de baixa disponibilidade no recorte. Thiago tem um minuto de liga e aparece na lista B. [Escalação AEK](https://www.uefa.com/uefachampionsleague/news/02ab-21f9907f0a27-6a6a67e9775d-1000--champions-league-starting-and-predicted-line-ups-matchday-4-/), [Boletim de treino de 10/11](https://www.realmadrid.com/es-ES/noticias/futbol/primer-equipo/entrenamientos/el-equipo-se-esta-entrenando-10-11-2026), [Histórico Opta de Rodrygo](https://theanalyst.com/players/1646/rodrygo/career), [Perfil de Mendy](https://www.realmadrid.com/en-US/football/first-team/players/ferland-mendy), [Convocação Valencia](https://www.realmadrid.com/es-ES/noticias/futbol/primer-equipo/convocatorias/convocatoria-del-real-madrid-ante-el-valencia-06-11-2026/).

### Lista oficial de 26 e comparação com o catálogo

**MANTER** (22) e **ENTRA** (1) identificam os 23 recomendados. `research-rma-2026-shirt-*` é identificador auxiliar. A posição exibe **proposta / LaLiga / UEFA**, e as siglas nacionais usam o catálogo e as fontes oficiais. `GB` é a codificação existente para os jogadores de nacionalidade esportiva inglesa. A letra A ou B em UCL refere-se à inscrição, sem relação com posição. `—` indica dado sem confirmação na fonte usada.

| Camisa | Jogador | ID atual ou auxiliar | Nac. | Pos. proposta / Liga / UEFA | Liga min / jogos / titular | UCL jogos (lista) | Decisão | Motivo | Fonte |
|---:|---|---|:---:|:---:|---:|---:|---|---|---|
| 1 | Thibaut Courtois | real-madrid-2026-001 | BE | G / G / G | 990 / 11 / 11 | 4 (A) | MANTER | 11 titularidades de Liga e quatro jogos europeus. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/) |
| 2 | Raúl Asencio | real-madrid-2026-022 | ES | D / D / D | 0 / 0 / 0 | 0 (A) | SUBSTITUIR | Zero partidas na LaLiga e na Champions. Recuperação informada pelo clube em 10/11. Saída proposta para abrir vaga de lateral-direito. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/), [evidência](https://www.realmadrid.com/es-ES/noticias/futbol/primer-equipo/entrenamientos/el-equipo-se-esta-entrenando-10-11-2026) |
| 3 | Éder Militão | real-madrid-2026-005 | BR | D / D / D | 5 / 1 / 0 | 1 (A) | MANTER | Cinco minutos de liga. Titular na Champions contra o AEK em 04/11, evidência de uso competitivo recente. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/), [evidência](https://www.uefa.com/uefachampionsleague/news/02ab-21f9907f0a27-6a6a67e9775d-1000--champions-league-starting-and-predicted-line-ups-matchday-4-/) |
| 4 | Dean Huijsen | real-madrid-2026-003 | ES | D / D / D | 862 / 10 / 10 | 2 (A) | MANTER | 10 titularidades de Liga e profundidade na zaga. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/), [evidência](https://www.realmadrid.com/es-ES/noticias/futbol/primer-equipo/cronicas/cronica-atletico-real-madrid-20-09-2026) |
| 5 | Jude Bellingham | real-madrid-2026-010 | GB | M / M / M | 957 / 12 / 11 | 4 (A) | MANTER | 11 titularidades e sete participações em gols de Liga. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/) |
| 6 | Eduardo Camavinga | real-madrid-2026-011 | FR | M / M / M | 294 / 11 / 2 | 2 (A) | MANTER | 11 aparições na Liga e duas na Champions. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/) |
| 7 | Vinícius Júnior | real-madrid-2026-016 | BR | A / A / A | 585 / 8 / 7 | 4 (A) | MANTER | Oito jogos de Liga, quatro de Champions, mantém cobertura de ponta. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/), [evidência](https://www.laliga.com/es-NG/partido/temporada-2026-2027-laliga-ea-sports-real-madrid-villarreal-cf-8) |
| 8 | Federico Valverde | real-madrid-2026-012 | UY | M / M / M | 891 / 11 / 11 | 4 (A) | MANTER | 11 titularidades de Liga e dois gols na Champions. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/) |
| 9 | Endrick | real-madrid-2026-017 | BR | A / A / A | 84 / 4 / 1 | 3 (A) | MANTER | Quatro jogos de Liga e três de Champions, opção de centroavante. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/) |
| 10 | Kylian Mbappé | real-madrid-2026-018 | FR | A / A / A | 1073 / 12 / 12 | 4 (A) | MANTER | 12 titularidades, 13 gols de Liga e quatro na Champions. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/) |
| 11 | Rodrygo | real-madrid-2026-019 | BR | A / — / A | 0 / 0 / — | 0 (A) | MANTER | Zero aparições na LaLiga pelo histórico Opta e na Champions pela UEFA. Recuperação informada pelo clube em 10/11. A vaga preserva profundidade de pontas durante a temporada. | [Opta](https://theanalyst.com/players/1646/rodrygo/career), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/), [evidência](https://www.realmadrid.com/es-ES/noticias/futbol/primer-equipo/entrenamientos/el-equipo-se-esta-entrenando-10-11-2026) |
| 12 | Trent Alexander-Arnold | real-madrid-2026-006 | GB | D / D / D | 356 / 7 / 4 | 1 (A) | MANTER | Quatro titularidades de Liga, opção de lateral-direito. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/) |
| 13 | Andriy Lunin | real-madrid-2026-002 | UA | G / G / G | 90 / 1 / 1 | 0 (A) | MANTER | Uma titularidade de Liga, único outro goleiro na lista oficial. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/) |
| 14 | Aurélien Tchouaméni | real-madrid-2026-013 | FR | M / M / M | 628 / 8 / 7 | 2 (A) | MANTER | Sete titularidades de Liga, opção de meio-campo defensivo. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/) |
| 15 | Arda Güler | real-madrid-2026-014 | TR | M / M / M | 744 / 12 / 10 | 2 (A) | MANTER | Dez titularidades e sete participações em gols de Liga. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/) |
| 16 | Ibrahima Konaté | real-madrid-2026-004 | FR | D / D / D | 990 / 11 / 11 | 2 (A) | MANTER | 11 titularidades de Liga, zagueiro de maior minutagem no recorte. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/) |
| 17 | Marc Cucurella | real-madrid-2026-007 | ES | D / D / D | 835 / 12 / 8 | 4 (A) | MANTER | 12 jogos de Liga e quatro de Champions, lateral-esquerdo. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/) |
| 18 | Álvaro Carreras | real-madrid-2026-008 | ES | D / D / D | 246 / 5 / 4 | 4 (A) | MANTER | Cinco jogos de Liga e quatro de Champions, segunda opção na esquerda. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/) |
| 19 | Carlos Espí | real-madrid-2026-023 | ES | A / A / A | 33 / 7 / 0 | 4 (A) | MANTER | Sete jogos de Liga com dois gols e quatro de Champions com um gol. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/) |
| 20 | Bernardo Silva | real-madrid-2026-015 | PT | M / M / M | 544 / 10 / 6 | 3 (A) | MANTER | Dez jogos de Liga e três de Champions, rotação no meio. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/) |
| 21 | Brahim Díaz | real-madrid-2026-020 | MA | A / M / A | 149 / 6 / 1 | 3 (A) | MANTER | LaLiga agrupa em M. Real Madrid e UEFA listam como A. Recomendação A, mantendo a categoria usada nas duas listas oficiais do recorte. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/), [evidência](https://www.realmadrid.com/en-US/news/football/first-team/latest-news/dorsales-del-real-madrid-para-la-temporada-2026-27-10-08-2026) |
| 22 | Antonio Rüdiger | real-madrid-2026-009 | DE | D / D / D | 306 / 4 / 3 | 3 (A) | MANTER | Quatro jogos de Liga e três de Champions, cobertura de zaga. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/) |
| 23 | Ferland Mendy | research-rma-2026-shirt-023 | FR | D / — / — | 0 / 0 / — | — (ausente) | FORA | Zero partidas e minutos no perfil do clube. Sem linha na tabela disciplinar LaLiga e sem inscrição na lista UEFA consultada. Cirurgia confirmada em 11/05, situação clínica atual sem conclusão. | [Clube](https://www.realmadrid.com/en-US/football/first-team/players/ferland-mendy), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/), [evidência](https://www.realmadrid.com/en-US/news/football/first-team/medical-reports/parte-medico-de-mendy-11-05-2026) |
| 24 | Denzel Dumfries | research-rma-2026-shirt-024 | NL | D / D / D | 447 / 8 / 5 | 3 (A) | ENTRA | Oito jogos de liga, três de Champions e titular contra o AEK. Lateral-direito na linha de quatro. Entrada proposta no lugar de Asencio. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/), [evidência](https://www.uefa.com/uefachampionsleague/news/02ab-21f9907f0a27-6a6a67e9775d-1000--champions-league-starting-and-predicted-line-ups-matchday-4-/) |
| 25 | Yan Diomande | real-madrid-2026-021 | CI | A / A / M | 714 / 12 / 7 | 4 (A) | MANTER | UEFA agrupa em M. Clube, LaLiga e escalação da UEFA contra o AEK sustentam A pelo uso na linha da frente. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/), [evidência](https://www.realmadrid.com/en-US/news/football/first-team/latest-news/dorsales-del-real-madrid-para-la-temporada-2026-27-10-08-2026) |
| 27 | Thiago Pitarch | research-rma-2026-shirt-027 | ES | M / M / M | 1 / 1 / 0 | 0 (B) | FORA | Um minuto na liga. Lista B da UEFA, sem atuação na fase de liga até o corte. Convocado contra o Valencia. Permanece fora da proposta. | [Liga](https://www.laliga.com/clubes/real-madrid/estadisticas), [UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/), [evidência](https://www.realmadrid.com/es-ES/noticias/futbol/primer-equipo/convocatorias/convocatoria-del-real-madrid-ante-el-valencia-06-11-2026/) |

**Brahim Díaz** fica em A, como aparece no clube e na UEFA. A LaLiga o agrega em M. **Yan Diomande** fica em A, como aparece no clube e na LaLiga. A UEFA o registra em M, mas sua escalação contra o AEK o coloca na linha de ataque. Mantêm-se os dois códigos já usados no JSON. [Lista do clube](https://www.realmadrid.com/en-US/news/football/first-team/latest-news/dorsales-del-real-madrid-para-la-temporada-2026-27-10-08-2026), [LaLiga](https://www.laliga.com/clubes/real-madrid/estadisticas), [Inscrição UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/), [Escalação AEK](https://www.uefa.com/uefachampionsleague/news/02ab-21f9907f0a27-6a6a67e9775d-1000--champions-league-starting-and-predicted-line-ups-matchday-4-/).

## Estrelas

Proposta de seis estrelas para revisão. A evidência prioriza gols, assistências, presença em campo e influência ofensiva observável. Gols da Champions e dados da LaLiga ficam em colunas próprias.

| Jogador | Estrela atual | Liga min | Liga gols + assist. | UCL gols | Proposta e evidência |
|---|:---:|---:|---:|---:|---|
| Kylian Mbappé | Sim | 1073 | 13 + 2 | 4 | Manter. Principal finalizador, com 49 remates de liga. |
| Jude Bellingham | Sim | 957 | 4 + 3 | 1 | Manter. Sete participações diretas em gols e 11 titularidades de liga. |
| Arda Güler | Sim | 744 | 3 + 4 | 1 | Manter. Sete participações em gols, incluindo quatro assistências. |
| Federico Valverde | Sim | 891 | 1 + 2 | 2 | Manter. 11 titularidades de liga e dois gols europeus em quatro jogos. |
| Vinícius Júnior | Sim | 585 | 1 + 3 | 0 | Manter. Quatro participações em oito jogos de liga. O histórico de 16 gols e cinco assistências na liga anterior é complementar. |
| Aurélien Tchouaméni | Sim | 628 | 0 + 1 | 0 | Propor retirada da estrela. Mantém importância como volante, com 17 interceptações na liga, e permanece no elenco. |
| Yan Diomande | Não | 714 | 3 + 2 | 1 | Propor estrela. Cinco participações em gols, 37 dribles certos em 12 jogos de liga e um gol europeu. |

[Produção ofensiva LaLiga](https://www.laliga.com/clubes/real-madrid/estadisticas), [Gols na Champions](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/), [Histórico ofensivo de 2025/26](https://www.xg-edge.com/la-liga/real-madrid/2025/squad).

## Disciplina dos 26 candidatos

A tabela principal usa exclusivamente a LaLiga 2026/27. Os minutos e as aparições europeias ficam separados. Na fonte em espanhol, `FALTAS C` representa faltas cometidas, `FALTAS R` recebidas e `DUE` total de duelos. Os destaques oficiais por clube confirmam a leitura de `FALTAS C`. Campos vazios no CSV correspondem a valores desconhecidos. O zero de participação de Mendy e Rodrygo vem de fontes externas à tabela disciplinar de liga. Asencio tem linha de zero participação na própria LaLiga. [Estatísticas em espanhol](https://www.laliga.com/clubes/real-madrid/estadisticas), [Perfil oficial de Mendy](https://www.realmadrid.com/en-US/football/first-team/players/ferland-mendy), [Histórico Opta de Rodrygo](https://theanalyst.com/players/1646/rodrygo/career).

| Camisa | Jogador | Min | Jogos | Amarelos | Vermelhos | Faltas | Duelos | Sugestão V16 | Revisão |
|---:|---|---:|---:|---:|---:|---:|---:|---|---|
| 1 | Thibaut Courtois | 990 | 11 | 0 | 0 | 0 | 1 | — | REVIEW_REQUIRED |
| 2 | Raúl Asencio | 0 | 0 | 0 | 0 | 0 | 0 | — | REVIEW_REQUIRED |
| 3 | Éder Militão | 5 | 1 | 0 | 0 | 0 | 0 | — | REVIEW_REQUIRED |
| 4 | Dean Huijsen | 862 | 10 | 2 | 1 | 8 | 59 | — | REVIEW_REQUIRED |
| 5 | Jude Bellingham | 957 | 12 | 2 | 0 | 8 | 149 | — | REVIEW_REQUIRED |
| 6 | Eduardo Camavinga | 294 | 11 | 2 | 0 | 5 | 37 | — | REVIEW_REQUIRED |
| 7 | Vinícius Júnior | 585 | 8 | 2 | 1 | 7 | 77 | — | REVIEW_REQUIRED |
| 8 | Federico Valverde | 891 | 11 | 0 | 0 | 5 | 45 | — | REVIEW_REQUIRED |
| 9 | Endrick | 84 | 4 | 0 | 0 | 0 | 10 | — | REVIEW_REQUIRED |
| 10 | Kylian Mbappé | 1073 | 12 | 3 | 0 | 9 | 116 | 1 Cordeirinho | SUGERIDO |
| 11 | Rodrygo | 0 | 0 | — | — | — | — | — | REVIEW_REQUIRED |
| 12 | Trent Alexander-Arnold | 356 | 7 | 0 | 0 | 0 | 11 | — | REVIEW_REQUIRED |
| 13 | Andriy Lunin | 90 | 1 | 1 | 0 | 0 | 0 | — | REVIEW_REQUIRED |
| 14 | Aurélien Tchouaméni | 628 | 8 | 2 | 0 | 4 | 42 | — | REVIEW_REQUIRED |
| 15 | Arda Güler | 744 | 12 | 1 | 0 | 6 | 87 | — | REVIEW_REQUIRED |
| 16 | Ibrahima Konaté | 990 | 11 | 3 | 0 | 13 | 61 | 3 Caneleiro | SUGERIDO |
| 17 | Marc Cucurella | 835 | 12 | 1 | 0 | 12 | 76 | — | REVIEW_REQUIRED |
| 18 | Álvaro Carreras | 246 | 5 | 1 | 0 | 1 | 20 | — | REVIEW_REQUIRED |
| 19 | Carlos Espí | 33 | 7 | 0 | 0 | 0 | 4 | — | REVIEW_REQUIRED |
| 20 | Bernardo Silva | 544 | 10 | 0 | 0 | 9 | 53 | — | REVIEW_REQUIRED |
| 21 | Brahim Díaz | 149 | 6 | 0 | 0 | 0 | 14 | — | REVIEW_REQUIRED |
| 22 | Antonio Rüdiger | 306 | 4 | 1 | 0 | 2 | 13 | — | REVIEW_REQUIRED |
| 23 | Ferland Mendy | 0 | 0 | — | — | — | — | — | REVIEW_REQUIRED |
| 24 | Denzel Dumfries | 447 | 8 | 1 | 0 | 2 | 28 | — | REVIEW_REQUIRED |
| 25 | Yan Diomande | 714 | 12 | 2 | 0 | 11 | 128 | — | REVIEW_REQUIRED |
| 27 | Thiago Pitarch | 1 | 1 | 0 | 0 | 0 | 2 | — | REVIEW_REQUIRED |

- **Dean Huijsen** tem um vermelho no agregado de liga e expulsão relatada pelo clube no dérbi de 20/09/2026. O relato ao vivo da liga também menciona VAR e o vermelho. Duração da sanção de 2026/27 pendente. Em evento separado, o Comitê de Apelação confirmou suspensão de uma partida em setembro de 2025. [Relato do dérbi](https://www.realmadrid.com/es-ES/noticias/futbol/primer-equipo/cronicas/cronica-atletico-real-madrid-20-09-2026), [Cronologia LaLiga](https://www.laliga.com/partidazos/el-derbi-de-madrid), [Decisão RFEF 2025](https://rfef.es/sites/default/files/sanciones/Comite_de_apelacion_18092025.pdf).
- **Vinícius Júnior** tem um vermelho no agregado e no relato oficial contra o Villarreal em 10/10/2026. Duração da sanção pendente. [Eventos da partida](https://www.laliga.com/es-NG/partido/temporada-2026-2027-laliga-ea-sports-real-madrid-villarreal-cf-8).
- **Rodrygo** tem registro complementar na Champions 2025/26 de cinco jogos, 229 minutos, dois amarelos e um vermelho. O histórico de sanções formais dessa expulsão permanece pendente. Para os demais atletas, os totais de cartões estão nas respectivas linhas e a pesquisa de decisões disciplinares individuais segue pendente. [Histórico de competições Opta](https://theanalyst.com/players/1646/rodrygo/career).
- O histórico separado traz uma observação doméstica de **2025/26 para cada candidato**, com 25 linhas com amarelos, 24 com vermelhos e 21 com faltas confirmadas. Cada linha identifica competição, equipe e fornecedor. Quando a fonte de minutos difere da fonte de disciplina, o CSV explicita ambas. Endrick usa o recorte do Lyon. Diomande usa jogos e disciplina da Bundesliga e minutos do FBref no mesmo recorte de 33 partidas. Em linhas de cobertura parcial, ficam vazios os campos sem verificação. O histórico anterior serve de contexto para a revisão, separado da entrada V16. [Histórico complementar](../catalogos/_pesquisa/comportamento/espanha/2026/real-madrid/historico-complementar-2025-26.csv).

### Classificador V16

O classificador usa no mínimo 900 minutos. O índice por 90 minutos é `(faltas + 2 × amarelos + 5 × vermelhos) × 90 / minutos`.

**Configuração efetivamente usada.** Grupo único `laliga-2026-27-rodada12-elites-fontes-conferidas`, divisão inicial 1 como premissa do experimento, 26 candidatos e 10 referências com pelo menos 900 minutos. Os comparadores vêm de Barcelona, Atlético de Madrid, Betis, Real Sociedad e Villarreal com tabelas compatíveis com o corte. A amostra representa parte da elite observada, com baixa cobertura por posição. O modelo calcula posição no grupo e gera uma estimativa de comportamento de jogo. O campo `confidence` fica vazio em todas as linhas por ausência de confiança calibrada.

| Atleta | Dados usados (min / A / V / F) | Índice | Percentil e N | Saída bruta | Tratamento para revisão |
|---|---|---:|---|---|---|
| Ibrahima Konaté | 990 / 3 / 0 / 13 | 1,7273 | 0,75; 6 D | 3 Caneleiro | Sugestão experimental. A taxa de faltas e cartões o posicionou acima da mediana da pequena coorte D. |
| Kylian Mbappé | 1073 / 3 / 0 / 9 | 1,2582 | 0,25; 2 A | 1 Cordeirinho | Sugestão experimental com base muito pequena. O índice ficou abaixo do único comparador A. |
| Thibaut Courtois | 990 / 0 / 0 / 0 | 0 | 0,50; 5 G | 2 Cavalheiro | REVIEW_REQUIRED. Os cinco goleiros empataram em zero, o que gera percentil central sem discriminação. |
| Jude Bellingham | 957 / 2 / 0 / 8 | 1,1285 | 0,50; 1 M | 2 Cavalheiro | REVIEW_REQUIRED. Percentil central calculado com uma única observação. |

Os outros 22 candidatos ficaram abaixo de 900 minutos, incluindo os casos com estatísticas disciplinares ausentes. O CSV usa `SUGERIDO` para as duas sugestões experimentais e `REVIEW_REQUIRED` para os outros 24. A saída original mantém o estado nativo `REVISAO` quando o V16 identifica insuficiência. Códigos possíveis: 0 Fair Play, 1 Cordeirinho, 2 Cavalheiro, 3 Caneleiro, 4 Caceteiro e 5 Sarrafeiro. [Coleta e referências por jogador](../catalogos/_pesquisa/comportamento/espanha/2026/real-madrid/fontes-e-coleta-2026-27.json), [Fonte disciplinar principal](https://www.laliga.com/clubes/real-madrid/estadisticas), [Implementação V16](../tools/offline_catalog/behavior_v16.py).

```powershell
$py = 'C:/Users/Daniel/AppData/Local/Python/pythoncore-3.14-64/python.exe'
$root = 'D:/elifoot_98/catalogos'
$rel = '_pesquisa/comportamento/espanha/2026/real-madrid'
& $py -B tools/offline_catalog/behavior_v16.py --root $root `
  --input (Join-Path $root "$rel/entrada-v16-2026-27.json") `
  --output "$rel/proposta-v16-2026-27.json"
```

O comando foi executado da raiz do projeto e a CLI retornou código 0. A saída usa criação exclusiva. Para repetir o comando em disco, use um nome de saída novo. [Entrada V16](../catalogos/_pesquisa/comportamento/espanha/2026/real-madrid/entrada-v16-2026-27.json), [Saída bruta V16](../catalogos/_pesquisa/comportamento/espanha/2026/real-madrid/proposta-v16-2026-27.json), [CSV de disciplina](../catalogos/_pesquisa/comportamento/espanha/2026/real-madrid/disciplina-2026-27.csv).

## Treinador e táticas

**José Mourinho, PT**, anunciado em 11/06/2026 e identificado na lista da UEFA. A LaLiga mostra 4-2-3-1 em casa contra Rayo Vallecano (12/09) e Sevilla (18/10), e fora contra Espanyol (22/08) e Valencia (07/11). [Anúncio do clube](https://www.realmadrid.com/en-US/news/club/latest-news/comunicado-oficial-mourinho-11-06-2026), [Registro UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid/squad/), [Rayo](https://www.laliga.com/partido/temporada-2026-2027-laliga-ea-sports-real-madrid-rayo-vallecano-5), [Sevilla](https://www.laliga.com/es-US/partido/temporada-2026-2027-laliga-ea-sports-real-madrid-sevilla-fc-9), [Espanyol](https://www.laliga.com/es-BR/partido/temporada-2026-2027-laliga-ea-sports-rcd-espanyol-de-barcelona-real-madrid-2), [Valencia](https://www.laliga.com/es-AR/partido/temporada-2026-2027-laliga-ea-sports-valencia-cf-real-madrid-12).

No motor, **4-5-1** é a adaptação mais adequada quando o objetivo é preservar quatro defensores e um centroavante. Os dois volantes e os três meias adiantados passam ao mesmo bloco M. A adaptação perde a separação entre as duas linhas e o detalhe da amplitude. O 4-2-4 deslocaria três meias adiantados para A e mudaria a função do centroavante isolado. O **4-3-3** tem evidência em partida fora, AEK em 04/11, pela composição da escalação oficial e pela etiqueta do transmissor. Um jogo confirma uso. A frequência como segunda preferência continua pendente. [Onze UEFA contra o AEK](https://www.uefa.com/uefachampionsleague/news/02ab-21f9907f0a27-6a6a67e9775d-1000--champions-league-starting-and-predicted-line-ups-matchday-4-/), [TNT Sports, formação inicial](https://www.tntsports.co.uk/football/champions-league/2026-2027/live-aek-athens-real-madrid_mtc21878498/live-lineup.shtml).

Proposta para `coach.preferredTactics`: `{"home":["4-5-1"],"away":["4-5-1","4-3-3"]}`. A segunda opção em casa fica sem entrada por falta de evidência. As duas formações propostas são códigos nativos e cabem no limite de duas por contexto. [Schema 1.1](../catalogos/_schemas/clube-1.1.schema.json).

## Força coletiva e ranking

**`teamStrength: 20` para aprovação.** O recorte mostra 30 pontos e dez vitórias em 12 rodadas de LaLiga, quatro vitórias na fase de liga da Champions, contra Inter, Roma, Leipzig e AEK e 96,4 no Opta Power Rankings, sexto no mundo e segundo entre clubes espanhóis na consulta. A nota 20 é uma escolha de calibração no catálogo 1..20. A força individual inicial e a regra 017B seguem o fluxo existente. [Tabela LaLiga via Opta](https://theanalyst.com/competition/la-liga/table), [Real Madrid no Opta](https://theanalyst.com/football/team/scm-186/real-madrid), [Resultados europeus do clube](https://www.realmadrid.com/es-ES/noticias/futbol/primer-equipo/actualidad/calendarios-del-real-madrid-en-la-primera-fase-de-la-champions-2026-27-29-08-2026).

**`historicalRanking: {"rating":96.4,"source":"OPTA","referenceDate":"2026-11-12"}` para aprovação.** A data é a fotografia observada da consulta. A interface da fonte exibia atualização relativa, com a hora exata de publicação pendente. Para comparar clubes de países diferentes no mesmo grupo de força, usar a mesma fotografia do Opta masculino, a mesma escala e o mesmo dia. O catálogo exige fonte e data iguais em todo o grupo de força. Quando a comparação não puder ser feita, fica pendente definir prioridades manuais de desempate. O coeficiente UEFA, com Real Madrid em segundo e 144,500 na tabela quinquenal consultada, serve de verificação europeia em escala separada. [Método Opta](https://theanalyst.com/articles/power-rankings-your-club-ranked), [Painel LaLiga Opta](https://theanalyst.com/competition/la-liga), [Painel Premier League Opta](https://theanalyst.com/competition/premier-league), [Coeficientes UEFA](https://www.uefa.com/nationalassociations/uefarankings/), [Ordenação do catálogo](../docs/catalogos/ELIFOOT98_ESTRUTURA_CATALOGOS.md).

## Alterações futuras sujeitas a aprovação

| Campo | Proposta em revisão | Pendência |
|---|---|---|
| `players` | Manter 22 jogadores e incluir Dumfries no lugar de Asencio. | Aprovar o recorte. Atribuir ID próprio ao novo atleta em etapa autorizada e preservar o ID de Asencio em registros históricos. |
| `players[].position` | Manter A para Brahim Díaz e Yan Diomande. | Aprovar a posição primária diante das categorias diferentes nas fontes. |
| `players[].isStar` | Cinco estrelas atuais mantidas. Propor troca de Tchouaméni por Diomande. | Aprovação editorial da seleção de seis. |
| `players[].behavior` | Duas sugestões experimentais para revisão. | Aumentar as coortes, verificar lacunas de 2025/26 e decisões de sanção, e revisar os 26. Aprovar e aplicar somente em etapa autorizada, com proveniência. |
| `coach` | José Mourinho, PT, e as formações registradas acima. | Aprovar a adaptação 4-5-1 e o uso observado de 4-3-3 fora. |
| `teamStrength`, `historicalRanking`, `tieBreakPriority` | 20/20 e 96,4 Opta em 12/11/2026. Prioridade manual pendente se necessária. | Aprovar a calibração e garantir fonte e data comuns antes de comparar clubes. |
| `status` do clube e do manifest | DRAFT. | Concluir a disciplina do elenco escolhido, aprovar os campos acima, executar a validação e obter autorização explícita para promover. |

## Verificações locais

- A CLI experimental terminou com código 0. A reprodução em memória conferiu os 36 resultados da saída. Os dois CSVs têm os mesmos 26 IDs únicos, e o recorte contém 23 atletas na distribuição 2/8/6/7.
- O validador offline terminou com código 0. Foram lidos sete catálogos. Espanha 2026 está válida, com 0 erros, 29 avisos e `ready: false`. Os avisos são 23 `BEHAVIOR`, 1 `COACH`, 2 `TACTICS`, 1 `TACTICS_MINIMUM`, 1 `TEAM_STRENGTH` e 1 `RANKING_RESOLUTION`.
- As somas SHA-256 dos JSONs do clube, manifest e identidade e dos três arquivos rastreados com mudanças locais preexistentes coincidiram com as iniciais. `git diff --check` passou.

A etapa gravou apenas o relatório e cinco arquivos auxiliares. Nenhuma operação de aprovação V16, alteração de save, criação de mundo, commit, push ou deploy foi executada. [Contrato de proveniência e revisão](../docs/catalogos/ELIFOOT98_V16_APROVACAO_PROVENIENCIA.md).
