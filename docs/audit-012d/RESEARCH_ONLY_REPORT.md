**ELIFOOT_ECONOMY_012D_RESEARCH_ONLY — pesquisa de 30/10/2026**

Existe uma base econômica reutilizável, mas as regras propostas para a 012D ainda exigem mudanças no crédito, nos contratos e nos motivos dos leilões. **O principal conflito com a 012C é o recálculo automático de salários quando a força muda.**

A pesquisa foi exclusivamente de leitura. Não alterei arquivos, não acessei o banco, não executei testes, migrations ou comandos Git e não interagi com a sessão da 012C. Este relatório não foi gravado na pasta compartilhada. Como a 012C está em andamento, o diagnóstico representa o código observado durante esta consulta.

**O que já está implementado e o que falta.**

| Tema | Implementação observada | Lacuna para a 012D |
|---|---|---|
| Empréstimo de R$ 500.000 | O serviço aceita valores entre **R$ 50.000 e R$ 1.500.000**, com prazo de 5–20 rodadas. A interface solicita R$ 300.000 em 10 rodadas. | Não existe modalidade específica de R$ 500.000. |
| Juros de R$ 25.000 por partida | São calculados **1,5% sobre o saldo devedor**, com pagamento automático de prestação. | Separar juros fixos, principal e quitação; definir cobrança por partida efetivamente disputada. |
| Crédito por clube | Todos os clubes estão sujeitos ao mesmo máximo de **dois empréstimos ativos**. | Não há limite monetário individual nem regra por divisão, patrimônio ou receita. |
| Renovação salarial | A renovação manual acrescenta rodadas e cobra luvas. No vencimento, uma heurística pode renovar automaticamente por 20 rodadas e recalcular salário. | A renovação manual não negocia salário. Falta uma proposta salarial explícita. |
| Pedidos de aumento | Não encontrei entidade, estado pendente ou comando para responder a pedidos. | Implementação ausente. |
| Leilão por recusa salarial | Existe leilão por **vencimento contratual sem renovação automática**. | Não existe recusa explícita nem motivo salarial registrado no leilão. |
| Leilão por insolvência | A IA anuncia excedentes e aplica descontos quando o orçamento está apertado. | Não há processo formal de insolvência nem abertura compulsória de leilão para humanos e IA. |
| Elenco mínimo | Já são protegidos **11 jogadores, pelo menos um goleiro e dez de linha**. Transferências e vencimentos consultam essa proteção. | Aplicá-la também aos novos fluxos de recusa e insolvência, incluindo operações simultâneas. |
| Inflação salarial e de transferências | Existem fórmulas de referência dependentes de força e outros atributos. A IA limita suas avaliações de compra. | Não há índice geral de inflação. Salário contratado e salário de referência estão misturados. |

As principais evidências estão no [serviço de contratos e empréstimos](D:/elifoot_98/src/Elifoot.Application/ClubAndMarketApplicationService.cs:377), no [processamento financeiro](D:/elifoot_98/src/Elifoot.Domain/MarketAndEconomyEngine.cs:294) e no [validador de elenco](D:/elifoot_98/src/Elifoot.Domain/TacticsAndSquadRules.cs:51).

O leilão atual merece uma distinção: o jogador perde o vínculo antes da disputa; o lance mínimo começa em 65% do valor justo; o ex-clube **não recebe o arremate**, pois o atleta é tratado como livre. Sem vencedor válido, pode ocorrer contratação gratuita por uma IA. Esse comportamento está em [SettleAuction](D:/elifoot_98/src/Elifoot.Domain/MarketAndEconomyEngine.cs:882) e tem um [teste existente que exige ausência de crédito ao ex-clube](D:/elifoot_98/tests/Elifoot.Domain.Tests/MetricAiAudit007Tests.cs:122). Portanto, não basta reutilizar esse leilão sem distinguir sua finalidade.

**Evidências históricas e seus limites.**

