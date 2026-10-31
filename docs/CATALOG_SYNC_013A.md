# Sincronização — 013A

Pipeline: FETCH → VALIDATE → STAGE em transação → COMMIT → ACTIVATE. O snapshot ativo só muda dentro da transação que persiste o payload e as projeções. Atualização inválida ou erro de banco mantém o snapshot anterior; um teste injeta erro SQL depois do stage e verifica o rollback completo.

Validação: direitos declarados de armazenamento/uso comercial, referência de licença, país, categoria, temporada, tipo de dado, versão, data UTC, lista não vazia, quantidade esperada, total de páginas, duplicações e elencos. Dados mais antigos e reduções inesperadas são rejeitados. Redução legítima exige `AllowCountDecrease=true` no import autorizado. Reutilizar uma versão com conteúdo diferente é erro; importar o mesmo conteúdo retorna o mesmo snapshot.

Clubes devem existir no escopo compatível antes de importar seus elencos/rankings. Não se aceita referência desconhecida para outro país/categoria/temporada/liga. Clube pode ter fornecedor diferente do elenco/ranking. Metadados compartilhados de países/fornecedores usam upsert para suportar sincronizações de escopos diferentes.

`ICatalogSource` é o contrato de integração. `AuthorizedJsonCatalogSource` consome manifesto JSON normalizado por HTTPS e só opera com autorizações configuradas. Não interpreta diretamente o formato proprietário de API-Football/Sportmonks; um conversor autorizado deverá produzir o contrato `CatalogImportPackage`. Tokens ficam na configuração/ambiente, não no snapshot, job ou mensagem de erro.

Importação local: POST `/api/catalog-013a/import` (JSON) e `/import-csv` (manifesto + CSV). As rotas só existem quando habilitadas e exigem loopback e `X-Catalog-Import-Token`, configurado fora do repositório. CSV usa parsing com aspas, cabeçalhos por tipo e normalização de posição. Os exemplos do contrato estão em `WorldCreatorDtos.cs` e as fixtures em `CatalogFixtureFactory.cs`.

Agendamento: `CatalogSyncService.RunDueAsync` ou POST `/api/catalog-013a/run-due`, com o mesmo token local. Não há cron, timer ou serviço permanente instalado. Frequências configuráveis em minutos: Countries=43200, Leagues=10080, Clubs=1440, Squads=720, Rankings=10080. O teste do runner controlado comprova deduplicação, intervalo, timeout e rate limit. A ativação futura por agendador externo deve chamar o runner, respeitando a licença.
