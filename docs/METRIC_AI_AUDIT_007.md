# Auditoria de métricas, IA, disciplina e recuperação financeira — 007

Data: 30/10/2026, America/Sao_Paulo. Projeto: D:\elifoot_98. **STATUS: PARTIAL**. As correções comprovadas e a regressão passaram. A cobertura da auditoria tem limites explícitos: o motor não atribui todas as faltas a jogadores; eventos de tentativa anteriores a007 não existiam; um snapshot recente não equivale à carreira inteira.

## Baseline, preservação e população

Git: `git status --short` e `git log -3 --oneline` retornaram “not a git repository”. Não houve reset, checkout, commit ou sobrescrita de mudanças alheias. Foram copiados75 arquivos de código/documentação antes de alterações, com SHA256 em [baseline-manifest.json](audit-007/baseline-manifest.json). Configurações e bancos DEV existentes não foram substituídos. Os relatórios005/006 originais continuam com seus hashes originais; resultados reexecutados foram direcionados por `ELIFOOT_AUDIT_OUTPUT` para subpastas próprias.

Baseline reproduzida: seed199801, temporadas1–100,128 clubes,128 treinadores IA,32 clubes de liga por temporada e96 distritais,2200 rodadas,35100 partidas reais. As contagens de100 temporadas não descrevem uma temporada isolada nem apenas os32 treinadores inicialmente nas ligas. Os27 testes anteriores passaram antes das correções. .NET SDK10.0.302; Flutter3.38.5; Dart3.10.4.

## Defeitos e decisões

| ID | Classe | Evidência | Tratamento |
|---|---|---|---|
| METRIC-007-01 | METRIC_ERROR | 8800 bilaterais comparadas a médias que incluem24525 leilões | Separar mecanismos na origem do coletor; projeção dos nove indicadores a partir de fatos únicos |
| BUG-007-01 | BUG | Controle0–100,0–3 aos83: abaixo65 não reage; acima65 escolhe segunda preferência defensiva | Reação gradual por placar/tempo, somente preferências próprias adequadas e banco disponível; cooldown10min |
| BUG-007-02 | BUG | Recarregar JSON elimina cache ignorado na serialização e repete liquidação | Guard de rodada persistido; reconstrução para saves006 com patrocínio completo; contratos, lesões, suspensões, empréstimos e obras testados |
| BUG-007-03 | BUG | SQLite: primeira gravação esperava256 lançamentos, tinha200 | Remover TakeLast(200); gravar todos os novos lançamentos; repetir save sem duplicar |
| HARDENING-007-01 | BUG / prevenção por invariante | Código permitia débito mesmo se a perna de crédito já existisse | Verificar ambas as chaves antes da transferência; teste de perna pré-existente |
| DOC-007-01 | DOCUMENTATION_ERROR | Pesos disciplinares e patrocínios descritos diferiam do código006 reproduzido | Corrigir documentação atual, preservar os relatórios históricos |
| DESIGN-007-01 | DESIGN_CHOICE | Fair Play tem peso0 documentado; demais eventos não geram cartões | Preservar; não inventar regra do executável histórico |
| DESIGN-007-02 | DESIGN_CHOICE | Patrocínio nas22 rodadas; salários apenas com jogo | Preservar valores; tornar dependência e recaídas transparentes |

O limiar65 foi uma escolha **intencional e documentada** em005, BALANCEAMENTO-002. Sua intenção era preservar identidade. O defeito reproduzido foi o comportamento extremo e a escolha cega de índice1, não simplesmente a existência de um limiar. A correção mantém as formações favoritas e seus pesos, sem mudar a escolha pré-jogo nem sortear novas preferências.

## Reconciliação das compras

Fonte: [TRANSFER_RECONCILIATION_007.json](TRANSFER_RECONCILIATION_007.json); registros canônicos completos no ledger comprimido indicado nesse JSON. Uma compra é o débito concluído, contado uma vez por FinancialTransaction.Id; a venda correspondente aponta para TransferTransactionId. Lances, tentativas, avaliações, ofertas pendentes, renovações e contratos gratuitos não são compras.

| Período/cenário | Bilaterais | Leilões pagos | Compras concluídas | Diferença contra soma por treinador |
|---|---:|---:|---:|---:|
| Baseline006 reconstituída,100 temporadas | 8800 | 24525 | 33325 | 0 |
| Candidato007, mesmas100 temporadas/seed | 8800 | 23373 | 32173 | 0 |

