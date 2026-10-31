# Mundos jogáveis com clubes reais — 013C

```text
TASK: ELIFOOT_PLAYABLE_REAL_CLUB_WORLDS_013C
STATUS: PASS
WORKTREE: D:\elifoot_98_integration
BRANCH: feature/playable-real-club-worlds-013c
BASE_COMMIT: d838cc5acf8302f1fa2dd56ce8ca6e7781a14b17
CHECKPOINT_COMMIT: pendente de commit local após validação
012D_PRESERVED: motores e contabilidade preservados; regressão de 100 temporadas reconciliada.
013A_PRESERVED: montagem 4x8+96, escolha manual, snapshots, cache e compatibilidade.
013B_PRESERVED: provedores, direitos, referências e cobertura preservados.
REAL_MALE_CLUBS: 158
REAL_FEMALE_CLUBS: 232
REAL_CURRENT_PLAYERS: 1 no catálogo e no mundo feminino exercitado.
GENERATED_PLAYERS: 2304 no mundo masculino; 2303 no feminino; autorização explícita.
REAL_CURRENT_COMPLETE_SQUADS: 0
AUTOMATIC_DIVISIONS: PASS, masculino.
MANUAL_DIVISIONS: PASS, feminino, 128 atribuições preservadas.
DISTRICTS: PASS, 96 por mundo, manual/sorteio/ranking exercitados.
RANKING_SOURCE: Elo local estimado masculino; ordem técnica por seed no feminino.
RANKING_CONFIDENCE: masculino até 0,35 e grafos desconectados; feminino 0.
WORLD_SNAPSHOT: país/categoria/clube/atribuições/modo/seed/ranking/proveniência congelados.
DATABASE: PostgreSQL próprio em loopback 55436; mundos e catálogo separados.
CACHE: snapshots recuperados após reinício real do PostgreSQL e API.
FALLBACK: testes herdados conservam o último snapshot válido após falha de fonte.
END_TO_END: login, clube, atletas, escalação, tática, partidas, resultados, avanço, save e reinício.
DOTNET_TESTS: 552; execução integral de 551 e repetição após correção do limite de 24; sem exclusões na execução integral.
FLUTTER_TESTS: 76; analyze limpo; build web concluído.
NODE_TESTS: 2.
LICENSE_LIMITATIONS: direitos preservados; sem nova autorização para mídia ou APIs proprietárias.
KNOWN_ISSUES: KNOWN_ISSUES_013C.md; cobertura real permanece PARTIAL.
```

A base integrada d838cc5 foi escolhida explicitamente pelo usuário. Os checkpoints 7a00bdd e 47608e9 e seus worktrees originais foram preservados; não houve merge automático, push ou publicação.

| Mundo | WorldId | Clubes reais | Atletas reais atuais | Fictícios | Montagem / distritais | Rodada recuperada |
|---|---|---:|---:|---:|---|---:|
| Male | 0ec5dc78-5997-4fa7-85ef-fef49826f9ad | 128 | 0 | 2304 | Automatic / Random | 4 |
| Female | 1e134d3e-e095-4922-add3-9e879f5f679a | 128 | 1 | 2303 | Custom / Manual | 4 |

Cada mundo concluiu três rodadas via HTTP e o MatchEngine existente, incluindo partida oficial do clube humano. Nesses dois ensaios o relógio foi comandado por ticks, sem fabricar resultados ou chamar provedores externos. Um terceiro mundo real completou outras três rodadas com o relógio normal da sala, sem chamadas HTTP de tick; evidência em normal-clock-acceptance.json. Um empréstimo repetido criou apenas uma dívida de R$ 500.000; R$ 25.000 de juros foram cobrados/pagos na partida elegível. O prazo zero não eliminou o principal. Salários, contratos, atletas e saldos foram recuperados sem alteração após reinício.

O modo A recusou a criação por falta de elencos; B sem consentimento não gerou jogadores. Em C, Brasil isolado produziu 128 clubes somente com autorização para equipes e atletas. A insuficiência sem autorização permaneceu bloqueada.

A identificação CLUBE REAL — ELENCO SIMULADO, origens de atletas, fontes, datas e classificação estimada/provisória aparecem na interface. Referências históricas ficam separadas do elenco atual. Não se declara nenhum elenco simulado como real completo.

Defeitos corrigidos: perda de aspas escapadas pelo leitor CSV, detectada no round-trip da proveniência; e risco de loop ao completar posições ausentes em um elenco já com 24 atletas. A substituição agora exclui a posição que está sendo adicionada, garantindo progresso. Um teste com 24 meio-campistas reais verifica conclusão, posições mínimas e identidades únicas. Metadados de origem e proteção salarial agora sobrevivem à exportação/importação; o formato antigo continua aceito. A primeira execução foi interrompida após as duas falhas de CSV, corrigida e seguida de regressão integral limpa de 551 casos. A revisão posterior do limite de 24 repetiu toda a suíte de domínio (388) e os dois fluxos PostgreSQL 013C. O consolidado registra 552 casos distintos, sem contar repetições. As 24 simulações aprovadas usam a fábrica legada e não chamam o gerador alterado; seus motores e fontes permaneceram idênticos. Os registros de cada fase foram preservados.

Execução local: iniciar scripts/test-playable-worlds-013c.ps1; build Release da solução com -p:OutputPath=bin/Playable013C/net10.0/ -p:AppendTargetFrameworkToOutputPath=false; scripts/run-playable-worlds-013c.ps1 -RealCatalog -ApiPort 5143. Para o cliente: flutter run -d chrome --dart-define=API_BASE_URL=http://127.0.0.1:5143. O runner não carrega fixtures no catálogo real. Credenciais/dados ficam em artifacts/013c fora do Git. O runner usa relógio normal por padrão. Somente a aceitação controlada usa -ManualClock e os endpoints de ticks.

Relatórios complementares: WORLD_DATA_MODES_013C.md, REAL_AND_SIMULATED_ROSTERS_013C.md, WORLD_CREATION_ACCEPTANCE_013C.md e KNOWN_ISSUES_013C.md. Evidência estruturada: audit-013c/verification.json.
