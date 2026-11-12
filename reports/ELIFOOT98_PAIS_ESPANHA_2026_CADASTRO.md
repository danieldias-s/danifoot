# Elifoot 98 — Relatório de Cadastro e Revisão V1.6: Espanha 2026/27 (`es-2026-principal`)

**Data de referência / revisão V1.6:** `2026-11-12`  
**Plano de referência principal:** `ELIFOOT98_PLANO_MESTRE_IMPORTACAO_V1_6_FINAL_PARA_VALIDACAO.md` (priorizando decisões aprovadas nas Seções 11–21 e Aditivo Final Seção 18 de `12/11/2026`).  
**Status do catálogo:** `DRAFT` (`valid: true`, `ready: false` apenas pela ausência de faltas individuais 2025/26 no Transfermarkt -> `behavior: null` e campos nulos pré-existentes do `real-madrid`).

---

## 1. Resumo Executivo da Revisão V1.6

O catálogo **Espanha 2026/27** (`172` clubes: `1` baseline pré-existente `real-madrid` + `171` clubes novos nas 4 categorias oficiais da Espanha) foi auditado e corrigido conforme o **Plano Mestre V1.6**, reutilizando integralmente os dados já coletados em cache local sem refazer o scraping/importação completa.

### Principais entregas e correções aplicadas

1. **Correção da seleção até 23 jogadores (Seções 11.3 e 17):**
   - Removido o viés anterior em que `wiki_conf` (afetado por colchetes `[[` residuais do parser da Wikipédia) e `shirt <= 25` precediam minutagem/titularidade, e em que tetos posicionais estreitos (`A <= 6` / `D <= 8`) descartavam titulares de `1.500–2.305` minutos em favor de atletas com `0–336` minutos.
   - Aplicada prioridade esportiva V1.6: `isStar (AWARD_OR_BEST_XI / destaque forte)` > `minutes > 0` > `regularStarter (>=1500 min ou >=18 titularidades)` > `protectedProspect (idade <=21 com >=600 min, >=7 titularidades ou >=4 G+A)` > `minutes` > `starts` > `appearances` > `goals+assists` > `1 <= shirt <= 25` > `-shirt` > `wikiConfirmed` > `-tmPlayerId`.
   - Auditoria contra os `124` cortes sinalizados em `ELIFOOT98_ESPANHA_2026_CORTES_PARA_REVISAR.csv`: **`115` jogadores agora retidos** (`Dani Vivian`, `Alberto Jiménez`, `Sergio Gómez`, `Fidel`, `Alex Camacho`, `Rafita Garrido`, `Chupe`, `Anthony Gordon`, `Giovani Lo Celso`, entre outros); nos **`9` casos restantes** (`96..1026` min), o jogador de menor minutagem comparado anteriormente também foi excluído, restando **`0` excluídos com `>= 1500` min** em toda a Espanha e apenas `2` retenções por `protectedProspect` U21 (`Buba Sangaré` 19a/541m > `Bambo Diaby` 28a/794m no `elche-cf`; `Jones El-Abdellaoui` 20a/1169m > `Couhaib Driouech` 24a/1323m no `rc-celta-de-vigo`).
   - Aplicado teto de **`2..3` goleiros por elenco** (`{2: 151, 3: 20}` nos `171` clubes novos), removendo o 4º goleiro do `real-madrid-castilla` (`Javi Navarro`, U19, `EXCLUIDO_LIMITE_3_GOLEIROS_V16`).
2. **Padronização de `historicalRanking.source` para `"OPTA"`:**
   - Todos os `171` clubes novos foram atualizados de `"Opta Power Rankings"` para `"OPTA"`, compatibilizando com `OfflineCatalogRanking.Family` (`src/Elifoot.Domain/Catalog/OfflineCatalogRanking.cs`).
