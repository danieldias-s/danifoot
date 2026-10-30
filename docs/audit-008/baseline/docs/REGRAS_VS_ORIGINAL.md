# Regras vs. Original (Elifoot 98): Comprovado, Aproximação e Inovação

Este documento separa rigorosamente o que vem de observação/pesquisa histórica independente sobre a experiência do **Elifoot 98**, o que foi modelado por aproximação matemática limpa e determinística (sem uso de binários ou código proprietário), e as inovações deliberadas exigidas para esta edição **Elifoot Classic Multiplayer**.

## 1. Referências Históricas Preservadas em Espírito

Fontes públicas de referência:
- `https://www.geocities.ws/ri_keller/ellifoot.html`
- `https://marmota.org/elifoot98/jogar.php`
- `https://github.com/trsthales/desvendando-elifoot-98`
- `https://www.theifab.com/laws/latest/the-players/`

### Elementos Clássicos Mantidos
1. **Quatro setores simples (`G`, `D`, `M`, `A`) e Força Inteira (`1..50`)**:
   - Sem dezenas de sub-atributos ocultos, sem sistema de idade/aposentadoria.
2. **Caractere literal `*` para craques (`João Silva*`)**:
   - Exibido diretamente no nome nas tabelas densas, sem substituir por ícone gráfico.
   - Concede bônus real de desempenho calibrado (`+18%` no ataque `A`, `+15%` no meio-campo `M`, `+10%` em `D`/`G`, além de maior peso na seleção de finalizadores).
3. **Seis comportamentos disciplinares clássicos**:
   - Ordenados estritamente da menor para a maior probabilidade de faltas/cartões:
     `Fair Play` < `Cordeirinho` < `Cavalheiro` < `Caneleiro` < `Caceteiro` < `Sarrafeiro`.
   - `Fair Play` possui peso `0.0` para faltas intencionais e `0.0` para expulsão direta por agressividade.
   - Não há bônus oculto de desarme para `Sarrafeiro` ou `Caceteiro`.
4. **Colaboração do Meio-Campo (`M`) em Ataque e Defesa**:
   - Os médios contribuem simultaneamente para a força defensiva (`+45%` de `MidfieldSectorPower`) e para a força ofensiva (`+50%` de `MidfieldSectorPower`).
5. **Ordenação Clássica do Ranking de Treinadores**:
   - 1º critério: **Nº de Campeonatos (Ligas)**
   - 2º critério: **Nº de Taças**
   - 3º critério: **Pontos de Carreira do Treinador** (Vitória fora = `3`, Vitória em casa = `2`, Empate fora = `2`, Empate em casa = `1`, Derrota = `0`).

## 2. Alterações Deliberadas e Regras Modernizadas Exigidas

1. **Ausência Total do Sistema de Estrangeiros**:
   - A pesquisa histórica aponta mecânicas de nacionalidade/estrangeiros no jogo clássico. Nesta edição, **não existe sistema de estrangeiros, cotas ou bônus oculto por nacionalidade**. O campo `Country` nos clubes é puramente organizacional/informativo.
2. **Substituições e Oportunidades (Regra IFAB Moderna Adaptada ao Multiplayer)**:
   - **5 substituições** no tempo normal (`90'`), realizadas em até **3 oportunidades (janelas de parada)** com bola rolando, além do intervalo gratuito (`45'`).
   - **Taça com prorrogação (`90' -> 120'`)**: concede **+1 substituição (total 6)** e **+1 oportunidade de parada (total 4)**, além de intervalos gratuitos antes da prorrogação (`90'`) e no intervalo da prorrogação (`105'`).
3. **Formação `5-0-5` no Modelo de Compatibilidade**:
   - Quando `AllowClassic505 = true`, a formação `5-0-5` é permitida por nostalgia histórica, mas recebe um fator de transição explícito (`0.82x` na criação ofensiva por ausência de ligação no meio-campo) para evitar exploits irreais.
4. **Multiplayer Sincronizado (`0/N`) e Parada Global (`GLOBAL_PAUSED`)**:
   - Até 8 treinadores humanos simultâneos via Web/Android com relógio autoritativo no servidor. Quando um humano solicita parada, **todas as partidas da sala congelam no mesmo minuto**.
5. **IA de Treinadores Independente do Clube (`AiManager`)**:
   - Técnicos de IA são entidades persistentes com 1 a 3 táticas favoritas (pesos somando 100%) e personalidade própria, dirigindo o planejamento de elenco (`ManagerSquadPlan`) e o mercado de transferências.
