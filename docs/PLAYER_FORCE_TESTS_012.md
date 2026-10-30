# Testes e relatório final — 012

30/10/2026, America/Sao_Paulo. **STATUS: PARTIAL.** A correção de escala, histórico e regressões locais foram implementados. Não há comprovação executada das regras originais de evolução por rodada ou perda por contusão.

## Evidência e comandos

Executar na raiz `D:\elifoot_98`. Os logs ficam em [audit-012](audit-012/). A variável de saída é importante: os testes antigos foram ajustados para respeitá-la, preservando os relatórios 005–011. Nenhum banco de jogo existente foi substituído.

```powershell
$env:ELIFOOT_AUDIT_OUTPUT = 'D:\elifoot_98\docs\audit-012'
$env:ELIFOOT_FORCE012_MODE = 'baseline'
# Este comando de baseline foi executado antes da alteração de produção.
dotnet test tests\Elifoot.Simulation.Tests -c Release --filter FullyQualifiedName~Force012_100Seasons

$env:ELIFOOT_AUDIT_OUTPUT = 'D:\elifoot_98\docs\audit-012\regression'
dotnet test Elifoot.slnx -c Release --filter 'FullyQualifiedName!~Force012_100Seasons' --logger 'trx;LogFilePrefix=regression012' --results-directory docs\audit-012\tests

$env:ELIFOOT_AUDIT_OUTPUT = 'D:\elifoot_98\docs\audit-012'
$env:ELIFOOT_FORCE012_MODE = 'candidate'
dotnet test Elifoot.slnx -c Release --filter 'FullyQualifiedName~PlayerForce012|FullyQualifiedName~Force012_100Seasons' --logger 'trx;LogFilePrefix=force012-final' --results-directory docs\audit-012\tests

node scripts\report-player-force-012.mjs
Set-Location client\elifoot_flutter
flutter analyze
flutter test --reporter expanded
```

Para refazer uma comparação de código, a baseline deve ser executada novamente num checkout isolado do checkpoint 011 com **somente** a instrumentação dos testes 012. Executar `MODE=baseline` no código já corrigido não recria o comportamento anterior. Os arquivos before/after desta missão foram capturados na ordem correta.

A primeira restauração NuGet foi bloqueada pelo sandbox; a execução local autorizada restaurou dependências e prosseguiu. Logs da tentativa foram mantidos. Houve duas falhas de instrumentação corrigidas: um teste supunha que todo declínio vinha do alvo, ignorando o ramo de poucas aparições; outro comparava JSON indentado como texto. O código final usa comparação estrutural. Nenhuma dessas falhas foi ocultada. O primeiro logger direcionado à solução usou nome fixo e sobrescreveu seu próprio TRX entre projetos; o fechamento usa prefixos e preserva um arquivo por execução/projeto.

## Matriz obrigatória: comportamento atual versus fidelidade histórica

PASS abaixo significa teste do **projeto**, nunca teste do executável original. PENDENTE indica requisito histórico ainda não demonstrado, mesmo quando o comportamento local foi caracterizado.

