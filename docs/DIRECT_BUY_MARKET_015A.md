# Compra direta e negociação — 015A

A compra direta já existia no servidor, mas faltavam confirmação de dados do anúncio, formulário claro e gestão completa no Flutter. A lista agora separa preço fixo e leilão. O proprietário pode anunciar, rever, alterar preço ou retirar anúncio; o comprador vê COMPRAR JOGADOR com preço, salário real, contrato e vendedor, e escolhe COMPRAR AGORA, FAZER PROPOSTA ou CANCELAR.

Cada anúncio possui `TransferListingVersion` persistida no snapshot JSON. A confirmação envia vendedor, preço, versão do anúncio e RoundVersion. Mudança de preço ou republicação invalida a confirmação antiga. As operações usam o mesmo semáforo por sala, recibos persistentes, chave idempotente e salvamento das finanças. A consulta de mercado também usa o semáforo e devolve uma versão coerente dos anúncios.

COMPRAR AGORA executa a transferência existente pelo preço fixo, sem exigir outra autorização do vendedor. Fundos disponíveis, contrato vigente, proprietário, estado de rodada, mínimo de 11 com goleiro e 10 de linha, máximo de 24 e vagas reservadas por leilões continuam validados. Uma compra encerra anúncio e cancela propostas Pending/Countered concorrentes no mesmo salvamento. A segunda compra retorna JOGADOR JÁ VENDIDO. Repetir a chave da compra vencedora devolve o recibo, sem novo débito.

FAZER PROPOSTA persiste Pending e não muda proprietário nem preço público. O vendedor pode recusar, aceitar ou propor outro valor; o comprador confirma a contraproposta. Uma oferta abaixo do preço não transfere automaticamente. Durante a oferta outro comprador continua podendo pagar o preço anunciado.

Valores com centavos deixaram de ser arredondados para reais inteiros em anúncios e ofertas. A revisão visual também corrigiu a contraproposta que mostrava salário/preço zero quando esses campos não estavam no DTO da oferta: a interface usa o anúncio e o jogador do clube; omite campos ausentes.

Duas vendas foram executadas no navegador com saldo conferido pela API/PostgreSQL:

| Operação | Flamengo antes → depois | Porto antes → depois |
|---|---|---|
| Oferta 600.000,25, contraproposta aceita 675.000,25 | 4.200.000 → 4.875.000,25 | 4.200.000 → 3.524.999,75 |
| Compra imediata 825.000,50 | 4.875.000,25 → 5.700.000,75 | 3.524.999,75 → 2.699.999,25 |

Flame-01 e Flame-13 mudaram uma única vez de clube; salários observados permaneceram R$ 2.449 e R$ 3.235. Não houve criação de dinheiro nas transferências. As fórmulas preexistentes de contratação e salários não foram modificadas.

Doze casos novos de integração, incluindo corrida HTTP simultânea com SQLite e PostgreSQL, cobrem um vencedor, um crédito/débito, recibo repetido, reinício do host, invalidação de ofertas, metadados antigos, consentimento humano, retirada/republicação, saldo, contrato, limite e último goleiro. A corrida foi validada numa API autoritativa por sala, conforme a arquitetura atual; não se afirma exclusão distribuída entre duas instâncias independentes.

Evidências: [confirmação desktop](evidence-015a/desktop-direct-confirmation.png), [mobile](evidence-015a/mobile-direct-confirmation.png), [contraproposta com dados reais](evidence-015a/counteroffer-actual-metadata.png), [saldos antes](evidence-015a/browser-balances-before-direct.json), [após negociação](evidence-015a/browser-after-counteroffer.json), [após compra](evidence-015a/browser-after-immediate-buy.json).
