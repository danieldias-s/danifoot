# Entrega dos ajustes anteriores à missão do modal salarial

Continuação do checkpoint de mercado `ad384d3`, no worktree isolado
`D:/elifoot_98/.worktrees/account-login-lobby`. Nenhum merge, push ou produção.

## Menus e calendário

- Equipa: Jogadores, Finanças e História — Em breve. Mercado, Banco e Estádio
  ficam no rodapé, inclusive Estádio no celular.
- Campeonato: removidos os atalhos Defensores e Jogos ao vivo; adicionado
  Assistências. Não foi criada uma categoria restrita a meias, conforme a
  correção do proprietário. A categoria antiga de defensores continua na API
  e no seletor geral por compatibilidade; seu atalho foi retirado desse menu.
- Calendário clássico mostra apenas partidas definidas para o clube do
  treinador, usando os identificadores dos clubes. Rodadas sem confronto do
  clube não exibem jogos de terceiros. Resultados conserva sua consulta geral.

## Artilheiros e assistências

Os dois rankings aceitam **G, D, M e A**. Há coluna Posição no web e POS no
mobile. Assistências segue o mesmo critério de artilheiros: maior total,
cobertura das aparições, média por jogo e identificador estável no desempate.
Não exige o mínimo de 20 partidas usado por outros rankings.

Os recortes carreira inteira, temporada atual, temporada escolhida, divisão e
taça compartilham a mesma lógica. A auditoria confirmou que artilheiros já
filtra a **divisão registrada na partida**, não a divisão do clube atual do
jogador. Um atleta pode acumular entre temporadas na quarta divisão e continuar
com esse histórico depois de se transferir para a primeira. O mesmo vale para
assistências. Gols e assistências da taça não entram nas divisões.

### Registro novo das assistências

O simulador anterior não registrava o autor do último passe. Agora, nos novos
gols de bola rolando, esse dado de apresentação/estatística é simulado com um
fluxo aleatório determinístico independente. Ele seleciona um atleta ativo da
equipe atacante, de qualquer posição. Se selecionar o próprio finalizador, o
gol é individual, sem assistência. Pênaltis não recebem assistências. Esse
modelo não alega reproduzir uma fórmula comprovada do Elifoot original.

Não há novo consumo do sorteio que determina chutes, gols, cartões e lesões.
Foram capturados 20 resultados do assembly anterior e comparados por SHA-256
com as mesmas partidas após a alteração, excluindo somente os campos novos.
Placares, eventos originais, autores de gols, forças e lesões foram preservados.
O teste permanente está em `AssistOutcomeProbe`/`AssistRankingsTests`.

Assistências só são creditadas uma vez, em partida oficial concluída, a outro
jogador da mesma equipe que estava em campo. Gols contra não dão assistência.
O snapshot persiste autor do passe, cobertura e totais por temporada/competição/
divisão/clube. Os dados antigos não são reconstruídos a partir de nomes ou
substituições. Recortes com histórico sem cobertura mostram uma informação
explícita e somam apenas assistências registradas. Não foi criado prêmio
financeiro por assistências.

## Estádio

No web, o desenho menor fica à esquerda e os dados e ações à direita. Em uma
janela de 1366×1000, dados e botões ficaram visíveis sem rolagem dessa tela.
No mobile a apresentação continua vertical. Reforma usa azul; Ampliação usa
amarelo, com contraste correspondente.

**Novas** reformas e ampliações terminam depois de uma rodada global. A
bilheteria da rodada em curso é liquidada antes da conclusão da obra: ela usa
a capacidade anterior. A obra concluída vale na partida seguinte. Isso também
funciona numa rodada da taça sem partida do clube. Custos e efeito da reforma
permanecem os existentes. Obras antigas mantêm sua duração restante e o
progresso anterior por jogo; não foram reduzidas automaticamente.

Na sala de QA, a ampliação deixou a capacidade em 36.240 ao contratar, com uma
rodada restante. Depois da rodada, ela passou a 41.240 e zero rodadas restantes.

## Banco

Banco foi organizado para web/mobile com título, crédito do clube, caixa,
contratação, patrocínio, folha por jogo, saldo disponível, limite individual,
dívida e caixa negativo. Valores, limites, condições e quitação continuam vindo
do servidor. Os pedidos salariais antigos não são exibidos no Banco. A mensagem
anterior de seleção de atleta é limpa ao entrar em Banco/Estádio. Não foram
alterados crédito, juros, empréstimos, contratos ou decisões de salário.

## Validação final

- **496 testes de domínio passaram**, incluindo auditoria de divisão histórica
  e todas as posições, 20 comparações de partidas anteriores, assistências,
  progressão da obra, persistência e idempotência.
- **11 testes de integração passaram**: consulta HTTP dos rankings, SQLite após
  reinício, obras, contas, observados e intervalo com espectadores.
- **384 testes Flutter passaram**, com novos casos de posição/assistências,
  banco sem salário/nome de atleta, cores diferentes das obras, alinhamento do
  estádio, calendário do clube e menus.
- Análise Flutter sem problemas; API release sem avisos/erros; Web release
  compilada. Logs nesta pasta.
- Navegador Chromium com API/PostgreSQL locais: banco em 1366×1000 e 390×1000,
  estádio, obra real, uma rodada concluída e os rankings preenchidos por essa
  rodada. Foram observados 31 atletas com 34 assistências registradas, sem
  mínimo de jogos. A amostra teve meias, defensores e atacantes; elegibilidade
  de goleiros foi coberta pelos testes controlados. Nenhum `pageerror`.

Medições: [evidence.json](evidence.json). Capturas:
[banco web](banco-web.png), [banco mobile](banco-mobile.png),
[estádio web](estadio-web.png), [menu campeonato](menu-campeonato-web.png),
[assistências web](assistencias-web.png),
[assistências mobile](assistencias-mobile.png),
[artilheiros com posição](artilheiros-posicao-web.png) e
[calendário do clube](calendario-clube-web.png).

Os testes de navegador usaram uma conta e sala exclusivas de QA; não houve
operações no save do proprietário. Mobile foi validado por viewport, sem teste
em aparelho físico. A prévia fica em <http://127.0.0.1:5242/>.

## Fila preservada

**Novo modal de pedido de aumento salarial: pendente.** A missão anexada em
`C:/Users/Daniel/.codex/attachments/7ff8cb34-2550-447b-a3ca-bb3eadef4bc9/Texto colado.txt`
foi lida. Não foi iniciada a implementação de seu novo gatilho, transação,
recusa/leilão ou modal. A retirada do card salarial do Banco acima pertence ao
pedido anterior da tela bancária. A entrega atual conclui os ajustes que
precediam essa missão, conforme a prioridade solicitada pelo proprietário.
