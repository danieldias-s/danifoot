# Elifoot 98 — compatibilidade offline V1.6

Data: 12/11/2026. Repositório: `D:\elifoot_98`. Branch: `main`.

**Status geral: PARTIAL.** Módulos 1, 2, 3, 5 e 6 aprovados no escopo técnico autorizado. O módulo 4 entrega o classificador experimental offline, mas não aplica ajustes automaticamente ao mundo: a definição/aprovação e persistência da proveniência do comportamento efetivo continua pendente, conforme a ordem de interromper integrações ambíguas. Não houve push ou deploy.

## Baseline e preservação

O HEAD inicial real era `630023f8ea2795f22ffdd1ecd3b7f5a614d9fc84`, posterior à referência `f1764fb`. Foram lidos a autorização `CODEX_ELIFOOT98_CORRECOES_V16.md`, o plano mestre e o pacote local Real Madrid. As instruções antigas de auditoria dentro do ZIP foram tratadas como contexto; o escopo implementado é o da autorização V16 atual.

Já existiam alterações locais em:

- `docs/audit-007/controlled/adaptability-controlled.json`
- `docs/audit-007/controlled/behavior-controlled.json`
- `docs/audit-008/browser-fixture.json`

Os três arquivos foram preservados byte a byte, conferidos contra SHA256 inicial e excluídos de todos os commits. Arquivos e diretórios não rastreados preexistentes também ficaram fora dos commits. Baseline com hashes de 486 arquivos: `artifacts/v16-compatibility/baseline.json` (evidência local, ignorada pelo Git).

Nenhuma migration, tabela, conta, sala ou save existente foi alterado. Não foram alterados `MatchEngine.cs`, `ClassicForceEvolutionEngine.cs`, `ChampionshipStrengthLimits.cs`, `Catalog/InitialPlayerStrength.cs` ou `Catalog/InitialSquadStrength017B.cs`: seus SHA256 finais coincidem com o baseline. Fórmulas, probabilidades, seeds de jogos existentes, escalação A/M, gols da Taça, salários e economia foram preservados.

## Matriz por módulo

| Módulo | Status | Implementação / evidência | Pendência |
|---|---|---|---|
| 1 — Táticas 1..4 | PASS | Schema 1.1 e validador aceitam 0..2 por contexto e exigem total >=1 em READY; IA consulta outro contexto antes do padrão. | Validar factualidade das preferências ao cadastrar clubes reais. |
| 2 — Corte por divisão | PASS | Seleção determinística após distribuição; mantém catálogo integral e candidatos/cortes na origem congelada; geradores de força intactos. | Pesquisa de prodígios/titulares deve ser fornecida com evidências, sem inferir dados ausentes. |
| 3 — Excedentes opcionais | PASS | Toggle explícito; 127 reais + um clube fictício distrital, 23 atletas efetivamente cortados e identidades originais. | Com dados insuficientes, revisar candidatos ou desligar a opção; não há preenchimento automático. |
| 4 — Comportamentos offline | PARTIAL | Índice, parâmetros, distribuição por posição/faixa de divisão, revisão, exportação e testes implementados. | Aprovar contrato de aplicação/proveniência do comportamento efetivo na prévia e no mundo; integração interrompida. |
| 5 — Ranking Opta | PASS | Mesma família OPTA/OPTA_INTERPOLATED somente com mesma referência; ferramenta auxiliar rastreável, sem extrapolação. | Evidências fornecidas pelo operador ainda precisam de revisão factual; ferramenta não verifica fontes externas. |
| 6 — Real Madrid DRAFT | PASS | Pacote validado isoladamente antes de registrar manifest; UTF-8 sem BOM; permanece DRAFT e bloqueado no serviço de seleção. | Revisão factual, campos obrigatórios e restante dos clubes READY para um mundo real. |

### 1. Táticas

`src/Elifoot.Domain/Catalog/OfflineCatalogRules.cs:38` exige pelo menos uma tática no total para READY 1.1; cada lista é limitada a duas, sem repetição e com formações nativas válidas. O schema `catalogos/_schemas/clube-1.1.schema.json` reflete essa regra. A versão 1.0 mantém sua validação anterior.

`src/Elifoot.Domain/AiManagerBrain.cs:7` filtra as formações utilizáveis no contexto atual, tenta o outro contexto e usa 4-4-2 quando nenhuma é utilizável. O fallback não reescreve as preferências originais nem ativa 5-0-5 em mundos que não o permitem. Treinadores sem identidade offline mantêm o caminho anterior. `OfflineWorldFactory.Create` inicializa preferências pelo contexto preenchido, conservando ambos os contextos originais na identidade.

