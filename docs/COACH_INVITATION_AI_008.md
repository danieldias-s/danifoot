# Convites e troca de treinador 008

30/10/2026. STATUS: PASS. Humanos decidem por ACEITAR/RECUSAR; IA avalia deterministicamente o projeto. O ranking oficial permanece lexicográfico; a nota de seleção de uma vaga é outra métrica.

## Projeto e compatibilidade

Divisão numerada 1–5, sendo 5 Distrital. Índice arredondado a três casas:

project = 12×(5−divisão) +0,30×reputação +0,60×forçaMédia +3×log10(max(1,caixa)) +2×capacidadeConstruída/10000.

Usa caixa atual, capacidade construída e elenco completo, sem alterar finanças, estádio ou jogador. A posição atual é exibida na proposta e protege o líder de elite; não entra como coeficiente geral da nota de projeto. Expectativa competitiva é refletida na divisão/qualidade/prestígio e no objetivo exibido.

Pré-requisito: moral VERDE e outro clube. Humano nunca Distrital. Desempregado recém-demitido espera duas rodadas; para Primeira/Segunda precisa de ≥1 campeonato OU ≥60 pontos OU ≥15 vitórias. Sem isso, Terceira/Quarta; IA também Distrital. Um desempregado legado pode não ter data de demissão conhecida: não se inventa cooldown histórico.

Empregado espera três rodadas desde a mudança e requer melhoria de projeto ≥ 6 + Pragmatism/25 (humano usa 50, portanto mínimo 8). Líder da Primeira, com ao menos um jogo e reputação ≥75 exige melhoria ≥16. Também precisa de divisão superior OU melhoria estrutural simultânea: reputação +15, estádio ≥1,3× e caixa ≥1,3×. Não basta uma boa colocação passageira. Um líder pequeno pode aceitar clube muito maior da mesma divisão; um líder grande não sai para projeto inferior/equivalente. Pragmatism influencia a cautela da IA; identidade tática e demais traços são preservados na mudança.

## Ordem de candidatos

Para N entradas do ranking, posição rank (1 é melhor):

score = 20×(N−rank)/max(1,N) + moral/10 + (temCampeonato ? 4 : 0) + min(10,pontos/50) − 3×distânciaEntreDivisões.

Desempregado usa distância zero. Maior nota primeiro; CoachId desempata. Uma boa carreira ajuda, mas um candidato no topo pode recusar projeto ruim. Amarelos/vermelhos não recebem oferta. A explicação CANDIDATE_HIGH_RANK registra a posição numérica avaliada, sem significar que todo candidato tenha classificação absoluta de elite.

## Ciclo da vaga

Vaga tem ID determinístico com seed, clube, ordinal e sequência. Convite tem ID derivado da vaga+candidato. São consultados os candidatos já tentados nessa vaga; não há reoferta infinita. IA incompatível registra recusa e segue para o próximo. Primeiro humano elegível recebe oferta e a busca aguarda sua resposta. Máximo três pendentes por humano e uma oferta ativa por vaga. Validade: três rodadas (ordinal atual < vencimento). Mudança de moral/projeto ou expiração invalida a pendência e permite prosseguir.

Humano recusa: registra CANDIDATE_REJECTED, mantém seu clube/carreira e busca o próximo imediatamente. Sem candidatos, interino permanece e a busca pode ser reavaliada na próxima rodada, respeitando a lista já tentada. Chamadas repetidas na mesma rodada não reiniciam a pesquisa; recusa explícita libera a próxima tentativa. Interino pode ser IA verde livre fora do cooldown ou treinador novo determinístico. Ele não interrompe calendário, mercado ou preparação.

Aceite humano/IA: fecha emprego anterior, remove técnico substituído, cria novo emprego, preserva moral, pontos, títulos e personalidade; zera só a sequência e os cinco resultados do trabalho anterior. Expira demais propostas da mesma vaga ou do mesmo treinador. O clube anterior abre vaga com interino. Nunca há dois titulares. A troca só ocorre em PREPARING/RESULTS/FINISHED; nunca durante simulação/pausa/intervalo. O serviço HTTP usa o mesmo SemaphoreSlim por mundo do relógio; revalida após adquirir o lock, salva, incrementa a versão e publica contador/snapshot.

