# ELIFOOT 98 — Cadastro Offline Schema V1.1: Espanha 2026/27 (`ES`)

**Situação Geral: `DRAFT` (172/172 clubes estruturados e validados, `errors = 0`).**  
Nenhum comportamento foi autoaprovado (`players[].behavior = null` nos 171 novos clubes) e nenhuma nova estrela foi promovida (`players[].isStar = false` nos 171 novos clubes). Os arquivos pré-existentes de `real-madrid` foram preservados byte a byte, com divergências documentadas para revisão humana.

---

## 1. Escopo, Parâmetros e Corte Temporal

| Campo | Valor Adotado |
|---|---|
| País / Código / Slug | Espanha / `ES` / `espanha` |
| Ano do Catálogo / Temporada-Alvo | `2026` / `2026/27` |
| Temporada de Referência (desempenho/disciplina/V16) | `2025/26` (pesquisada inclusive no clube anterior para transferidos) |
| Data de Corte (`squadSnapshotDate` & `rankingReferenceDate`) | `2026-11-12` |
| Gênero | `Male` |
| Schema | V1.1 (`identidade-1.1`, `manifest-1.1`, `clube-1.1`), UTF-8 sem BOM |
| Manifest | [`catalogos/espanha/2026/manifest.json`](../catalogos/espanha/2026/manifest.json) (`status: "DRAFT"`, `name: "Espanha 2026/27"`, `172` clubes) |

**Justificativa da data de corte (`2026-11-12`):** alinha a pesquisa pré-existente de `real-madrid` (`2026-11-12`, ~12ª rodada de LaLiga) ao snapshot ao vivo do **Opta Power Rankings** (`Last updated Nov 12, 2026`) e atende à exigência estrutural de `OfflineCatalogV11Loader` (`historicalRanking.referenceDate` dentro do ano de catálogo `2026`). Estatísticas de minutagem, titularidades, gols, assistências e cartões mantêm como base a temporada concluída **2025/26**, inclusive via coleta de **450** clubes externos de origem de transferências de verão de 2026 (`0` erros de coleta).

---

## 2. Cobertura da Pirâmide Espanhola (`172` Clubes em `4` Categorias)

Cruzamento triplo entre listas oficiais **LaLiga / RFEF**, **Transfermarkt 2026/27** e **Wikipédia EN/ES 2026–27** em [`inventario_clubes.csv`](../catalogos/_pesquisa/espanha/2026/inventario_clubes.csv):

| Categoria Nacional | Competição Oficial | Grupos | Clubes | Clubes Filiais B/C | Atletas Selecionados | G | D | M | A |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|
| **1** | Primera División (La Liga EA Sports) | 1 | **20** | 0 | **460** | 45 | 160 | 129 | 126 |
| **2** | Segunda División (La Liga Hypermotion) | 1 | **22** | 2 | **506** | 50 | 171 | 144 | 141 |
| **3** | Primera Federación | 2 | **40** | 5 | **909** | 91 | 314 | 242 | 262 |
| **4** | Segunda Federación | 5 | **90** | 24 | **2.031** | 197 | 686 | 572 | 576 |
| **Total** | **4 divisões** | **9** | **172** | **31** | **3.906** | **383** | **1.331** | **1.087** | **1.105** |

Observações documentadas de elegibilidade:
- **Rebaixamentos/vagas administrativas (2026-07-01):** `real-madrid-c` (assumiu vaga de CD Arenteiro) e `cd-castellon-b` (assumiu vaga de SD Tarazona) em Segunda Federación, registrados em [`fontes.csv`](../catalogos/_pesquisa/espanha/2026/fontes.csv).
- **FC Andorra (`fc-andorra`):** clube sediado em Andorra filiado à RFEF e participante da Segunda División; utiliza `country: "ES"` no JSON de clube por exigência do carregador `OfflineCatalogV11Loader` (documentado em [`pendencias.csv`](../catalogos/_pesquisa/espanha/2026/pendencias.csv) e [`metadados.json`](../catalogos/_pesquisa/espanha/2026/metadados.json)).
- **Clubes Filiais (`31` entidades):** inventariados em `Segunda División` (2: `celta-fortuna`, `real-sociedad-b`), `Primera Federación` (5) e `Segunda Federación` (24) com vínculo `parentClubId` registrado no inventário e sem sobreposição física de atletas no recorte de 23 jogadores.

---

## 3. Identidades Visuais e Estádios (Lote B)

