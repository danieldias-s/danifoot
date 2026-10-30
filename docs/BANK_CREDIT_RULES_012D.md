# Crédito individual por clube — 012D

Regra adaptada e versionada `CLASSIC_ADAPTED_ASSET_CREDIT_V1`, sem alegação de fórmula histórica original.

Indicador = capacidade utilizável atual do estádio × R$ 50. O cálculo usa patrimônio operacional observável; tomar dinheiro emprestado não aumenta o indicador. A avaliação persistida informa clube, divisão, indicador, limite, empréstimos ativos, vagas e dívida. É refeita em consultas financeiras, operações, liquidações e mudanças de temporada/divisão. Dívidas existentes nunca são eliminadas quando o limite cai.

| Divisão | Limite base | Limiares do indicador que concedem uma vaga adicional cada | Máximo |
|---|---:|---|---:|
| Primeira | 2 | R$ 1.600.000; 1.900.000; 2.200.000 | 5 |
| Segunda | 2 | R$ 1.000.000; 1.250.000 | 4 |
| Terceira | 1 | R$ 500.000; 750.000 | 3 |
| Quarta | 1 | R$ 400.000 | 2 |
| Distrital | 1 | nenhum | 1 |

Na quarta divisão, 6.000 lugares utilizáveis produzem limite 1 e 9.000 produzem limite 2. Os clubes da mesma divisão podem, portanto, ter capacidades diferentes. O estado inicial real já distribui estádios em torno desse limiar; desgaste e obras podem modificar avaliações futuras.

Todo empréstimo com saldo positivo ocupa uma vaga, incluindo os legados com prazo encerrado. Limite disponível = máximo(0, limite individual − empréstimos ativos). Redução do limite não altera empréstimos vigentes; apenas impede novas contratações acima do limite. Valores e limiares são configuração explícita da adaptação, sujeita a avaliação pelos dados de 100 temporadas.

Na execução final específica, as faixas foram mantidas para a validação funcional: cinco operações liberaram R$ 2.500.000 de principal, todos quitados, com R$ 1.275.000 de juros efetivamente pagos. O custo observado equivaleu a 51% do principal desembolsado; a receita bancária não foi gratuita. Um empréstimo mantido por 20 partidas custa R$ 500.000 de juros, além do principal. A quarta divisão continuou apresentando limites individuais 1 e 2 em T100. Não ocorreu colapso generalizado por crédito. Entretanto, a acumulação de caixa nas receitas preexistentes impede declarar esses tetos definitivamente calibrados para escassez estratégica em todo o horizonte; essa pendência está registrada.