Na baseline, ofensivos44:12945 compras (3640 bilaterais+9305 leilões), média294,2045; equilibrados42:10314 (2504+7810), média245,5714; defensivos42:10066 (2656+7410), média239,6667. Valores impressos com arredondamento em005/006 não devem ser usados como registros inteiros. Não havia duplicidade: havia diferença de categorias incluídas.

As novas transações capturam PlayerId, BuyerClubId, SellerClubId, ManagerId responsável na execução, TransactionType, Amount, SeasonNumber e RoundNumber. O histórico é atribuído pelo ID imutável do treinador, mesmo que o clube troque de técnico depois. A oferta grava seu autor na submissão; o aceite usa o responsável na execução. A projeção `AiMarketMetrics.FromRecords` não mantém contadores paralelos. A transferência paga32173 inclui832 contratações gratuitas **fora dessa soma**: são outras movimentações, explicitamente FreeAgentSigning de valor0.

Indicadores007 nesta população: avaliações8922; tentativas8800; ofertas submetidas0; aceites de ofertas0; compras32173; lances150573; leilões vencidos23373; vendas8800; renovações38289. As ofertas são zero porque o cenário principal tem somente IA; o fluxo IA→humano→aceite foi executado separadamente em teste real do serviço. Tentativa é visita de execução após validações iniciais de identidade/idempotência ou submissão de proposta; uma proposta e sua execução posterior são etapas distintas. As definições precisas estão no JSON. Tentativas e lances de006 são **desconhecidos**, não zero.

## Qualidade das contratações

As32173 compras têm justificativa e snapshot de força, titular/reserva da posição, preço, FairValue, salário, posição, comportamento, estrela, formação e caixa restante. 17969 têm força inferior à referência titular; isto, isoladamente, não é defeito. As categorias diferenciam cobertura, titular urgente, melhoria titular e oportunidade. Nenhuma excedeu1,25x FairValue; maior razão observada 1.248791. O teste de força11 por R$2milhões rejeita a compra com o mercado ativo. Não se alterou ranking de alvos, orçamento, curva de preço ou diversidade estratégica.

A ordenação atual prioriza listados e depois utilidade por preço dentro de até6 candidatos por posição. Isso não prova ótimo global; não foi encontrado um caso reproduzido que justifique trocar essa estratégia. Não se afirma que todos os jogadores não examinados eram alternativas disponíveis/vendáveis equivalentes.

## Adaptabilidade e personalidade

[AI_ADAPTABILITY_007.json](AI_ADAPTABILITY_007.json) responde às dez perguntas e registra33 controles (11 atributos × empate/desvantagem1/desvantagem3) com elencos e seed iguais. Esses são estados controlados de decisão, não resultados de partidas inventados. As alterações de gameplay foram verificadas em100 temporadas pelo motor real.

Perdendo: minuto mínimo `clamp(84 - A*0.28 - max(0,desvantagem-1)*8,55,84)`; protegendo vantagem: `clamp(88 - A*0.14 - max(0,vantagem-1)*3,70,88)`. A avaliação continua nos checkpoints62/74/83/112 e intervalos; lesões permitem avaliação imediata. Cooldown10min baseado nos eventos efetivamente aplicados. Não acrescenta avaliações a cada minuto. Apenas preferências próprias que respondam ao placar ou reduzam improvisações são elegíveis, com encaixe e quotas. Sem opção adequada, mantém a formação. Não substitui expulso nem repõe a vaga perdida.

64 e65 tomam as mesmas decisões nos controles registrados. A0 já pode reagir à desvantagem3 (no cenário sequencial, aos74); A100 reage antes. Empate não dispara mudança aleatória de formação. Substituição, formação e pré-escalação são indicadores distintos. Houve58 treinadores que mudaram formação em jogo na baseline e128 no candidato, ao longo de100 temporadas, não em todo jogo. Preferências/identidade/atributos continuam na entidade persistente do treinador.

## Disciplina e distribuição das expulsões

O arquivo inteiro MatchEngine.cs é idêntico à baseline: SHA256 **C4A053C673F0044D89C035DC323103F6CB760DC6594C74FF7902549FCD07DF9B**. Nenhuma probabilidade de gol, peso disciplinar ou fórmula defensiva foi recalibrada nesta missão. A variação observada vem das decisões da IA e de suas consequências ao longo das temporadas.

