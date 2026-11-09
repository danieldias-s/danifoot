# Validar e reindexar o catálogo

Na raiz do projeto, valide os arquivos antes de atualizar o PostgreSQL.

```powershell
dotnet run --project src/Elifoot.Api --no-launch-profile -- catalog-validate
```

Com a configuração habitual dos bancos do catálogo e de mundos no ambiente local, atualize o índice.

```powershell
dotnet run --project src/Elifoot.Api --no-launch-profile -- catalog-reindex
```

A configuração `CatalogLocal:Root` pode apontar para outra pasta que contenha `paises`. A raiz padrão é `catalogo`. Para sincronizar na partida da API, configure `CatalogLocal:ReindexOnStartup=true`.

Os comandos retornam o número de arquivos, países, versões e jogadores, os anos encontrados e a lista de erros por arquivo. `playersInFiles` conta os jogadores nos registros válidos. `playersImported` conta os jogadores do índice aceito na reindexação. O identificador da versão inclui um SHA-256 calculado sobre os caminhos e conteúdos dos JSON em ordem estável. O mesmo conjunto de arquivos mantém o mesmo hash.

Erros de leitura, estrutura, campos, cores, posições ou identidades duplicadas impedem a publicação do índice inteiro. O processo retorna código 2 para uma validação inválida. Depois de corrigir os arquivos apontados, execute os comandos novamente. O relatório da tentativa de reindexação fica registrado no banco do catálogo.

A reindexação usa o conteúdo atual da pasta `paises`. Arquivos removidos deixam de aparecer em novas montagens quando a próxima reindexação válida é publicada. A ausência da pasta `paises` conta como erro de validação.
