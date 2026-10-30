# Medição de salários, mercado e inflação — 012D

Nenhum índice arbitrário de inflação foi introduzido. A curva de valor justo anterior permanece; a curva salarial passa a ser referência, separada do salário contratado. Transferências e novos contratos podem negociar salários novos; a mudança de força isolada não modifica a folha vigente.

A simulação econômica observa separadamente: salário contratado médio, salário de referência a qualidade constante, valor justo médio, valor justo a qualidade constante, preços e categorias das transferências, caixa, empréstimos, juros, principal e crises. As faixas de força exata são 1, 10, 20, 30, 40 e 50; uma faixa vazia é `null`, sem substituir por um zero artificial.

O controle de qualidade fixa força, posição M, comportamento Cavalheiro, ausência de estrela e dez partidas contratuais. Assim, mudanças na distribuição de força, posições ou contratos não são confundidas com alteração da curva de preços. O JSON contém todas as temporadas e os marcos 1/5/10/25/50/75/100, além da distribuição de crédito por divisão e dívida agregada.

A referência fixa constante demonstra ausência de indexação temporal do modelo. Não demonstra estabilidade de caixa, salários efetivos ou preços de arremate. Acumulação de dinheiro por patrocínios e receitas pode coexistir com queda da força média, e deve ser relatada como tal. Patrocínio por rodada inclusive em folgas é comportamento anterior preservado, com efeito especialmente relevante no Distrital.

Os valores efetivos e a conclusão de balanceamento serão apresentados em `ECONOMY_100_SEASONS_012D.md`, gerado a partir da execução concluída. Não se deve declarar uma economia equilibrada com base apenas em conservação contábil ou ausência de inflação da curva fixa.

A conferência independente da execução específica encontrou R$ 18.107.128.451,84 de caixa final. Com decisões mantidas, subtrair aritmeticamente os R$ 12.566.400.000 de patrocínios ainda deixaria R$ 5.540.728.451,84; isso é uma decomposição contábil, não outra simulação. Portanto, cortar apenas essa receita não demonstra solução do balanceamento. Bilheteria, prêmios, salários, estádio e contratações de livres também precisam ser considerados de forma conjunta antes de substituir políticas anteriores. Não se atribui a expansão monetária a empréstimos cujo principal foi inteiramente quitado.
