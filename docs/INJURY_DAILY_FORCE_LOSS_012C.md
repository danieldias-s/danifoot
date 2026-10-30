# Perda diária por lesão — 012C

STATUS: PASS. Adaptação autorizada, versão 1.

Após a ocorrência, duração d usa os pesos exatos de 2 a 15 dias. O orçamento máximo é floor(forçaInicial × d / 30), limitado também por forçaInicial−1. Sorteia-se uniformemente um inteiro entre zero e esse máximo, incluindo os extremos. Não existe perda nominal na hora da lesão. A força inicial e o orçamento são imutáveis no episódio, inclusive após transferência ou ganho de reserva.

No dia virtual k, a perda acumulada planejada é floor(totalSorteado × min(k,d) / d). A parcela é a diferença entre acumulados consecutivos. ProcessedDays avança mesmo quando a parcela é zero ou o piso 1 impede perda efetiva; ActualLoss registra apenas pontos efetivamente retirados. Dias e datas ficam no save, sem usar relógio do computador.

Exemplo máximo: força 50, d=15, total=25. Parcelas: 1, 2, 2, 1, 2, 2, 1, 2, 2, 1, 2, 2, 1, 2, 2. Soma 25; força final 25 antes de eventuais ganhos. Duração 2 pode tirar no máximo 3 de força 50; duração 8 pode tirar 13. Nem todo episódio sorteia o máximo. Recuperação acontece em StartDate+d e não restaura pontos.

O calendário aplica cada parcela pendente na sua data antes da partida. Um salto de vários dias executa as parcelas intermediárias em ordem, com histórico individual. Não se permite avançar data com partida em andamento. Na mesma data, INJURY_DAILY_LOSS pode anteceder RESERVE_PROGRESS como registros separados. DIV1 líder força 50 lesionado pode terminar acima de 25 pelos ganhos; DIV4 líder força 50→25 não ganha enquanto acima do teto 12; DIV3 último força 5 pode ganhar e continuar perdendo as parcelas previstas. A perda normal PLAYED_MATCH_DECLINE é omitida para quem se lesionou naquela partida.

Testes exercitam 2/8/15 dias, total máximo, total menor, zero, piso 1, reincidência, transferência, interrupção/releitura SQLite, mesmo dia com dois motivos e repetição da liquidação. Em 100 temporadas houve 11.866 episódios, 5.459 com orçamento zero, 5.379 sorteados no máximo permitido (inclusive casos cujo máximo era zero). Orçamento agregado 14.805 pontos; perda efetiva 14.805 pontos, sem exceder orçamento por episódio e sem repetição de InjuryId+data. Os registros brutos de episódios são capturados na criação; os eventos comprimidos registram a execução das parcelas.

Evidência: docs/audit-012c/regression/classic-force-012c/injuries.jsonl.gz e force-events.jsonl.gz; verificação automática em scripts/report-classic-force-012c.mjs.
