# Ensaios diferenciais e regressão — 012B

30/10/2026, America/Sao_Paulo. **STATUS: PARTIAL.** A caracterização local e a comparação das descrições públicas foram executadas. **A comparação dinâmica original × clone não foi executada**, por ausência de original legítimo identificável nos locais examinados. Nenhum resultado do clone preenche campos do original.

## Matriz solicitada

`PlayerForce012BResearchTests.AllRequestedStrengthParticipationAndTableCombinations` executa:

| Eixo | Valores |
| --- | --- |
| Força inicial | 1, 10, 25, 40, 50 |
| Participação | Titular, banco sem entrar, intervalo, entrada aos 80, inicialmente lesionado, recuperado |
| Classificação controlada | Líder (1), meio (4), último (8) |
| Divisão | Primeira, Segunda, Terceira, Quarta |
| Posição do atleta | Atacante e goleiro |
| Acompanhamento | Duas temporadas controladas, 22 rodadas globais por temporada |

São **720 cenários**, com **31.680 registros de rodada**, **20.160 partidas controladas** e **120 partidas de preparação de recuperação**. São 1.440 trajetórias anuais de atleta, não 1.440 campeonatos completos de 128 clubes. As outras competições não são simuladas nesse controle; tabela/divisão permanecem fixas e mercado automático fica ausente. O encerramento da força usa a rotina já aprovada da 012.

Controles adicionais: seed 199812, formação 4-4-2, comportamento Fair Play e contratos longos. Dois goleiros extras por clube evitam que a condição de goleiro no banco force titular lesionado por falta de alternativa. São dados da fixture de teste; não contratação ou regeneração introduzida no jogo. As três posições de tabela têm o mesmo contexto aleatório em cada comparação pareada.

Titulares são medidos antes da preparação e nos ticks. Substituições são aplicadas **depois** de concluir o minuto 45 ou 80; o primeiro tick em campo é o seguinte. Se não houve entrada, `ActualEntryMinute` e `StrengthAfterEntry` ficam nulos. Não se grava uma entrada inexistente. A condição inicialmente lesionado começa com três jogos de afastamento e retorna naturalmente; recuperado é preparado com um jogo real que zera contador anterior de um jogo.

Registros: `audit-012b/regression/force-012b/rounds-division-{1..4}.jsonl` e `summary-division-{1..4}.json`. Cada rodada inclui força antes, após entrada quando houve, após apito, após liquidação e após eventual fechamento sazonal; disponibilidade, participação efetiva, contexto e motivo também são registrados.

## Resultado observado no clone

Não houve alteração nominal ao escalar, entrar, completar os ticks, terminar ou liquidar a partida. As mudanças observadas de nominal ocorreram apenas no fechamento sazonal. O verificador exige igualdade de trajetórias das posições 1/4/8 em cada combinação equivalente.

| Força inicial | Menor força ao fim das duas temporadas | Maior força ao fim das duas temporadas |
| --- | --- | --- |
| 1 | 1 | 3 |
| 10 | 10 | 12 |
| 25 | 24 | 27 |
| 40 | 38 | 41 |
| 50 | 48 | 50 |

Esses intervalos agregam posições, divisões e participações controladas. Não representam probabilidades do original, promessa de crescimento em toda trajetória ou limite universal por divisão. A escala 1–50, inclusive o atleta inicialmente 50, permaneceu válida.

## Contusão: controles de cobertura separados

Com o contexto determinístico fixo, a matriz principal produziu **zero novas contusões do atleta monitorado**. Ela exercita afastamento/recuperação inicializados, mas esse zero não seria prova sobre perda em evento novo nem probabilidade de lesão zero. A repetição de contexto também impede tratar seus 720 cenários como 720 amostras aleatórias independentes.

Por isso foi acrescentado `RealInjuryAndReinjuryAtEveryRequestedStrengthForKeepersAndForwards`: quatro divisões × duas posições × cinco forças, com **dois episódios reais do mesmo atleta**, separados por recuperação em jogos reais. São **80 episódios observados**, 40 em goleiros e 40 em atacantes. A força é medida no tick, no fim do jogo e após cada jogo de afastamento. O teste exige indisponibilidade, duração positiva, não decremento no próprio jogo, recuperação do contador e disponibilidade antes da segunda contusão. Nominal permanece igual à inicial no clone.

Cada busca é limitada a 512 seeds a partir de 199812, recomeçando tentativas rejeitadas do mesmo snapshot. O registro inclui seed selecionada e quantidade de tentativas. **São eventos selecionados para cobertura, não amostra para estimar probabilidade, gravidade ou dependência histórica da força.** A seed escolhida em um episódio não é alegada como sequência de RNG do executável original.