| Nº | Caso | Teste/evidência local | Limite histórico |
| --- | --- | --- | --- |
| 1 | Força 1 | PASS: 1→2 sem salto para 10; matriz comprada/30 temporadas | Ritmo original pendente |
| 2 | Força 50 | PASS: teto e ausência de evento 50→50; preço/salário legados preservados | Escala sustentada por pesquisa |
| 3 | Titular | PASS: força nominal estável durante jogo, ganho sazonal controlado | Avaliador original pendente |
| 4 | Reserva | PASS: banco/entrada tratados separadamente | Elegibilidade original pendente |
| 5 | Reserva sem entrar | PASS: crescimento lento pelo ramo legado abaixo do alvo | Regra original pendente |
| 6 | Entrada no intervalo | PASS: substituição real e ausência de penalidade nominal | Não comprova mecanismo original |
| 7 | Entrada aos 80 | PASS: substituição real, aparição e força nominal preservada | Não se inventa fadiga |
| 8 | Líder | PASS: controle posição 1, log por rodada | Bônus original pendente |
| 9 | Último | PASS: controle posição 8; mesma trajetória que posições 1/4 | Penalidade original pendente |
| 10 | Promoção | PASS: histórico Quarta→Terceira→Segunda→Primeira; acessos reais na regressão | Base original pendente |
| 11 | Rebaixamento | PASS: contexto volta à Quarta; rebaixamentos reais na regressão | Não se deduz teto original |
| 12 | Limite | PASS: 1–50, força 48→49 nas cinco divisões, sem clamp na compra | Faixa ±5 contraditória nas fontes |
| 13 | Lesão em jogo | PASS: evento real com duração positiva | Frequência original pendente |
| 14 | Perda por lesão | **PENDENTE:** teste comprova ausência local de perda nominal | Não se implementou fórmula sem evidência |
| 15 | Já lesionado | PASS: exclusão com alternativas disponíveis | Exceção de emergência legada documentada |
| 16 | Recuperação | PASS: contador, idempotência, disponibilidade e nominal estável | Recuperação histórica de força pendente |
| 17 | Segunda lesão | PASS: segundo evento real no mesmo atleta recuperado | Risco especial original pendente |
| 18 | Goleiro | PASS: política sazonal atual e lesão real de goleiro | Guarda extra de 50% não ativada isoladamente |
| 19 | Troca de clube | PASS: transferência financeira real e preservação de eventos | Sem corte imediato por divisão |
| 20 | Persistência | PASS: SQLite, JSON/reload, força, clube e histórico | Não é novo ensaio PostgreSQL de produção |
| 21 | Idempotência | PASS: guard sazonal serializado; repetição após reload | Sem segundo mecanismo de evolução |
| 22 | Replay | PASS: snapshots/seed produzem mesmos eventos e preços; regressão de partidas | Não houve replay do original |
| 23 | Mercado | PASS: preço/salário atualizados, snapshot de força da contratação | IA de mercado inalterada |
| 24 | IA/disp. | PASS: escalação exclui lesionado quando há reposição; regressão ampla | Fallback de emergência preservado |
| 25 | Ranking histórico | PASS: consultas e evolução não reescrevem ranking/prêmios; regressão 009 | Cobertura de força começa agora |
| 26 | Multiplayer | PASS: oito avanços concorrentes, reload/reconexão e oito leituras iguais; suítes HTTP/SignalR | Sem nova partida Android/navegador |

## Resultados e distribuição

A regressão ampla aprovou 266 casos (221 domínio, 5 aplicação, 31 integração, 9 simulação). O fechamento acrescenta três casos de domínio e o novo ensaio de 100 temporadas: **270 casos .NET únicos aprovados**, incluindo os 238 anteriores. Repetições entre execuções não são contadas como casos novos. **50 testes Flutter aprovados**; análise estática sem problemas. O [verificador](audit-012/verification.json) faz a união por nome dos casos TRX finais e verifica preservação criptográfica.

Comparação principal: baseline 011 × candidato 012, seed 199801, **100 temporadas completas em cada lado**, 128 clubes, 2.200 rodadas e **35.100 partidas por lado**, pelo motor real. Histograma, mínimo, máximo e média por divisão e posição são idênticos em todas as observações iniciais/anuais. O seed padrão começa acima do mínimo corrigido; os ensaios de força 1 cobrem esse efeito separadamente. Não se espera alteração dos resultados desse mundo padrão.

A assinatura agregada do harness, preservada entre os lados, é `86071:41813:41898:77493:5515:116295:4993807894,75`. É uma assinatura de totais e saldo final, **não hash de cada lance ou de todo o estado**. O replay específico de força compara snapshots completos com os mesmos IDs; a suíte anterior também verifica determinismo do motor de partidas. Ambos os controles têm escopos diferentes.

Fontes: [baseline](audit-012/force-012/baseline-100-seasons.json), [candidato](audit-012/force-012/candidate-100-seasons.json), [verificação e distribuições finais](audit-012/verification.json). Não houve chegada em massa à força 50; o ensaio verifica limite absoluto e fração abaixo de 50% em cada observação anual.

A regressão ampla inclui ainda 100 temporadas com captura financeira 007, 152 temporadas do ensaio 006 (incluindo replays e estresse), 52 de carreira (50+dois replays), uma temporada 009 e matriz de 4.000 partidas táticas. São **305 temporadas completas adicionais aprovadas**, além da comparação 100+100. A matriz 012 contém **540 trajetórias anuais de atleta**, **7.560 partidas controladas** e **11.880 observações de rodada**; não são 540 campeonatos completos. Uma primeira execução candidata de 100 temporadas concluiu os jogos mas falhou na comparação de indentação do coletor; não foi contada como ensaio aprovado.