| Comportamento — baseline100 temporadas | Minutos em campo | Participações com minuto | Amarelos | Diretos | Segundo amarelo | Sanções de suspensão | Vermelhos/10000min |
|---|---:|---:|---:|---:|---:|---:|---:|
| FairPlay | 11181618 | 131812 | 0 | 0 | 0 | 0 | 0.0000 |
| Cordeirinho | 12363189 | 147843 | 4874 | 20 | 26 | 1542 | 0.0372 |
| Cavalheiro | 20770586 | 247352 | 17472 | 218 | 211 | 5988 | 0.2065 |
| Caneleiro | 14503461 | 177922 | 22650 | 564 | 445 | 8308 | 0.6957 |
| Caceteiro | 8753557 | 108084 | 21130 | 1114 | 978 | 8827 | 2.3899 |
| Sarrafeiro | 3834105 | 49042 | 12996 | 1297 | 862 | 6246 | 5.6310 |

Minuto de expulsão conta; reserva entra na exposição a partir do próximo minuto simulado. Suspensões são sanções geradas por expulsão/terceiro amarelo, não jogos efetivamente cumpridos. Fair Play não gera falta intencional/cartão nos mecanismos implementados, mas participa de gols, lesões, impedimentos e substituições. Não existe evento disciplinar de reclamação neste modelo; sua imunidade a tal evento não pode ser testada como se ele existisse.

[PLAYER_BEHAVIOR_DISCIPLINE_007.json](PLAYER_BEHAVIOR_DISCIPLINE_007.json) contém diretos/segundos amarelos, comportamento, formação no evento, divisão, força individual, minuto, mando e ID do jogador, com exposição individual e taxas por comportamento. Baseline5735=3213+2522; candidato5717=3175+2542. Pesos ordenados0/0,35/0,75/1,45/2,35/3,60, com cautela após amarelo. Cordeirinho e Cavalheiro são distintos; Caceteiro/Sarrafeiro têm maior taxa normalizada.

**Limite de atribuição:** faltas sem cartão e pênaltis não escolhem/registram infrator individual. Não é possível calcular sua distribuição exata por comportamento em equipes mistas sem inventar dados ou alterar o motor. Por isso esses campos são null. Executaram-se2400 partidas controladas,400 por comportamento, para medir faltas exatas em equipes homogêneas, mantendo o motor real e as mesmas seeds. A amostra de400 não transforma ausência ocasional de vermelho de Cordeirinho em probabilidade zero.

## Os21 clubes em crise de006

Crise é caixa abaixo deR$150mil no pós-mercado da rodada, com clube nas ligas. Recuperação é atingirR$350mil; não significa falência anterior nem saúde perpétua. A contagem original21/21 foi reproduzida. Há27 episódios recuperados entre esses21 clubes;5 recaíram e16 cumprem a definição observacional de sustentabilidade (nenhuma recaída amostrada, pelo menos22 rodadas posteriores, caixa final≥350mil). Nenhuma previsão após a temporada100 é feita.

