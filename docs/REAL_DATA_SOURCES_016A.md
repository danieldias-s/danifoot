# Fontes reais — 016A

Referência da missão: 01/11/2026. Implementação e consultas realizadas nessa data, em DEV isolado. Resultado da cobertura atual: **INCOMPLETE**.

## API-Football

A [documentação oficial v3](https://www.api-football.com/documentation_v3) foi consultada antes da implementação. O servidor usa HttpClientFactory e `x-apisports-key` obtido exclusivamente de `APIFOOTBALL_API_KEY`. `/teams?search` descobre candidatos; o administrador revisa equipe, país, categoria e liga antes de registrar o ID. Nenhum ID API-Football foi inventado ou aprovado nesta execução.

Após revisão: `/teams?id`, `/leagues?team&current=true`, validação das datas e temporada efetivamente disponível, depois `/players/squads?team`. `/players` fornece estatísticas, não substitui a confirmação de elenco atual. `/transfers` fica reservado para revisão adicional quando necessária e permitida. Não foi chamado sem necessidade. `/status` informa plano e quota; não é contado como chamada diária faturável pelo fornecedor.

**Sem chave por decisão explícita do usuário**: NOT_CONNECTED, zero chamadas e zero jogadores atuais desse fornecedor. Cobertura de 2026/2026-27 e quota real da conta continuam desconhecidas. Não se presumiu disponibilidade do plano.

## Wikidata

A [interface EntityData e as orientações de acesso](https://www.wikidata.org/wiki/Wikidata:Data_access) fundamentam as chamadas `wiki/Special:EntityData/Q….json` e consultas SPARQL delimitadas. Os seis clubes foram verificados por ID, rótulos/aliases, P17 (país), P31 (tipo) e P641 (futebol). Os vínculos usam afirmações P54 e qualificadores P580/P582; P413 identifica posição. Datas abertas não comprovam vínculo atual.

Foram persistidos 12 recursos reais: 6 entidades e 6 respostas SPARQL. Cinco respostas chegaram ao limite de 500 linhas e foram recusadas como elenco completo. Barcelona Feminino retornou 155 linhas, 126 IDs de pessoas e 113 registros de atletas com nome/posição válidos; todos históricos/não confirmados como atuais. Uma linha SPARQL não equivale necessariamente a um jogador.

Uma consulta exploratória anterior ao importador encontrou maxlag. O bloqueio foi respeitado; a interface documentada EntityData foi usada posteriormente. O orçamento persistido de 12 contabiliza o importador, não o histórico de navegação da pesquisa.

## Catálogo existente

48 snapshots de dados abertos foram copiados de forma somente leitura da base DEV 015B para o catálogo DEV 016A. Nenhum mundo, preset, rascunho ou preferência foi copiado. Fontes originais e licenças foram mantidas. Não se reapresentou dado herdado como nova consulta. IDs e paletas existentes foram preservados.

Evidência: [inventário](evidence-016a/catalog-evidence.json), [mapeamento](CLUB_IDENTITY_MAPPING_016A.md), [licenças](REAL_DATA_LICENSES_016A.md).
