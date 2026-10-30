# Vermelho direto e suspensão — 012C

STATUS: PASS.

A chance de expulsão não foi alterada. Após um gatilho de cartão, o atleta é escolhido pelo peso efetivo de falta. Só então a chance de vermelho direto depende do comportamento: Cordeirinho 0,5%, Cavalheiro 1,2%, Caneleiro 2,5%, Caceteiro 4,8%, Sarrafeiro 8,5%; FairPlay não entra nos candidatos a faltas intencionais. Esses percentuais são condicionais à decisão de cartão e escolha do infrator, não por minuto nem por partida.

Depois de ocorrer o vermelho, o RNG separado usa seed do mundo, temporada, rodada, índice determinístico da partida, minuto do cartão e token estável do jogador (salt 21021). NextInt(0,100), limite superior exclusivo, mapeia 0..49→1; 50..79→2; 80..94→3; 95..99→4 jogos. Não consome sorteio adicional do fluxo principal de faltas, gols e lesões. A duração sorteada fica no evento e no histórico, sem re-sorteio no encerramento ou reload.

| Jogos | Esperado % | Amostras | Observado % |
| --- | --- | --- | --- |
| 1 | 50 | 50104 | 50,104 |
| 2 | 30 | 30119 | 30,119 |
| 3 | 15 | 14802 | 14,802 |
| 4 | 5 | 4975 | 4,975 |

100.000 sorteios, seed 210031; cada frequência dentro de 6 desvios-padrão binomiais +2 contagens. Usando Bernstein e soma dos quatro limites de cauda, a probabilidade familiar de rejeição sob sorteios independentes corretos é limitada por 1.517e-7. Os 100 valores de entrada também são testados exaustivamente, cobrindo todos os limites.

Na simulação real, 3125 vermelhos diretos produziram durações [1,2,3,4] em contagens [1562, 932, 488, 143]. O teste estatístico separado controla os pesos; a quantidade real de cartões depende das escalações/comportamentos e não é forçada à proporção exata de duração.

Expulsão remove o jogador imediatamente. Não participa de jogadas posteriores, não volta ao campo e não pode ser substituído. Um amarelo isolado anterior no mesmo jogo continua histórico; se completou o terceiro acumulado, a punição final é max(1,duraçãoDoVermelho), sem soma. O próprio jogo não serve a punição. Jogos seguintes oficiais do clube atual consomem um por encerramento, compartilhando Liga e Taça, inclusive após transferência/virada de temporada.