3. **Atribuição de estrelas (`isStar`) conforme Seção 18.2 aprovada em `12/11/2026`:**
   - `G` e `D` permanecem **sempre `isStar = false`**.
   - `13` atletas em `M`/`A` com prêmios/seleções comprovadas 2025/26 marcados como `AWARD_OR_BEST_XI` sem teto artificial (`fc-barcelona`: Lamine Yamal, Pedri, Fermín López, Raphinha; `racing-de-santander`: Andrés Martín, Iñigo Vicente; `atletico-de-madrid`: Arnau Ortiz; `real-betis`: Pablo Fornals; `villarreal-cf`: Alberto Moleiro; `real-sociedad`: Mikel Oyarzabal; `ud-almeria`: Sergio Arribas; `fc-andorra`: Jordi Cano; `cd-tenerife`: Enric Gallego).
   - `226` atletas em `M`/`A` marcados como `LOCAL_HIGHLIGHT` (`1` a `3` apenas quando sustentado por dados verificáveis de 2025/26 em clubes sem `AWARD_OR_BEST_XI`).
   - `7` clubes da Tier 4 permaneceram com **`0` estrelas** por insuficiência de minutagem/dados verificáveis (`< 720` min no TM: `arosa-sc`, `atletico-de-madrid-c`, `cd-arnedo`, `cf-calamocha`, `pena-sport-fc`, `rcd-mallorca-b`, `real-murcia-imperial`).
   - Total: **`239` estrelas** nos `171` clubes novos (**`245`** no catálogo completo com as `6` pré-existentes do `real-madrid`).
4. **Esquema completo em `selecao_jogadores.csv` (Seção 11.5) e pesquisa disciplinar:**
   - `catalogos/_pesquisa/espanha/2026/selecao_jogadores.csv` atualizado com `4.179` linhas (`3.905` retidos `candidate=true` e `274` excluídos `candidate=false`: `260` `EXCLUIDO_LIMITE_23_MENOR_MINUTAGEM_2025_26`, `13` `EXCLUIDO_DUPLA_INSCRICAO_FILIAL`, `1` `EXCLUIDO_LIMITE_3_GOLEIROS_V16`), incluindo `protectedStar`, `protectedProspect`, `starClassification`, `retentionEvidence`, `sourceUrls`, `candidate`, `excludedReason` e `confidence`.
   - `jogadores_excluidos.csv`, `pendencias.csv`, `metadados.json` e as `171` pastas em `catalogos/_pesquisa/comportamento/espanha/2026/<cid>/` regenerados via `tools/offline_catalog/behavior_v16.py`, preservando `behavior: null` e `status: "REVISAO"` pela ausência de `fouls` no Transfermarkt.
5. **Preservação absoluta do `real-madrid` e das fórmulas do motor:**
   - `catalogos/identidades/real-madrid/identidade.json` (`SHA256: 5B8A8E2E57495DC4126465A8EF4E6531DAA66E6C4F3C43320B70DEAE44A6DFA0`), `catalogos/espanha/2026/clubes/real-madrid.json` (`SHA256: 52132E4C3D8B61FB5B23BC4F5E68DB9757B18CEB43F9A86BF4F2D96DB02EFD81`) e todos os arquivos em `catalogos/_pesquisa/comportamento/espanha/2026/real-madrid/` preservados byte a byte.
   - Nenhuma fórmula do motor (`InitialSquadStrength017B`, `InitialPlayerStrength`, `ClassicForceEvolutionEngine`, `MatchEngine`, `MarketAndEconomyEngine`, `ChampionshipStrengthLimits`) foi modificada.

---

## 2. Comparação Plano Mestre V1.6 vs. Código Atual e Conflitos Técnicos

