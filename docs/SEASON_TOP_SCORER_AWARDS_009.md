# Artilheiro da temporada e prêmio 009

30/10/2026. Cálculo, pagamento e persistência: PASS. Conferência visual real: BLOCKED (Work desabilitou navegador local).

## Regra

Somar GoalsScored de todas as ligas e fases oficiais da Taça na temporada, inclusive preliminares e prorrogação. Transferência não reinicia o total. Shootout não conta. Encerramento só avalia a temporada atual na rodada final, com fixtures existentes todos concluídos, consolidação feita e sem sinal de estatísticas incompletas. Totais zero não geram conquista nem prêmio.

Todos empatados no máximo de gols ganham. Jogos, média, força e PlayerId não retiram título de um empatado; ID serve apenas à ordem estável da apresentação. O histórico clássico de temporada, que antes tinha um nome, agora apresenta todos os vencedores e o total oficial sem dupla contagem da Taça.

Cada vencedor com clube existente no momento do fechamento gera **3.000.000**, categoria PrizeMoney. Clube de fechamento, não o clube onde marcou, recebe o valor. Dois do mesmo clube geram6milhões; três9milhões. Não se divide o prêmio. Sem clube, o jogador ganha título e registro individual com ClubId nulo/AwardAmount0 e motivo, sem crédito nem destinatário fictício.

## Idempotência

Chave: season_top_scorer:{WorldId}:{Season}:{PlayerId}. AwardId determinístico derivado da chave. Antes do pagamento, verifica registro individual; o ledger usa a chave pela rotina idempotente de economia existente. ClosedPlayerAwardSeasons impede reabrir a temporada, inclusive zero gols. Registro, contador, caixa e ledger são persistidos no checkpoint de encerramento.

Registros imutáveis: PlayerId,DisplayName,Season,TotalGoals,TotalMatches,ClubId,ClubNameAtClose,AwardAmount,AwardGrantedAt,AwardId,IdempotencyKey. Renomear, vender, expirar contrato ou avançar temporada não reescreve clube/nome/pagamento históricos. SeasonTopScorerTitles soma conquistas individuais; testes de cinco temporadas verificam exatamente cinco, sem duplicação.

## Temporada real executada

| Jogador | Gols | Jogos | Clube no fechamento | Prêmio |
| --- | --- | --- | --- | --- |
| Thiago Machado* | 10 | 20 | Real Pampulha | 3.000.000 |
| Mateus Alves* | 10 | 17 | Guarani da Serra | 3.000.000 |

725 gols individuais,351 partidas,32.520 minutos,22 rodadas. Os dois empataram em10 gols com20/17 jogos; ambos receberam título. Ledger de duas entradas3milhões;6milhões total. Os deltas de caixa de todos os clubes foram reconciliados com todas as transações novas do encerramento, além de verificar isoladamente as entradas de prêmio.

[Temporada real](audit-009/REAL_SEASON_009.json) e [validação HTTP versus SQLite](audit-009/api-persisted-validation.json). Testes determinísticos cobrem campeão único, dois/três/cinco empatados, mesmo clube, clubes diferentes, livre, clube alterado antes de fechar, zero gols, repetição/reload e guarda do ledger. Fixtures artificiais são identificadas nos testes e não apresentadas como resultados da temporada real.

Reinício real da API: PID 19248→2696; IDs, valores e total6milhões iguais após reinício. [Prova](audit-009/process-restart-validation.json), [antes](audit-009/awards-before-process-restart.json), [depois](audit-009/awards-after-process-restart.json).

## Interface

Perfil: ARTILHEIRO: X VEZES, cada temporada/gols/jogos/clube/prêmio. Encerramento: todos os vencedores, premiação individual e total por clube, em tabela verde clássica. O diálogo automático aparece uma vez ao detectar avanço de temporada na sessão; o painel permite consultar encerramentos após reconexão, sem repetir pagamento.

Testes Flutter verificam ARTILHEIRO: 5 VEZES, as cinco conquistas e dois campeões do mesmo clube com total6milhões. A inspeção no navegador ficou pendente porque a ferramenta continua negando Browser Use. Há uma alternativa de renderização Flutter local documentada ao final, sem captura de navegador.

Limitação: temporada com autor/eventos ausentes pode não ter máximo individual comprovável; não se premia automaticamente esse conjunto. Saves antigos não recebem títulos/prêmios retroativos inventados. [Migração](PLAYER_RANKINGS_MIGRATION_009.md).

## Validação complementar desta retomada

O navegador oficial continuou retornando o bloqueio, apesar da ativação informada pelo usuário; o inventário expôs zero navegadores. Como alternativa permitida, dois testes renderizaram os widgets de produção pelo Flutter local com 59 respostas HTTP reais capturadas da API/SQLite, reproduzindo somente o transporte em memória. Não se inventaram estatísticas. Foram geradas e inspecionadas imagens de rankings, perfil, premiação e largura 390px. Isso não é uma sessão de navegador: **WEB_BROWSER continua BLOCKED e STATUS geral PARTIAL**.

[Relatório e imagens da alternativa local](PLAYER_RANKINGS_LOCAL_VALIDATION_009.md) · [2 testes de renderização](audit-009/live-flutter-render-tests.log) · [28 regressões Flutter reexecutadas](audit-009/resumed-flutter-tests.log). As 237 regressões .NET anteriores foram mantidas, pois nenhum arquivo de produção foi alterado nesta retomada.

