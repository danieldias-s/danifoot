# Catálogo histórico offline 1.1 — resultado

Data: 12/11/2026. Projeto `D:\elifoot_98`, branch `main`.
HEAD inicial: `3f4adf6595167df792b999f79048a3fb9d866e0a`.

**Status geral: PARTIAL.** A estrutura, seleção/ranking, validação, UI e classificador estão implementados. A criação de mundo 1.1 permanece bloqueada pela dependência expressamente autorizada: não existe conversão aprovada de força coletiva 1..20 para forças individuais 1..50. Não foi inventada fórmula. O caminho 1.0 continua funcional.

## Entrega e limites

- Identidade global em `identidades/<catalogClubId>/identidade.json`: nomes, sigla, exatamente duas cores HEX e escudo local opcional. Validação de referências, caminhos, arquivos e IDs; cache por identidade/varredura compartilhado pelos anos. Hash inclui bytes da identidade/escudo e definição anual. Metadados são congelados na prévia, sem dependência posterior dos arquivos.
- Contratos e schemas 1.1 próprios: arquivo anual com país, ano, teamStrength, ranking histórico, prioridade, estádio opcional, treinador e elenco. Campos de identidade não são duplicados no arquivo anual. Jogadores usam somente id/name/position/nationality/behavior/isStar. `strength` é rejeitado mesmo null. Comportamento 0..5 obrigatório em READY; null em DRAFT não vira Fair Play. Estrela verdadeira de G/D é rejeitada, somente M/A a admitem.
- Os códigos preservados são 0 Fair Play, 1 Cordeirinho, 2 Cavalheiro, 3 Caneleiro, 4 Caceteiro, 5 Sarrafeiro. Não há sorteio de comportamento nem ligação a estrela/força. O mundo conserva seu modelo de força 1..50; nenhuma força ou comportamento existente foi alterado.
- Os quatro manifests 1.0 DRAFT e seus JSONs foram preservados. Novos scaffolds vazios DRAFT: Espanha 2026, Inglaterra/GB 2026 e Itália/IT 2026. Cinco países preparados e três ainda indefinidos no plano. Há templates 1.1 e exemplo sintético completo de uma identidade em 1998/2026, fora da indexação ativa. Nenhum clube, jogador ou escudo real foi cadastrado/baixado.
- Ranking inicial: teamStrength DESC; rating DESC apenas para grupo de força com a mesma fonte/escala e data; prioridade manual ASC. Dados de anos incompatíveis não são comparados diretamente. Sem ranking confiável pode-se declarar ranking MANUAL com fonte/data ou usar prioridade explícita sem rating inventado. Empates entre divisões bloqueiam a prévia até prioridade ou divisão manual de todos os membros empatados. Ordem pela chave apenas apresenta empates internos.
- Seleção global sem cotas: 384 candidatos são preservados; UI mostra classificação, primeiros 128 pré-selecionados e 256 excedentes com seleção manual. Confirmação explícita continua obrigatória. Divisões 8/8/8/8 + 96 distritais, sem calendário novo. O nono de nove clubes força 20 conserva força 20 na segunda divisão. Ajustes manuais respeitam capacidade e não mudam atributos.
- Treinadores/IA preservados: duas preferências em casa e duas fora, códigos nativos, alternativas estratégicas livres, identidade/carreira independente. learnedHome/learnedAway opcionais no mundo continuam sem efeito; nenhum aprendizado foi implementado.
- A importação 1.0 conserva força literal e a serialização antiga: metadados V11 opcionais são omitidos quando null. Montagem não mistura formatos sem equivalência de forças aprovada. Saves antigos não precisam das identidades novas. Nenhuma migration foi criada.

## Evidência da dependência de forças

`WorldCatalogRules016B.PowerAt` e `ClubInitialPower` são classificação na escala 1..20, não geração individual. `InitialPlayerStrength` estima por desempenho/ranking relativo e seed, sem mapeamento de teamStrength. `InitialSquadStrength017B`/`docs/INITIAL_STRENGTH_RULES_017B.md` definem forças por divisão/posição (segunda: 26..38), contrariando a independência da força coletiva em relação à alocação. Usar qualquer dessas políticas diretamente no catálogo novo exigiria inventar uma equivalência não aprovada.

