# Auditoria da força inicial dos jogadores

A geração fictícia do catálogo usava o mesmo nível de referência para as quatro divisões. Com o seed `199801` e os oito países na ordem da tabela do teste manual (AR, BR, DE, ES, FR, GB, IT, PT), o servidor reproduziu os 22 atletas do PT-032 e os dez valores visíveis na escalação 4-4-2. A reprodução é uma nova geração: nenhum save do teste manual foi aberto ou alterado.

| Divisão | Clubes / atletas | Mín. / média / máx. antes | Mín. / média / máx. depois | Média dos 11 titulares | Média de todos os reservas |
| --- | --- | --- | --- | --- | --- |
| 1ª | 8 / 172 | 19 / 25,88 / 34 | 34 / 40,51 / 48 | 28,53 → 43,36 | 23,11 → 37,52 |
| 2ª | 8 / 154 | 19 / 26,70 / 34 | 27 / 33,34 / 39 | 28,63 → 35,14 | 24,14 → 30,94 |
| 3ª | 8 / 164 | 19 / 26,32 / 34 | 21 / 26,50 / 32 | 28,66 → 28,50 | 23,61 → 24,18 |
| 4ª | 8 / 160 | 19 / 26,11 / 34 | 16 / 20,87 / 26 | 28,67 → 22,78 | 22,97 → 18,53 |

Os números vêm de 650 jogadores em 32 elencos materializados por `WorldCreatorEngine.Create` antes da primeira partida. O XI é a escalação inicial do servidor (88 titulares por divisão); reservas são todos os não titulares, incluindo quem ficou fora do banco. A média da coluna mín./média/máx. pondera por jogador. Dando o mesmo peso a cada clube, as médias da 1ª à 4ª foram **25,91 / 26,72 / 26,28 / 26,13** antes e **40,54 / 33,36 / 26,47 / 20,89** depois.

Na 4ª, havia 33 atletas de força 31–40, dos quais dois eram reservas. Depois não houve nenhum atleta acima de 26 nessa geração. No PT-032, a média do elenco passou de **26,59 para 21,23**, a do XI de **29,64 para 23,45**, e o maior valor de **34 para 26**. Em quatro mundos inteiramente fictícios, 30 dos 32 clubes da 4ª tinham média pelo menos igual à do clube mais fraco da 1ª em seu mundo. Depois da correção foram 0 de 32.

## Causa e regras aplicadas

- **Criação do mundo:** `WorldCreatorEngine.Preview` preenche vagas com clubes inventados e usa `PlayableWorldPolicy.Prepare` para gerar os atletas. `WorldCreatorEngine.Create` materializa as forças da prévia. Ele usa `WorldSeedFactory` para a estrutura do mundo e descarta os elencos dessa fábrica. Antes, a força do elenco substituto vinha de `generated-club-level-v1`: na ausência de ranking esportivo, base 26, variação determinística de até 7 pontos por seed/identidade/posição e +1 para meias. O sorteio explica quais atletas chegaram a 34; o erro de integração foi usar essa referência neutra para todas as vagas inteiramente fictícias. O caminho 013A sem modo de dados tinha problema semelhante: a sequência 15–30 também era independente da divisão.
- **Ranking e escalas:** [013A](CLUB_ASSIGNMENT_013A.md) e [016B](WORLD_CLUB_RANKING_016B.md) definem a alocação inicial. [ClubInitialPower](CLUB_INITIAL_POWER_016B.md) vai de 1 a 20 e ordena o catálogo com os desempates de ranking. O PT-032 tinha potência 1 e ordinal técnico 32 sem confiança esportiva. `Player.Strength` vai de 1 a 50. A potência do clube e o ordinal técnico não são notas dos atletas. A força individual dos importados continua na [calibração relativa 013B](PLAYER_STRENGTH_CALIBRATION_013B.md); a seleção por indicadores da [016B](PLAYER_SQUAD_SELECTION_016B.md) continua a decidir quem entra no elenco. Para complementos de clubes do catálogo, o [modelo 013C](REAL_AND_SIMULATED_ROSTERS_013C.md) conserva Elo, ranking ordinal confiável ou base neutra 26 conforme a evidência de origem. Ranking técnico e ausência de dados não viram força esportiva.
- **Fundação dos fictícios:** para preencher uma vaga inventando o clube inteiro, a prévia agora usa a distribuição inicial já existente na fábrica clássica: 34–48 / 27–39 / 21–32 / 16–26; distrital 10–21. A regra vale para o sorteio de fundação desses atletas. Não há validação que barre atletas acima dessas faixas em qualquer divisão. Clubes de catálogo mantêm sua calibração se o usuário os mover, e montagens confirmam o elenco definido na prévia ou no pacote. O controle Elo trocou o líder com um clube da 4ª: o líder preservou os mesmos atletas, média 44,39 e máximo 50 na 4ª.
- **Evolução:** a [012](DIVISION_FORCE_LIMITS_012.md) registra alvos da evolução sazonal antiga; a [012C](CLASSIC_FORCE_EVOLUTION_012C.md) define tetos de ganho pela posição na liga. O teto de evolução inicial 8 na 4ª, visível na imagem, não é uma regra de nascimento nem reduz automaticamente um atleta acima dele. A correção não altera nenhuma dessas rotinas, o MatchEngine, a economia nem as fórmulas de preço, salário e contrato. A inicialização usa a força corrigida como entrada das mesmas fórmulas. A política entra antes de gravar a prévia e não roda ao carregar saves ou ao confirmar prévias já congeladas.