| Clube | Primeira crise T/R | Caixa ao entrar | Rodadas até primeira recuperação | Principal receita da primeira recuperação | Recaídas | Caixa final T100 |
|---|---:|---:|---:|---|---:|---:|
| Brasil de Pelotas | 15/12 | 53.349,76 | 7 | RECOVERY_BY_REVENUE | 0 | 22.609.919,80 |
| São Bento de Imperatriz | 15/22 | 101.493,93 | 3 | RECOVERY_BY_REVENUE | 0 | 36.513.158,56 |
| Avaí da Ilha | 16/10 | 64.110,67 | 5 | RECOVERY_BY_REVENUE | 0 | 16.874.263,37 |
| Sampaio Boliviano | 17/18 | 60.225,51 | 5 | RECOVERY_BY_REVENUE | 0 | 2.988.432,14 |
| Vila Nova Colorada | 19/2 | 135.224,60 | 3 | RECOVERY_BY_REVENUE | 2 | 19.340.501,55 |
| Paysandu da Amazônia | 24/18 | 50.125,10 | 7 | RECOVERY_BY_REVENUE | 1 | 13.975.859,01 |
| Santa Cruz Coral | 26/2 | 106.414,94 | 1 | RECOVERY_BY_REVENUE | 0 | 7.237.139,14 |
| Ferroviário de Campo Grande | 35/18 | 13.917,85 | 4 | RECOVERY_BY_REVENUE | 0 | 4.902.728,74 |
| Criciúma Carvoeiro | 44/12 | 97.224,40 | 2 | RECOVERY_BY_REVENUE | 1 | 1.453.203,42 |
| Nacional de Uberlândia | 46/6 | 72.659,36 | 2 | RECOVERY_BY_REVENUE | 1 | 10.616.425,74 |
| Atlético de Campo Grande 2 | 55/8 | 27.382,72 | 9 | RECOVERY_BY_REVENUE | 0 | 9.971.332,32 |
| União de Imperatriz | 58/6 | 45.011,84 | 6 | RECOVERY_BY_REVENUE | 0 | 11.622.539,13 |
| Desportivo de Uberlândia | 59/6 | 137.417,89 | 2 | RECOVERY_BY_REVENUE | 0 | 15.998.094,67 |
| Independente de Petrópolis 2 | 60/20 | 37.262,72 | 2 | RECOVERY_BY_COMPETITION_REWARD | 0 | 92.259.704,23 |
| Sport Club Atlântico | 65/8 | 92.144,92 | 3 | RECOVERY_BY_REVENUE | 0 | 1.008.093,10 |
| Esportivo de Imperatriz 2 | 65/8 | 11.543,25 | 3 | RECOVERY_BY_REVENUE | 0 | 10.929.819,17 |
| ABC Potiguar | 75/2 | 17.920,24 | 3 | RECOVERY_BY_REVENUE | 0 | 8.567.238,31 |
| Real de Imperatriz | 75/12 | 143.131,36 | 3 | RECOVERY_BY_REVENUE | 1 | 840.693,89 |
| Ferroviário de Arapiraca 2 | 79/18 | 43.133,39 | 2 | RECOVERY_BY_REVENUE | 0 | 12.193.668,49 |
| Primavera de Ponta Grossa | 83/2 | 88.903,14 | 2 | RECOVERY_BY_SALES | 0 | 16.536.190,38 |
| Associação de Uberlândia | 88/8 | 67.711,93 | 2 | RECOVERY_BY_SALES | 0 | 6.661.417,17 |

Cada episódio em [FINANCIAL_RECOVERY_007.json](FINANCIAL_RECOVERY_007.json) traz causa observada/fluxos de entrada, receitas e despesas posteriores, vendas identificáveis, folha antes/depois, obras e situação final. Entre27 episódios:23 predominantemente por receitas,2 por premiação e2 por vendas. Uma receita dominante não prova causalidade exclusiva. Na nova trajetória:19 clubes atingiram crise,19 recuperaram pelo menos uma vez,8 recaíram e11 satisfizeram o critério observacional de sustentabilidade.

Patrocínio é receita externa explícita:185/125/92/72mil por rodada nas quatro divisões e20mil no Distrital. Total da baselineR$12.566.400.000 em100 temporadas. Continua sendo pago em22 rodadas, mesmo após eliminação, enquanto salários/contratos exigem participação em jogo. Essa política intencional de005 sustenta caixa e favorece recuperação; não foi identificada transferência bilateral criadora de dinheiro nem crédito emergencial oculto. Não se igualou patrocínio automaticamente a subsídio indevido. O cálculo sem patrocínio no JSON é aritmético com decisões fixas, não outra simulação econômica.

## Conservação, liquidação e estádios

Baseline: R$3.317.455.881,00 debitados e creditados; candidato: R$3.314.681.535,00 em cada lado; diferençaR$0,00 em ambos. Zero IDs/chaves duplicados e zero pares bilaterais ausentes/desbalanceados. Caixa inicial candidatoR$138.800.000,00; finalR$5.664.382.164,15; todos os fluxos e saldos foram reconciliados em centavos inteiros. Saldo inicial é estoque de abertura, não receita operacional. Arremates de atletas sem contrato são saída para fora da economia de clubes, sem vendedor; não devem ser confundidos com transferências conservadas entre dois clubes.

O teste de reload verifica salários, contratos, lesões, suspensões, empréstimos, reforma e ampliação sem processar duas vezes. O teste SQLite verifica256 registros na primeira gravação e idempotência da segunda. A serialização do guard de rodada não exige alterar o schema SQL; metadados novos vivem no snapshot JSON. A tabela relacional conserva os campos financeiros anteriores, agora sem truncar os novos lançamentos.