Testes: `OfflineTacticsV16Tests` (16 novos), incluindo 1/0, 0/1, 2/0, 0/2, 1/2, 2/2, 0/0 inválido, excesso, duplicação, formação inválida e planejamento real em casa/fora. Mais seis regressões existentes de táticas executadas isoladamente. Suítes gerais aprovadas.

### 2. Corte de elenco

`src/Elifoot.Domain/Catalog/OfflineRosterV16.cs:15` é chamado somente na criação/prévia V1.6, após a divisão ser definida (`OfflineWorldFactory.cs:98`) e antes da aplicação inalterada da 017B. O algoritmo não modifica força de nenhum candidato.

| Divisão inicial | Elenco selecionado | Goleiros |
|---|---|---|
| 1ª | 20..21 | 2 |
| 2ª / 3ª | 19..20 | 2 |
| 4ª | 17..19 | 2 |
| Distrital | 13..23 | 1..2; 3 somente com total 23 e três candidatos elegíveis |

O tamanho é escolhido por SHA256 versionado de seed + chave do clube. Compara elencos viáveis para formações habilitadas e maximiza lexicograficamente estrelas M/A, prodígios documentados, titulares importantes documentados e minutos informados; resolve o restante por qualidade já gerada, sorteio estável e ID. Não cria pesos numéricos de importância nem presume idade, prodígio ou titularidade. `RetentionEvidence`, opcional na requisição de criação, exige identificação, evidência textual e minutos não negativos quando informados.

`OfflineClubSource.Definition.Players` permanece completo. `EligibleCatalogClub.ReferencePlayers` preserva todos os candidatos; `Players` contém selecionados; `CoverageWarnings` registra divisão, contagem e IDs cortados. Essas estruturas já existiam no contrato de origem salvo. O hash da prévia inclui requisição, selecionados, referências e cortes. `Create` usa os IDs selecionados, em vez de recriar o elenco integral, e congela a origem no mundo.

Compatibilidade: `ApplyV16RosterPolicy=false` por padrão no DTO e omitido na serialização quando falso. `RetentionEvidence=null` também é omitido. Há teste comparando a serialização com a forma anterior. A interface de nova criação 1.1 oferece a seleção V1.6 marcada por padrão; o usuário pode manter o fluxo antigo. Catálogo 1.0 conserva seus valores literais e não aceita o corte V1.6. Não há chamada dessa seleção em transferência, promoção, rebaixamento ou carregamento de save.

Testes: `OfflineRosterV16Tests` (19), incluindo as cinco divisões, 11/13/17/19/20/21/23 candidatos, insuficiência explícita, 1/2/3 G, todos os extremos de tamanho, seeds, preservação, alteração da divisão em nova prévia, hash, JSON de save e ausência de recálculo retroativo. O teste de nove estrelas verifica preservação mesmo quando a primeira formação viável perderia atletas protegidos.

### 3. Clube único de excedentes

`src/Elifoot.Domain/Catalog/OfflineSurplusV16.cs:10` e `OfflineWorldFactory.cs:108` exigem o corte V1.6 e a opção `IncludeSurplusClub=true`. Desligada: 128 reais. Ligada: exatamente 127 reais, com 95 distritais reais e o clube especial como último item/global 128, distrital. A interface está em `client/elifoot_flutter/lib/offline_catalog_creator.dart`.

O clube tem identidade explicitamente fictícia `v16-surplus-fictional`, treinador explicitamente virtual e força coletiva 1 por regra especial, sem ranking Opta inventado. Recebe três G e vinte jogadores de linha dos cortes; busca 7 D, 7 M e 6 A, ajustando apenas com candidatos existentes e exigindo posições para 4-4-2. A prioridade percorre clubes mais fortes/classificados primeiro e os melhores cortados de cada clube. Insuficiência de goleiros, linha ou posições gera erro e bloqueia criação. IDs ambíguos entre clubes/anos também são bloqueados.

Os atletas não são duplicados nos elencos reais. Canonical IDs e `OfflinePlayerIdentity` mantêm catálogo/país/ano de origem, inclusive quando o clube especial recebe atletas de outro país/ano. A lista inteira de cortes fica congelada nas referências dos clubes originais; somente 23 entram no especial. Os demais não são fabricados, apagados do catálogo ou inseridos automaticamente no mundo.

Quando há um 128º clube real disponível, sua referência é usada somente para manter a mesma coorte de calibração do gerador existente ao comparar as opções ligada/desligada; ele não entra no mundo 127+1. Com apenas 127 reais, a comparação com uma criação de 128 reais não é possível. Nenhuma fórmula de força foi modificada.

