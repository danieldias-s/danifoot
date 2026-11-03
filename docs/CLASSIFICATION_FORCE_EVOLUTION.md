# Evolução de força por classificação

A rodada oficial produz uma decisão por atleta depois de aplicar todos os resultados. A posição é a classificação esportiva do clube dentro da divisão. A Taça usa a última classificação válida da liga. Antes da primeira rodada de liga, o sistema conserva a referência provisória do 4º lugar. Ao iniciar outra temporada, a classificação anterior deixa de servir de referência.

| Posição | 4ª divisão | 3ª divisão | 2ª divisão | 1ª divisão |
| ---: | ---: | ---: | ---: | ---: |
| 1 | 12 | 25 | 38 | 50 |
| 2 | 11 | 24 | 37 | 50 |
| 3 | 10 | 23 | 36 | 50 |
| 4 | 8 | 21 | 34 | 46 |
| 5 | 6 | 19 | 32 | 44 |
| 6 | 4 | 17 | 30 | 42 |
| 7 | 2 | 15 | 28 | 40 |
| 8 | 1 | 14 | 27 | 39 |

`ChampionshipStrengthLimits` é a tabela de domínio compartilhada com a referência de fundação da 017B. As faixas do sorteio de fundação continuam na 017B. `ClubInitialPower` continua sendo a métrica de ordenação do catálogo.

- Quem ficou fora ganha exatamente um ponto se estiver abaixo do limite do clube atual. Isso inclui banco não utilizado, suspensão, lesão que impediu a entrada e clube de folga na Taça. Força no limite ou acima dele permanece igual. Distritais e agentes livres têm referência 1, sem possibilidade de ganho pela regra.
- Quem começou em campo ou entrou por substituição oficial tem uma decisão determinística de 50% para perder um ponto. A chance usa seed, temporada, rodada e identidade do atleta. A força 1 é o piso. Uma entrada registrada no último minuto conta mesmo sem minuto inteiro decorrente. Participações repetidas na rodada dão uma decisão só. Em transferências vale o clube atual no fechamento para o contexto do histórico e o limite de crescimento.
- No modo clássico, as perdas diárias de uma lesão são aplicadas pelo calendário antes do fechamento esportivo e continuam até a data de recuperação. Se o atleta se lesionou em campo na rodada, o episódio de lesão tem precedência e não recebe também a perda de participação. Quem já estava lesionado e ficou fora segue a regra de ausência sobre a força após perdas diárias. Recuperar a disponibilidade não restaura pontos. A suspensão tem seu próprio processamento e contador.
- Saves legados conservam seu relógio e regras de lesão. A nova decisão passa a valer no próximo fechamento, incluindo o legado que usava evolução sazonal. O calendário oficial deixa de aplicar o ajuste antigo no fim da temporada. A reabertura de uma rodada já contabilizada preserva seus resultados e forças. A marca de temporada e rodada no snapshot impede repetir as decisões.

O fechamento preserva os valores de salário e de contrato e usa o cálculo de valor de mercado existente para refletir a nova força. Nenhum arquivo do MatchEngine, da disciplina, da economia ou dos layouts foi alterado.

## Resultado no motor real

Foram executadas duas simulações de 100 temporadas, com 35.100 partidas cada. Ambas usaram seed 199801, a mesma geração inicial da 017B, 128 clubes e controle por IA. A execução anterior foi compilada de `1ecfc6a` em uma cópia descartável. As médias abaixo são ponderadas por atleta e medidas antes das promoções e rebaixamentos. [Simulação anterior](evidence-classification-force/baseline-100-seasons.json) · [Simulação nova](evidence-classification-force/current-100-seasons.json)

| Divisão | Média inicial | Média final anterior | Média final nova | Faixa das médias dos clubes na regra nova | Força 1 na regra nova | Força 50 na regra nova |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 1ª | 44,65 | 25,02 | 30,06 | 22,06–37,41 | 2 | 7 |
| 2ª | 32,66 | 11,96 | 23,47 | 20,05–29,79 | 5 | 0 |
| 3ª | 19,85 | 8,37 | 14,49 | 5,42–21,46 | 11 | 0 |
| 4ª | 6,75 | 4,09 | 5,18 | 1,00–10,36 | 30 | 0 |
| Distrital | 15,51 | 1,14 | 1,02 | 1,00–1,95 | 1.742 | 0 |

| Temporada | Média global anterior | Média global nova | Atletas força 1, anterior → nova | Atletas força 50, anterior → nova |
| ---: | ---: | ---: | ---: | ---: |
| Fundação | 18,15 | 18,15 | 12 → 12 | 13 → 13 |
| 1 | 17,59 | 17,19 | 24 → 23 | 1 → 8 |
| 25 | 8,33 | 5,70 | 257 → 1.546 | 0 → 2 |
| 50 | 5,10 | 5,63 | 1.032 → 1.753 | 0 → 2 |
| 75 | 3,89 | 5,27 | 1.567 → 1.803 | 0 → 5 |
| 100 | 4,15 | 5,23 | 1.729 → 1.790 | 0 → 7 |

- As perdas de titulares passaram de **92.325 para 296.671** e as de reservas utilizados de **21.128 para 58.580**. Ganhos por ausência passaram de **92.722 para 342.299**, incluindo **230.106** na Taça. As perdas por lesão totalizaram **15.462 pontos** com a regra nova e **10.610** com a anterior. A fórmula de lesões ficou igual nas duas execuções, mas o estado esportivo e os atletas expostos mudaram ao longo das temporadas.
- A concentração no piso é o efeito mais forte. Na 100ª temporada, **1.742 de 1.765 atletas distritais** estavam em força 1 (98,7%), ante 1.599 de 1.709 na referência (93,6%). A média global caiu mais cedo, apesar de terminar acima da referência. As quatro divisões de liga terminaram com médias ordenadas, com sobreposição entre equipes de divisões vizinhas.
- As duas execuções registraram 8.800 compras e quatro arremates. Foram observadas **5.024** mudanças de clube entre divisões na regra nova, ante **5.565** na anterior. Entre essas mudanças, **103** ocorreram com força 40 ou mais, ante **9**. A medição compara o clube nos fechamentos consecutivos e exclui promoções e rebaixamentos. O limite restringe novos ganhos e conserva a força ao transferir.
- No teste isolado, as mesmas 1.000 seeds produziram **491 perdas e 509 manutenções** tanto na Liga como na Taça. Os totais de perdas da simulação longa incluem a exposição a piso e lesões. A regra continua usando 50% para cada decisão elegível.