Estádios: custos fixos/variáveis, manutenção, desgaste, ocupação, condição, reformas e ampliações aparecem no fluxo por clube. O clube controlado em crise não pode iniciar ampliação sem caixa; decisão sazonal respeita seus limiares de caixa/ROI. Não foram alterados coeficientes, capacidade, preço ou política de manutenção. Caixa baixo pode impedir manutenção e deteriorar capacidade; receitas continuadas podem restaurar capacidade financeira. Não se afirma causalidade exclusiva do estádio onde há simultaneamente transferências e receitas.

## Regressão e limitações

**52 casos .NET únicos aprovados**, incluindo os27 anteriores, e8 testes Flutter. Build Release sem avisos/erros; Flutter analyze sem problemas. O total52 é a união dos49 casos não estatísticos do fechamento com os3 estatísticos já aprovados após a correção tática. Após essa execução longa só houve projeção de métricas, persistência SQL, documentação e reforço de testes; o gameplay não mudou novamente. Não foram repetidas100 temporadas para uma remoção de TakeLast na persistência. Consulte [REGRESSION_TESTS_007.md](REGRESSION_TESTS_007.md) para comandos e TRX.

Multiplayer: testes HTTP/SignalR em TestServer com repositório InMemory, salas2/4/6/8, ciclo de100 rodadas, pausa global, quotas e reconexão. SQLite em memória foi executado para persistência financeira. **Não é validação PostgreSQL de produção nem partida Android**. A verificação externa da porta5080 encontrou o servidor parado; os servidores dos testes são efêmeros. O script DEV existente permite iniciar uma sessão local. Não foi substituído seu banco para fazer smoke test.

A primeira captura financeira completa recontou os128 saldos iniciais como fluxo novo e seu assert de instrumentação falhou. Os35100 jogos e arquivos brutos terminaram; a reconciliação independente corrigiu abertura versus fluxo sem reescrever os brutos. O coletor final já exclui a abertura e valida0 divergências. Falha inicial NuGet por sandbox foi resolvida com execução local autorizada. Logs brutos, inclusive reproduções com falha, foram preservados.

## Arquivos e reprodução

Modificados (comparação com cópia original):

- tests/Elifoot.Simulation.Tests/MultiSeasonAndBenchmarkTests.cs
- src/Elifoot.Infrastructure/EfWorldRepository.cs
- src/Elifoot.Application/ClubAndMarketApplicationService.cs
- docs/ECONOMIA_ESTADIOS.md
- docs/IA_TREINADORES.md
- docs/MOTOR_DEFESA.md
- docs/MERCADO_IA.md
- src/Elifoot.Contracts/Enums.cs
- src/Elifoot.Domain/CompetitionAndSeasonEngine.cs
- src/Elifoot.Domain/AiManagerBrain.cs
- src/Elifoot.Domain/Entities.cs
- src/Elifoot.Domain/MarketAndEconomyEngine.cs

Criados em código/scripts:

- src/Elifoot.Domain/AiMarketMetrics.cs
- tests/Elifoot.Domain.Tests/MetricAiAudit007Tests.cs
- tests/Elifoot.Integration.Tests/FinancialPersistence007Tests.cs
- tests/Elifoot.Simulation.Tests/MetricAudit007Capture.cs
- scripts/report-metric-ai-007.mjs
- scripts/finish-metric-ai-007.mjs

Entregas: este relatório; TRANSFER_RECONCILIATION_007.json; AI_ADAPTABILITY_007.json; PLAYER_BEHAVIOR_DISCIPLINE_007.json; FINANCIAL_RECOVERY_007.json; ECONOMIC_INTEGRITY_007.json; REGRESSION_TESTS_007.md; KNOWN_ISSUES_007.md. Evidências em docs/audit-007, com baseline, logs, TRX, ledger/ações/fluxos comprimidos e verificação de hashes. O arquivo source-diff.patch registra as diferenças nos arquivos existentes.

