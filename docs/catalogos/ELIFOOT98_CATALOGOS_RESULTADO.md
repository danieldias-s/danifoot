# Resultado — preparação de catálogos históricos offline

Execução iniciada em 11/11/2026 e encerrada em 12/11/2026. Projeto `D:\elifoot_98`, branch `main`. HEAD inicial `4c0b64fb465a24d31a6798236606f361cd1654fa`.

**Status final: PASS.**

## Entrega implementada

- Estrutura `catalogos/<pais>/<ano>/manifest.json` e `clubes/*.json`, schema JSON 1.0, templates editáveis e exemplo completo exclusivamente sintético fora da indexação.
- Brasil 1998/2026, Portugal 1998 e Espanha 1998 preparados como DRAFT vazio. Planejamento de oito países com cinco posições não definidas; nenhum clube real cadastrado.
- Loader/validador sem banco/rede, CLI antecipada, validações estruturais, completude, referências, duplicatas, país/ano, atributos, quatro preferências e segurança de caminhos.
- Entrada **USAR CATÁLOGO OFFLINE** no criador: filtrar país/ano, selecionar catálogos e clubes, distribuir divisões, validar prévia e confirmar explicitamente um novo mundo.
- Importação separada da recalibração 017B. Força, comportamento, estrela, nacionalidade, nomes, estádio/capacidade e treinador são copiados dos arquivos.
- Novos GUIDs para entidades do mundo, origem congelada, confirmação autenticada/idempotente e persistência no PostgreSQL e SQLite de teste.
- Treinador com identidade/carreira independente, duas preferências em casa e duas fora; alternativas estratégicas continuam disponíveis. Campos futuros opcionais aprendidos não têm cálculo ou efeito.
- Auditoria do editor registrada antes da expansão: `ELIFOOT98_EDITOR_COMPATIBILIDADE_INICIAL.md`. Instruções de preenchimento/uso em `ELIFOOT98_ESTRUTURA_CATALOGOS.md`.

## Evidências de validação

| Verificação | Resultado nesta execução |
|---|---|
| Build Release da solução | PASS, zero erros e zero avisos |
| .NET domínio | PASS, 636 testes |
| .NET aplicação | PASS, 63 testes |
| .NET integração completa | PASS, 359 testes, zero ignorados, com PostgreSQL temporário próprio |
| .NET simulações completas | PASS, 44 cenários distintos, sem reduzir temporadas |
| Catálogo em cópia isolada do HEAD + arquivos desta tarefa | PASS, 58 testes, zero ignorados, incluindo PostgreSQL |
| Flutter analyze | PASS, No issues found |
| Flutter completo | PASS, 560 testes |
| Flutter novo fluxo offline | PASS, 6 testes; 360/768/1024/1440 px e confirmação explícita |
| CLI na raiz ativa | PASS, saída 0; quatro rascunhos reconhecidos, nenhum elegível como completo |
| CLI no exemplo sintético isolado | PASS, saída 0; exemplo válido fora da raiz ativa |
| Preservação de mudanças anteriores | PASS por comparação de SHA-256 de 27 arquivos registrados com o baseline |
| Instância existente | API OK, Web HTTP 200, PostgreSQL 55442 aceitando conexões; sem reinício |

Contagem consolidada: **1.102 testes .NET aprovados** (636 domínio + 63 aplicação + 359 integração + 44 simulações), sem casos ignorados nas execuções finais. Os 58 testes específicos estão incluídos na integração, e sua execução isolada adicional não foi somada novamente.

As 44 simulações foram verificadas contra o inventário descoberto: 17 casos já concluídos nesta mesma execução mais 27 casos nas quatro partições independentes. Não foi reutilizado resultado de execução anterior, omitido caso ou reduzido número de temporadas. Os resultados constam dos TRX e de `validation-final.json`.

Logs, TRX e baseline: `artifacts/offline-catalog-v1/`. Os relatórios volumosos de simulação estão na pasta temporária indicada em `test-evidence-location.json`; o PostgreSQL temporário usou a porta 55441 e dados nessa pasta, sem reutilizar o cluster do jogo em 55442, e foi encerrado após os testes. A revisão isolada está registrada em `commit-review.json` e `tests/commit-review-final.trx`.

Tentativas iniciais não foram consideradas aprovação: uma asserção nova comparava listas de records por referência e foi corrigida para comparar o conteúdo serializado; os testes Flutter novos precisaram selecionar o Scrollable externo. A regressão detectou o registro de rotas offline num harness que não registrava esses serviços; o registro passou a ser condicionado à presença do loader, preservando os hosts antigos. Houve ainda diferenças de cache NuGet entre sandbox/usuário, snapshots de testes não configurados, permissões do host Windows e falta de espaço em D:. A execução validada usa cache NuGet local, snapshots locais existentes, bancos temporários novos por rodada e relatórios grandes em C:. Na cópia temporária de fontes, o descriptor do fixture 008 precisou ser copiado antes da rodada final; o save referenciado continuou sendo aberto em modo somente leitura. A revisão final também tornou números e categoria estritos conforme o schema; sete testes adicionais foram aprovados, seguidos de nova suíte de integração completa. Apenas uma cópia incompleta de build criada nesta tarefa foi removida.

## Cobertura dos requisitos

