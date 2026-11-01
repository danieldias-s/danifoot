# Formulário de leilão — 015A

Antes, Vender anunciava imediatamente pelo preço sugerido; os campos do leilão estavam num botão separado da tabela. O lance também era calculado e enviado diretamente. A correção disponibiliza ANUNCIAR JOGADOR DO MEU CLUBE, COLOCAR EM LEILÃO e VENDA POR PREÇO FIXO, além da ação no plantel.

O formulário informa jogador, posição, força e salário contratado recebido da API. Lance mínimo começa em R$ 500.000, aceita zero e até dois decimais. A duração começa em 2 e oferece 1, 2 ou 3 rodadas. REVISAR ANÚNCIO apresenta modalidade, mínimo, duração, início e encerramento; somente CONFIRMAR faz a requisição. CANCELAR em qualquer etapa não publica.

O endpoint continua usando `CreateVoluntaryAuctionAsync` e `VoluntaryAuctionEngine`, sem alterar a contagem aprovada. Criado na preparação R7: duração 1 termina após R7; 2 após R8; 3 após R9. A prévia contempla criação em RESULTS e mudança de temporada. Somente conclusão oficial global reduz o prazo; reload, reconexão e partidas individuais não contam.

No navegador real foram criados e persistidos três leilões:

| Jogador | Mínimo | Duração | Encerramento oficial | Arremate |
|---|---:|---:|---|---:|
| Flame-05 | R$ 575.000,25 | 1 | R1 | R$ 595.000,25 |
| Flame-03 | R$ 500.000 | 2 | R2 | R$ 580.000 |
| Flame-04 | R$ 500.000 | 3 | R3 | R$ 750.000,75 |

Uma prévia de R$ 575.000,25/3 rodadas foi cancelada antes dessas publicações: permaneceu sem anúncio. A captura `desktop-auction-review.png` é dessa prévia cancelada, não do leilão final de três rodadas.

Outro treinador abriu DAR LANCE, teve valor 1 rejeitado e confirmou R$ 650.000,75. A API registrou o lance; a IA posteriormente venceu com valor maior pelas regras existentes. O diálogo mostra mínimo, líder e rodadas restantes, sem revelar o teto privado da IA.

Leilões automáticos, mínimo zero, vínculo por insolvência, livre após recusa salarial legal, crédito ao vendedor e proteção de elenco permanecem cobertos pela regressão. A troca já existente de anúncio fixo para leilão continua uma operação única, sem anúncios incompatíveis simultâneos.

Evidências: [padrões desktop](evidence-015a/desktop-auction-defaults.png), [padrões mobile](evidence-015a/mobile-auction-defaults.png), [mínimo personalizado](evidence-015a/desktop-auction-custom-review.png), [criação](evidence-015a/desktop-auction-created.png), [lance](evidence-015a/desktop-custom-bid.png), [estado após R3](evidence-015a/browser-market-after-round3.json).
