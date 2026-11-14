# Real Madrid — auditoria do elenco do catálogo V1.1

**Status da auditoria: PARTIAL.** Em 12/11/2026, os 23 jogadores cadastrados foram identificados no elenco oficial de 2026/27. O recorte do elenco, as duas classificações de posição que variam por fonte, os 23 comportamentos, a força coletiva, o ranking ou prioridade e as táticas do treinador ainda exigem revisão. O clube e o manifest continuam DRAFT.

Branch `main`, HEAD `43b99bb98e63848fbd57aadf99ef6b04bc7afc30`. O ano abaixo vem do JSON. A temporada vem da lista publicada pelo clube e da inscrição UEFA. A data da publicação da lista, 10/08/2026, serve como referência de comparação. A data de corte do catálogo ainda precisa ser definida.

## Arquivos e identidade

| Campo | Valor no catálogo e conclusão da auditoria |
|---|---|
| JSON anual | `D:/elifoot_98/catalogos/espanha/2026/clubes/real-madrid.json` |
| País, ano, versão | Espanha (`ES`), `2026`, schema `1.1` |
| `catalogClubId` | `real-madrid` |
| Manifest | `D:/elifoot_98/catalogos/espanha/2026/manifest.json`, ID `es-2026-principal`, com a referência `clubes/real-madrid.json` |
| Status | Clube e manifest `DRAFT` |
| Identidade compartilhada | `D:/elifoot_98/catalogos/identidades/real-madrid/identidade.json`. Mesmo ID, nome e nome curto `Real Madrid`, sigla `RMA`. |
| Escudo | `crestFile: null`. A identidade local não referencia imagem. Escudo opcional no schema. |
| Duas cores | `#FFFFFF` e `#111111`. Ambas têm formato HEX válido. O par é o valor cadastrado e a escolha do tom secundário ainda precisa de confirmação do proprietário. |
| Força coletiva | `teamStrength: null`. Pendente valor inteiro de 1 a 20 baseado no critério do catálogo. |
| Ranking e desempate | `historicalRanking: null` e `tieBreakPriority: null`. Para READY, precisa de ranking válido com nota, fonte e data do ano cadastrado, ou prioridade explícita. |
| Estádio | `stadium: null`, campo opcional. O estádio identificado é Santiago Bernabéu. O clube informa capacidade de 81.000 espectadores em sua página de segurança. Confirmar a capacidade aplicável ao recorte se for cadastrar. |
| Treinador | `coach: null`. José Mourinho, português (`PT`), confirmado em fontes oficiais para 2026/27. A inclusão no JSON e as preferências seguem pendentes. |