A prévia 1.1 conserva identidade/elenco/metadados, mas não cria atletas jogáveis sem força válida. Retorna mensagem de bloqueio e a confirmação do servidor também rejeita 1.1, mesmo que um cliente remova os erros. Não há força zero, 50 para todos, roster completado ou mundo inválido. Pendente: aprovar uma política determinística e reprodutível de distribuição individual por teamStrength, independente de divisão, comportamento e estrela. Só depois integrar geração/congelamento e testar criação/persistência 1.1. Essa parte **não recebeu PASS**.

## Classificador auxiliar

CLI Python 3.12+, biblioteca padrão, sem rede e sem integração com o motor. Lê CSV/JSON local, valida e preserva contagens desconhecidas como null; aceita também taxas per90 documentadas sem reconstruir contagens. Normalização por minutos e coortes de país/ano/posição/competição/fonte; fontes divergentes ou recortes múltiplos exigem revisão e não são somados.

Indisciplina = 0,65 × percentil(cartões90) + 0,35 × percentil(faltas90); cartões ponderam amarelo 1/vermelho 3. Intensidade física separada por duelos90, com faltas como evidência complementar. Pesos, limites e amostras são configuráveis/versionados: referência inicial 900 minutos, 10 partidas e 10 atletas por métrica; Sarrafeiro exige amostra maior e eventos consistentes, nunca só um vermelho. Não se afirma calibragem histórica ou fórmula original do Elifoot.

Estados SUGERIDO/REVISAO/SEM_DADOS/MANUAL, motivos e confiança; sem certeza automática. Relatório contém estatísticas originais, taxas, coortes, dimensões, sugestões, versão/configuração/hashes e revisão. Evidências em `_pesquisa/comportamento/<país>/<ano>`, ignoradas pelo jogo. `review` exige decisão/justificativa e vincula o hash do clube anual; somente `apply` explícito muda behavior do jogador aprovado. Valida escopo/pertencimento/versão, faz backup, substituição e recibo; repetição idempotente. Nunca promove READY, altera estrela, outro ano ou save. Manual completo em `ELIFOOT98_CLASSIFICADOR_COMPORTAMENTO.md`.

## Validações executadas

| Verificação | Resultado nesta execução |
|---|---|
| Build Release da solução | PASS, zero erros/avisos |
| .NET domínio completo | PASS, 636 testes |
| .NET aplicação completa | PASS, 63 testes |
| .NET integração completa final | PASS, 396 testes, zero ignorados, PostgreSQL temporário e coleções sem paralelismo |
| Revisão isolada HEAD + somente escopo do catálogo | PASS, 95 testes offline (58 anteriores + 37 novos), incluindo SQLite/PostgreSQL |
| Flutter isolado HEAD + somente os dois arquivos da tela | PASS, 10 testes; package root relativo ao checkout isolado |
| Classificador Python | PASS, 24 testes, CLI completa e seis perfis |
| CLI disciplinar + validador .NET | PASS em cópia sintética: classify/review/apply, só behavior alterado, validação posterior com saída 0 |
| Flutter analyze final | PASS, No issues found |
| Flutter completo | PASS, 564 testes |
| Tela offline específica | PASS, 10 testes incluídos na suíte; 360/768/1024/1440 px, 384 candidatos, seleção e bloqueio 1.1 |
| CLI de validação ativa | PASS, saída 0, sete manifests DRAFT não elegíveis |
| CLI no exemplo sintético 1.1 | PASS, saída 0, duas edições READY, sem mundo criado |
| Preservação de alterações anteriores | PASS por SHA-256 dos 23 arquivos de código/documentação protegidos |
| Instância do jogo | API health OK, Web HTTP 200, PostgreSQL 55442 aceitando conexões, sem reinício |