Fontes locais: [episódios e recuperação](audit-012b/regression/force-012b/injury-probe-summary.json), [eventos imediatos](audit-012b/regression/force-012b/injury-probe-events.jsonl). As verificações de reincidência anteriores da 012 também foram preservadas e executadas na regressão.

## Comparação técnica das descrições públicas

`PlayerForce012BPublicModelTests` compara predicados independentes derivados das duas descrições publicadas. Força e base são entradas sintéticas 1–50, com três avaliações, duas posições e duas possibilidades de sorteio: **30.000 combinações**, das quais **220 discordam**. O resultado original fica null em cada contraexemplo. Esse teste comprova a não equivalência das descrições, não qual delas corresponde ao original.

O teste não implementa AvaliarRendimento, não estima NivelBaseTime e não adiciona evolução de rodada ao projeto. Os [resultados](audit-012b/regression/force-012b/published-model-counterexamples.json) tornam a ambiguidade verificável sem copiar código proprietário.

## Plano de observação do original

[original-capture-plan.json](audit-012b/original-capture-plan.json) contém as mesmas 720 células, sem observações inventadas. Exige versão/hash do executável, referência de licença, hash do save e método de controle de aleatoriedade. Estágios: preparação, entrada antes/depois, ticks de lesão, intervalo, apito antes da avaliação, avaliação posterior, afastamento, recuperação e segunda lesão.

Seed do clone e seed do original são campos diferentes. Ações de substituição podem mudar o consumo de sorteios; um save/seed igual não isola sozinho o efeito causal de entrar em campo. Reprodução emparelhada deve preservar snapshots e registrar o que foi controlado. Não se transfere a fórmula 005 do clone para preencher um resultado original ausente.

## Comandos e resultados

```powershell
# Proveniência pública; revisão 2f7ff68 fixada por padrão; apenas seis textos, metadados e hashes.
node scripts\research-player-force-012b.mjs

dotnet build Elifoot.slnx -c Release
$env:ELIFOOT_AUDIT_OUTPUT = 'D:\elifoot_98\docs\audit-012b\regression'
$env:ELIFOOT_FORCE012_MODE = 'candidate'
dotnet test Elifoot.slnx -c Release --logger 'trx;LogFilePrefix=regression012b' --results-directory docs\audit-012b\tests

# Fechamento da telemetria e cobertura adicional de eventos reais de contusão.
dotnet test tests\Elifoot.Simulation.Tests -c Release --filter FullyQualifiedName~PlayerForce012BResearchTests --logger 'trx;LogFilePrefix=closing012b' --results-directory docs\audit-012b\tests
node scripts\report-player-force-012b.mjs

Set-Location client\elifoot_flutter
flutter analyze
flutter test --reporter expanded
```

Build Release aprovado, **zero erros e zero avisos**. `flutter analyze` sem problemas e **50 testes Flutter aprovados**. A suíte .NET completa aprovou **275 casos**: 225 domínio, 5 aplicação, 31 integração, 14 simulação. O fechamento reexecuta os quatro casos da matriz e acrescenta um caso de cobertura de contusões: **276 casos .NET únicos aprovados**, sem contar repetições como casos novos.

A execução completa precedeu apenas o refinamento de campos nulos quando não ocorre entrada e o teste adicional de episódios selecionados. Não houve mudança de gameplay; o fechamento específico valida a telemetria e esse novo teste. Logs: [build](audit-012b/dotnet-build.log), [suíte completa](audit-012b/dotnet-tests.log), [fechamento](audit-012b/closing-tests.log), [Flutter](audit-012b/flutter-tests.log), [análise](audit-012b/flutter-analyze.log). Os TRX próprios estão em `audit-012b/tests`.

## Regressão de 100 temporadas e funcionalidades aprovadas

Mesmo sem alteração de regras, a suíte completa executou o ensaio real de **100 temporadas, 35.100 partidas e 2.200 rodadas**, com 128 clubes. Histogramas, médias e extremos por divisão, posição e observação anual são comparados estruturalmente ao candidato final 012 pelo verificador. A assinatura agregada também deve coincidir; não é apresentada como hash de cada lance. Os quatro setores G/D/M/A estão cobertos nessa população.

O restante da suíte ainda inclui as 305 temporadas completas dos testes anteriores, matriz tática de 4.000 partidas, força 1/50, reservas, goleiros, lesões/recuperação, promoção/rebaixamento, transferência, SQLite/JSON, idempotência, replay e HTTP/SignalR/multiplayer. São **405 temporadas completas nesta regressão ampla**, contando os replays e separando as trajetórias controladas. Não são temporadas do jogo original.

Nenhum arquivo de produção foi alterado. [verification.json](audit-012b/verification.json) valida a preservação dos arquivos anteriores e a distribuição do mundo de 100 temporadas; inclui distribuições da matriz separadas da população normal. Testes do clone passaram; fidelidade histórica segue PARTIAL.
