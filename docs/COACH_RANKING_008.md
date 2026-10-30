# Ranking e pontuação de treinadores 008

30/10/2026. STATUS: PASS. O ranking de jogadores/artilheiros não foi modificado.

## Ordenação

Comparar a tupla (campeonatos DESC, Taças DESC, pontosCarreira DESC, CoachId ASC), lexicograficamente. Não existe soma ponderada entre título e ponto. Vitórias e ser humano não são mais desempates. ID persistente dá ordem estável mesmo sem clube. Os pontos são decimal no domínio/DTO/JSON e aceitam 0,5/1,5 exatamente no servidor; a UI apresenta o valor recebido sem conversão para inteiro.

Exemplos verificados: (1 campeonato,0 Taças,10 pontos) fica acima de (0,10,5000); com campeonatos iguais, mais Taças vence quaisquer pontos; (2,1,60) fica acima de (2,1,50). Demissão/reload/troca preservam a tupla. IA distrital que já tem campeonato não perde lugar no ranking por estar na Distrital. Treinadores sem atividade/título podem seguir os filtros históricos de população do ranking; não confundir entradas com total de técnicos existentes.

## Pontos de liga

| Divisão | Vitória | Empate | Derrota |
| --- | --- | --- | --- |
| Primeira | 4 | 2 | 0 |
| Segunda | 3 | 1,5 | 0 |
| Terceira | 2 | 1 | 0 |
| Quarta | 1 | 0,5 | 0 |
| Distrital | 0 | 0 | 0 |

Para divisões 1–4: vitória = 5−divisão; empate = (5−divisão)/2m. A classificação da liga continua 3 pontos por vitória e 1 por empate, como antes; não usa esses pontos de carreira. HomeCoachAtKickoffId/AwayCoachAtKickoffId recebem o crédito mesmo que o vínculo tenha mudado antes da liquidação. CareerResultsApplied persistido impede duplicação de estatísticas, moral e pontos.

## Taça

| Fase vencida | Pontos |
| --- | --- |
| 64 | 3 |
| 32 | 4 |
| Oitavas | 5 |
| Quartas | 6 |
| Semifinal | 7 |
| Final | 8 |
| Preliminares distritais | 0 |

Seis vitórias = 33 pontos e +1 Taça em campo separado. WinnerClubId resolve vitória por prorrogação/pênaltis, inclusive empate no placar de gols. Derrotado recebe zero. Bye sem minuto jogado não dá prêmio; final sem vencedor válido não inventa título. Não há fallback do campeão da liga para a Taça.

## Campeonatos e dono do título

Cada uma das quatro divisões vale um campeonato. Conclusão exige todos os fixtures de liga da divisão encerrados e aplicados. O campeão vem da classificação existente. A regra explícita de responsabilidade é: **treinador capturado no início da última partida de liga do campeão**, ordenada por rodada e DeterministicMatchIndex. O nome atual do técnico não substitui o dono registrado. Se sair após esse último jogo antes da liquidação, recebe a conquista por essa regra; quem treinou apenas partidas anteriores não recebe o título retroativamente.

Registro: temporada, divisão, clube, treinador e tipo LEAGUE/CUP. Chave de idempotência: temporada+divisão+tipo (Taça tem divisão nula). A final credita ao treinador vencedor capturado no início. Testes encerram as quatro divisões, repetem a liquidação e verificam quatro títulos, sem duplicar. Nas 50 temporadas houve exatamente 200 campeonatos e 50 Taças.

## Comparação com as regras antigas sobre os mesmos resultados

Antes: pontos por mando (casa vitória2/empate1; fora vitória3/empate2), inclusive Taça, sem prêmio de fase; apenas Primeira gerava campeonato. O teste calculou a seguinte comparação a partir das mesmas partidas realmente simuladas e dos mesmos treinadores ao início:

| Humano | Pontos novos | Pontos por mando antigos | Campeonatos novos (4 divisões) | Só títulos da Primeira | Taças |
| --- | --- | --- | --- | --- | --- |
| Carreira 1 | 987.5 | 908 | 3 | 1 | 1 |
| Carreira 2 | 717 | 755 | 2 | 0 | 1 |
| Carreira 3 | 909.5 | 793 | 3 | 1 | 1 |
| Carreira 4 | 1408 | 1055 | 7 | 2 | 3 |
| Carreira 5 | 1142.5 | 932 | 8 | 2 | 2 |
| Carreira 6 | 1106.5 | 918 | 4 | 2 | 2 |
| Carreira 7 | 809.5 | 891 | 3 | 0 | 0 |
| Carreira 8 | 478 | 627 | 2 | 0 | 0 |

Essa é uma comparação de fórmula sobre resultados iguais. Não é uma segunda trajetória de contratações com o código antigo nem permite atribuir diferenças de carreira exclusivamente a uma regra. O JSON integral também inclui todos os técnicos IA para comparação. A prioridade campeonatos/Taças/pontos já existia e foi preservada; completaram-se a contabilização, atribuição e estabilidade.

Top 5 final:

| # | Treinador | Campeonatos | Taças | Pontos |
| --- | --- | --- | --- | --- |
| 1 | Henrique Andrade | 8 | 3 | 1475.5 |
| 2 | Carreira 5 | 8 | 2 | 1142.5 |
| 3 | Caio Mendes | 7 | 5 | 1175 |
| 4 | Eduardo Alves | 7 | 4 | 1386.5 |
| 5 | Leandro Mendes | 7 | 3 | 1457.5 |

## Saves antigos

Agregados inteiros anteriores passam a decimal sem perda; não são recalculados com regra nova. Jogos terminados na migração ficam aplicados. Títulos legados mantêm seus contadores, sem inventar registros com divisão/dono desconhecidos. Dados detalhados novos começam na versão 8. [Migração e evidências](COACH_CAREER_AUDIT_008.md), [teste JSON legado](audit-008/closure-domain-tests/career32-final.trx).
