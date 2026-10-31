# Matriz de direitos — 013B

Acesso, armazenamento, exibição, redistribuição e direito a um mundo salvo independente são permissões distintas em `CatalogUsageRights`. Uma chave de API não habilita automaticamente nenhuma delas.

| Fonte | Armazenamento | Exibição/comercial | Distribuição/exportação | Atribuição/mídia |
|---|---|---|---|---|
| Wikidata estruturado | CC0 | dados estruturados sob CC0 | CC0 | atribuição de origem exibida; não transfere direitos de logos/fotos |
| API-Football | confirmar retenção pretendida | termos admitem projetos, mas o escopo precisa corresponder ao contrato | revenda de dados é restrita; exportação/perpetuidade exigem revisão | mídia não foi usada |
| Sportmonks | termos tratam cache/armazenamento | produto e contrato devem ser compatíveis | revenda exige autorização; confirmar distribuição pretendida | mídia não foi usada |
| football-data.org | conferir plano e retenção | condições restringem referência após cancelamento | não presumir direito a saves redistribuíveis permanentes | exige atribuição; logos/fotos exigem direitos próprios |
| Opta | contrato não fornecido | não comprovado para o projeto | não comprovado | não foram coletados ranking, logos ou fotos |
| openfootball | declara domínio público | conferir o arquivo efetivamente escolhido | declara domínio público | nenhum pacote foi importado nesta missão |

Fontes: [Wikidata — licença](https://www.wikidata.org/wiki/Wikidata:Licensing), [API-Football — termos](https://www.api-football.com/terms), [Sportmonks — termos](https://www.sportmonks.com/terms-of-service/), [Sportmonks — uso dos dados](https://www.sportmonks.com/integrity-support/), [football-data — condições, itens 9.1 e 9.2](https://www.football-data.org/about), [atribuição football-data](https://www.football-data.org/documentation/faq).

O catálogo permanente recusa fontes com RetainUntil definido, em vez de guardar dados além de um prazo contratual. UseUntil vencido impede novas utilizações. A criação exige exibição, uso comercial e OfflineWorldsAllowed: esse último campo deve representar direito permanente ao save já criado, inclusive após cancelamento. Sem evidência, a permissão deve permanecer falsa. Exportação JSON/CSV de mundos com restrição de WorldExportAllowed retorna 403.

O piloto não importa mídia. Adapters descartam URLs de logos/fotos. Tokens são lidos de variáveis de ambiente indicadas por nome e enviados em headers; nenhum token entra em snapshots, configuração versionada, URLs ou mensagens de erro. As páginas de autenticação e erros do Sportmonks apresentam orientações divergentes sobre header/query; a opção documentada de header está implementada e ainda exige homologação contratada.