Primeira entrada ainda permite escolher clube não distrital, conforme o fluxo aprovado. Treinador com carreira estabelecida não usa reconexão/join para pular convites. Um interino não precisa ser contratado como titular para o clube continuar jogando.

## API e interface

- GET /api/worlds/{worldId}/invitations/{coachId}: pendências, emprego, moral/histórico, índice atual, títulos e regras.
- POST /api/worlds/{worldId}/invitations/{invitationId}/accept?coachId={id}.
- POST /api/worlds/{worldId}/invitations/{invitationId}/reject?coachId={id}.

A proposta mostra clube, divisão, posição, força média, caixa, estádio, objetivo e comparação dos índices. Botões ficam protegidos contra comando duplicado; após resposta, recarrega sessão, plantel e snapshot e limpa seleção antiga de jogador. As validações do servidor também rejeitam repetição, vaga encerrada e disputa simultânea.

## Exemplos reais da execução final

| Candidato | Nome | Momento | Decisão | Explicação registrada | InvitationId |
| --- | --- | --- | --- | --- | --- |
| IA | Caio Mendes | T1 R9 | ACEITOU | CANDIDATE_HIGH_RANK=16;CLUB_PROJECT_UPGRADE;DIVISION_COMPATIBLE;CANDIDATE_GOOD_MORALE;project=77,762;improvement=22,582 | eca0fc26-ad8f-2983-5dc5-a589f9110d74 |
| IA | Leandro Vieira | T1 R9 | RECUSOU | CANDIDATE_HIGH_RANK=2;CANDIDATE_REJECTED;PROJECT_NOT_UPGRADE;project=77,762;improvement=-23,000 | 32f19844-24eb-627d-bc1a-524c9deb93d0 |
| Humano | Carreira 5 | T1 R18 | ACEITOU | CANDIDATE_HIGH_RANK=9;CLUB_PROJECT_UPGRADE;DIVISION_COMPATIBLE;CANDIDATE_GOOD_MORALE;project=101,260;improvement=19,795 | eccdc201-efb5-dd75-5b83-dfc90e43dc9e |
| Humano | Carreira 8 | T3 R1 | RECUSOU | CANDIDATE_HIGH_RANK=38;DIVISION_COMPATIBLE;CANDIDATE_GOOD_MORALE;project=82,970;improvement=82,970 | 197d12e0-0dfe-d025-df05-141906d93000 |

Para a mesma primeira vaga, candidato de posição 2 rejeitou melhoria −23; depois posição 16 aceitou melhoria +22,582. Isso demonstra por que a seleção não se reduz a contratar o primeiro colocado. Registros originais estão no [JSON integral](audit-008/career-final-evidence/COACH_CAREER_50_SEASONS_008.json). Houve 1402 aceites e 27323 recusas IA; humanos 220/49. Recusa humana registrada em AuditLogs tem motivo Recusa humana; a Explanation do convite mantém o diagnóstico original do projeto.

O teste SearchContinuesAfterAiRefusalAndPreservesPersonalityOnAcceptedMove verifica o clube anterior com substituto, exclusividade e igualdade da identidade, pragmatismo, adaptabilidade, agressividade financeira, paciência com jovens e preferências. O teste HTTP de recusa verifica uma nova oferta para o próximo humano na mesma vaga. Aceitação concorrente aceita exatamente uma solicitação; mudança em RUNNING_FIRST_HALF é recusada explicitamente. [TRX integração](audit-008/closure-integration-tests/career7-final.trx).

No navegador, houve recusa Ferroviária e aceite Joinville, seguido de partida real e reinício. [Capturas e procedimento](COACH_CAREER_TESTS_008.md). Pesos são uma política explicável e testada, sem alegação de ótimo estatístico universal.
