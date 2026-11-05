# Pedido de aumento salarial — entrega anterior à 017C

Base: 710da1a. Branch codex/salary-request-modal. Sem merge, push ou produção.

## Auditoria e regras reutilizadas
- Modelo existente: ClassicEconomyState.SalaryRequests, SalaryEligibility, ContractHistory e SalaryRequestRounds. Persistência no SnapshotJson do save, incluindo PostgreSQL.
- Gatilho mantido: evolução positiva da força ou promoção de divisão. Seleção determinística existente pelo maior desvio salarial; nenhuma probabilidade original do Elifoot foi presumida nem criada.
- A pesquisa de referência encontrou relatos de jogadores sobre renovação e venda automática, mas não documentação primária comprovando uma fórmula probabilística do Elifoot 98. A implementação segue as regras auditadas do projeto.
- Valor solicitado continua usando CalculateFairSalaryPerRound, fator já existente da divisão e MaximumSalaryRaiseFraction (10%, metade para caixa negativo), arredondamento existente. Unidade: por partida oficial disputada. Nada é cobrado por abrir o modal.
- Somente sem contrato vigente (ContractRoundsRemaining <= 0), não renovado nesta temporada, não anunciado e não reservado em leilão. Sem geração enquanto existirem partidas não finalizadas no lote.
- Um pedido pendente por clube e um pedido por clube/temporada/rodada. Verificações sem evento também são persistidas em SalaryRequestCheckedRounds.
- Aceitar aplica exatamente o valor persistido e usa ProtectSeasonContract até o fim da temporada; não cria prazo arbitrário de 20 partidas. Salário permanece, marcador é consultado pelo número da temporada.
- Recusar reutiliza NewSpecialAuction e o fluxo SalaryRenewalRefused: atleta torna-se livre, lance mínimo zero conforme política anterior, sem receita ao clube de origem e sem transferência imediata a comprador.
- Duração sorteada por RNG determinístico independente: 1, 2 ou 3 rodadas globais, mesmos limites do mecanismo voluntário; encerramento oficial respeita passagem de temporada. A duração fica no AuctionItem e é exposta no mercado.
- Elenco mínimo continua protegido. Uma recusa humana inviável retorna erro e mantém o pedido; não é convertida silenciosamente em aceitação. IA mantém sua política prévia de renovação obrigatória neste caso.

## Consistência, autorização e save
- Mesmo lock de mundo usado pelo processamento, HTTP e finanças. Decisão repetida com chave igual ou outra chave não duplica salário, contrato ou leilão. Decisão oposta posterior falha.
- Autorização de conta existente e controle atual do clube obrigatórios. ResolvedByAccountId vem da sessão autenticada, nunca da interface.
- Novos campos aditivos e opcionais: timestamps, ator, conta e versão de decisão; novo conjunto de rodadas verificadas. Nenhuma migração destrutiva ou banco paralelo.
- Pedido só é marcado recusado depois que o anúncio é criado. Falha de validação mantém pendência. Finance grava receipt e save antes da notificação e reenvio pode repetir a durabilidade.
- No modelo atual o save é um agregado JSON e a exclusão mútua é por processo da sala, como nas demais transações existentes. Não foi introduzida uma topologia com múltiplos servidores proprietários da mesma sala.

## Interfaces
- Modal compartilhado, somente na tela inicial do próprio time em PREPARING, fora de lobby/partida/táticas/configurações. Nenhum card em Finanças/Banco.
- Clássico: faixa azul, painel creme, fonte embarcada, aceitar verde, recusar neutro/vermelho. Atual usa a apresentação corrente. Mobile com botões empilhados e corpo rolável.
- Sem X ou fechamento que resolva pedido. Bloqueio de cliques duplos, indicador de progresso e erro preservam a decisão pendente.
- Conteúdo: jogador, posição, identificação do clube, SEM CONTRATO, salários persistidos e diferença por jogo.

## Validação nesta branch
- .NET domínio: 500 aprovados (salary-domain-final.log).
- .NET aplicação: 31 aprovados (salary-application.log).
- Integração HTTP/contas/finanças/SignalR: 14 aprovados, incluindo duas abas simultâneas, conta alheia e recuperação após reinício (salary-integration-final.log).
- Flutter: suíte completa 388 aprovada; após adicionar dois cenários de abertura integrada, bateria focada de 8 cenários aprovada.
- Analyzer sem problemas; build API Release e Web Release aprovados. Web usa recursos locais, sem CDN.
- Inspeção visual: atual-web.png, atual-mobile.png, classico-web.png, classico-mobile.png.
- PostgreSQL real: apenas sala QA criada pelo agente (evidence.json). Pedido preparado na base de teste, API reiniciada, modal observado nas duas apresentações e após refresh. Recusa pela UI persistiu REFUSED, versão 1, conta responsável e um AuctionId. Nenhum pageerror.
- Saves do proprietário não foram editados nos testes. Testes antigos que fabricavam pedidos para atletas contratados foram adequados à nova restrição; teste do card financeiro foi substituído pela ausência do card, com testes próprios para o modal.

## Checkpoint e sequência
Modal salarial entregue antes de iniciar a 017C. Implementação 017C deverá partir deste checkpoint, mantendo as alterações anteriores.