- Uma FAQ histórica espelhada, com atualização indicada em **24/02/2000**, descreve o mínimo de 11 jogadores, incluindo um goleiro e dez de linha, máximo de 24 e disponibilidade de empréstimos na versão registrada. É documentação preservada em site de terceiros, sem autenticação do executável. [Richard’s Page](https://www.geocities.ws/ri_keller/ellifoot.html).
- Um guia de **09/09/2010** descreve juros altos cobrados **a cada partida** e recomenda quitar rapidamente. Não informa o principal nem o valor dos juros. [Evite empréstimos](https://elifoot98.blogspot.com/2010/09/evite-emprestimos.html).
- Um guia de **30/05/2010** recomenda renovar no começo da temporada, relata pedidos salariais crescentes quando a renovação é adiada e afirma que a venda automática por falta de dinheiro alcança jogadores sem contrato renovado. É evidência descritiva, sem fórmula ou limiar financeiro. [Renovação de contratos](https://elifoot98.blogspot.com/2010/05/renovacao-de-contratos.html).
- Uma retrospectiva de **06/04/2017**, acompanhada de entrevista com André Elias, descreve ultimatos salariais e saída imediata em leilão, com pequeno retorno financeiro ao treinador/clube. Não determina percentual, elegibilidade ou tratamento do elenco mínimo. [Record — 30 anos de Elifoot](https://www.record.pt/record-gaming/retrogaming/detalhe/30-anos-de-elifoot-um-jogo-pioneiro-e-um-papa-babado).
- O projeto público de pesquisa técnica consultado apresenta contratos e finanças no roteiro de investigação e ainda propõe aprofundar mercado, leilões e bancarrota. O material examinado não fornece uma fórmula econômica completa verificável. [Roadmap da pesquisa](https://github.com/trsthales/desvendando-elifoot-98/blob/main/ROADMAP.md).

**Não encontrei confirmação independente dos valores exatos de R$ 500.000 e R$ 25.000**, de uma tabela histórica de crédito por clube ou de percentuais gerais de inflação. Esses números podem ser especificados para a 012D, mas devem permanecer identificados como parâmetros da missão até haver comprovação histórica.

R$ 25.000 sobre R$ 500.000 corresponde aritmeticamente a **5% por partida**. Isso, isoladamente, não esclarece capitalização, amortização, vencimento ou quitação parcial.

**Riscos concretos encontrados no código.**

1. **Quitação com saldo restante.** `BankLoan.IsPaidOff` retorna verdadeiro quando o prazo acaba, mesmo que ainda exista saldo devedor. A prestação usa acréscimo total de 15%, enquanto a evolução da dívida aplica 1,5% periodicamente. Uma conferência aritmética independente para R$ 500.000 em 20 prestações deixa aproximadamente R$ 8,6 mil ao término. É uma constatação estática, sem execução do projeto. [BankLoan](D:/elifoot_98/src/Elifoot.Domain/Entities.cs:443).
2. **Salários negociados seriam sobrescritos.** A evolução clássica atual altera `SalaryPerRound` junto com o preço de mercado. O motor legado também possui atribuição equivalente. [Motor clássico](D:/elifoot_98/src/Elifoot.Domain/ClassicForceEvolutionEngine.cs:271), [motor legado](D:/elifoot_98/src/Elifoot.Domain/PlayerStrengthEvolutionEngine.cs:65).
3. **Comandos financeiros precisam compartilhar a sincronização da sala.** Os métodos de empréstimo e renovação consultados não apresentam o mesmo bloqueio por mundo usado pelo orquestrador. A 012D deverá validar fase, versão e repetição dos comandos dentro desse bloqueio.
4. **A interface estima um salário diferente do servidor.** A lista de mercado calcula salário como 3,5% do valor justo, embora o DTO já forneça `SalaryPerRound`. Isso deve ser corrigido ao apresentar propostas salariais. [Interface do mercado](D:/elifoot_98/client/elifoot_flutter/lib/main.dart:3262).

**Arquivos que precisarão ser alterados ou integrados.**

| Arquivos | Trabalho previsto |
|---|---|
| [Entities.cs](D:/elifoot_98/src/Elifoot.Domain/Entities.cs) | Estado econômico versionado, modalidades de empréstimo, propostas salariais e motivo/vendedor dos leilões. |
| [MarketAndEconomyEngine.cs](D:/elifoot_98/src/Elifoot.Domain/MarketAndEconomyEngine.cs) | Cobranças, vencimentos, liquidação dos diferentes leilões e avaliação financeira. |
| [ClubAndMarketApplicationService.cs](D:/elifoot_98/src/Elifoot.Application/ClubAndMarketApplicationService.cs) | Contratar/quitar empréstimos, renovar salários, responder a pedidos e proteger comandos concorrentes. |
| [MultiplayerRoundOrchestrator.cs](D:/elifoot_98/src/Elifoot.Application/MultiplayerRoundOrchestrator.cs), [CompetitionAndSeasonEngine.cs](D:/elifoot_98/src/Elifoot.Domain/CompetitionAndSeasonEngine.cs) | Integrar processamento por partida, decisões pendentes e transição de temporada. |
| [ClassicForceEvolutionEngine.cs](D:/elifoot_98/src/Elifoot.Domain/ClassicForceEvolutionEngine.cs), [PlayerStrengthEvolutionEngine.cs](D:/elifoot_98/src/Elifoot.Domain/PlayerStrengthEvolutionEngine.cs) | Separar atualização de valor de mercado da alteração do salário contratado. |
| [AiManagerBrain.cs](D:/elifoot_98/src/Elifoot.Domain/AiManagerBrain.cs), [WorldSeedFactory.cs](D:/elifoot_98/src/Elifoot.Domain/WorldSeedFactory.cs) | Orçamento com dívida e salários comprometidos; inicialização das regras econômicas. |
| [Dtos.cs](D:/elifoot_98/src/Elifoot.Contracts/Dtos.cs), [Enums.cs](D:/elifoot_98/src/Elifoot.Contracts/Enums.cs), [Program.cs](D:/elifoot_98/src/Elifoot.Api/Program.cs:596) | Contratos públicos, categorias financeiras e endpoints. |
| [api_client.dart](D:/elifoot_98/client/elifoot_flutter/lib/api_client.dart), [main.dart](D:/elifoot_98/client/elifoot_flutter/lib/main.dart) | Crédito disponível, dívida, juros, quitação, ofertas salariais e consequências das decisões. |
| [EfWorldRepository.cs](D:/elifoot_98/src/Elifoot.Infrastructure/EfWorldRepository.cs) | Compatibilidade dos snapshots e persistência dos novos estados. |
| Suítes de domínio, aplicação, integração, simulação e Flutter existentes | Acrescentar cobertura econômica e preservar as expectativas anteriores por modalidade. |

O validador de elenco existente deve ser reutilizado. Alterá-lo só será necessário se surgir uma regra adicional explícita.

A persistência principal utiliza JSON do mundo; **não está demonstrada necessidade de migration relacional** para os novos estados. Já a auditoria histórica completa exige atenção: o livro-caixa SQL atual não conserva todos os metadados presentes nas transações do snapshot.

**Possíveis conflitos com a 012C.**

- **Força e salários:** conflito funcional direto descrito acima. A 012D deverá preservar força, lesões, probabilidades e histórico da 012C.
- **Dias virtuais e partidas:** a 012C avança datas e pode adiar jogos. Esse avanço não deverá gerar juros de “partida” sem uma partida disputada.
- **Elenco cadastrado e disponibilidade:** proteger 11 atletas registrados é diferente de exigir 11 disponíveis naquele momento. A 012D precisa respeitar as soluções da 012C para lesões e suspensões.
- **Ordem de processamento:** hoje as finanças são liquidadas antes da atualização de resultados/força e do mercado da IA. A definição do momento de gerar pedidos salariais afeta o valor oferecido e a oportunidade de resposta. [Fluxo atual](D:/elifoot_98/src/Elifoot.Application/MultiplayerRoundOrchestrator.cs:1049).
- **Arquivos compartilhados:** entidades, contratos, serviço de aplicação, orquestrador, IA e interface já aparecem no [escopo documentado da 012C](D:/elifoot_98/docs/CLASSIC_FORCE_EVOLUTION_012C.md:69).
- **Balanceamento:** patrocínio entra em todas as rodadas, enquanto salários e empréstimos dependem da participação. Essa assimetria já é documentada e pode mascarar insolvência ou acumular caixa. [Economia existente](D:/elifoot_98/docs/ECONOMIA_ESTADIOS.md:41).

A implementação futura deverá partir do estado final da 012C e revalidar estes pontos, sem sobrescrever arquivos com versões anteriores.

**Plano de implementação da 012D — proposta, sem execução.**

1. **Fechar a especificação econômica.** Versionar as regras independentemente do modo de evolução de força. Registrar as decisões ainda abertas: teto de crédito por clube, prazo dos empréstimos, quitação parcial, gatilho de insolvência, elegibilidade para venda e política sem lances.

2. **Implementar a modalidade bancária.** Para o cenário solicitado, representar unidades de R$ 500.000 e juros de R$ 25.000 por unidade e partida. Separar principal e juros; oferecer quitação explícita; controlar crédito disponível pelo principal comprometido. Preservar empréstimos antigos segundo sua modalidade, sem conversão silenciosa.

3. **Separar salário contratado e referência salarial.** Mudanças de força atualizam a referência de mercado. O salário contratado muda por negociação ou outra regra econômica expressamente definida. Persistir cada pedido com valor, validade e estado: pendente, aceito, recusado ou expirado.

4. **Distinguir os leilões.** Identificar vencimento contratual, recusa salarial e insolvência. Definir vendedor, beneficiário do pagamento e vínculo durante a disputa. Preservar a modalidade atual de atleta livre. Revalidar caixa, propriedade e elenco na liquidação, com proteção contra venda simultânea e comprometimento excessivo de recursos.

5. **Tratar insolvência após a liquidação financeira.** Diferenciar caixa negativo, inadimplência e insolvência. Como proposta alinhada ao relato histórico, avaliar primeiro jogadores sem renovação protegida. Se não houver atleta removível, manter o piso do elenco e registrar a crise; a política de recuperação deverá ser explícita.

6. **Investigar inflação por componentes.** Medir separadamente salários contratados, referência salarial, valor justo, preço efetivo das transferências e caixa. Comparar atletas de qualidade equivalente para não confundir evolução de força com inflação. Não acrescentar multiplicador anual sem evidência ou decisão de projeto.

7. **Integrar API, interface e IA.** Mostrar principal, juros por partida, crédito disponível, proposta salarial e consequência da recusa. Aplicar as mesmas regras econômicas a humanos e IA, dentro da sincronização da sala.

8. **Validar futuramente em ambiente isolado.** Usar mundos em memória e banco descartável próprio. Cobrir cobrança única por partida, ausência de cobrança em adiamentos, quitação real, crédito concorrente, decisões salariais, conservação financeira dos leilões e proteção do elenco. Depois, executar simulações com várias seeds e estratégias, preservando as regressões da 012C.

Os critérios centrais de aceite são: nenhuma cobrança duplicada; nenhuma dívida encerrada apenas pelo fim do contador; nenhum salário negociado sobrescrito pela evolução de força; nenhuma remoção abaixo do elenco mínimo; e rastreabilidade do dinheiro em cada modalidade de leilão.

**Nenhuma implementação ou publicação foi realizada.**
