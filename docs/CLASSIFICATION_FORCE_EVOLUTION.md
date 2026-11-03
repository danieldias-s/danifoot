# Evolução de força por classificação

A rodada da Liga produz uma decisão por atleta depois de aplicar todos os resultados. A posição é a classificação esportiva do clube dentro da divisão. Na Taça, titulares, reservas e atletas de clubes eliminados conservam a força em relação à participação. O relógio e as perdas por lesão seguem o calendário. Cartões, expulsões e cumprimento de suspensões seguem o processamento disciplinar oficial.

O mundo de 128 clubes tem 21 rodadas. Cada bloco contém duas rodadas da Liga e uma eliminatória da Taça. Todos os 128 clubes entram na primeira eliminatória. Cada vencedor participa da seguinte, até a final que encerra a temporada.

| Bloco | Rodadas da Liga | Rodada da Taça | Clubes na Taça |
| ---: | --- | ---: | ---: |
| 1 | 1 e 2 | 3 | 128 |
| 2 | 4 e 5 | 6 | 64 |
| 3 | 7 e 8 | 9 | 32 |
| 4 | 10 e 11 | 12 | 16 |
| 5 | 13 e 14 | 15 | 8 |
| 6 | 16 e 17 | 18 | 4 |
| 7 | 19 e 20 | 21, final | 2 |

O formato é registrado no snapshot por `LeagueFirstSchedule`. Saves com a temporada já iniciada conservam seu calendário e usam o novo formato na próxima temporada. A nova regra de força da Taça passa a valer nos fechamentos futuros. Os tamanhos 64, 256, 512 e 1024 também usam chaveamento com todos os clubes, com a final após a Liga. A alternância exata de dois para um corresponde ao mundo de 128. Montagens antigas com outras quantidades conservam a geração anterior.

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

- Na Liga, quem ficou fora ganha exatamente um ponto se estiver abaixo do limite do clube atual. Isso inclui banco não utilizado, suspensão e lesão que impediu a entrada. Força no limite ou acima dele permanece igual. Distritais e agentes livres têm referência 1, sem possibilidade de ganho pela regra.
- Na Liga, quem começou em campo ou entrou por substituição oficial tem uma decisão determinística de 50% para perder um ponto. A chance usa seed, temporada, rodada e identidade do atleta. A força 1 é o piso. Uma entrada registrada no último minuto conta mesmo sem minuto inteiro decorrente. Participações repetidas na rodada dão uma decisão só. Em transferências vale o clube atual no fechamento para o contexto do histórico e o limite de crescimento.
- No modo clássico, as perdas diárias de uma lesão são aplicadas pelo calendário antes do fechamento esportivo e continuam até a data de recuperação. Se o atleta se lesionou em campo na Liga, o episódio de lesão tem precedência sobre a perda de participação. Quem já estava lesionado e ficou fora da Liga segue a regra de ausência sobre a força após perdas diárias. Na Taça aplica-se somente o efeito da lesão sobre a força. Recuperar a disponibilidade não restaura pontos. A suspensão tem seu próprio processamento e contador.
- Saves legados conservam seu relógio e regras de lesão. A nova decisão passa a valer no próximo fechamento, incluindo o legado que usava evolução sazonal. O calendário oficial deixa de aplicar o ajuste antigo no fim da temporada. A reabertura de uma rodada já contabilizada preserva seus resultados e forças. A marca de temporada e rodada no snapshot impede repetir as decisões.

O fechamento preserva os valores de salário e de contrato e usa o cálculo de valor de mercado existente para refletir a nova força. Nenhum arquivo do MatchEngine, da disciplina, da economia ou dos layouts foi alterado.

## Medição com a evolução exclusiva da Liga