Testes: quatro `OfflineSurplusV16Tests`, com 127+1, 128 reais, insuficiências, unicidade, determinismo, forças dos reais iguais entre opções, identidades e save restaurado. `OfflineV16ApiTests` confirma que flags sobrevivem ao armazenamento da prévia, que alterações posteriores nos JSON não vazam e que confirmar duas vezes devolve o mesmo mundo. Um novo teste Flutter verifica o payload 127+1 e a criação desabilitada quando o servidor informa insuficiência. A prévia exibe selecionados, candidatos preservados e cortes.

### 4. Classificação experimental

`tools/offline_catalog/behavior_v16.py:16`: `I=(fouls + 2*yellowCards + 5*redCards)*90/minutes`. Pesos, mínimo de minutos (nunca abaixo de 900) e metas são configuráveis por arquivo versionado. Dados ausentes ou amostra insuficiente geram REVISAO, sem proposta automática.

Grupos: posição G/D/M/A + faixa de divisão inicial + `comparisonGroup` declarado pelo operador. Metas agregadas para tipos 0..5: D1/D2 `[20,20,25,20,10,5]`; D3/D4 `[15,15,20,25,15,10]`; distrital `[10,10,15,30,20,15]`. Percentil médio resolve empates com resultado igual; grupos pequenos/empates podem afastar as proporções. Não há quota por clube.

A exportação distingue `historicalStatistics`, `index`, `baseBehavior` e `effectiveBehaviorProposal`, com rótulo `GAMEPLAY_ESTIMATE`. Somente escreve um arquivo novo sob `_pesquisa/`; não sobrescreve catálogo ou save. O classificador 1.0 existente não foi alterado. Oito testes novos cobrem fórmula, metas, posições, minutos, faltantes, empate, configuração, exportação, preservação e JSON persistido.

**Interrupção obrigatória da integração:** a autorização diz: “Se a solução exigir mudança de contrato ambígua, parar e apresentar proposta antes de implementar essa parte.” O catálogo READY exige um valor-base, enquanto o ajuste depende da divisão escolhida depois. Não foi introduzida uma aplicação silenciosa nem um contrato de proveniência não aprovado. `Player.Behavior` continua recebendo o valor-base pelo fluxo atual e ficando fixo no save; a ferramenta não é chamada em transferências ou promoções.

Proposta e contrato de entrada/CLI detalhados em `tools/offline_catalog/V16_COMPORTAMENTOS.md`: revisão explícita na prévia, registro de base/efetivo/versão/fontes/divisão/aprovação incluído no hash e persistência imutável junto à origem. Essa aplicação, proveniência e seus testes de persistência no mundo ainda não estão implementados. Não declarar o módulo completo.

### 5. Opta

`src/Elifoot.Domain/Catalog/OfflineCatalogRanking.cs:15` reconhece OPTA e OPTA_INTERPOLATED como uma família, sem reescrever `source`. Continua exigindo rating presente e mesma data; demais regras de escopo/ano do loader e de força coletiva da ordenação permanecem. Outras fontes/escalas não são equiparadas. Prioridade manual e bloqueio de empate atravessando divisões são preservados.

`tools/offline_catalog/opta_interpolation.py:9` exige posição mundial conhecida entre duas âncoras distintas, ambas originais OPTA, com mesma data ISO, notas finitas compatíveis, IDs e evidências textuais. Fórmula linear entre âncoras; sem extrapolação ou inversão. Exporta estimativa e âncoras completas apenas em `_pesquisa/`, sem modificar automaticamente o catálogo ou alegar consulta externa. Cinco testes .NET e três Python, inclusive datas/fontes diferentes e proveniência nula, aprovados.

### 6. Real Madrid

ZIP local SHA256: `4f9b0528155fd7a53842046c0c29a88f7e49397d750cadde3f97c88005ee4741`. Os dois JSON foram validados com o loader em uma raiz isolada antes da referência ser adicionada ao manifest real. Nenhuma entrada anterior foi removida. Não foi aplicado o corte de 21 proposto em arquivo auxiliar: o JSON-base conserva os 23 nomes fornecidos.

`catalogos/espanha/2026/clubes/real-madrid.json` e `catalogos/espanha/2026/manifest.json` permanecem DRAFT. `teamStrength`, `historicalRanking`, `tieBreakPriority`, `stadium`, `coach` e os 23 `behavior` permanecem nulos. Não há força individual nem escudo inventado. Estrelas e nomes são propostas do pacote, não dados certificados. A nota `catalogos/_pesquisa/real-madrid-2026/REVISAO_V16.md` identifica expressamente a possibilidade de transferências hipotéticas e a ausência de verificação factual externa.