## Arquivos e preservação

Produção: novo `PlayerStrengthEvolutionEngine.cs` e contrato `PlayerStrengthDtos.cs`; estado persistente em Entities; chamada única em CompetitionAndSeasonEngine; projeção em PlayerStatisticsEngine/PlayerHistoryDto; painel em `player_rankings.dart`. Sem alteração de MatchEngine, defesa, IA, mercado, táticas, finanças, carreira, orquestrador multiplayer ou repositório EF. O preço é calculado pelas funções anteriores.

A execução ampla antecedeu o ajuste final que preserva a reprecificação legada quando um delta é limitado em 50→50. Esse caminho recebeu asserções específicas de preço/salário; o fechamento reexecutou os 28 casos de domínio 012, as duas integrações 012 e as 100 temporadas candidatas. As alterações posteriores no Flutter foram apenas restauração de formatação alheia ao escopo.

Testes novos: PlayerForce012Tests, PlayerForce012IntegrationTests, PlayerForce012SimulationTests, PlayerForce012ControlledTests e player_strength_012_test. Instrumentação opcional no harness existente e variável de saída nos geradores 008/009 evitam sobrescrever evidência antiga. Script de verificação: `scripts/report-player-force-012.mjs`. Os sete documentos 012 e artefatos próprios ficam no projeto.

## Relatório final obrigatório

```text
STATUS: PARTIAL
ORIGINAL_RESEARCH: concluída dentro das fontes públicas acessíveis; original não executado
EVIDENCE_QUALITY: técnica parcial, limites contraditórios, relatos históricos sem coeficientes
FORCE_1_PROGRESS: PASS local; 1→2 correto; 22 jogando/15 sem jogar após 30 anos controlados
RESERVE_PROGRESSION: PASS local; ramo sazonal abaixo do alvo, não comprovação do original
ON_FIELD_STRENGTH_CHANGE: sem penalidade nominal ao entrar; efeito temporário de lesão preservado
LEAGUE_POSITION_EFFECT: ausente na política local; original NAO_CONFIRMADO
DIVISION_LIMITS: escala 1–50; alvos legados não são tetos; base original desconhecida
GOALKEEPER_PROGRESS: política legada preservada; guarda pública de 50% ainda não integrada

INJURIES: evento/indisponibilidade/efeito de finalização existentes preservados
INJURY_DURATION: 1–3 jogos do clube; regra local, não confirmada historicamente
IMMEDIATE_STRENGTH_LOSS: não implementada; fórmula histórica pendente
PERMANENT_STRENGTH_LOSS: NAO_CONFIRMADO no original; ausente por lesão no projeto
RECOVERY_BEHAVIOR: disponibilidade ao zerar contador; sem restauração nominal automática
REINJURY: testada no clone; sem risco especial novo

OLD_SEASONAL_SYSTEM: auditado, isolado e preservado exceto mínimo incorreto 10
NEW_EVOLUTION_SYSTEM: correção 1–50 + histórico + idempotência; sem regra original inventada
NO_DUPLICATE_PROGRESS: PASS; uma chamada sazonal e trava persistida
DETERMINISTIC_REPLAY: PASS no clone; snapshots e ensaios de partida
PERSISTENCE: PASS SQLite/JSON, transferência e leitura HTTP
MULTIPLAYER_REGRESSION: PASS; concorrência, reconexão, HTTP/SignalR das suítes existentes

DOTNET_TESTS: 270 casos únicos PASS; falhas iniciais de instrumentação preservadas nos logs
FLUTTER_TESTS: 50 PASS; flutter analyze sem problemas
SIMULATED_SEASONS: 100 baseline + 100 candidato aprovados; mais 305 da regressão ampla
KNOWN_ISSUES: perda histórica por contusão e evolução original por rodada pendentes; ver KNOWN_ISSUES_012.md
```

O status não é PASS porque a principal mecânica histórica de contusão continua pendente. Acrescentar uma fórmula não sustentada só para encerrar a missão violaria a instrução de não inventar mecânicas.
