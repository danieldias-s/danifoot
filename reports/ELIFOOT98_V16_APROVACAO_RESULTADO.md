# V16 — resultado da aprovação e aplicação de comportamentos

**PASS** em 12/11/2026. A aprovação individual e em lote, a pesquisa manual, o histórico e a aplicação confirmada ao JSON V1.1 foram implementados e validados com dados sintéticos. O catálogo real foi consultado pelo validador local. Nenhuma classificação de jogador real foi registrada ou aplicada.

## Git e escopo

A branch inicial e final é `main`. O HEAD inicial era `9268d9e48f5623fb6abdcc53840820a3d95a7f5c`. A inspeção inicial registrou branches, status e arquivos preexistentes em `artifacts/v16-approval/`. Os três JSONs de auditoria que já tinham alterações foram preservados byte a byte e ficaram fora do índice. Os 250 caminhos não rastreados que apareciam no status inicial continuaram presentes.

Foram auditados o relatório V16, os guias de cadastro e classificação, os schemas 1.0 e 1.1, o loader, o validador e o criador offline. A V1.6 experimental continua produzindo sugestões com o mesmo índice, configuração, percentis e tratamento de dados ausentes. O workflow reaproveita a leitura JSON estrita e a gravação atômica do classificador 1.0.

## Resultado por área

| Área | Estado | Evidência |
|---|---|---|
| Contrato e proveniência | PASS | `behavior-approval-1.0` vincula sugestão a clube, jogador, país, ano, posição, hashes, configuração, estatísticas, fontes e qualidade. Confiança fica nula quando indisponível. |
| Aprovação individual e em lote | PASS | Seleção explícita e confirmação separada. Aceitar, escolher manualmente, rejeitar ou adiar. Cada decisão registra operador, data UTC e motivo. |
| Pesquisa manual | PASS | Aceita escolha justificada de 0 a 5 sem relatório estatístico. Fonte ausente fica declarada. |
| Histórico e substituição | PASS | Estados PENDING, APPROVED, REJECTED, REVIEW_REQUIRED e SUPERSEDED. Eventos encadeados por SHA-256. Substituição exige o ID anterior e troca de valor já gravado exige confirmação específica. |
| Aplicação e dry-run | PASS | Validação de todos os alvos antes de gravar, validador real em cópia temporária e no catálogo final, recibo idempotente e diário de recuperação. |
| Permissões | PASS | Política privada fixa na pasta pai do catálogo, alias ligado à conta do sistema, comandos apenas na CLI local. |
| Compatibilidade V1.0 e V1.1 | PASS | Validador real preservou os bytes do catálogo V1.0. Clube V1.1 READY continuou válido após aplicação e DRAFT manteve seu status. |
| Mundo e saves | PASS | Nova prévia recebe o valor aprovado no JSON. Prévia anterior e save serializado conservam o valor congelado, com a mesma força e identidade. |
| Real Madrid | PASS | Validação estrutural do catálogo ativo com 7 catálogos e 0 READY. Clube e manifest continuam DRAFT e os 23 valores de `behavior` seguem nulos. |

O contrato, os campos, os estados, a política local e um exemplo sintético completo estão no [guia de aprovação e proveniência](../docs/catalogos/ELIFOOT98_V16_APROVACAO_PROVENIENCIA.md). O exemplo foi executado a partir do próprio guia e passou pelas etapas de consulta, prévia da revisão, confirmação, dry-run, aplicação e reaplicação. A saída está em `artifacts/v16-approval/example.log`.

## Regressões executadas

| Validação | Resultado | Arquivo sob `artifacts/v16-approval/` |
|---|---|---|
| `dotnet build Elifoot.slnx -c Release` | 0 avisos, 0 erros | `build-final.log` |
| Domínio .NET | 680 aprovados | `Domain.trx` |
| Aplicação .NET | 63 aprovados | `Application.trx` |
| Integração .NET completa, PostgreSQL isolado | 402 aprovados, 14m57s | `integration.trx` |
| Integração .NET final V1.1 e V16 | 44 aprovados, com 1 caso novo | `integration-final.trx` |
| `flutter analyze --no-pub` | Sem ocorrências | `flutter-analyze.log` |
| `flutter test --no-pub --reporter expanded` | 565 aprovados | `flutter-test.log` |
| `python -B -m unittest discover -s tools/offline_catalog/tests -v` | 66 aprovados, 31 novos | `python.log` |
| `offline-catalog-validate` na raiz ativa | Válido com avisos de DRAFT | `catalog-validation.json` |
| `git diff --check` | Aprovado | Conferido por escopo e ao encerrar |

