# Histórico do jogador — missão 017C

A consulta existente `/api/worlds/{world}/players/{player}/history` recebeu
campos adicionais; identidade e estatísticas continuam vinculadas ao ID do
jogador. Comprar, vender ou trocar de divisão não apaga a carreira.

A tela mantém o resumo, as estatísticas e os eventos de força existentes e
acrescenta totais por temporada, assistências, passagens pelos clubes, títulos
com participação registrada, artilharias por competição, transferências,
força inicial/final de cada temporada e recordes individuais.

Os totais por clube/competição/divisão usam o contexto da partida original.
O total de carreira foi comparado com a soma das temporadas para jogos, gols
e assistências. Cartões, minutos, assistências e métricas de desempenho vêm
das estatísticas já persistidas. Cobertura incompleta é identificada; não são
criadas notas ou eventos de futebol externo para atletas reais importados.

A força inicial só é conhecida quando a coleta começou no início efetivo da
temporada. A força final é registrada no fechamento do ano, após as regras
vigentes da transição. Consultar a história não executa evolução. O recorde de
força usa os extremos preservados nos eventos, os marcos anuais e a força
atual. Forças antigas desconhecidas permanecem nulas.

As artilharias usam os rankings existentes e aceitam qualquer posição.
Há registros geral, por divisão histórica e da Taça. Empates no maior número
de gols são preservados. O pagamento vigente do artilheiro geral não foi
alterado e não se criaram pagamentos por esses novos resumos. As métricas e
o mínimo de 20 jogos dos rankings existentes de goleiros/defensores não mudam.

Um título de clube só aparece na carreira se houver ao menos uma participação
oficial registrada pelo atleta naquele clube e competição na temporada. Isso
é uma apresentação histórica, sem nova regra de premiação. Artilharias gerais
legadas são recuperadas dos prêmios existentes; divisões antigas sem evidência
não são estimadas.

O histórico é acessível pelo plantel, pelos detalhes do mercado e pelos
rankings, nas duas aparências. Em jogos ao vivo permanece o bloqueio de
navegação. A base não possui empréstimo de atletas; não foi implementada essa
negociação na 017C. Empréstimos bancários são outro recurso.
