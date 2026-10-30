# Amarelos globais — 012C

STATUS: PASS. Contador por jogador/mundo, compartilhado entre Campeonato e Taça.

| Ciclo antes | Primeiro amarelo no jogo | Se houver segundo amarelo no mesmo jogo | Punição final pela dupla |
| --- | --- | --- | --- |
| 0 | 1 | Restaurar 0 | 1 |
| 1 | 2 | Restaurar 1 | 1 |
| 2 | 0 e suspensão provisória por terceiro | Restaurar 2; substituir motivo por SECOND_YELLOW | 1 |

Três amarelos isolados em três jogos diferentes completam o ciclo, concedem um jogo e reiniciam o ciclo em zero. O total vitalício não zera. Transferir com dois, receber um na estreia e cumprir no próximo jogo do novo clube é coberto em teste com transferência real de mercado.

BUG-006-01 já evitava contar duas vezes o segundo amarelo quando o ciclo anterior era 0 ou 1. Permanecia uma falha quando o anterior era 2: o primeiro amarelo acionava o terceiro e apagava o ciclo; o ramo do segundo não o recuperava. MatchPlayerDiscipline agora guarda CycleBefore e a decisão é reconstruída a partir desse valor. O segundo amarelo restaura exatamente o ciclo anterior e gera um jogo. CardHistory conserva ambos os cartões; SeasonYellows e rankings contam dois amarelos e um vermelho. Não há dois registros AWARD independentes para a mesma partida/jogador.

Um terceiro amarelo durante o jogo não retira o atleta; ele continua elegível até eventual expulsão/lesão. O saldo é para a partida seguinte. Primeiro amarelo pode ser contabilizado provisoriamente durante o jogo e revertido pelo segundo, sem apagar o evento histórico do cartão. Se houver vermelho direto depois de um amarelo que completou três, usa a maior punição.

TotalYellowCards, YellowCardsInCurrentCycle, TotalDirectRedCards, TotalSecondYellowDismissals, saldo, motivo e históricos seguem no jogador. SeasonYellows/SeasonReds seguem sendo estatísticas sazonais. Nenhuma punição disciplinar retira força nominal; quando não participa por suspensão no Campeonato, o atleta segue elegível ao ganho de reserva da 012C dentro do teto. Na Taça não cresce.

Na migração, só totais mínimos verificáveis e eventos retidos são recuperados. O ciclo antigo é mantido; partidas antigas em andamento com marcador explícito de terceiro permitem reconstruir o valor anterior 2. Sem prova, não se inventam datas, cartões nem tipos de vermelho. As lacunas são expostas por avisos de cobertura, e punições antigas não são sorteadas novamente.