Contagem consolidada desta execução: **1.095 testes .NET aprovados** (636 domínio + 63 aplicação + 396 integração), mais **564 Flutter** e **24 Python**. As revisões isoladas de 95 .NET e 10 Flutter são execuções adicionais dos mesmos casos e não foram somadas novamente. Não foram reutilizadas contagens da entrega 1.0 como execução atual. As 44 simulações longas da entrega anterior não foram repetidas nem incluídas nesta contagem: não houve alteração de motor/regras, e a regressão atual executa domínio/aplicação/integração e Flutter reais. O cluster temporário próprio de testes foi encerrado após a validação; os serviços do jogo em 5242/5288/55442 permanecem ativos. Logs e TRX: `artifacts/offline-catalog-v11/`; relatórios volumosos e cluster de teste em pasta temporária indicada em `postgres-test.json`. O banco do jogo não foi usado.

Tentativas iniciais registradas: o teste novo de rolagem em 360 px saltava além do texto e foi ajustado para passos menores, sem mudar as asserções ou esconder overflow; dez testes finais passaram. Analyze detectou uma chave de bloco ausente no teste, corrigida. A primeira integração completa (395 casos) encontrou duas falhas: regressão do loader ao inspecionar JSON null, corrigida com verificação do tipo antes de ler schemaVersion; e uma falha na consulta de convite de carreira, que passou isoladamente sem mudança na carreira. Rodada final usa versão corrigida, bancos novos e coleções sem paralelismo. A rodada final completa aprovou os 396 casos, incluindo o de carreira, sem alterar regras nem testes de carreira. A primeira falha não foi declarada preexistente sem prova histórica: o resultado registrado é a aprovação isolada e na rodada completa serial.

## Cobertura dos requisitos

| Grupo | Evidência / status |
|---|---|
| Identidade, cores, referências, cache e duplicatas | PASS, OfflineCatalogV11Tests, incluindo identidade única em vários anos e duplicação entre países |
| Elenco 1.1, comportamento, estrela, sem strength | PASS, testes parametrizados do loader; DRAFT null distinto de 0 |
| Geração individual 1..50 a partir de teamStrength | PARTIAL, regra não aprovada; criação 1.1 bloqueada no cliente/servidor |
| Classificação 20–40 do pedido | PASS nos testes Python: taxas, percentis/coortes, nulos, amostras, divergências, seis perfis, revisão, reprodução e aplicação explícita |
| Força 1/20/inválida, ranking e desempate, nove clubes | PASS, prévia 8/8/8/8/96, força preservada e fontes não comparadas indevidamente |
| Três países/384 candidatos/seleção | PASS, leitura real de 384 arquivos sintéticos e UI nas quatro larguras, sem cotas/exclusão |
| Identidade congelada/persistência | PASS para serialização da origem/prévia; importação/saves 1.0 testados. Mundo 1.1 aguarda geração e não foi simulado como concluído |
| Treinadores, alternativas, aprendizado inativo | Regressões offline 1.0/IA preservadas; sem alteração no AiManagerBrain |
| Mercado, contratos/renovação, economia, carreira, Taça, A/M, saves e multiplayer | Suítes reais de domínio/aplicação e integração; resultado final acima |

## Arquivos deste escopo

Alterados (arquivos anteriormente limpos):

- `catalogos/plano.json`
- `client/elifoot_flutter/lib/offline_catalog_creator.dart`
- `client/elifoot_flutter/test/offline_catalog_creator_test.dart`
- `docs/catalogos/ELIFOOT98_ESTRUTURA_CATALOGOS.md`
- `src/Elifoot.Contracts/Catalog/OfflineCatalogDtos.cs`
- `src/Elifoot.Domain/Catalog/OfflineCatalogRules.cs`
- `src/Elifoot.Domain/Catalog/OfflineWorldFactory.cs`
- `src/Elifoot.Infrastructure/Catalog/OfflineCatalogLoader.cs`
- `src/Elifoot.Infrastructure/Catalog/OfflineWorldCreatorService.cs`