Total de **1.146 casos .NET aprovados**, deduplicados pelo identificador TRX. A rodada de 44 integrações repetiu 43 casos da suíte completa e acrescentou `UpdatedCatalogBehaviorIsUsedOnlyByNewPreviewsAndWorlds`. As suítes indicadas terminaram sem falhas ou casos ignorados. A suíte longa de simulação não foi executada. As regressões de domínio e integração exercitaram as regras de força inicial, evolução, mercado, economia, carreira, Taça, multiplayer e treinadores.

| Itens obrigatórios | Cobertura executada |
|---|---|
| 01–07 | PENDING inicial, aceitação, rejeição, correção, lote, seleção parcial e confirmação explícita. |
| 08–17 | Fonte ausente, minutos insuficientes, conflito de fontes declarado, todos os códigos 0..5 e rejeição de tipos/valores inválidos. |
| 18–25 | Identidades e escopo incompatíveis, evidência alterada, JSON e manifest alterados, reaplicação, lote em múltiplos arquivos e falha após o primeiro arquivo. |
| 26–31 | Cadeia de histórico, supersessão, pesquisa manual, política de operador, dry-run e comparação estrutural do JSON para comprovar que só `behavior` mudou. |
| 32–38 | Loader real em cópia e após gravar, DRAFT, READY, V1.0/V1.1, restauração do save e URL de evidência registrada sem abrir conexão. |
| 39–47 | Suítes de domínio e integração e teste V1.1 final com catálogo alterado, prévia anterior congelada, força/identidade iguais e código copiado do JSON. |

O diário foi testado para interrupção antes e depois do recibo. A suíte também simulou uma edição concorrente externa e verificou que a edição foi preservada, que os outros arquivos foram restaurados e que o diário ficou disponível. O bloqueio coordena escritores da CLI. A integração com editores externos usa rechecagem de bytes, com o limite de isolamento documentado no guia. A política vincula a execução à conta do sistema no ambiente local. A validade factual das fontes depende de revisão humana.

## Arquivos da entrega

Criados

- `tools/offline_catalog/behavior_approval_v16.py`
- `tools/offline_catalog/behavior_apply_v16.py`
- `tools/offline_catalog/tests/test_behavior_approval_v16.py`
- `docs/catalogos/ELIFOOT98_V16_APROVACAO_PROVENIENCIA.md`
- `reports/ELIFOOT98_V16_APROVACAO_RESULTADO.md`

Modificados

- `.gitignore`
- `tools/offline_catalog/behavior_classifier.py`
- `tools/offline_catalog/V16_COMPORTAMENTOS.md`
- `tests/Elifoot.Integration.Tests/OfflineCatalogV11Tests.cs`
- `docs/catalogos/ELIFOOT98_CLASSIFICADOR_COMPORTAMENTO.md`
- `docs/catalogos/ELIFOOT98_REGRAS_CADASTRO_CATALOGOS_V11.md`

## Preservação e conclusão

O diff a partir de `9268d9e` não contém alterações em `src/`, `client/` ou `catalogos/`. Os arquivos do Real Madrid, o classificador experimental e os três JSONs preexistentes coincidiram com os SHA-256 iniciais. O jogo continuou respondendo HTTP 200 na web local e no endpoint de saúde da API. O banco do jogo e o mundo QA ficaram preservados. A integração usou um cluster PostgreSQL temporário na porta 55441, encerrado ao final. O fluxo implementado é local e usa o validador offline. Sem scraping, consulta a APIs externas, push, deploy ou reinicialização do jogo.

| Commit local | Escopo |
|---|---|
| `ef7d96f` | Contrato executável, aplicação controlada e regressões Python. |
| `7ba4032` | Regressão .NET de prévia congelada e save. |
| Commit deste relatório | Guia, documentação de cadastro e evidências finais. O HEAD da entrega é o commit retornado por `git log -1 --format=%H -- reports/ELIFOOT98_V16_APROVACAO_RESULTADO.md`. |

O cadastro real continua pendente de pesquisa e decisões explícitas do proprietário. Para operar sobre a raiz ativa, o proprietário provisiona a política local e seleciona as evidências e os jogadores a revisar. A entrega técnica foi concluída com **PASS**.