| Tema / Regra V1.6 | Estado no Código Atual (`main`) | Status / Ação Tomada | Conflito Técnico / Observação |
| --- | --- | --- | --- |
| Táticas 1..4 por técnico (`home`/`away`, sem duplicar) | Implementado e validado em `clube-1.1.schema.json`, `OfflineCatalogRules.cs:33-38`, `AiManagerBrain.cs:7-16` (`OfflineTacticsV16Tests`: 16 testes na baseline) | Preservado; 171 clubes novos com `(1,1):21, (1,2):28, (2,1):14, (2,2):108` e `0` fallbacks | Sem conflito |
| Elenco variável por divisão + seed determinística | Implementado e validado em `OfflineRosterV16.cs:29-37`, `OfflineWorldFactory.cs:98-107` (`OfflineRosterV16Tests`: 19 testes na baseline) | Preservado no código C#; `real-madrid-castilla` limitado a `<= 3 GKs` no catálogo | **Conflito 1:** `OfflineRosterV16.cs:32-33` usa `keepG=3` só em `target==23` na D5 (`1..2G` se `target<23`). Se um clube com `22` atletas reais (`3G + 19 linha`: `penya-deportiva` [#166] e `real-madrid-castilla` [#44]) cair na D5 e sortear `target==22` com `keepG=2`, `22-3=19 < 20` lançaria `ArgumentException`. Mantidos os dados reais e código validado sem alterar lógica C#. |
| Mínimo de elenco V1.1 vs. D5 (`13`) | `OfflineCatalogRules.cs:40` aceita `11..23`; `OfflineRosterV16.cs:29` exige `13..23` na criação do mundo | Preservado conforme Seção 16 (V1.1 continua `11..23`, evitando quebrar testes V1.1 como `OfflineTacticsV16Tests`) | **Conflito 2:** Validador estático V1.1 (`11..23`) difere do piso distrital D5 (`13`); todos os 172 clubes da Espanha possuem `20..23` atletas. |
| Excedente único slot 128 (`OfflineSurplusV16`) | Implementado em `OfflineSurplusV16.cs:16-18`, `OfflineWorldFactory.cs:108-112`, `client/elifoot_flutter/lib/offline_catalog_creator.dart` | Preservado | **Conflito 3:** `OfflineSurplusV16.cs` ordena cortes por `Strength` (`InitialPlayerStrength`) sem consultar `IsStar` ou `RetentionEvidence`. |
| Ranking Opta (`"OPTA"` / `"OPTA_INTERPOLATED"`) | `OfflineCatalogRanking.cs:15` aceita apenas `"OPTA"` e `"OPTA_INTERPOLATED"` como família `"OPTA"` | **Corrigido no catálogo:** 171 clubes alterados para `"source": "OPTA"` | Conflito resolvido sem afrouxar o domínio validado |
| Nacionalidade / país do clube (`fc-andorra`) | `OfflineCatalogV11Loader.cs:65` exige `club.Country == manifest.Country.Code` | Mantido `country: "ES"` em `fc-andorra.json` | **Conflito 4:** Clube andorrano filiado à RFEF exige `"ES"` por restrição estrutural do loader. |
| Comportamento V1.6 (proposta pendente) | Recalibração experimental isolada em `tools/offline_catalog/behavior_v16.py` + fluxo manual (`behavior_approval_v16.py`, `behavior_apply_v16.py`); `OfflineWorldFactory.cs:200` lê `p.Behavior!.Value` | **Não implementado no runtime** (proposta pendente); `behavior: null` e `status: "DRAFT"` mantidos | TM não fornece `fouls` (`total_fouls_conceded`); nenhuma aprovação automática realizada. |
| Evidências de retenção `_pesquisa/.../selecao_jogadores.csv` | `OfflineWorldCreatorService.cs` aceita `OfflineRetentionEvidence` via `OfflineWorldRequest` | CSV completo gerado com colunas V1.6 Seção 11.5 | **Conflito 5:** Loader não lê `_pesquisa` automaticamente; evidências dependem de envio na requisição ou integração futura. |
| Limite de `128` clubes do mundo atual | `OfflineWorldFactory.cs:74-94` mantém `128` clubes (`20+22+22+22+42`) | Preservado sem alterar limite nem fórmulas do motor | Catálogo entrega todos os `172` clubes da Espanha (pronto para futura expansão para 256). |
| Compilação `dotnet test` na árvore atual | Alterações pré-existentes fora de escopo em `src/Elifoot.Domain/AiVoluntaryAuctionEngine.cs` (linhas 63, 85, 112) impedem `dotnet test` local | Código C# fora de escopo preservado intacto | **Bloqueio pré-existente registrado:** `AiVoluntaryAuctionEngine.cs` (`Club.ShortName`, `AiFixedAskingPrice`, `AiAuctionValuation`) impede recompilação da suíte de testes `.NET` nesta árvore; validação do catálogo executada com `artifacts/local-play/api/Elifoot.Api.dll` e suíte Python. |

