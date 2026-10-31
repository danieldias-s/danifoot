# Fontes abertas avaliadas — 013B

Consulta esportiva de referência: 2026-10-30. Datas exatas de coleta e hashes estão nos manifestos; pushed_at do repositório não certifica a atualização de cada dado. Conteúdo público não implica permissão geral de reutilização.

| Repositório OpenFootball | Licença efetiva | Último push observado | Revisão fixada |
|---|---|---|---|
| clubs | CC0-1.0 | 2025-01-03T18:08:49Z | ae3800227c449447b3a337fc0aac79a8f02f4c8b |
| football.json | CC0-1.0 | 2026-09-22T09:33:50Z | e6744429ee395bc86f247348c6184bb08d4eb361 |
| world | CC0-1.0 | 2026-09-23T11:34:28Z | 42df8203e73e922cdfa36104e3acee3aea1f2bc0 |
| south-america | CC0-1.0 | 2026-10-20T11:43:55Z | f51b2dcf5e0fab9f0abd5206ab8c24a413e67277 |
| europe | CC0-1.0 | 2026-10-15T14:36:29Z | 63df627f3c87ce71708f016522ea236cf74332c3 |
| players | CC0-1.0 | 2024-06-17T10:39:47Z | 125d20f7cc06cac7e758b40df535a7695632680a |

As licenças CC0 das revisões fixadas foram verificadas no arquivo LICENSE de cada repositório, além da identificação da API GitHub. Uso comercial e armazenamento dos dados estruturados importados: permitidos, sem retenção temporal contratual. Escudos, fotos e marcas gráficas não foram importados. Fontes: [clubs](https://github.com/openfootball/clubs), [football.json](https://github.com/openfootball/football.json), [world](https://github.com/openfootball/world), [south-america](https://github.com/openfootball/south-america), [europe](https://github.com/openfootball/europe), [players](https://github.com/openfootball/players).

| Fonte | Países/categoria/temporada avaliados | Clubes e atletas encontrados | Decisão |
|---|---|---|---|
| clubs | Oito arquivos nacionais; diretório sem temporada certificada | 973 entradas de diretório antes de cruzamentos; variantes e clubes antigos; sem elencos | Usado para aliases e candidatos, com status não verificado |
| football.json | BR, AR, EN, ES, DE, IT, FR, PT; masculino; 2025, 2025-26, 2026, 2026-27 conforme arquivos disponíveis | 339 identidades após deduplicação; 158 com temporada 2026; 4819 resultados jogados; sem atletas | Importado para clubes e Elo estimado |
| world | Ásia, África e América do Norte no recorte recente examinado | Não acrescenta clubes dos oito países nem elencos no recorte | Avaliado; não importado |
| south-america | BR/AR; masculino; Brasil 2026, Argentina 2025 nos arquivos disponíveis | Clubes/partidas em texto; sem vínculos de atletas; sobreposição com JSON | Avaliado; evitar dupla importação |
| europe | França/Portugal e outros países europeus, masculino; 2025-26/2026-27 disponíveis | Clubes/partidas em texto; sem vínculos; sobreposição com JSON | Avaliado; JSON usado como representação normalizada |
| players | Listas internacionais por nacionalidade/posição/nascimento; datas variáveis | Pessoas reais, mas sem vínculo clube-temporada confiável | Não atribuir jogadores a clubes a partir dessa lista |
| awesome-football | Diretório de links para bases heterogêneas; sem temporada ou contagem própria de clubes | Não é uma base uniforme de elencos; nenhuma licença de dados identificada na árvore consultada | Usado só na pesquisa; licença de um link não cobre seus destinos |
| Wikidata | Oito países, feminino separado por Q51481377/Q28140340; P17/P118; vínculos P54/P580/P582 e categoria P21 | 407 registros femininos após exclusão de seleções e liga norte-irlandesa; 3101 vínculos aceitos, 3079 pessoas distintas | Dados estruturados CC0 importados; temporalidade e categoria não presumidas |

Wikidata: [política de licenciamento](https://www.wikidata.org/wiki/Wikidata:Licensing), [acesso aos dados](https://www.wikidata.org/wiki/Wikidata:Data_access). Apenas dados estruturados CC0 foram usados. Consultas: descoberta masculina por país LIMIT 900 (saturada em BR/GB/ES/DE/IT/FR, portanto parcial); descoberta feminina limitada; vínculos por lotes de até 16 equipes/LIMIT 2400; categoria por até 100 pessoas/LIMIT 300. Respostas de vínculos no limite são recusadas. QIDs, declarações, datas e hashes ficam conservados. Associação de liga sem datas não prova temporada atual.

Outras fontes: [EWF Database](https://github.com/probjects/ewf-database) oferece partidas das duas primeiras divisões femininas inglesas desde 2011/2014, atualização ao fim de temporada, sob CC BY-SA 4.0; permite uso comercial com atribuição e compartilhamento pela mesma licença. Não foi importada nesta entrega porque o fluxo de exportação não implementa ainda esses avisos/obrigações para derivados. Não há elencos individuais nesses três datasets. [Hudl/StatsBomb Open Data](https://github.com/hudl/open-data) tem jogos/lineups/eventos de competições selecionadas, inclusive femininas, e licença própria em LICENSE.pdf; não foi aprovada como autorização permissiva para redistribuir catálogo comercial e não foi importada. [awesome-football](https://github.com/openfootball/awesome-football) também aponta scrapers; licença do código de um scraper não autoriza automaticamente os dados do site.

Nenhuma API paga foi contratada, ativada ou usada. Os adapters comerciais anteriores permanecem preparados e desativados. Nenhum ranking oficial mundial foi encontrado com licença e cobertura suficientes para importação; feminino fica não classificado e masculino usa somente Elo local identificado.

Reprodução no worktree isolado: node tools/catalog-013b/bootstrap-coverage.mjs; collect-coverage.mjs; prepare-coverage.mjs; collect-memberships.mjs; build-coverage-bundle.mjs (primeira passagem de descoberta); collect-player-genders.mjs; build-coverage-bundle.mjs (final). O importer coverage recusa bundle sem conferência de categoria. Scripts e manifestos são locais, sem agendamento automático; arquivos crus ficam em artifacts/013b-coverage/sources. Importar com a conexão isolada ELIFOOT_013B_CATALOG_PG e depois executar scripts/verify-coverage-013b.ps1. Correções de contagem revisadas exigem flag específica; ausência/falha de lotes não deve ativar nova versão.