Próximas recomendações: usar os IDs/eventos e escopo explícito nos relatórios; não interpretar snapshot de duas temporadas como carreira inteira; decidir em tarefa própria se é necessário ampliar retenção consultável; acompanhar patrocínio/caixa e recaídas com playtest antes de ajustar economia; só adicionar atribuição de faltas se houver requisito de produto que justifique alterar telemetria do motor. Nenhuma publicação em produção.

## Relatório final obrigatório

```text
STATUS: PARTIAL — limites de dados históricos/granularidade e retenção descritos; correções e regressão PASS
TRANSFER_METRICS: PASS — fatos únicos e mecanismos separados
TOTAL_TRANSFERS: 32173 compras pagas na trajetória007 de100 temporadas;832 movimentações gratuitas separadas
DIRECT_PURCHASES: 7107 lista direta +1693 negociações IA/IA; propostas aceitas0 neste cenário
AUCTION_PURCHASES: 23373
PURCHASE_ATTEMPTS: 8800; avaliações8922; lances150573
PURCHASES_COMPLETED: 32173
RECONCILIATION: PASS —32173 por comprador=32173 por treinador; diferença0; duplicidades0
METRIC_ROOT_CAUSE: total bilateral comparado a médias que também incluíam leilões
AI_ADAPTABILITY: PASS nos controles e100 temporadas reais
THRESHOLD_65: intencional em005; substituído após defeito extremo comprovado
BELOW_65_BEHAVIOR: reage a desvantagem extrema quando há preferência adequada e recursos
ABOVE_65_BEHAVIOR: antecipa reação gradualmente, sem mudança aleatória em empate
TACTICAL_PERSONALITY: preservada; usa somente preferências próprias
FIX_APPLIED: tempo/placar, encaixe, banco, quotas, cooldown10min; nenhuma alteração pré-jogo
DISCIPLINE: pesos/calibração006 preservados; granularidade limitada explicitamente
FAIR_PLAY:0 cartões; regra documentada do projeto
CORDEIRINHO: baseline0,0372 vermelhos/10000min
CAVALHEIRO: baseline0,2065 vermelhos/10000min
CANELEIRO: baseline0,6957 vermelhos/10000min
CACETEIRO: baseline2,3899 vermelhos/10000min
SARRAFEIRO: baseline5,6308 vermelhos/10000min
RED_CARD_DISTRIBUTION: JSON com evento, minuto, mando, jogador, força, divisão e formação
SECOND_YELLOW: PASS;2522 baseline/2542 candidato; suspensão/removal verificados
FINANCE: fluxos reconciliados; patrocínio e recaídas explícitos
CRISIS_CLUBS: baseline21; candidato19 (mesmo seed/período, trajetórias diferentes)
RECOVERED_CLUBS: baseline21; candidato19, ao menos uma vez
RECOVERY_MECHANISMS: baseline27 episódios:23 receitas/2 premiações/2 vendas; sem crédito emergencial oculto
SUSTAINABLE_RECOVERY: baseline16 sem recaída amostrada; candidato11, conforme definição observacional
TRANSFER_MONEY_CONSERVATION: PASS; diferençaR$0,00 em ambas as populações
FINANCIAL_IDEMPOTENCE: PASS; memória/reload/save006/SQLite, com defeitos corrigidos
STADIUM: sistema preservado; clube sem caixa não financia expansão no teste
STADIUM_FINANCIAL_EFFECTS: bilheteria, operação, manutenção e obras rastreadas nos fluxos e capturas
MATCH_ENGINE: real minuto a minuto; código inteiro inalterado
DEFENSE_PRESERVED: PASS, SHA256 idêntico à baseline
DETERMINISTIC_REPLAY: PASS de assinatura de gameplay em duas temporadas iguais por seed
MULTIPLAYER_REGRESSION: PASS HTTP/SignalR/TestServer/InMemory;2/4/6/8 e100 rodadas
DOTNET_TESTS:52 casos únicos PASS (49 fechamento+3 estatísticos pós-fix);27 anteriores preservados
FLUTTER_TESTS:8 PASS; analyze sem problemas
FILES_MODIFIED: lista acima e audit-007/source-diff.patch
FILES_CREATED:6 fontes/scripts listados,8 entregas obrigatórias e evidências audit-007
KNOWN_ISSUES: KNOWN_ISSUES_007.md; não há novo PASS Android/PostgreSQL/visual
NEXT_RECOMMENDATIONS: retenção consultável e atribuição de faltas em escopo próprio; observar economia antes de retocar valores
```

