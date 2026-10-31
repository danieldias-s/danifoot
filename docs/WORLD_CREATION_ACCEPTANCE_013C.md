# Aceitação — 013C

STATUS: PASS

| Suíte .NET | Aprovados | Falhas | Ignorados |
|---|---:|---:|---:|
| elifoot.application.tests.dll | 6/6 | 0 | 0 |
| elifoot.domain.tests.dll | 388/388 | 0 | 0 |
| elifoot.integration.tests.dll | 134/134 | 0 | 0 |
| elifoot.simulation.tests.dll | 24/24 | 0 | 0 |

Build Release: zero avisos/erros. A execução usa OutputPath isolado bin/Playable013C/net10.0, preservando a profundidade esperada pelos testes; não utiliza filtro. Os TRX integrais têm prefixo final-013c e totalizam 551 casos. Depois da correção de limite do gerador, os TRX boundary-013c registram 388 casos de domínio e dois fluxos PostgreSQL repetidos. O consolidado por assembly e testId estável totaliza 552 casos distintos: baseline 533 mais 17 casos de domínio e dois de integração novos. O build final também foi repetido. A alteração tardia restringiu-se ao gerador dos modos novos; as simulações legadas não o utilizam, e seus motores/fontes permaneceram idênticos. Não houve repetição dos 26 minutos de simulações já aprovadas. Nomes truncados de teorias não são usados para deduplicar casos.

Flutter: 76 aprovados, baseline 70 mais seis casos novos, incluindo larguras 390 e 1366, recusa, consentimento, reset de modo e rótulos. Analyze sem problemas e build Web aprovados. Node: dois testes existentes aprovados.

A aceitação HTTP usa o bundle real preservado, separado das fixtures sintéticas dos testes automatizados. Ela cobre masculino/feminino, oito países selecionados, um país, montagem automática, manual completa, distritais aleatórios/manuais/por ranking, determinismo, duplicação, mínimo de elenco, posições, força, comportamento, contrato, salário, valor de mercado, origem, login, clube humano, escalação e três rodadas. O script de reinício compara hashes de TODOS os mundos e snapshots reais locais, reinicia processos próprios e verifica dados, confirmação de draft e empréstimo idempotente via HTTP.

| Matriz | Resultado e evidência |
|---|---|
| A, cobertura insuficiente | Bloqueio e geração zero; actual-modes.json. |
| A, cobertura completa | Contrato sintético em ambos os gêneros; não contado como dado real. |
| B, recusa | Bloqueio; nenhum jogador fictício gerado. |
| B, autorizado | Dois mundos reais de 128 clubes; actual-worlds.json. |
| C, Brasil | 128 clubes após autorização; equipes geradas restritas ao país escolhido. |
| Manual soberano | 128 atribuições preservadas no feminino; força independe da divisão. |
| Dados históricos/incertos | Seis casos de vínculo inválido excluídos do elenco atual. |
| Persistência / catálogo | API e PostgreSQL reiniciados; atualização do catálogo não altera o mundo; teste novo de integração. |
| Salário / banco / leilões / insolvência | Regressão econômica herdada e reconciliação independente abaixo. |
| Cache / fallback / direitos | Regressão 013A/B integral, mantendo guards de licença. |

| Protocolo de 100 temporadas | Caixa final | Dívida final | Partidas reais | Linhas reconciliadas |
|---|---:|---:|---:|---:|
| Seed012D | 137353919.79 | 3270104.68 | 35100 | 556238 |
| ImportedFixtureMale | 79157786.17 | 4366066.93 | 35100 | 553936 |
| ImportedFixtureFemale | 79157786.17 | 4366066.93 | 35100 | 553936 |

Os campos SourceCheckpoint herdados nos resumos identificam protocolos históricos; o código efetivamente executado é identificado pela base e pelo manifesto desta missão. As 24 simulações da suíte foram executadas, inclusive testes longos; não houve exclusão de MultiSeasonAndBenchmarkTests. Os três conjuntos acima reproduzem exatamente caixa e dívida da base integrada. Todas as identidades anuais de caixa e dívida, saldos por clube e pares de transferências foram reconciliados independentemente. As métricas de salários, mercado, inflação, empréstimos e crises permanecem separadas nos arquivos herdados produzidos nesta execução.

Esses protocolos longos usam a fábrica 012D e fixtures importadas 014A. Não representam 100 temporadas dos dois mundos reais 013C: estes foram ensaiados por três rodadas e reinício. A transição de liquidez dos mundos de fixtures continua visível e não comprova equilíbrio universal. Nenhum índice inflacionário, teto de caixa ou dreno financeiro novo foi introduzido.

Evidências: audit-013c/full-build.log, full-regression.log, tests/final-013c_*.trx, tests/boundary-013c_*.trx, boundary-exits.json, final-build-exit.json, flutter-exits.json, actual-worlds.json, actual-modes.json, restart-verification.json, actual-restart-http.json, financial-reconciliation.json e final-manifest.json. Ledgers e séries comprimidas grandes permanecem locais com SHA-256 no manifesto; os resumos e resultados são versionados.
