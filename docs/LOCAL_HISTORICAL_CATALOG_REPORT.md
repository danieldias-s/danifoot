# Entrega do catálogo histórico local

A implementação está no checkpoint `1348bfb`, a partir de `1921e3c`. A worktree foi criada para esta missão após conferir os ramos e os trabalhos locais. O catálogo canônico está preparado para o cadastro pela equipe. A verificação funcional usou arquivos sintéticos e bancos exclusivos de QA.

| Campo solicitado | Resultado |
| --- | --- |
| STATUS | Implementação concluída e validada localmente. Cadastro dos dados históricos reais pendente da equipe. |
| WORKTREE | `D:/elifoot_98/.worktrees/local-historical-catalog` |
| BRANCH | `codex/local-historical-catalog` |
| CHECKPOINT | `1348bfb` |
| CATALOG_ROOT | `catalogo` na worktree. Aceita configuração por `CatalogLocal__Root`. |
| LOCAL_FILES | Canônico com zero arquivos de clube. QA com 128 arquivos em `artifacts/local-historical-qa/catalogo`. |
| JSON_SCHEMA | `catalogo/modelos/club.schema.json`, modelo em `catalogo/modelos/clube.json`. |
| POSTGRESQL_IMPORT | Reindexação atômica por hash, índice ativo e auditoria. Testada em PostgreSQL local e SQLite. |
| COUNTRIES | Descobertos nos arquivos válidos da categoria. QA com Portugal e Espanha. |
| AVAILABLE_YEARS | Descobertos no índice para os países escolhidos. QA com 1985 e 2025. |
| CLUB_VARIANTS | Identidade por país, ano, clube e categoria. QA com 128 variantes. |
| PLAYERS_IMPORTED | Zero no catálogo canônico. QA com 1.408 jogadores sintéticos dos arquivos. |
| API_FOOTBALL_DISABLED | Provedores externos desregistrados no modo local, inclusive com opções legadas habilitadas no ambiente. |
| AUTOMATIC_EXTERNAL_REFRESH_DISABLED | Worker semanal e consulta de quota desregistrados no modo local. |
| COUNTRY_SELECTION | Até oito países da categoria com clubes válidos. |
| MULTI_YEAR_SELECTION | Nova terceira etapa, cobertura por país e quantidade por ano. Seleção preservada entre etapas. |
| AUTOMATIC_DIVISIONS | Quatro divisões com oito clubes pela posição e pontuação do catálogo. |
| MANUAL_CLUB_SWAP | Filtros por país, ano e nome. Troca de posições validada em teste e navegador. |
| MANUAL_CLUB_REPLACEMENT | Substituição devolve a variante anterior à lista disponível. Oito vagas preservadas. |
| SINGLE_YEAR_NAMES | Nomes sem sufixo, verificados em testes. |
| MULTI_YEAR_NAMES | Ano em todos os nomes e siglas do save. Função central em `HistoricalCatalogPolicy.DisplayName`. |
| DISTRICT_CLUBS | 96 variantes exclusivas, fora das quatro divisões. |
| TOTAL_WORLD_CLUBS | 128 variantes exclusivas. |
| CURRENT_UI | Seleção de anos, divisões e troca conferidas no navegador. |
| CLASSIC_UI | Equipe de 2025 e adversário de 1985 conferidos no jogo. Testes das duas aparências. |
| DOTNET_TESTS | 518 de domínio, 31 de aplicação e 62 de integração aprovados. Teste adicional da montagem com nome de país alterado aprovado. |
| FLUTTER_TESTS | 420 testes aprovados, análise sem apontamentos e build Web aprovado. Após ajustar os textos da prévia histórica, oito testes focados aprovados. |
| BROWSER_TEST | Cadastro da conta de QA, países, dois anos, celular de 390 px, troca entre anos, 96 distritais, prévia real de 128 variantes, confirmação, lobby e entrada no jogo clássico. |
| KNOWN_ISSUES | A pasta canônica aguarda dados reais fornecidos pela equipe. A prévia na porta 5282 usa dados sintéticos de QA. |

## Arquivos e operações

Para abrir a prévia já compilada nesta máquina, execute `INICIAR-CATALOGO-LOCAL.cmd` na raiz desta worktree, pelo Windows. O iniciador verifica o PostgreSQL, reutiliza os bancos de QA existentes, inicia API e Web nas portas 5281 e 5282 e abre o navegador. A verificação inclui o token CSRF, o cookie de formulário e a origem permitida da tela, além do status geral da API. Se o banco estiver se recuperando após um desligamento, aguarde a mensagem `Sistema pronto`. A prévia usa dados sintéticos de QA; os arquivos canônicos continuam em `catalogo`.

Uma API iniciada dentro do ambiente restrito do agente pode responder ao status geral e falhar ao acessar as chaves Data Protection do perfil Windows. Nesse caso, o iniciador informa a falha de autenticação. A correção local validada foi reiniciar somente o processo desta API sob o usuário normal; as chaves, contas, bancos e saves foram preservados. Após o reinício, o endpoint de formulário respondeu com HTTP 200 e a sessão de QA existente voltou a abrir o jogo no navegador.

Pelo PowerShell:

```powershell
& 'D:\elifoot_98\.worktrees\local-historical-catalog\INICIAR-CATALOGO-LOCAL.cmd'
```

Os sete documentos da missão estão em `docs`. A validação e a importação podem ser executadas da raiz da worktree.

```powershell
dotnet run --project src/Elifoot.Api -- catalog-validate
dotnet run --project src/Elifoot.Api -- catalog-reindex
```

A reindexação exige as conexões separadas de catálogo e mundos configuradas no ambiente da API. O relatório JSON inclui versão, hash e erros. Em uma varredura inválida, o índice válido anterior permanece ativo. A sincronização opcional na inicialização usa `CatalogLocal__ReindexOnStartup=true`.

Os dados anteriores do PostgreSQL ficam preservados. Os saves e montagens privadas guardam suas próprias cópias. A criação e a leitura de montagens privadas resolvem os países pelo código do snapshot, permitindo diferenças de nome como “España” e “Espanha”.

## Evidências locais

- [Anos no desktop](../artifacts/local-historical-qa/screenshots/anos-desktop.jpg)
- [Anos no celular](../artifacts/local-historical-qa/screenshots/anos-mobile.jpg)
- [Duas versões nas divisões](../artifacts/local-historical-qa/screenshots/divisoes-desktop.jpg)
- [Filtro de ano na troca](../artifacts/local-historical-qa/screenshots/troca-ano.jpg)
- [Equipe de 2025 contra a versão de 1985 no jogo clássico](../artifacts/local-historical-qa/screenshots/lobby-multiplayer.jpg)
- [Importação PostgreSQL de QA](../artifacts/local-historical-qa/postgres-import.json)
- [Validação do catálogo canônico vazio](../artifacts/local-historical-qa/canonical-validation.json)

A primeira execução do teste PostgreSQL encontrou o banco em recuperação. O teste passou após o banco aceitar conexões. A validação de navegador usou autenticação AccountTest e um banco de mundos de QA separado. O MatchEngine e a evolução dos jogadores conservam seus arquivos do checkpoint inicial. O trabalho permanece no ramo isolado, com publicação e integração na branch principal reservadas à decisão do proprietário.