Quatro testes `OfflineV16DraftTests`: estrutura, campos pendentes, bloqueio efetivo no serviço de seleção e UTF-8 sem BOM dos três JSON. Validação final: sete catálogos estruturalmente válidos, um clube importado DRAFT e zero catálogos READY. Nenhum mundo real foi criado.

Para READY: validar vínculo de cada atleta na data de corte, posições/nacionalidades/estrelas, identidade/cores/escudo quando fornecido, força coletiva, ranking comparável ou prioridade explícita, treinador/táticas comprovadas e comportamentos-base. Depois executar o validador. Um mundo real exige ainda todos os 128 clubes READY, ou 127 e cortes elegíveis com opção especial explícita; não serão fabricados clubes para completar vagas.

## Validações executadas

| Validação | Resultado | Evidência local |
|---|---|---|
| Build da solução Release | PASS, 0 avisos / 0 erros | `artifacts/v16-compatibility/build-review.log` |
| Domínio, suíte completa final | 680 PASS, 0 falhas / ignorados | `tests/domain-full.trx` |
| Aplicação, suíte completa | 63 PASS, 0 falhas / ignorados | `tests/application-full.trx` |
| Integração, suíte completa capturada | 401 PASS, 0 falhas / ignorados; 12m53s | `tests/integration-full.trx` |
| Integração final V16/V1.1 | 43 PASS, 0 falhas / ignorados | `tests/integration-v16-final.trx` |
| Flutter, suíte completa | 565 PASS | `flutter-full.log` |
| Flutter, tela offline final | 11 PASS, incluindo 360/768/1024/1440 px | `flutter-offline-final.log` |
| Flutter analyze final | PASS, sem ocorrências | `flutter-analyze.log` |
| Python offline, suíte completa | 35 PASS | `python.log` |
| CLI catálogo ativo e pacote isolado | PASS estrutural; DRAFT não é READY | `catalog-validation.json`, `real-madrid-package-validation.json` |
| `git diff --check` | PASS | Verificado antes dos commits e no encerramento |

As evidências da tabela estão sob `artifacts/v16-compatibility/`. As 43 integrações finais foram executadas após os últimos refinamentos do corte e os novos testes: 42 repetem casos da suíte capturada e uma é a nova confirmação V16 pela API. São **402 casos de integração distintos cobertos**, não 444. Com domínio e aplicação: **1.145 testes .NET distintos aprovados**. Não somar execuções isoladas/repetidas ao total. A suíte longa `Elifoot.Simulation.Tests` não foi executada; não houve alteração do motor, e foram executadas as regressões de partida/força do domínio e integração.

Erros encontrados durante implementação foram corrigidos: nome de enum no novo teste, tipagem do mapa de fixture Flutter, espera do quadro após rolagem no novo teste, localização do repositório para o runtime externo e uma ocorrência de lint no teste. Nenhuma falha remanescente foi rotulada como preexistente para aprovar esta entrega. Não foram enfraquecidas expectativas existentes.

### Comandos principais

```powershell
# Artefatos de build separados; restore somente com cache local.
dotnet restore Elifoot.slnx --artifacts-path $env:TEMP/elifoot-v16-build --source C:/Users/Daniel/.nuget/packages --packages C:/Users/Daniel/.nuget/packages -p:NuGetAudit=false
dotnet build Elifoot.slnx -c Release --no-restore --artifacts-path $env:TEMP/elifoot-v16-build
dotnet test tests/Elifoot.Domain.Tests/Elifoot.Domain.Tests.csproj -c Release --no-build --no-restore --artifacts-path $env:TEMP/elifoot-v16-build
dotnet test tests/Elifoot.Application.Tests/Elifoot.Application.Tests.csproj -c Release --no-build --no-restore --artifacts-path $env:TEMP/elifoot-v16-build
# Integração usou cópia do runtime dentro do repositório, execução serial e ambiente PG isolado.
dotnet test artifacts/v16-compatibility/runtime/Elifoot.Integration.Tests.dll
dotnet test artifacts/v16-compatibility/runtime-final/Elifoot.Integration.Tests.dll --filter 'FullyQualifiedName~OfflineV16ApiTests|FullyQualifiedName~OfflineV16DraftTests|FullyQualifiedName~OfflineCatalogV11Tests'

# Em client/elifoot_flutter:
flutter analyze --no-pub
flutter test --no-pub --reporter expanded
flutter test --no-pub test/offline_catalog_creator_test.dart --reporter expanded

# Na raiz, Python instalado localmente; não há dependências externas novas.
& C:/Users/Daniel/AppData/Local/Python/pythoncore-3.14-64/python.exe -B -m unittest discover -s tools/offline_catalog/tests
dotnet $env:TEMP/elifoot-v16-build/bin/Elifoot.Api/release/Elifoot.Api.dll offline-catalog-validate --CatalogOffline:Root=D:/elifoot_98/catalogos
```