Criados:

- `catalogos/_pesquisa/comportamento/brasil/1998/.gitkeep`
- `catalogos/_pesquisa/comportamento/brasil/2026/.gitkeep`
- `catalogos/_pesquisa/comportamento/espanha/1998/.gitkeep`
- `catalogos/_pesquisa/comportamento/espanha/2026/.gitkeep`
- `catalogos/_schemas/clube-1.1.schema.json`
- `catalogos/_schemas/identidade-1.1.schema.json`
- `catalogos/_schemas/manifest-1.1.schema.json`
- `catalogos/espanha/2026/clubes/.gitkeep`
- `catalogos/espanha/2026/manifest.json`
- `catalogos/identidades/.gitkeep`
- `catalogos/inglaterra/2026/clubes/.gitkeep`
- `catalogos/inglaterra/2026/manifest.json`
- `catalogos/italia/2026/clubes/.gitkeep`
- `catalogos/italia/2026/manifest.json`
- `catalogos/modelos/clube-1.1.template.json`
- `catalogos/modelos/exemplo-1.1/brasil/1998/clubes/clube-sintetico.json`
- `catalogos/modelos/exemplo-1.1/brasil/1998/manifest.json`
- `catalogos/modelos/exemplo-1.1/brasil/2026/clubes/clube-sintetico.json`
- `catalogos/modelos/exemplo-1.1/brasil/2026/manifest.json`
- `catalogos/modelos/exemplo-1.1/identidades/demo-v11/identidade.json`
- `catalogos/modelos/identidade.template.json`
- `catalogos/modelos/manifest-1.1.template.json`
- `docs/catalogos/ELIFOOT98_CATALOGO_V11_RESULTADO.md`
- `docs/catalogos/ELIFOOT98_CLASSIFICADOR_COMPORTAMENTO.md`
- `src/Elifoot.Contracts/Catalog/OfflineCatalogV11Dtos.cs`
- `src/Elifoot.Domain/Catalog/OfflineCatalogRanking.cs`
- `src/Elifoot.Infrastructure/Catalog/OfflineCatalogV11Loader.cs`
- `tests/Elifoot.Integration.Tests/OfflineCatalogV11Tests.cs`
- `tools/offline_catalog/.gitignore`
- `tools/offline_catalog/behavior-config-1.0.json`
- `tools/offline_catalog/behavior_classifier.py`
- `tools/offline_catalog/tests/test_behavior_classifier.py`

## Preservação e commit local

Baseline: main/HEAD informado, 14 arquivos rastreados já modificados e demais arquivos não rastreados anteriores preservados. Hashes e cópias dos 23 arquivos relevantes preexistentes em `artifacts/offline-catalog-v11/baseline.json` e `preexisting/`. Eles não entram no commit desta entrega. Nenhuma ação sobre bancos/saves QA, regras de A/M, Taça, economia, contratos, calendário, força existente ou carreira. O jogo aberto continua com sua compilação publicada anterior: esta entrega não faz deploy nem substitui o build web usado manualmente.

Decisão de commit: conjunto acima validado, **41 arquivos** (9 alterados anteriormente limpos e 32 criados), excluindo todo o escopo preexistente; mensagem `feat(elifoot98): add offline catalog v1.1 and behavior classifier`. HEAD final será registrado em `artifacts/offline-catalog-v11/commit.json` após a criação e na entrega ao proprietário. Para consulta no Git: `git log -1 --format=%H -- docs/catalogos/ELIFOOT98_CATALOGO_V11_RESULTADO.md`. A referência externa evita reescrever o próprio commit para inserir seu hash. Sem push.

Pendências: aprovação da política de geração individual e sua integração/testes de mundo 1.1; calibragem do classificador com dados documentados pelo proprietário; preenchimento dos clubes reais e definição dos três países restantes. Não houve importação real, pesquisa automática, ampliação de competições, V4 ou aprendizado tático.
