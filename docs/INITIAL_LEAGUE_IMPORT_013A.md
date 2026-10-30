# Carga inicial e fornecedores — 013A

Pesquisa em 30/10/2026. Não foram fornecidas credenciais ou contratos específicos. Nenhum dado real de clubes/atletas/ranking foi importado. A carga DEV contém 8 países, 16 ligas fictícias, 320 equipes (160 por categoria), 5.760 atletas (2.880 por categoria) e 320 posições fictícias de ranking, em 48 snapshots. Existem 20 clubes e 18 atletas por clube por país/categoria. Quantidade de clubes REAIS importados: 0; jogadores REAIS: 0.

Prioridades de descoberta:

| País | Masculino | Feminino | Quantidade/elencos reais |
|---|---|---|---|
| Brasil | campeonato nacional principal | campeonato nacional feminino | verificar por temporada no fornecedor |
| Argentina | campeonato nacional principal | campeonato nacional feminino | verificar cobertura feminina antes de contratar |
| Inglaterra | primeira liga | primeira liga feminina | verificar por temporada no fornecedor |
| Espanha | primeira liga | primeira liga feminina | verificar por temporada no fornecedor |
| Alemanha | primeira liga | primeira liga feminina | verificar por temporada no fornecedor |
| Itália | primeira liga | primeira liga feminina | verificar por temporada no fornecedor |
| França | primeira liga | primeira liga feminina | verificar por temporada no fornecedor |
| Portugal | primeira liga | primeira liga feminina | verificar por temporada no fornecedor |

Não se deduzem quantidades nem disponibilidade de elencos a partir do nome da competição. O manifesto da carga deve registrar temporada, total de equipes, páginas esperadas, IDs e posição de cada atleta; o import valida os valores efetivamente recebidos.

## Candidatos avaliados

API-Football publica [cobertura por competição e temporada](https://www.api-football.com/coverage), incluindo torneios femininos; isso não comprova todos os elencos necessários nos oito países. O [plano gratuito](https://www.api-football.com/) anuncia 100 requisições/dia. A [documentação](https://www.api-football.com/documentation-v3) deve orientar paginação e campos de cobertura. Os [termos](https://www.api-football.com/terms) permitem projetos, incluindo jogos, mas restringem revenda dos dados. Confirmar retenção e uso comercial específico antes da carga; nenhuma contratação foi feita.

Sportmonks anuncia [Starter €29/mês para cinco ligas e 2.000 chamadas por entidade/hora](https://www.sportmonks.com/football-api/plans-pricing/). A [FAQ](https://www.sportmonks.com/faq/) cita a liga feminina inglesa, mas não comprova a profundidade dos oito países. A página de [integridade](https://www.sportmonks.com/integrity-support/) e os [termos](https://www.sportmonks.com/terms-of-service/) tratam armazenamento/cache e produtos comerciais, com restrições à revenda. Conferir ligas, jogadores, posições, histórico, retenção após cancelamento e direitos adicionais no contrato selecionado.

football-data.org publica [cobertura](https://www.football-data.org/coverage) para ligas masculinas dos países prioritários e alguns torneios femininos; não comprova os oito campeonatos femininos. O [plano gratuito](https://www.football-data.org/pricing) lista 12 competições e dez chamadas/minuto; elencos aparecem nos planos com dados detalhados. A [FAQ](https://www.football-data.org/documentation/faq) exige atribuição. Verificar plano, direitos de armazenamento e uso comercial; não foi selecionado como fonte completa.

Preços são os anunciados na data da pesquisa e não constituem contratação. Não se presumiu licença a partir de acesso público. A integração entregue é `ICatalogSource` + adapter HTTPS de JSON normalizado + importação autorizada JSON/CSV. Conversões proprietárias e homologação de cobertura real ficam condicionadas ao fornecedor/contrato escolhido. Ranking real também precisa de fonte compatível com a categoria e licença; a missão usa ranking inventado exclusivamente para testes.