A UEFA informa a 2ª posição no seu ranking de coeficientes ao fim de 2025/26. Esse dado fica como referência de pesquisa em sua própria escala. O catálogo ainda precisa da escolha da escala e da nota datada ou de uma prioridade manual explícita. [UEFA, perfil do clube para 2026/27](https://www.uefa.com/uefachampionsleague/news/02a9-2182ee1e8646-da64a5fc6ba9-1000--real-madrid-in-the-2026-27-champions-league-what-are-their-/)

Fontes da identidade e do estádio. [História do Santiago Bernabéu](https://www.realmadrid.com/en-US/the-club/history/stadium/santiago-bernabeu), [capacidade publicada pelo clube](https://www.realmadrid.com/en-US/the-club/transparency/security), [história do escudo](https://www.realmadrid.com/en-US/news/club/latest-news/se-cumplen-106-anos-desde-que-el-club-recibio-el-titulo-de-real-29-06-2026).

## Os 23 jogadores cadastrados

Cada nome e nacionalidade foi confrontado com a inscrição UEFA de 2026/27. Inglaterra (`ENG` na competição) corresponde a `GB` na codificação ISO aceita pelo catálogo. Os nomes, posições, códigos, estrelas e comportamentos abaixo reproduzem o JSON local. A coluna status descreve a auditoria de cada atleta. [Inscrição oficial UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid//squad/), [lista oficial de camisas 2026/27](https://www.realmadrid.com/en-US/news/football/first-team/latest-news/dorsales-del-real-madrid-para-la-temporada-2026-27-10-08-2026).

| Nº | ID | Nome | Posição | Nacionalidade | Estrela | Comportamento | Status |
|---:|---|---|:---:|:---:|:---:|:---:|---|
| 1 | `real-madrid-2026-001` | Thibaut Courtois | G | BE | Não | `null` | Disciplina pendente |
| 2 | `real-madrid-2026-002` | Andriy Lunin | G | UA | Não | `null` | Disciplina pendente |
| 3 | `real-madrid-2026-003` | Dean Huijsen | D | ES | Não | `null` | Disciplina pendente |
| 4 | `real-madrid-2026-004` | Ibrahima Konaté | D | FR | Não | `null` | Disciplina pendente |
| 5 | `real-madrid-2026-005` | Éder Militão | D | BR | Não | `null` | Disciplina pendente |
| 6 | `real-madrid-2026-006` | Trent Alexander-Arnold | D | GB | Não | `null` | Disciplina pendente |
| 7 | `real-madrid-2026-007` | Marc Cucurella | D | ES | Não | `null` | Disciplina pendente |
| 8 | `real-madrid-2026-008` | Álvaro Carreras | D | ES | Não | `null` | Disciplina pendente |
| 9 | `real-madrid-2026-009` | Antonio Rüdiger | D | DE | Não | `null` | Disciplina pendente |
| 10 | `real-madrid-2026-010` | Jude Bellingham | M | GB | Sim | `null` | Disciplina pendente |
| 11 | `real-madrid-2026-011` | Eduardo Camavinga | M | FR | Não | `null` | Disciplina pendente |
| 12 | `real-madrid-2026-012` | Federico Valverde | M | UY | Sim | `null` | Disciplina pendente |
| 13 | `real-madrid-2026-013` | Aurélien Tchouaméni | M | FR | Sim | `null` | Disciplina pendente |
| 14 | `real-madrid-2026-014` | Arda Güler | M | TR | Sim | `null` | Disciplina pendente |
| 15 | `real-madrid-2026-015` | Bernardo Silva | M | PT | Não | `null` | Disciplina pendente |
| 16 | `real-madrid-2026-016` | Vinícius Júnior | A | BR | Sim | `null` | Disciplina pendente |
| 17 | `real-madrid-2026-017` | Endrick | A | BR | Não | `null` | Disciplina pendente |
| 18 | `real-madrid-2026-018` | Kylian Mbappé | A | FR | Sim | `null` | Disciplina pendente |
| 19 | `real-madrid-2026-019` | Rodrygo | A | BR | Não | `null` | Disciplina pendente |
| 20 | `real-madrid-2026-020` | Brahim Díaz | A | MA | Não | `null` | Disciplina e posição pendentes |
| 21 | `real-madrid-2026-021` | Yan Diomande | A | CI | Não | `null` | Disciplina e posição pendentes |
| 22 | `real-madrid-2026-022` | Raúl Asencio | D | ES | Não | `null` | Disciplina pendente |
| 23 | `real-madrid-2026-023` | Carlos Espí | A | ES | Não | `null` | Disciplina pendente |

G = goleiro, D = defensor, M = meio-campo, A = atacante. Contagem local de **2 G, 8 D, 6 M e 7 A**. Nenhum ID ou nome duplicado, inclusive após normalizar maiúsculas, espaços e acentos. Há **6 estrelas**, quatro em M e duas em A. Todas respeitam a regra posicional. A escolha dos seis destaques é de gameplay e segue sujeita a revisão. Os 23 comportamentos estão pendentes, com `null`.

Brahim Díaz está como atacante no JSON, na lista do clube e na UEFA, enquanto a ficha de estatísticas da LaLiga o agrupa como meio-campista. Yan Diomande está como atacante no JSON, na lista do clube e na LaLiga, enquanto a UEFA o agrupa como meio-campista. Documentar o critério de posição primária do jogo antes de incluí-los nas coortes de comparação. [LaLiga, estatísticas do Real Madrid](https://www.laliga.com/en-GB/clubs/real-madrid/stats), [UEFA, Brahim Díaz](https://www.uefa.com/uefachampionsleague/clubs/players/250088039--brahim-diaz/), [UEFA, Yan Diomande](https://www.uefa.com/uefachampionsleague/clubs/players/250220373--yan-diomande/).

A lista oficial de camisas tem **26 nomes**. Ferland Mendy, Denzel Dumfries e Thiago Pitarch aparecem na lista e ficaram fora deste recorte. O schema limita o JSON anual a 23 atletas. Para fechar a seleção, documentar o motivo do recorte e manter separadamente a evidência dos atletas que ficaram fora. O clube publicou os vínculos de Konaté, Cucurella, Bernardo Silva, Diomande e Carlos Espí em sua notícia sobre as estreias oficiais, junto com Dumfries. [Lista oficial de camisas](https://www.realmadrid.com/en-US/news/football/first-team/latest-news/dorsales-del-real-madrid-para-la-temporada-2026-27-10-08-2026), [estreias oficiais publicadas pelo clube](https://www.realmadrid.com/es-ES/noticias/futbol/primer-equipo/actualidad/debut-en-partido-oficial-de-los-seis-nuevos-fichajes-del-real-madrid-22-08-2026/).

Códigos de comportamento do jogo. `0` Fair Play, `1` Cordeirinho, `2` Cavalheiro, `3` Caneleiro, `4` Caceteiro, `5` Sarrafeiro. O valor `null` significa pendência.

## Pesquisa disciplinar dos 23

Para **cada ID da tabela**, levantar minutos, faltas **cometidas**, amarelos, vermelhos, segunda amarela, contexto das expulsões, histórico de sanções e duelos quando disponíveis. Registrar também partidas cobertas, equipe no período, competição, temporada, data de corte, data de coleta, fonte e URL ou referência. O mesmo checklist se aplica aos 23 atletas. Esta auditoria identificou as fontes e não coletou totais para classificação.

- Usar LaLiga 2026/27 como série doméstica inicial. A página do clube na liga reúne minutos, disciplina e duelos. Conferir a legenda da edição consultada, sobretudo faltas cometidas versus sofridas e a contabilização da segunda amarela. Salvar o recorte e confirmar números ambíguos nas súmulas. [Estatísticas do Real Madrid na LaLiga](https://www.laliga.com/en-GB/clubs/real-madrid/stats)
- Usar os perfis por atleta e súmulas da Champions 2026/27 como série europeia separada. Os perfis são acessíveis pelo elenco UEFA. Os agregados do clube servem para conferência e não identificam o total individual. [Elenco e perfis UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid//squad/), [estatísticas UEFA do clube](https://pt.uefa.com/uefachampionsleague/clubs/50051--real-madrid/statistics/)
- Para Courtois e Lunin, conferir minutos de goleiro e distinguir ausência de dados de zero observado. Para Konaté, Cucurella, Bernardo Silva, Diomande e Carlos Espí, registrar o trecho de vínculo com o Real Madrid e guardar séries de clubes anteriores em separado. Para Endrick e Rodrygo, conferir jogos e minutos nas competições escolhidas. Para Brahim e Diomande, resolver a posição do catálogo antes da comparação. Os demais atletas também precisam de totais e fontes por ID.
- Guardar duelos disputados e ganhos quando a fonte distinguir os dois. A versão experimental V16 usa minutos, faltas, amarelos e vermelhos. Duelos e decisões disciplinares complementam a revisão humana. Um dado ausente fica `null`. O zero indica evento ausente em uma amostra conhecida.
- O período 2026/27 ainda está em curso na data da auditoria. Definir um corte explícito e usar o mesmo período na coorte. Se a equipe preferir a temporada completa 2025/26 como base histórica, registrar esse período em separado com clube e competições correspondentes. Comparar só amostras compatíveis por posição e recorte. Para as metas do classificador, justificar também `initialDivision` e `comparisonGroup`, que dependem do cenário de classificação escolhido.

## Treinador e táticas

O Real Madrid anunciou José Mourinho em 11/06/2026. A inscrição da UEFA de 2026/27 confirma José Mourinho e nacionalidade portuguesa. No JSON o objeto está nulo. Faltam o cadastro de ID estável, nome, nacionalidade e a decisão sobre as formações que podem ser representadas no jogo. [Anúncio oficial](https://www.realmadrid.com/en-US/news/club/latest-news/comunicado-oficial-mourinho-11-06-2026), [inscrição UEFA](https://www.uefa.com/uefachampionsleague/clubs/50051--real-madrid//squad/).

| Contexto | Evidência da escalação inicial | Situação das preferências |
|---|---|---|
| Casa | 4-2-3-1 diante do [Rayo Vallecano, 12/09/2026](https://www.laliga.com/partido/temporada-2026-2027-laliga-ea-sports-real-madrid-rayo-vallecano-5) e do [Sevilla, 18/10/2026](https://www.laliga.com/es-US/partido/temporada-2026-2027-laliga-ea-sports-real-madrid-sevilla-fc-9). | Formação observada em mais de um jogo. Primeira preferência nativa pendente pela incompatibilidade de formato. Segunda preferência pendente de evidência. |
| Fora | 4-2-3-1 diante do [Espanyol, 22/08/2026](https://www.laliga.com/es-BR/partido/temporada-2026-2027-laliga-ea-sports-rcd-espanyol-de-barcelona-real-madrid-2) e do [Betis, 04/09/2026](https://www.laliga.com/es-US/partido/temporada-2026-2027-laliga-ea-sports-real-betis-real-madrid-4). | Formação observada em mais de um jogo. Primeira preferência nativa pendente pela incompatibilidade de formato. Segunda preferência pendente de evidência. |

As súmulas documentam as formações de partida. A ordem das preferências precisa de um critério de pesquisa por mando. O formato V1.1 aceita `coach: null` em DRAFT ou um treinador com `preferredTactics.home: []` e `away: []`. READY exige treinador preenchido e pelo menos **uma tática válida no total**, com **0 a 2 por contexto** e sem repetição dentro de cada contexto. É possível deixar um mando desconhecido em READY quando o outro tem tática válida. Se os dois forem desconhecidos, o clube fica DRAFT.

O schema e o motor exigem **três blocos** D-M-A que somem dez jogadores de linha. O 4-2-3-1 observado tem quatro blocos e não pode ser gravado literalmente. Uma conversão como 4-5-1 perderia a distinção entre volantes e meias ofensivos, e precisaria de critério explícito antes do cadastro. Nenhuma conversão ou formação adicional foi atribuída. [Schema do clube](../catalogos/_schemas/clube-1.1.schema.json), [validador do catálogo](../src/Elifoot.Domain/Catalog/OfflineCatalogRules.cs), [validador do motor](../src/Elifoot.Domain/TacticsAndSquadRules.cs).

## Fluxo V16 preparado para uso posterior

Pasta de pesquisa proposta `D:/elifoot_98/catalogos/_pesquisa/comportamento/espanha/2026/real-madrid/`. Reservar `estatisticas.json`, `proposta-v16.json`, `evidencias.json` e `revisao.json` para as etapas futuras. Somente este relatório foi criado. O histórico confirmado da CLI fica em `catalogos/_pesquisa/comportamento/v16/history.json`.

1. **Estatísticas:** lista JSON com `playerCatalogId` de cada atleta, `position`, `initialDivision`, `comparisonGroup`, `baseBehavior`, `minutes`, `fouls`, `yellowCards` e `redCards`. Para o catálogo atual, o valor-base é `null` para os 23. Duelos, recortes e observações podem ficar na ficha auxiliar de pesquisa. O mínimo experimental é 900 minutos com as quatro métricas preenchidas. Evidência insuficiente exige revisão.
2. **Proposta:** `behavior_v16.py` emite estatísticas, índice, percentil, versão, configuração e sugestão. A sugestão experimental não é aprovação. Guardar os bytes do relatório para vincular o SHA-256.
3. **Evidências:** documento `behavior-approval-1.0` com `proposal.path` relativo a `catalogos` e `proposal.sha256`. Em `players[]`, incluir `catalogId: "es-2026-principal"`, `clubId: "real-madrid"`, `country: "ES"`, `year: 2026`, `target: "espanha/2026/clubes/real-madrid.json"`, `playerId`, `position` e `sources[]` com `name`, `reference`, `referenceDate` e `collectedAt`. Registrar `sourceConflict` quando houver divergência. Para pesquisa manual sem estatísticas, usar `proposal: null` e preservar as fontes disponíveis.
4. **Revisão:** `revisao.json` contém `version: "behavior-approval-1.0"` e `selected[]`, com um registro para aprovação individual ou vários para lote. Cada entrada usa `suggestionId`, `action` e `reason`. A ação `MANUAL` inclui `behavior` 0..5. As demais ações são `ACCEPT`, `REJECT` e `DEFER`. O lote inclui apenas os IDs selecionados. O operador é autorizado pela política privada local `D:/elifoot_98/behavior-operators.local.json`. A confiança calibrada está indisponível no classificador V16 e é registrada como `null`.
5. **Aplicação:** utilizar os `decisionId` das decisões efetivamente aprovadas. O dry-run não grava o clube. Aplicação confirmada exige validador .NET, verifica os hashes e preserva o status do catálogo. A aprovação e a aplicação são operações separadas. [Contrato completo e exemplos](../docs/catalogos/ELIFOOT98_V16_APROVACAO_PROVENIENCIA.md)

Comandos para a etapa futura, a partir de `D:/elifoot_98`. Substituir o alias pelo operador autorizado e os IDs ilustrativos pelos retornados na revisão confirmada. Os arquivos de pesquisa ainda precisam ser produzidos. Nesta auditoria foram executados somente o validador abaixo e a ajuda das CLIs.

```powershell
$py = 'C:/Users/Daniel/AppData/Local/Python/pythoncore-3.14-64/python.exe'
$root = 'D:/elifoot_98/catalogos'
$folder = '_pesquisa/comportamento/espanha/2026/real-madrid'
$classifier = 'D:/elifoot_98/tools/offline_catalog/behavior_v16.py'
$approval = 'D:/elifoot_98/tools/offline_catalog/behavior_approval_v16.py'
$dll = Join-Path $env:TEMP 'elifoot-v16-build/bin/Elifoot.Api/release/Elifoot.Api.dll'
$operator = 'ALIAS_AUTORIZADO'
$evidence = Join-Path $root "$folder/evidencias.json"
$review = Join-Path $root "$folder/revisao.json"
$approvalIds = @('--approval-id', 'DECISION_ID_1', '--approval-id', 'DECISION_ID_2')

# Proposta experimental e vínculo do relatório com as evidências
& $py -B $classifier --root $root --input (Join-Path $root "$folder/estatisticas.json") --output "$folder/proposta-v16.json"
(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root "$folder/proposta-v16.json")).Hash.ToLowerInvariant()
& $py -B $approval --root $root --operator $operator register --input $evidence
& $py -B $approval --root $root list

# Revisão individual ou em lote, conforme a lista selected do arquivo
& $py -B $approval --root $root --operator $operator review --input $review
& $py -B $approval --root $root --operator $operator review --input $review --confirm
& $py -B $approval --root $root history

# Prever e depois aplicar os IDs aprovados
& $py -B $approval --root $root --operator $operator apply @approvalIds --dry-run
& $py -B $approval --root $root --operator $operator apply @approvalIds --confirm --validator-dll $dll

# Validação offline do catálogo
dotnet $dll offline-catalog-validate "--CatalogOffline:Root=$root"
```

## Validação e pendências de READY

O validador local `offline-catalog-validate` terminou com código **0**. A raiz contém **7 catálogos**. Espanha 2026 está estruturalmente válida, com `ready: false`, **0 erros e 29 avisos**. A distribuição dos avisos foi 23 `BEHAVIOR`, 1 `COACH`, 2 `TACTICS`, 1 `TACTICS_MINIMUM`, 1 `TEAM_STRENGTH` e 1 `RANKING_RESOLUTION`.

1. Definir a data de corte e justificar o recorte de 23 dos 26 nomes publicados. Conferir a posição primária de Brahim e Diomande e os seis destaques marcados.
2. Levantar e revisar evidências disciplinares dos 23. Em etapa autorizada, aprovar e aplicar os comportamentos individualmente ou em lote.
3. Definir a força coletiva e cadastrar ranking válido ou prioridade de desempate.
4. Cadastrar treinador e pelo menos uma tática nativa sustentada por evidência, ou resolver explicitamente como representar o 4-2-3-1 verificado. O escudo e o estádio são opcionais.
5. Depois de preencher e validar, o proprietário pode decidir pela promoção explícita do clube e do manifest, se todos os itens estiverem satisfeitos. READY do clube não equivale a um mundo completo.

Somente a auditoria e este Markdown foram produzidos. Nenhuma alteração de JSON, comportamento, schema, jogo ou save. Nenhum mundo, commit ou push.
