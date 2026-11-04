# Rodada ao vivo e consulta de equipes — versão clássica

Implementação em `codex/classic-live-round-tactics`, no worktree isolado
`D:/elifoot_98/.worktrees/account-login-lobby`. Checkpoint inicial: `d09efd0`.

## Comportamento entregue

- No mobile, a lista usa títulos prateados por divisão, nomes de clubes com suas
  cores, placar preto e branco e o último lance oficial da partida ao lado.
- No web, a lista usa o formato compacto das referências: público, mandante,
  lance do mandante, placar, lance do visitante e visitante. As quatro divisões
  ficam na mesma lista com rolagem. O placar selecionado e o relógio permanecem
  acima; o futuro placar geral independente da seleção continua adiado conforme
  pedido do proprietário.
- Clicar em outro clube abre uma consulta com a formação, o campo, os jogadores
  em campo e os reservas daquela partida. O modal recebe atualizações da rodada
  e não envia comandos de pausa, escalação ou substituição.
- Clicar no próprio clube usa a pausa global e o painel de substituições já
  existentes, agora com a mesma visualização tática no clássico. Apenas a
  alteração confirmada consome as cotas existentes.
- Pausas e decisões oficiais do servidor têm prioridade sobre a consulta.
- O pós-rodada permanece automático: campeonato → classificação por cinco
  segundos → treinador; taça → treinador. A preparação seguinte continua sem
  botão manual de avançar.

Os dados exibidos são os fornecidos pelo servidor. Não foram inventadas
narrativas de ataque ou público. Quando a renda/público oficial ainda não veio,
a coluna indica apuração pendente. Cores conhecidas vêm do cadastro do clube e
passam pelo tratamento de contraste existente; clubes sem cores usam fallback.

## Arquitetura e escopo

`ClassicReferenceMatchList` apresenta os mesmos confrontos da rodada.
`LiveTeamInspectionDialog` e o painel existente compartilham
`MatchTeamTacticalPitch`; formação e atletas vêm do snapshot da partida.
O clique em outro time é estado local de apresentação. O clique no próprio time
reutiliza o comando de pausa existente.

A inspeção visual da prévia sem CDN detectou textos invisíveis nos modais porque
eles fixavam `Roboto`, que não está empacotada nessa prévia. No clássico, os
modais agora usam `ClassicCondensed`, já incluída no aplicativo. A escolha de
fonte da versão atual foi preservada.

Não houve alteração de backend, MatchEngine, regras de multiplayer, economia,
força, contratos, calendário ou formato de saves nesta implementação. Não houve
merge, push nem publicação em produção.

## Evidências

Na compilação final, `flutter analyze --no-pub` terminou sem problemas, os
**373 testes** da suíte Flutter passaram e a compilação web release terminou
com sucesso. A suíte foi repetida após a correção da fonte. Logs:
[análise](analyze.log), [regressão completa final](regression.log) e
[compilação final](build.log). A execução focada anterior, com 30 testes, está
em [focused.log](focused.log).

Os testes de widget cobrem larguras de 360, 390, 430, 768, 1366 e 1920 pixels,
atualização de minuto e formação durante consulta, abertura do próprio clube,
separação das ações de consulta e pausa, cores e o fluxo automático pós-rodada.

Na validação com API e PostgreSQL locais, duas contas exclusivas de QA jogaram
na mesma sala. Foram usadas janelas Chromium de 1366×1000 e 390×1000; não se
trata de teste em aparelho físico. Os resultados medidos estão em
[evidence.json](evidence.json), sem credenciais.

- Consulta externa: minuto 18 → 20, estado `RUNNING_FIRST_HALF` antes e depois,
  zero pedidos de pausa, modal aberto nas duas janelas.
- Próprio clube: uma solicitação de pausa, estado `GLOBAL_PAUSED`, minuto
  congelado em 36.
- Substituição real: João Gomes saiu e Paulo Carvalho entrou; o servidor
  registrou uma substituição e uma parada. Depois das duas confirmações, o
  estado voltou a `RUNNING_FIRST_HALF`, no minuto 38.
- Nenhum `pageerror` foi registrado no navegador.
- Após a rodada, o servidor já estava em `PREPARING`, rodada 2, com as duas
  janelas de volta a `#/plantel`.
- A compilação final foi recarregada nas duas janelas. Consulta externa e pausa
  do próprio clube foram verificadas novamente na rodada 2, e as capturas
  finais mostram os textos e nomes dos jogadores legíveis sem a fonte remota.

Capturas: [placar web](placar-web.png), [placar mobile](placar-mobile.png),
[consulta web](consulta-web.png), [consulta mobile](consulta-mobile.png),
[substituição web](substituicao-web.png),
[substituição mobile](substituicao-mobile.png).

## Como testar

Abra <http://127.0.0.1:5242/> e atualize a página para carregar a compilação
local. Escolha **VERSÃO CLÁSSICA (ANTIGA)**, envie a tática e clique nos nomes dos
clubes durante a rodada. Outro clube abre consulta; o próprio clube pede a pausa
e permite as substituições existentes.