O cenário de 100 temporadas foi reexecutado com seed 199801 e a fundação 017B. Foram concluídas 35.100 partidas. As 700 rodadas de Taça tiveram zero ganhos por ausência e zero perdas por participação. O calendário registrou perdas diárias por lesão também nessas datas. A execução registrou zero violações de escalação, tamanho de elenco e liquidação financeira duplicada. [Evidência atual](evidence-classification-force/league-only-cup-100-seasons.json)

| Divisão | Média inicial | Média ao fim da temporada 100 | Atletas força 1 | Atletas força 50 |
| --- | ---: | ---: | ---: | ---: |
| 1ª | 44,65 | 18,01 | 23 | 1 |
| 2ª | 32,66 | 12,27 | 24 | 0 |
| 3ª | 19,85 | 9,54 | 33 | 0 |
| 4ª | 6,75 | 4,42 | 59 | 0 |
| Distrital | 15,51 | 1,57 | 1.234 | 0 |

A média global final foi 4,07. Houve 196.181 perdas de titulares, 35.917 de reservas utilizados e 210.135 ganhos por ausência, todos na Liga. A força 1 apareceu em 1.373 dos 2.308 atletas. Entre os distritais, 1.234 dos 1.697 atletas ficaram no piso, 72,7%. Essa execução mede a mesma seed com o novo calendário e as novas regras da Taça.

## Regressão do calendário e da Taça

- Os 466 testes de domínio passaram em Debug. A execução Release anterior passou 465 casos, seguida dos dez testes de calendário no build final. Passaram também os 17 testes de aplicação. O build final Release completou com zero avisos e erros.
- A execução ampla de integração passou 260 dos 261 casos. O caso de importação PostgreSQL recebeu um nome de banco sem o marcador `013b` exigido pelo criador. Corrigida a conexão do ambiente de teste, a reexecução final aprovou sete casos, incluindo o de importação, o calendário e a persistência da Taça. A união dos resultados aprovados cobre os 261 casos distintos.
- O teste da Taça pelo fluxo oficial da API fechou 64 partidas, comparou as forças de todo o mundo e verificou refresh, reconexão SignalR e nova instância da API com PostgreSQL. Um teste separado confirmou que o treinador de clube eliminado pode acompanhar a rodada sem escalar o elenco.
- Flutter aprovou 276 testes e compilou para web. Node aprovou dois testes. A sala local `Teste Liga e Taça - 21 rodadas`, código `TEST21`, foi conferida na API e permaneceu disponível após reiniciar o processo com o build final. [Registro dos testes](evidence-classification-force/cup-regression.json)

## Medição anterior ao ajuste da Taça

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

## Evidências anteriores ao ajuste da Taça

- .NET Release registrou 453 testes de domínio e 17 de aplicação aprovados. A seleção de seis regressões de simulação incluiu o cenário clássico de 100 temporadas. Os dois cenários comparativos de 100 temporadas foram executados separadamente, com 35.100 partidas cada e zero violações de escalação, tamanho de elenco ou liquidação financeira duplicada.
- A suíte de integração Release registrou 260 aprovações entre 261 casos na execução ampla. O único caso divergente ainda esperava a concessão de força na virada da temporada. Ele foi atualizado para verificar que oito avanços concorrentes seguidos de oito leituras após recarregar o save preservam exatamente a força e o evento legado. A reexecução em Release aprovou esse caso e os outros dois testes de integração de força, completando a cobertura dos 261 casos.
- A regressão PostgreSQL fechou uma rodada de Taça pelo fluxo oficial da API e conferiu o ganho de um atleta de clube de folga. Refresh, reconexão SignalR e uma nova instância da API preservaram as forças de todo o mundo, as chaves do histórico e o salário do atleta verificado. Uma segunda checagem comparou as forças do elenco e o SHA-256 do snapshot persistido entre dois processos Kestrel. [Registro do reinício](evidence-classification-force/postgres-api-restart.json)
- Flutter aprovou 276 testes e compilou para web. O analisador registrou o aviso informativo preexistente `unnecessary_const` em `lib/live_match_view.dart:241`. Node aprovou dois testes.
