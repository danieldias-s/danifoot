# Importação 013B

Os adapters reutilizam o catálogo e a sincronização 013A. Não há compra ou contratação automática.

## Usar apenas o catálogo real já salvo

```powershell
cd D:\elifoot_98_013A
.\scripts\run-real-data-013b.ps1
# API 127.0.0.1:5134; PostgreSQL exclusivo 55434
```

Para registrar a fonte aberta na API, use `-WikidataPilot`; a seleção com UseSavedData=false tenta atualizar conforme o cache. O manifesto inclui somente duas equipes revisadas da Espanha e não contém tokens. `-Fixtures` é opcional e mistura fixtures no catálogo: não o use para avaliar a cobertura real exclusiva. O banco de testes é separado e usa fixtures por definição.

## CLI explícita

```powershell
# Inspeção de rede sem gravar no banco:
dotnet run --project tools/catalog-013b/importer -c Release -- inspect tools/catalog-013b/wikidata-pilot.json
# Importação no banco exclusivo (a conexão fica no ambiente, não em argumentos):
$env:ELIFOOT_013B_CATALOG_PG = 'Host=127.0.0.1;Port=55434;Database=elifoot_013b_catalog;Username=elifoot013b'
dotnet run --project tools/catalog-013b/importer -c Release -- sync tools/catalog-013b/wikidata-pilot.json ELIFOOT_013B_CATALOG_PG
```

`--force` permite vencer o prazo de reconsulta para uma execução controlada; não ignora validação, direitos, maxlag, quota nem rate limit. Antes de uma carga sobre banco existente, faça pg_dump e guarde o backup fora do Git. O piloto 013B possui backups antes/depois em artifacts/013b/backups.

## Adapters comerciais

O template de fornecedores pagos está desautorizado por padrão, sem credenciais e sem IDs de competição presumidos. Configure um manifesto revisado, use `-ProviderManifest caminho.json` e indique a variável de ambiente do token. Sem esse parâmetro nem -WikidataPilot, o runner limpa o manifesto que ele próprio poderia ter deixado no ambiente. Não coloque o valor do token em JSON, CLI, Git ou Flutter. Direitos de acesso, retenção, exibição, uso comercial, save offline e exportação são independentes e devem refletir o contrato.

API-Football: league/season + country name e nome da liga revisados; elencos usam todas as páginas de players por equipe/liga/temporada. Sportmonks: season ID e country ID próprios do fornecedor, além do gênero retornado. football-data.org: competição/ano e checagem explícita de currentSeason antes de Team.squad. Descubra/revise IDs de cada temporada com acesso autorizado, inclusive os campeonatos femininos; não reutilize IDs masculinos por conveniência.

Campos CSV adicionais de atletas reais: IdentityOrigin, CanonicalPlayerId opcional revisado, StrengthMethod, StrengthConfidence, Rating, RatingScale, Minutes, Appearances, Goals, Assists, MembershipStart, MembershipEnd. Origem/estimativa são obrigatórias para dados reais. Campos CSV adicionais de ranking: Score, Method, Confidence, ComparableGroup, Origin. Dados completos e direitos podem ser importados pelo JSON CatalogImportPackage.

As rotas administrativas mantêm `/api/catalog-013a` e exigem token local/loopback. `POST /map-club` registra evidência de conciliação. `POST /api/world-creator/coverage` usa a mesma identidade LocalDev do criador. Veja os documentos 013B para cobertura real, restrições e pontos ainda não homologados.
