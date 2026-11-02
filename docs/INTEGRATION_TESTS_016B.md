# Validação técnica da missão

Data: 02/11/2026. Evidências exclusivas em artifacts/016b/world-catalog; não reutilizar as contagens de checkpoints anteriores como execução desta missão.

- Flutter: análise com zero apontamentos, 215 testes aprovados e build web final aprovada. Os 15 novos casos cobrem criador, catálogo e lobby em 360/390/430/768/1366/1920, consentimento e decisões do host.
- .NET Application: 7/7 e Domain: 438/438 na regressão desta missão; 13 novos casos de regras de catálogo.
- .NET Integration: execução integral final 254 casos, 252 aprovados e duas falhas investigadas. Uma era a asserção antes da confirmação de desconexão long-polling; corrigido o teste para aguardar o evento. Outra era entrada real-season-world.json ausente no novo diretório de evidências; copiada a entrada original sem alterar o teste de produto. A reexecução final de 19 casos, incluindo ambos, passou integralmente. Cobertura agregada: 254 casos aprovados; não chamar a execução inicial de verde.
- Catálogo/lobby novos: 18/18 aprovados (11 de sincronização e 7 de lobby); incluem capacidades 1/2/4/6/8, concorrência, autorização, categoria revisada, cache e retomada.
- Node: 2/2 da reconciliação openfootball-directory.
- Simulações específicas 016B: masculino e feminino, 100 temporadas cada, 35.100 jogos concluídos por cenário, partidas pelo MatchEngine real. Mundos e atletas fictícios autorizados, sem alegar elencos atuais reais.

| Cenário | Caixa inicial | Caixa final | Dívida final | Transferências pareadas | Minutos reais simulados |
|---|---:|---:|---:|---:|---:|
| Masculino | R$ 138.800.000 | R$ 96.636.018,48 | R$ 0 | 8.840 | 3.273.660 |
| Feminino | R$ 138.800.000 | R$ 93.597.013,60 | R$ 3.162.490,60 | 8.824 | 3.256.080 |

Zero violações de escalação, limite de elenco ou liquidação financeira duplicada nos dois cenários. A dívida feminina permanece registrada; não se declarou que todos os clubes terminaram solventes. O campo SourceCheckpoint nos summaries é herdado do helper antigo, portanto não identifica a base 016B desta execução; a base efetiva é 00b68c5 mais o diff desta missão.

A primeira execução dos novos testes usou um nome de experimento não registrado no helper econômico. Corrigida apenas a chamada para a configuração aprovada `final`; os dois cenários completos foram repetidos com sucesso. A regressão completa de simulações terminou com 28 casos: 26 aprovados e dois casos 014B falharam por variável de diretório de snapshots ausente. Esses dois foram repetidos com a entrada preservada e passaram (7 min 38 s). Portanto os 28 cenários estão validados, sem ocultar as falhas da execução inicial. A execução integral durou 35 min 2 s. Os resultados por identidade e horário individual dos testes estão consolidados em validation-summary.json.

Total .NET validado por caso único após as reexecuções: **727/727** (7 Application + 438 Domain + 254 Integration + 28 Simulation), nenhum ignorado. A integração integral durou 10 min 34 s. Testes parametrizados de nomes truncados são contados por TestId do TRX, evitando colapsar casos distintos. Nenhuma nova execução integral foi necessária depois de revalidar as causas já identificadas.

HTTP/SignalR em PostgreSQL passou com rodada real, reconexões, sorteio e início. Snapshot antes/depois do restart teve hash idêntico. Testes de protocolo do fornecedor são fixtures declaradas; ausência de chave nunca foi usada como sucesso de API externa. Chrome e aprovação manual permanecem pendentes.