- **172/172** arquivos `catalogos/identidades/<clubId>/identidade.json` válidos no schema `identidade-1.1.schema.json` (1 preservado para `real-madrid` + 171 novos gerados).
- **Abreviações:** `172/172` únicas (corrigidas colisões iniciais e evitados códigos de playoff `GP1..GP5` da Wikipédia).
- **Contraste WCAG (>= 4.5:1):** `172/172` aprovados (`low_contrast = []`), preservando identidade real de camisa titular e evitando uso acidental de uniformes reserva.
- **Estádios:** `171/172` com nome e capacidade oficiais preenchidos; **`cf-calamocha`** (`Polideportivo Jumaya, Calamocha`) teve `stadium: null` por ausência de capacidade oficial confirmada (registrado em `pendencias.csv` com severidade `HIGH`).

---

## 4. Ranking Opta Ao Vivo `2026-11-12` e Força dos Clubes (Lote C)

- **Fonte primária única em JSON:** Opta Power Rankings ao vivo em [`dataviz.theanalyst.com/opta-power-rankings/`](https://dataviz.theanalyst.com/opta-power-rankings/) (`Europe (UEFA)`, última atualização `2026-11-12`, `94` páginas totais, `9.369` clubes europeus).
- **Cobertura atingida:** **`172 / 172` clubes espanhóis (`100%`)** ranqueados com verificação cruzada por **`optaId` permanente** (`142/146` IDs presentes na auditoria histórica `tonyelhabr/club-rankings`), eliminando riscos de homônimos (por exemplo: `Peña` EU #1212 confirmado por `optaId = ap7vwm0xiauem4rjqmzjeqprn` como `Peña Deportiva`, não `Penya` EU #2440 de rating `57.5`; `Gimnástica` EU #1201 confirmado por `optaId = 9jfjd6rherahv4z9s7tgtvm2b` como `Gimnástica Torrelavega`; `Badajoz` confirmado em EU #855 `2dro8et4pwdlwsgy0pm7voge4`).
- **Auditoria secundária (`tonyelhabr/opta-club-rankings.csv`, corte `2026-01-14`):** usada exclusivamente para conferência de histórico/nomenclatura e registrada em [`fontes.csv`](../catalogos/_pesquisa/espanha/2026/fontes.csv), **sem mistura de datas ou ratings nos JSONs de 2026-11-12**.

Top 5 e Base do Ranking Nacional ([`ranking_nacional.csv`](../catalogos/_pesquisa/espanha/2026/ranking_nacional.csv)):

| Pos. Nac. | `catalogClubId` | Cat. | Opta EU | Opta Global | Rating Opta | `teamStrength` |
|---:|---|---:|---:|---:|---:|---:|
| 1 | `fc-barcelona` | 1 | 4 | 4 | 97.5 | 20 |
| 2 | `real-madrid` | 1 | 6 | 6 | 96.4 | 20 *(preservado `null` no JSON)* |
| 3 | `atletico-de-madrid` | 1 | 20 | 20 | 91.9 | 19 |
| 4 | `real-betis` | 1 | 33 | 34 | 89.6 | 19 |
| 5 | `villarreal-cf` | 1 | 49 | 52 | 88.1 | 18 |
| ... | ... | ... | ... | ... | ... | ... |
| 171 | `ud-tamaraceite` | 4 | 1360 | 2659 | 64.3 | 1 |
| 172 | `cf-calamocha` | 4 | 1368 | 2675 | 64.2 | 1 |

Distribuição de `teamStrength` nos 172 JSONs: `20` (1: Barcelona; Real Madrid preservado `null`), `19..5` (2 clubes por nível = 30 clubes, posições nacionais 3..32), `1` (`140` clubes das posições nacionais `33..172`), `null` (1: `real-madrid` preservado).

---

## 5. Elencos, Estatísticas 2025/26, Treinadores e Táticas (Lotes D–E)

- **Atletas selecionados:** **`3.906`** (`100%` dentro de `13..23` jogadores por clube e `>= 2` goleiros em todos os 172 elencos).
- **Exclusões documentadas (`273` registros em [`jogadores_excluidos.csv`](../catalogos/_pesquisa/espanha/2026/jogadores_excluidos.csv)):**
  - `260` por corte determinístico no limite de `23` atletas (priorizando confirmação na Wikipédia `1.993` atletas, dorsal principal, minutagem/titularidades em `2025/26`).
  - `13` por dupla inscrição parent/filial no Transfermarkt (ex.: atletas de dorsal `26+` convocados pontualmente ao time principal foram mantidos em seus clubes filiais onde possuem dorsal `<=25` e minutagem principal).
- **Origem das estatísticas 2025/26 (`selecao_jogadores.csv`):**
  - `TM_ES_2025_26`: **2.826** atletas com histórico direto em clubes da base espanhola.
  - `TM_EXT_PRIOR_2025_26`: **504** atletas com estatísticas 2025/26 recuperadas em **450** clubes anteriores fora dos 172 espanhóis.
  - `TM_ES_0MIN_2025_26`: **249** atletas registrados em 2025/26 com `0` minutos.
  - `NONE_2025_26_REGISTERED_YOUTH_OR_NEW`: **304** atletas jovens/recém-promovidos sem registro prévio em 2025/26.
  - `PRESERVED_REAL_MADRID_JSON`: **23** atletas preservados de `real-madrid.json`.
- **Correção de parser aplicada e auditada:** na auditoria inicial do Lote H, identificou-se que capitães/lesionados/suspensos no Transfermarkt tinham `<span ...>` dentro de `<a ...profil/spieler/...>`, capturando temporariamente o valor de mercado (`€...`). O parser `fix_kader_names_172.py` corrigiu todos os **172** caches (`bad_names = 0`), mantendo apenas 1 homônimo real distinto em `atletico-central` (`Antonio Santos` `G` 2002 vs `Antonio Santos` `D` 2007).
- **Caso `Amir Saipi` (`cd-castellon`):** dupla nacionalidade Kosovo/Suíça; adotado `CH` porque `XK` não integra o enum ISO-3166 do schema `clube-1.1.schema.json` (registrado em `pendencias.csv`).
- **Treinadores e Táticas (`171/171` novos clubes):** `100%` com técnico, nacionalidade ISO válida e `preferredTactics.home` / `away` preenchidos com `1..2` táticas. Em **158** clubes houve conversão documentada de sistemas de 4 linhas (como `4-2-3-1`, `4-1-4-1`, `3-4-2-1`, `5-2-3`) para formações de 3 linhas compatíveis (`4-5-1`: 293 ocorrências; `4-4-2`: 157; `4-3-3`: 65; `5-4-1`: 46; `5-3-2`: 17; `3-5-2`: 11; `3-6-1`: 8; `3-4-3`: 3). Divergências nominais de técnicos recentes entre Transfermarkt (`2026-11-12`) e Wikipédia em Tier 2 foram confirmadas via busca web (Cádiz CF: Borja Jiménez em 22/10/2026; UD Las Palmas: Albert Sánchez em 29/10/2026; AD Ceuta FC: Sergio Lobera em 08/11/2026).

---

## 6. Classificação Disciplinar V16 e Candidatas a Estrela (Lote F)

- **Classificador V16 (`tools/offline_catalog/behavior_v16.py`):** executado para todos os **171** novos clubes com geração exclusiva sob [`catalogos/_pesquisa/comportamento/espanha/2026/<clubId>/`](../catalogos/_pesquisa/comportamento/espanha/2026/) (`disciplina-2026-27.csv`, `entrada-v16-2026-27.json`, `proposta-v16-2026-27.json`), deixando `behavior = null` em `100%` dos novos clubes.
- Como o Transfermarkt não disponibiliza `total_fouls_conceded` (faltas cometidas) para a temporada 2025/26, manteve-se `fouls = null`, resultando em `status = "REVISAO"` e `effectiveBehaviorProposal = null` para todos os atletas novos, em conformidade estrita com `ELIFOOT98_V16_APROVACAO_PROVENIENCIA.md`.
- **Candidatas a Estrela (mantidas `isStar: false` para revisão humana):** identificadas **111** candidatas em Tier 1 e Tier 2 (excluídos `G`/`D` conforme regra `allOf` do schema `clube-1.1`), anotadas em `selecao_jogadores.csv` e `pendencias.csv`. Destaques em 2025/26:

| Clube | Atleta | Pos. | Nac. | Gols + Assist. (2025/26) | Minutos | Origem dos Dados |
|---|---|---:|---:|---:|---:|---|
| `real-betis` | Troy Parrott | A | IE | 31 + 12 (43) | 3.770 | `TM_EXT_PRIOR_2025_26 (az-alkmaar)` |
| `fc-barcelona` | Lamine Yamal | A | ES | 24 + 18 (42) | 3.702 | `TM_ES_2025_26 (fc-barcelona)` |
| `ud-almeria` | Sergio Arribas | M | ES | 26 + 7 (33) | 3.830 | `TM_ES_2025_26 (ud-almeria)` |
| `racing-de-santander` | Andrés Martín | A | ES | 23 + 9 (32) | 3.486 | `TM_ES_2025_26 (racing-de-santander)` |
| `fc-andorra` | Jordi Cano | A | ES | 26 + 5 (31) | 3.510 | `TM_ES_2025_26 (ce-europa)` |
| `racing-de-santander` | Iñigo Vicente | A | ES | 9 + 21 (30) | 3.297 | `TM_ES_2025_26 (racing-de-santander)` |
| `fc-barcelona` | Fermín López | M | ES | 13 + 17 (30) | 3.053 | `TM_ES_2025_26 (fc-barcelona)` |
| `atletico-de-madrid` | Julián Álvarez | A | AR | 20 + 9 (29) | 3.549 | `TM_ES_2025_26 (atletico-de-madrid)` |
| `fc-barcelona` | Raphinha | A | BR | 21 + 8 (29) | 2.194 | `TM_ES_2025_26 (fc-barcelona)` |
| `sporting-de-gijon` | Nikita Iosifov | A | RU | 16 + 12 (28) | 4.063 | `TM_EXT_PRIOR_2025_26 (nk-celje)` |

---

## 7. Comparativo `real-madrid` Preservado vs. Snapshot `2026-11-12`

Os arquivos [`real-madrid.json`](../catalogos/espanha/2026/clubes/real-madrid.json), [`identidade.json`](../catalogos/identidades/real-madrid/identidade.json) e [`_pesquisa/comportamento/espanha/2026/real-madrid/`](../catalogos/_pesquisa/comportamento/espanha/2026/real-madrid/) permaneceram **inalterados** (SHA-256 verificado antes e após a execução dos lotes). Diferenças observadas contra a coleta de `2026-11-12`:
- **Ranking / Força:** JSON preservado mantém `teamStrength: null`, `historicalRanking: null`, `tieBreakPriority: null`; o Opta ao vivo em `2026-11-12` coloca o clube em **EU #6 / Global #6** (`rating: 96.4`, 2º na Espanha, `teamStrength` equivalente = `20`).
- **Treinador e Estádio:** JSON preservado mantém `coach: null`, `stadium: null`; no snapshot `2026-11-12` constam treinador **José Mourinho** (`PT`) e estádio **Bernabéu** (capacidade `83.186`).
- **Elenco:** JSON preservado mantém `23` atletas (incluindo `6` estrelas `isStar: true` pré-existentes); o elenco `kader` do Transfermarkt em `2026-11-12` lista `27` nomes (adicionando `Denzel Dumfries`, `Ferland Mendy`, `Thiago Pitarch` e `Jorge Cestero` — esses dois últimos com atuação principal em `real-madrid-castilla`, onde permaneceram alocados no catálogo).

---

## 8. Lacunas Documentadas entre Código e Regras Aprovadas

Durante a execução e validação oficial com `Elifoot.Api.dll offline-catalog-validate`, registraram-se os seguintes pontos entre regras aprovadas e comportamento atual do motor/carregador:
1. **`READY` vs. `DRAFT` para táticas (`TACTICS_MINIMUM`):** o validador .NET reporta `TACTICS_MINIMUM` (`READY exige pelo menos uma tática válida no total`) como aviso quando `home + away == 0` (observado hoje apenas no `real-madrid.json` preservado); os 171 novos clubes possuem táticas reais tanto em `home` quanto em `away`.
2. **Andorra na pirâmide espanhola (`fc-andorra`):** o carregador exige `country == manifest.country.code` (`ES`), exigindo uso de `"ES"` em vez de `"AD"` para o `FC Andorra`.
3. **Coexistência de 31 clubes filiais B/C:** os 31 times B/C (Tier 2–4) foram inventariados e estruturados como clubes distintos com `parentClubId` documentado em pesquisa, sem alterar regras de competição/elegibilidade do motor.
4. **Faltas ausentes e `BEHAVIOR` obrigatório em `READY`:** os 3.906 jogadores sem `behavior` atribuído (3.883 novos + 23 preservados) geram avisos `BEHAVIOR` (`Comportamento obrigatório: 0..5`) no validador oficial, mantendo `valid: true`, `ready: false` e `errors: 0` conforme esperado em `DRAFT`.

---

## 9. Resultados Consolidados de Validação (Lote H)

| Verificação | Escopo | Resultado |
|---|---|---|
| `validate_batch_h.py` (Schema V1.1 + BOM + invariantes) | 172 identidades, 1 manifest, 172 clubes, 8 arquivos `_pesquisa` | **PASS** — `errors = 0`, `boms = 0`, `dup_abbrs = 0`, `low_contrast = 0` (`>= 4.5:1`), `guardrails = 0` |
| `Elifoot.Api.dll offline-catalog-validate` | Raiz `D:\elifoot_98\catalogos` (`espanha/2026` + demais) | **PASS** (exit `0`) — `valid: True`, `ready: False`, `errors = 0`, `warnings = 3.912` (`3.906` `BEHAVIOR` + `6` apenas em `real-madrid.json` preservado) |
| `unittest discover -s tools/offline_catalog/tests` | Scripts Python V16 e ranking Opta | **PASS** — `66` testes (`OK`, `skipped = 1`) |
| Integridade `real-madrid` | JSON + 5 arquivos de comportamento pré-existentes | **PASS** — SHA-256 idêntico antes e depois dos lotes |