## Evidência e reprodução

O [detalhamento](audit-initial-strength/distributions.md) inclui as contagens por faixa para elenco, XI, reservas, banco e fora da relação, os 32 clubes da reprodução e o comparativo de todos os cenários. Os arquivos [antes](audit-initial-strength/before.jsonl) e [depois](audit-initial-strength/after.jsonl) registram os valores individuais.

Foram geradas dez configurações (320 clubes de liga por etapa): a fábrica clássica, quatro variantes fictícias, catálogo sintético com Elo, o mesmo Elo com troca manual, ranking ordinal, catálogo sem ranking e um mundo misto de clubes com Elo e vagas fictícias. Os catálogos de teste não representam rankings reais. As forças e escalações da fábrica clássica, do Elo, do ordinal e do catálogo sem ranking permaneceram idênticas. No misto, apenas as vagas fictícias mudaram. Sem ranking esportivo, o catálogo ainda pode ter divisões com médias semelhantes (1ª 25,74 e 4ª 26,40 no controle), como consequência da regra neutra da 013C.

Para reproduzir, execute a primeira linha no checkpoint `a2fcbd5` para o antes e na revisão corrigida para o depois, trocando o arquivo de saída. Execute a segunda linha com ambos os arquivos:

```powershell
dotnet run --project tools/initial-strength-audit -c Release -- docs/audit-initial-strength/after.jsonl
node tools/initial-strength-audit/report.mjs docs/audit-initial-strength/before.jsonl docs/audit-initial-strength/after.jsonl
```

A repetição da geração corrigida produziu o mesmo SHA-256 do arquivo depois: `49BC788BD471C0681092F29778E38D289F00E0A4974006C5B59B67D93A3311F5`.

## Regressão

- [Execução ampla](audit-initial-strength/regression.log): 448 testes de domínio, 17 de aplicação e 246 de integração aprovados. Nove casos de integração exigiam configuração de PostgreSQL; passaram nas reexecuções com [banco descartável](audit-initial-strength/postgres-regression.log) e [nome de catálogo ajustado para o caso de importação](audit-initial-strength/postgres-import-final.log).
- [Integração focada](audit-initial-strength/focused-regression.log): 29 aprovados, incluindo criação corrigida e recarga de save anterior em SQLite com forças, salários e contratos preservados. [Catálogo congelado e multiplayer](audit-initial-strength/fixture-regression.log): 5 aprovados, incluindo o teste de save. Há sobreposição de casos entre essas execuções.
- [Primeiras temporadas](audit-initial-strength/first-season.log): [masculino PT/BR](audit-initial-strength/initial-strength-season-199801.json) e [feminino DE/JP](audit-initial-strength/initial-strength-season-199802.json), 351 partidas reais cada. Nenhuma escalação inválida, violação de tamanho de elenco ou dupla liquidação financeira. As simulações adicionais de 100 temporadas não foram concluídas nesta auditoria.
- Diff vazio contra `8dda6d1` nos motores de partida, evolução, economia e contratos e no repositório de saves. Branch local `codex/initial-player-strength-audit`; checkpoints `a2fcbd5` (medição anterior) e `18a1f31` (correção). Sem merge ou publicação.
