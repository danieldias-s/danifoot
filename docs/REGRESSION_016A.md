# Regressão executada — 016A

Resultado final por suíte: **680/680 .NET, 173/173 Flutter, 2/2 Node**, sem testes ignorados nas suítes .NET. Flutter analyze: zero issues. Flutter build web: sucesso. Build final `dotnet build Elifoot.slnx -c Release`: zero avisos/erros.

| Suíte .NET | Baseline 456cd40 | Final executado |
| --- | ---: | ---: |
| Application | 7 | 7 |
| Domain | 409 | 425 |
| Integration | 198 | 222 |
| Simulation | 26 | 26 |
| Total | 640 | 680 |

A base real possui 168 testes Flutter, acrescidos de 5 da importação. As 16 adições de domínio cobrem política/proveniência, compatibilidade de enums, consentimento, geração, seleção 11/24/26, força determinística e transferências por ID. As 24 adições de integração cobrem protocolos isolados e persistência, com identificação explícita de fixtures.

## Execução e falhas corrigidas

`dotnet test Elifoot.slnx -c Release --no-build` foi executado. A primeira integração teve 217 aprovados e quatro falhas porque as guardas de segurança dos testes não conheciam a nova porta 55441. Foram ampliadas somente para localhost, prefixo de banco 016A e a porta exclusiva. Nenhuma asserção foi removida. O conjunto integral de integração foi reexecutado e terminou em **222/222**, já com todas as correções.

A primeira execução de domínio tinha 423 testes; a execução final tem 425/425 após incluir reconciliação de transferências. Application final: 7/7. Simulation: 26/26 em 1h09, executada nesta missão, com motor/economia/força idênticos à base. A correção posterior de precisão e de cobertura está no importador, não nos cenários legados da simulação; sua validação usa a suíte final de integração. A contagem 680 é a soma das execuções finais das quatro suítes, não a alegação de que a primeira chamada da solução passou integralmente.

A auditoria real também encontrou precisão de timestamp de PostgreSQL diferente da consulta HTTP: uma nova prévia em cache gerava versão redundante. O importador normaliza a microsegundo antes da criação do pacote e recupera UTC explicitamente no SQLite. Testes agora aplicam uma **nova** prévia em cache antes/depois do reinício e comparam os IDs dos snapshots. Os dois bancos passam. Uma prévia só de IDs também conserva a última cobertura de elenco coletada.

Durante a simulação, uma tentativa de rebuild encontrou DLL carregada pelo testhost. Uma build isolada foi usada sem interromper o processo. Após o término da suíte, a build normal final também passou. Logs antigos de falha foram preservados e não confundidos com resultados finais.

## Matriz da missão

| Área | Evidência efetiva |
| --- | --- |
| Chave ausente, autenticação, loopback/admin | Protocolos e HTTP local; zero segredo em respostas |
| Indisponibilidade, quota diária, 429, payload de limite/maxlag | Protocolos determinísticos; orçamento persistente e sem repetição externa após bloqueio |
| Vazio, parcial, posição ausente, equipe errada, temporada antiga | Rejeição/pendência explícita; catálogo anterior conservado |
| Nome ambíguo, masculino/feminino | Revisão de ID e categoria; seis identidades reais consultadas |
| ID duplicado, transferência, histórico | Deduplicação, vínculo mais recente unívoco; empate para revisão; não ativa histórico |
| DRY RUN, APPLY, rollback | Sem mutação do catálogo no preview; lote transacional e recibo persistido |
| Reinício/cache/idempotência | SQLite e PostgreSQL; API DEV reiniciada e replay real sem novos snapshots ou chamadas |
| Mundos masculino/feminino | HTTP real, PostgreSQL, 128 clubes e consentimento para 2.304 gerados por mundo; R3 oficial concluída |
| UI criador/plantel | Terceiro mundo criado no Chromium; 18 jogadores do Flamengo iguais à prévia congelada |
| Limites 11/24 e >24 no catálogo | Casos de política e protocolo com 26 atletas, sem cortar o armazenamento |
| Sem complementação / com autorização | Modo estrito bloqueia insuficiência; opção explícita para geração |
| Snapshot/contratos/salários | Comparação exata da prévia e plantel; parâmetros positivos simulados |
| Saves antigos / motor / economia | Hashes de mundos durante atualização e reinício; arquivos protegidos idênticos à base; worktrees anteriores sem mudanças rastreadas |
| Publicação | Nenhum push, merge ou deploy |

Os protocolos sintéticos não comprovam cobertura atual da API-Football. O piloto real usou somente Wikidata autorizado e permanece INCOMPLETE. Não há importação atual autenticada real aprovada.

## Simulações de 100 temporadas realmente executadas

| Cenário legado reexecutado | Partidas | Caixa final | Dívida final |
| --- | ---: | ---: | ---: |
| Equilíbrio operacional 012D_FIX | 35.100 | R$ 137.353.919,79 | R$ 3.270.104,68 |
| Mundo 014B masculino | 35.100 | R$ 86.753.694,92 | R$ 0,00 |
| Mundo 014B feminino | 35.100 | R$ 103.405.567,05 | R$ 0,00 |
| Criador 014A masculino | 35.100 | R$ 79.157.786,17 | R$ 4.366.066,93 |
| Criador 014A feminino | 35.100 | R$ 79.157.786,17 | R$ 4.366.066,93 |

Cada cenário acima tem 100 temporadas concluídas e zero violações de escalação, limite de elenco e liquidação financeira duplicada. São cenários legados, separados do piloto sem elenco atual de 016A; campos SourceCheckpoint dentro dos JSONs são rótulos herdados do teste, não o Git HEAD executado. [Resumos completos](evidence-016a/simulation-summaries.json). A suíte também reexecutou disciplina, força, replay determinístico e benchmarks. Não se introduziu índice de inflação nem alteração econômica nesta missão.

[TRXs e testes executados](evidence-016a/tests/executed-results.json) · [Isolamento e hashes](evidence-016a/isolation.json) · [Navegador](evidence-016a/browser-qa.json).
