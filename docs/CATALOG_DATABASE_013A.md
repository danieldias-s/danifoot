# Banco do catálogo — 013A

`CatalogDbContext` possui ciclo e conexão separados de `ElifootDbContext`. O prefixo das tabelas é `wc013a_`. O catálogo guarda países, temporadas, fornecedores/licença, ligas, clubes, jogadores, vínculos de elenco, rankings, escopos, snapshots, jobs de sincronização, drafts e a última configuração por proprietário.

Snapshots são imutáveis e carregam payload completo validado, versão, SHA-256, data externa, data de validação e flag IsFixture. As projeções relacionais de clube/liga/jogador/elenco/ranking são indexadas por snapshot e ID externo; a categoria e temporada vêm do escopo do snapshot. Elenco guarda a referência qualificada ao fornecedor do clube, permitindo fornecedor de elenco/ranking diferente.

Índices únicos: escopo, (escopo, versão), (snapshot, ID externo), (snapshot, jogador), (snapshot, clube do ranking), proprietário da última configuração. Índices de leitura: país/categoria/temporada/tipo; próxima tentativa; escopo/data; snapshot/nome; snapshot/clube; snapshot/rank; jobs por escopo/data; drafts por proprietário/validade.

`EnsureCreated` cria apenas bancos novos dedicados. Não há migration no banco da 012D ou alteração de tabelas econômicas/saves existentes. A ativação da funcionalidade recusa um catálogo PostgreSQL sem 013a no nome e recusa compartilhar o mesmo banco de mundos; o banco PostgreSQL de mundos também deve ter 013a no nome. SQLite é usado apenas nos testes portáveis.

Para evoluções posteriores do schema persistido, será necessário acrescentar migrations específicas do catálogo; esta é a primeira versão. Os dumps/volumes não são versionados no Git. O runner usa PostgreSQL 18.4 local e usuário trust restrito ao loopback na instância temporária, conforme os scripts DEV.