---

## 3. Distribuição Atualizada do Catálogo Espanha 2026/27

- **Total de clubes:** `172` (`19` Tier 1 novos + `real-madrid` + `22` Tier 2 + `40` Tier 3 + `90` Tier 4; inclui `31` filiais/equipes B/C inventariadas em `metadados.json`).
- **Total de jogadores no catálogo:** `3.905` (`3.882` nos `171` clubes novos + `23` em `real-madrid`).
- **Tamanhos de elenco nos 171 novos clubes:** `20` atletas (`5`), `21` (`7`), `22` (`22`), `23` (`137`).
- **Goleiros nos 171 novos clubes:** `2` GKs (`151` clubes), `3` GKs (`20` clubes), `4` GKs (`0` clubes).
- **Estrelas (`isStar: true`) por tier nos 171 novos clubes:**
  - Tier 1 (`19` novos): `{1: 6, 2: 9, 3: 3, 4: 1}` (`real-madrid`: `6` pré-existentes)
  - Tier 2 (`22`): `{1: 12, 2: 6, 3: 4}`
  - Tier 3 (`40`): `{1: 15, 2: 22, 3: 3}`
  - Tier 4 (`90`): `{0: 7, 1: 68, 2: 15}`
- **Fonte de `historicalRanking` nos 171 novos clubes:** `OPTA` (`171/171`, `0` nulos — todas as equipes cobertas na janela `2026-11-12` ou interpoladas anteriormente conforme metodologia Opta).
- **Táticas (`preferredTactics`) nos 171 novos clubes:** `(home, away)` = `(1, 1): 21`, `(1, 2): 28`, `(2, 1): 14`, `(2, 2): 108`.

---

## 4. Validações Executadas Após a Revisão Final

- **Validador oficial da API (`dotnet D:\elifoot_98\artifacts\local-play\api\Elifoot.Api.dll offline-catalog-validate --CatalogOffline:Root=D:\elifoot_98\catalogos`):**
  - `root_valid: True`, `root_issues: 0`
  - `espanha/2026/manifest.json`: `valid: True`, `ready: False`, `clubs: 172`, `errors: 0`, `warnings: 3911` (`BEHAVIOR: 3905` esperados em `DRAFT`, `COACH: 1`, `TACTICS: 2`, `TACTICS_MINIMUM: 1`, `TEAM_STRENGTH: 1`, `RANKING_RESOLUTION: 1` exclusivamente do `real-madrid`), `sourceHash: 451125F6B2A95255066100AFF44AACB391361A1246281F2FC078C3967F1609D4`.
- **Auditoria Python de invariantes e schemas (`validate_batch_h.py` + `audit_v16_post.py`):**
  - `Identities: total=172, schema_errs=0, boms=0, dup_abbrs={}, low_contrast=[]`
  - `Clubs: total=172, schema_errs=0, boms=0, guardrails=0, squad_out_13_23=[], gk_lt_2=[], beh_missing=[]`
  - `Excluded >= 1500 min: []` (`0` excluídos); apenas `2` retenções onde atleta U21 (`protectedProspect`) superou companheiro da mesma posição com mais minutos (`Buba Sangaré` no Elche; `Jones El-Abdellaoui` no Celta de Vigo).
- **Suíte Python `tools/offline_catalog/tests` (`python -m unittest discover -s D:\elifoot_98\tools\offline_catalog\tests -v`):**
  - `66` testes executados após as correções (`65 ok`, `1 skipped` — `test_real_validator_accepts_draft_then_ready_and_preserves_v10` ignorado por exigir variável/binário configurado de integração local).
- **Pacote ZIP de exportação (`exports/ELIFOOT98_CATALOGO_ESPANHA_2026.zip`):**
  - Regenerado com os arquivos corrigidos e verificado por `testzip() == None` e manifesto SHA-256 atualizado em `exports/EXPORT_MANIFEST_SHA256.txt`.
