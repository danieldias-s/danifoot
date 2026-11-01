# Resultado das importações — 016A

**PARTIAL**. Importador implementado e exercitado com dados reais Wikidata, mas o objetivo de elencos atuais permanece incompleto. O usuário decidiu continuar sem chave API-Football.

Aplicados em DEV: identidades dos seis clubes e 113 atletas históricos válidos de Barcelona Feminino. A associação do Corinthians existente foi revisada antes do APPLY. `IsFixture=false`; estado APPLIED_INCOMPLETE. Não houve elenco fictício introduzido no catálogo real.

A transação usa os mecanismos existentes de escopo/lease e grava clubes, elencos e vínculos de identidade como um lote. Falha reverte o lote. Repetição da mesma operação retorna recibo persistido. Novas prévias do mesmo cache após reinício reutilizam snapshots; duas execuções reais confirmaram que não acrescentam versões ou jogadores. Respostas vazias/parciais não apagam snapshots bons.

Defeitos encontrados e corrigidos durante a auditoria: identidade por nome precisava de revisão explícita; precisão de data diferente entre consulta e PostgreSQL causava versões extras; cache SQLite perdia Kind UTC; contagem de auditoria ignorava arrays aninhados; prévia apenas de IDs ocultava a cobertura de elenco já coletada. Os testes incluem repetição em cache, SQLite/PostgreSQL e reinício. Versões anteriores de DEV não foram apagadas para ocultar o defeito.

Persistência real: API reiniciada, relatório recuperado, orçamento/cache preservados, APPLY repetido e hash do SnapshotJson dos dois mundos inalterado. Há 48 snapshots herdados preservados e 14 versões de piloto no catálogo, 62 no total; versão ativa sem duplicação de IDs de jogador.

Mundos de QA masculino e feminino foram criados com clubes reais do catálogo importado e consentimento explícito para atletas gerados. 128 clubes e 2.304 atletas gerados por mundo, 18 por elenco; posição/força conferidas contra a prévia, contratos e salários positivos simulados. Isso valida a criação e o isolamento; **não comprova elenco atual real**. [Evidência dos mundos](evidence-016a/worlds-http-report.json).

[Regressão](REGRESSION_016A.md) · [Pendências](KNOWN_ISSUES_016A.md) · [Relatório final](DELIVERY_016A.md).

A auditoria adicional criou um terceiro mundo integralmente pela interface, selecionou o Flamengo, abriu o plantel em desktop/mobile e confrontou seus 18 atletas com a origem congelada. [Prova do navegador](evidence-016a/browser-qa.json). Os dois mundos HTTP de QA também concluíram a rodada oficial R3.
