# Seleção do atleta contundido — 012C

STATUS: PASS.

A ocorrência continua em 0,0018 (0,18%) por equipe/minuto elegível. Se não há ativo, não se sorteia. Depois da ocorrência, PickOne usa NextInt(0,quantidadeDeAtivos) sobre a lista efetivamente em campo. Não existem pesos de posição, força, comportamento, idade, tempo acumulado nem duração da lesão na escolha do atleta. Cada jogador ativo possui exposição de 1/N condicionada à ocorrência daquele lado/minuto. Mais minutos em campo aumentam a exposição por mais oportunidades, sem fator adicional.

Ordem mantida: disciplina/remoção de expulsos → lista atualizada → ocorrência de lesão → escolha uniforme → retirada do lesionado → recálculo de setores → jogadas. Quem já saiu, foi expulso ou ainda não entrou não pode ser escolhido. O goleiro faz parte da mesma população elegível. Os testes selecionam contextos determinísticos para atingir cada posição, sem mudar a probabilidade do motor.

A duração é um sorteio separado após a ocorrência. No modo ClassicAdapted continua exatamente a distribuição 012C, com 2..15 dias, 80% em 2..8 e 20% em 9..15. O sorteio legado de duração ainda é consumido no fluxo principal para preservar seu encadeamento; duração/perda clássica usam fluxo separado. O adendo não alterou ClassicForceEvolutionEngine.cs, ClassicForceState.cs, FORCE_CEILINGS_012C.json ou WEIGHTED_INJURIES_012C.json. Saves de modo LegacySeasonal mantêm seu calendário de lesão antigo até migração explícita de força, conforme compatibilidade anterior.

Retirada não é perda nominal imediata. No clássico, a força segue caindo por dias virtuais, com orçamento máximo floor(forçaInicial×duração/30), inclusive máximo 25 para força 50/duração 15; crescimento de não participação permanece possível no Campeonato após a parcela diária, dentro do teto. Suspensão não é lesão e não cria parcelas diárias.

Na calibração final: 11.847 lesões e 3.255.480 minutos. Os 11.847 episódios mantiveram duração 2..15, perda sorteada dentro do máximo e parcelas acumuladas sem exceder o orçamento; zero chaves de parcela diária duplicadas. Os 100.000 sorteios de duração já exigidos pela 012C foram repetidos pela suíte, com seed 120031, e continuam dentro da tolerância definida.

## Comparação com 006

| Indicador | Referência | Atual | Por jogo antes | Por jogo atual | Variação % |
| --- | --- | --- | --- | --- | --- |
| Faltas | 621767 | 640277 | 17,714 | 18,242 | 2,977 |
| Amarelos isolados (eventos YellowCard) | 77493 | 79775 | 2,208 | 2,273 | 2,945 |
| Vermelhos diretos | 3099 | 3125 | 0,088 | 0,089 | 0,839 |
| Expulsões por segundo amarelo | 2416 | 2251 | 0,069 | 0,064 | -6,829 |
| Expulsões totais | 5515 | 5376 | 0,157 | 0,153 | -2,52 |
| Lesões (referência legada 012) | 11869 | 11847 | 0,338 | 0,338 | -0,185 |
| Gols (referência legada 012) | 86071 | 107573 | 2,452 | 3,065 | 24,982 |

Cartões usam o JSON preservado DISCIPLINE_STATISTICS_006.json, bloco candidate006. A prosa antiga da 006 contém números diferentes de seu JSON; o quadro usa o JSON e explicita essa escolha. Gols/lesões vêm da reprodução legada preservada da 012 (seed 199801, 100 temporadas), porque o JSON disciplinar 006 não contém esses totais. A execução clássica final usa oito atletas existentes ajustados para força 1, como na auditoria 012C; demais condições da calibração mantidas. Logo, a comparação com 006 é descritiva, não um teste isolado de causalidade das novas durações de suspensão.

Comparação adicional com a 012C imediatamente anterior: gols 109687→107573, lesões 11866→11847. As constantes de chance e pesos de falta foram comparadas no fonte e permaneceram iguais. Mudanças agregadas são esperadas pela disponibilidade real, elenco ativo e força evoluindo por regras clássicas, sem retunar chances para obter contagens iguais.
