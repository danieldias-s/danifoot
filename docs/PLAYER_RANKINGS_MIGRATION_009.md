# Migração de saves 009

30/10/2026. STATUS de migração: PASS nos testes executados. Sem DDL, sem reinstalação de SDK, sem reset de carreira008.

## Abertura

Os três caminhos EF (GetWorldAsync, GetWorldByInviteCodeAsync e ListWorldsAsync) mantêm CoachCareerEngine.EnsureMigrated e depois chamam PlayerStatisticsEngine.EnsureMigrated. Campo ausente PlayerStatisticsVersion inicia0; mundo novo nasce9. Migração é versionada e repetível sem novos créditos. Snapshot migrado é gravado no próximo checkpoint normal, sem escrita surpresa em GET.

Preservam-se PlayerId, ClubId, moral, empregos, convites, ranking008, pontos decimal, títulos de treinadores, saldos e ledger. O teste usa backup SQLite consistente pela API BackupDatabase com o save008 aberto somente leitura, testa os três caminhos de carga e compara carreira, dinheiro e snapshotSQL original. O banco original008 não foi atualizado pelo teste. [Auditoria somente leitura](audit-009/sqlite008-readonly-audit.json).

## O que é recuperável

Eventos Goal/cartões/substituições de partidas concluídas conservadas. Titulares iniciais podem ser reconstruídos de ativos finais + substituídos para fora + expulsos − substituídos para dentro. Só se ambas as listas tiverem11 IDs conhecidos e os eventos necessários de entrada/expulsão existirem. Banco/entradas vêm das listas e eventos. Minuto/ordem original permitem atribuição de sofridos ao goleiro daquele momento.

Snapshots antigos não guardavam posição/força no início; nesses casos usam-se os dados disponíveis na migração, com aviso explícito. Esses campos não podem ser reconstruídos exatamente após uma edição de posição ou força. O cálculo estatístico não inventa força passada nem afeta o jogo.

Se não há escalação suficiente, recupera apenas fatos individuais presentes nos eventos. Jogos correspondem a participações comprovadas, mas podem ser parciais; minutos desconhecidos não são inventados. Médias nulas e G/D inelegíveis sinalizam a lacuna. Sem eventos de gol compatíveis com placar, não se distribui o placar a atletas/goleiro. Gol com evento e minuto válidos, mas sem autor, conserva o placar e sofrido verificável; não cria artilheiro.

## O que não é reconstruído

- Fixtures/eventos podados antes da009: carreira anterior fica parcial.
- Contadores CareerGoals/CareerMatches sem origem por jogo: não são repartidos artificialmente por temporada/divisão.
- Premiações passadas: fechamentos antigos são marcados encerrados e não recebem títulos/pagamentos retroativos sem prova. Históricos antigos de um único nome de artilheiro não estabelecem todos os empatados nem clube correto no fechamento.
- Temporada atual incompleta por eventos ausentes: prêmio automático suspenso e aviso, sem recebedor inventado.

O ranking mostra fatos recuperados; não promete reproduzir integralmente toda carreira anterior. Os contadores legados são mantidos para compatibilidade esportiva. A rota antiga top-scorers agora evita somar a Taça duas vezes, derivando de registros oficiais disponíveis.

## Proteção de dados e procedimento

Faça backup consistente do SQLite (incluindo transações WAL pelo mecanismo SQLite) ou dump PostgreSQL antes de abrir um save importante. Não copie isoladamente arquivo .db em uso. Abra com a API009, consulte coverageWarnings e confirme as limitações de cobertura. Não use exportação do editor como backup de carreira.

Guardas e estatísticas persistidas sobrevivem ao reset sazonal e poda antiga. Atualizações seguintes não voltam a executar a migração9. Recarregar não duplica a partida e chamar AwardSeason de temporada já fechada não altera dinheiro/título. Testes com clube atual diferente do clube histórico e ID estável conferem isso.

Na recuperação,129 testes de jogador,29 de integração no total e reinício real da API comprovaram as rotas relevantes. A verificação visual permanece bloqueada nas configurações do Work; isso não foi substituído por uma captura artificial.

## Validação complementar desta retomada

O navegador oficial continuou retornando o bloqueio, apesar da ativação informada pelo usuário; o inventário expôs zero navegadores. Como alternativa permitida, dois testes renderizaram os widgets de produção pelo Flutter local com 59 respostas HTTP reais capturadas da API/SQLite, reproduzindo somente o transporte em memória. Não se inventaram estatísticas. Foram geradas e inspecionadas imagens de rankings, perfil, premiação e largura 390px. Isso não é uma sessão de navegador: **WEB_BROWSER continua BLOCKED e STATUS geral PARTIAL**.

[Relatório e imagens da alternativa local](PLAYER_RANKINGS_LOCAL_VALIDATION_009.md) · [2 testes de renderização](audit-009/live-flutter-render-tests.log) · [28 regressões Flutter reexecutadas](audit-009/resumed-flutter-tests.log). As 237 regressões .NET anteriores foram mantidas, pois nenhum arquivo de produção foi alterado nesta retomada.

