# Fornecedores reais — 013B

Pesquisa em 30/10/2026. O usuário informou não possuir credenciais/licença de um fornecedor pago. Nenhum serviço foi contratado. Fonte selecionada para o piloto: Wikidata, somente dados estruturados CC0; os adapters comerciais foram testados com respostas sintéticas.

| Fonte | Dados/alcance | Acesso e limites | Situação entregue |
|---|---|---|---|
| Wikidata | identidades, vínculos P54, posições P413; cobertura comunitária desigual | API sem token, User-Agent, maxlag e consultas limitadas | integração real: 2 clubes e 41 atletas; sem ranking oficial |
| API-Football v3 | clubes, temporadas, atletas/estatísticas e tabelas por liga; masculino/feminino dependem da competição | token; gratuito anuncia 100 chamadas/dia; quotas diária/minuto por headers | parser nativo, paginação e mocks; sem chamada autenticada real |
| Sportmonks v3 | equipes por temporada, gênero explícito, elencos históricos e posições | token/plano; quota por entidade; consultar contrato | parser nativo e mocks; sem homologação com credencial |
| football-data.org v4 | equipes, temporadas, tabelas; Team.squad representa elenco atual | token; plano gratuito anuncia 10 chamadas/minuto; profundidade varia por plano | parser nativo; recusa rotular elenco atual como histórico |
| Opta/Stats Perform | ranking mundial independente para masculino e feminino | visualização pública não comprova API gratuita nem direitos de redistribuição | sem extração; importação licenciada JSON/CSV preparada |
| openfootball | resultados/competições em domínio público | arquivos sem token | candidato para resultados autorizados; não fornece a cobertura de elencos exigida |

Fontes primárias: [Wikidata — acesso](https://www.wikidata.org/wiki/Wikidata:Data_access), [API-Football — documentação](https://www.api-football.com/documentation-v3), [Sportmonks — equipes por temporada](https://docs.sportmonks.com/v3/endpoints-and-entities/endpoints/teams/get-teams-by-season-id), [Sportmonks — elenco histórico](https://docs.sportmonks.com/v3/endpoints-and-entities/endpoints/team-squads/get-team-squad-by-team-and-season-id), [football-data — política](https://docs.football-data.org/general/v4/policies.html), [Opta — metodologia](https://theanalyst.com/articles/power-rankings-your-club-ranked), [openfootball](https://github.com/openfootball/football.json).

`NativeFootballCatalogSource` separa clubes, elencos e classificação por CatalogDataType, reutilizando ICatalogSource e CatalogSyncService. `NativeProviderFactory` é compartilhada pela API e CLI. Configurações possuem referências revisadas de país/categoria/liga/temporada; os oito países não são limites da arquitetura. Nenhum código de fornecedor entrou no MatchEngine.