| Grupo solicitado | Evidência |
|---|---|
| 01 válido; 02 incompleto | `ValidFilesReadOfflineWithoutDatabasePreserveOptionalFieldsAndLiteralValues`, `IncompleteDraftIsEditableButNeverReadyAndNeverSilentlyFilled` |
| 03 país; 04 ano | `InvalidClubAndScopeAreRejected`, `MissingCountryYearManifestAndBrokenReferencesNeverBecomePlayable`, `AnyIsoCountryAndValidYearWorkWithoutPortugalSpainWhitelist` |
| 05 clube; 06 ID; 07 jogador duplicado | `DuplicateClubPlayerCoachAndReferenceIdsAreDetected` |
| 08 força; 09 posição; 10 comportamento; 11 estrela | `ReadyRejectsInvalidPlayerValues` e leitura literal true/false/1/50 |
| 12 quatro táticas; 13 tática inválida; 14 treinador incompleto | `ExactlyTwoValidTacticsPerContext`, `CoachMandatoryFieldsAreValidated` |
| 15 leitura offline | Loader puro e CLI sem host/banco/rede |
| 16 criação; 17 independência | `NewWorldCopiesExactRosterWithNewIdsAndKeepsSourceAndOldSnapshotsIndependent`, confirmação congelada em SQLite/PostgreSQL |
| 18 saves anteriores | `ExistingSnapshotsHaveNoNewPropertiesAndKeepOriginalBytes` e hashes dos arquivos preexistentes |
| 19 identidade; 20 persistência de preferências | `CoachKeepsIdentityCareerAndOriginalPreferencesWhenHiredByAnotherClub`, round-trip integral do save |
| 21 IA com outras formações | `CatalogPreferencesRemainSoftAndFutureLearningFieldsHaveNoEffect` |
| 22 editor existente | Novo caminho separado; suíte Flutter completa aprovada, editor antigo recusa drafts offline |
| 23 economia clássica/legada; 24 mercado | `CatalogPlayersStillTransferUnderLegacyAndClassicalEconomy` |
| 25 carreira | Contratação real pelo motor de carreira conserva ID, pontos, títulos, histórico e preferências |
| 26 regressão V3 | PASS: domínio, aplicação, integração, 44 simulações e Flutter |
| Segurança adicional | JSON malformado/duplicado/campos desconhecidos, traversal/paths absolutos, escudo congelado, limites 11..23 sem alterar máximo 24 do servidor |

## Arquivos do escopo

Novos dados: `catalogos/plano.json`; quatro manifests e quatro `.gitkeep`; `catalogos/_schemas/{manifest,clube}-1.0.schema.json`; dois templates; manifest e clube completos do exemplo sintético.

Novos contratos/domínio: `src/Elifoot.Contracts/Catalog/OfflineCatalogDtos.cs`; `src/Elifoot.Domain/Catalog/OfflineCatalogRules.cs`, `OfflineCatalogRuntime.cs`, `OfflineWorldFactory.cs`.

Novos serviços/API: `src/Elifoot.Infrastructure/Catalog/OfflineCatalogLoader.cs`, `OfflineWorldCreatorService.cs`; `src/Elifoot.Api/OfflineCatalogFeature.cs`.

Integrações pontuais em arquivos anteriormente limpos: `src/Elifoot.Contracts/Catalog/WorldCreatorDtos.cs`; `src/Elifoot.Domain/Entities.cs`, `AiManagerBrain.cs`; `src/Elifoot.Api/LocalCatalogCommand.cs`, `WorldCreatorEndpoints.cs`; `src/Elifoot.Infrastructure/Catalog/WorldCreatorService.cs`, `WorldAssemblyService.cs`; `client/elifoot_flutter/lib/world_creator.dart`.

Interface/testes novos: `client/elifoot_flutter/lib/offline_catalog_creator.dart`; `client/elifoot_flutter/test/offline_catalog_creator_test.dart`; `tests/Elifoot.Integration.Tests/OfflineCatalogTests.cs`.

Documentação: os três arquivos `docs/catalogos/ELIFOOT98_{EDITOR_COMPATIBILIDADE_INICIAL,ESTRUTURA_CATALOGOS,CATALOGOS_RESULTADO}.md`.

## Compatibilidade e pendências deliberadas

Não foram alterados gols, A/M, Taça, divisões, calendário, promoções/rebaixamentos ou fórmulas de economia, mercado, salário e contratos. Nenhuma migration foi adicionada. Máximo 24 do servidor preservado; importador novo aceita até 23. A nova IA contextual só se aplica a treinadores importados com perfil opcional; treinadores anteriores usam o caminho anterior.

O QA, saves, catálogos anteriores e a renovação de contratos permanecem preservados. Arquivos já modificados e arquivos não rastreados anteriores não pertencem ao commit desta tarefa. Os testes usam apenas bancos temporários, sem criar mundo no banco do jogo. A instância já publicada continua executando sua compilação anterior; esta preparação não publica ou reinicia serviços.

Pendências de conteúdo: o proprietário deve preencher e validar os elencos reais, definir cinco países restantes e reunir 128 clubes válidos para criar um mundo. Renderer clássico de escudo/sigla e edição visual completa dos novos campos não foram expandidos; metadados são preservados nos saves e editados em JSON. Exportação antiga de montagem não é backup integral desse formato. Aprendizado tático permanece apenas como extensão opcional, sem implementação.

## Commit

Decisão de commit: concluir a implementação em um commit local com os 36 arquivos deste escopo, excluindo todas as alterações preexistentes. Mensagem: `feat(elifoot98): prepare offline historical catalogs`. O hash é registrado após a criação em `artifacts/offline-catalog-v1/commit.json` e na entrega final. A consulta direta é `git log -1 --format=%H -- docs/catalogos/ELIFOOT98_CATALOGOS_RESULTADO.md`. Sem push.
