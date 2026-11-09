# Catálogo histórico local

O catálogo canônico fica em `catalogo/paises/<PAÍS>/<ANO>/times/<arquivo>.json`. Cada arquivo descreve uma versão de um clube em uma categoria. A identidade usa código de país, ano, ID do clube e categoria. O identificador de cada atleta inclui a versão do clube.

A API .NET lê os arquivos no comando administrativo, valida o conjunto e publica o índice ativo no banco de catálogo. O hash SHA-256 cobre os caminhos relativos e o conteúdo dos arquivos. Uma validação com erros mantém a versão ativa anterior. O banco de mundos continua separado do banco de catálogo.

O Criador consulta a API para listar os países com clubes na categoria, consultar a cobertura anual dos países escolhidos e carregar as variantes dos anos marcados. A leitura usa o índice ativo. Ao confirmar, o jogo guarda a montagem, os elencos, as referências do índice e os anos escolhidos no próprio save.

## Comandos locais

Execute na raiz da worktree. Use `CatalogLocal__Root` para configurar outra pasta e mantenha a estrutura `paises` dentro dela.

```powershell
dotnet run --project src/Elifoot.Api -- catalog-validate
dotnet run --project src/Elifoot.Api -- catalog-reindex
```

A validação lê os arquivos e imprime o relatório sem acessar o banco. Para reindexar, carregue as mesmas variáveis de conexão que a API de teste usa em `ConnectionStrings__Catalog013A` e `ConnectionStrings__DefaultConnection`. Configure o banco do catálogo conforme o exemplo local da API, com um banco PostgreSQL separado do banco de mundos. O relatório informa o resultado, o hash, os países, os anos, as variantes e os jogadores lidos. Erros de validação retornam código 2.

`CatalogLocal__ReindexOnStartup=true` também permite reindexar durante a inicialização. Ative essa opção com o diretório canônico disponível para a API. Em operação normal, rode o comando de reindexação depois de adicionar ou revisar arquivos.

## Ambientes antigos

Com `Catalog013A__Enabled=true`, o Criador usa o catálogo local por padrão. Nesse modo, a aplicação não registra os provedores externos ou a atualização semanal. A opção `CatalogLocal__UseLegacyProviders=true` é reservada para execução de ferramentas e testes de migração que ainda dependem da integração anterior. Os dados já armazenados continuam no banco de catálogo anterior, e os saves mantêm seus próprios snapshots.

Consulte o [schema do clube](CLUB_JSON_SCHEMA.md), a [validação da importação](CATALOG_IMPORT_VALIDATION.md) e o [fluxo do Criador](MULTI_YEAR_WORLD_CREATION.md).