O cluster PostgreSQL próprio V16 foi criado em diretório temporário, porta 55441, somente localhost, com bancos de testes separados. Saídas de auditoria foram redirecionadas para esse diretório. O cluster foi encerrado após a execução; dados de teste/evidências não substituem saves do jogo. Reexecutar integração exige iniciar novamente um ambiente isolado e configurar suas variáveis; não apontar os testes para a porta 55442 do jogo.

Verificação somente de leitura do jogo existente: API `http://127.0.0.2:5288/api/health` respondeu HTTP 200; web `http://127.0.0.2:5242/` respondeu HTTP 200; PostgreSQL 55442 aceita conexões. Nenhum desses serviços foi reiniciado ou atualizado. A versão em execução não recebeu este código, pois não houve deploy.

## Arquivos alterados

| Escopo | Arquivos |
|---|---|
| M1 | `catalogos/_schemas/clube-1.1.schema.json`; `src/Elifoot.Domain/Catalog/OfflineCatalogRules.cs`; `src/Elifoot.Domain/AiManagerBrain.cs`; `src/Elifoot.Domain/Catalog/OfflineWorldFactory.cs`; `tests/Elifoot.Domain.Tests/OfflineTacticsV16Tests.cs` |
| M2/M3 | `src/Elifoot.Contracts/Catalog/OfflineCatalogDtos.cs`; `src/Elifoot.Domain/Catalog/OfflineRosterV16.cs`; `src/Elifoot.Domain/Catalog/OfflineSurplusV16.cs`; `src/Elifoot.Domain/Catalog/OfflineWorldFactory.cs`; `client/elifoot_flutter/lib/offline_catalog_creator.dart`; `client/elifoot_flutter/test/offline_catalog_creator_test.dart`; `tests/Elifoot.Domain.Tests/OfflineRosterV16Tests.cs`; `tests/Elifoot.Domain.Tests/OfflineSurplusV16Tests.cs`; `tests/Elifoot.Integration.Tests/OfflineV16ApiTests.cs` |
| M4 | `tools/offline_catalog/behavior_v16.py`; `tools/offline_catalog/tests/test_behavior_v16.py`; `tools/offline_catalog/V16_COMPORTAMENTOS.md` |
| M5 | `src/Elifoot.Domain/Catalog/OfflineCatalogRanking.cs`; `tests/Elifoot.Domain.Tests/OfflineRankingV16Tests.cs`; `tools/offline_catalog/opta_interpolation.py`; `tools/offline_catalog/tests/test_opta_interpolation.py` |
| M6 | `catalogos/espanha/2026/manifest.json`; `catalogos/espanha/2026/clubes/real-madrid.json`; `catalogos/identidades/real-madrid/identidade.json`; `catalogos/_pesquisa/real-madrid-2026/REVISAO_V16.md`; `tests/Elifoot.Integration.Tests/OfflineV16DraftTests.cs` |
| Entrega | `reports/ELIFOOT98_V16_COMPATIBILIDADE.md` |

## Commits locais e encerramento

| Hash | Escopo |
|---|---|
| `2e5f431` | M1: táticas verificadas 1..4 |
| `308a920` | M5: família Opta e interpolação rastreável |
| `c82fdac` | M2: corte por divisão inicial |
| `de32893` | M4: propostas experimentais offline |
| `32bc4d2` | M6: estrutura Real Madrid DRAFT |
| `e9d5336` | M3: clube opcional de excedentes e UI |
| `3301bbb` | M2: preservação entre formações viáveis |
| `be3fe78` | M4: preservação/exportação de propostas |
| `5b58610` | M5: evidências e datas explícitas |
| `9a9976e` | M6: bloqueio do DRAFT no serviço |
| `d040d72` | M3: confirmação API congelada/idempotente e teste UI |

HEAD técnico validado: `d040d72`. Este relatório é registrado em um commit documental posterior; seu hash pode ser obtido com `git log -1 --format=%H -- reports/ELIFOOT98_V16_COMPATIBILIDADE.md`. Branch mantida em `main`; somente arquivos de escopo adicionados. Ao concluir, as três modificações rastreadas preexistentes continuam locais, sem serem incluídas nos commits V16. Sem push e sem deploy.
