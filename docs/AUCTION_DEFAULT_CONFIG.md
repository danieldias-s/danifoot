# ELIFOOT_AUCTION_DEFAULT_CONFIG

STATUS: PASS.

Worktree: `D:\elifoot_98_014C`

Branch: `feature/club-colors-theme-014c`

Base: `33d84882b61b4ff0c0903c7b2d0270e2de757e98`

Checkpoint: commit que adiciona este relatório (`git log -1 --format=%H`).

Antes da edição, o worktree estava limpo. A listagem de chats não indicava outra sessão ativa no projeto além desta. A implementação ficou somente no worktree ativo 014C; branches e serviços dos demais worktrees foram preservados. Nenhum merge, push ou deploy.

## Comportamento

A base possuía anúncio por preço fixo (VENDER) e leilões automáticos, sem criação manual de leilão. A nova ação LEILOAR no plantel abre um formulário próprio, mantendo a venda por preço fixo disponível.

Cada abertura de novo leilão voluntário inicia com MinimumBid=500000 e DurationRounds=2. O treinador pode escolher qualquer valor monetário válido maior ou igual a zero, com até duas casas decimais, e duração de 1, 2 ou 3 rodadas. O valor escolhido é enviado ao servidor sem arredondar centavos para reais inteiros. O servidor valida novamente os parâmetros; os padrões não alteram leilões existentes ou os criados automaticamente.

A configuração é feita antes da criação. Depois de confirmar, o leilão e seu prazo ficam persistidos; esta tarefa não acrescenta edição das condições de um leilão já aberto.

## Prazo por rodada global

Criado na PREPARAÇÃO R7 com duração 2: encerra após a conclusão oficial da R8. A conclusão de R7 conta como a primeira rodada; a conclusão de R8 é a segunda. Entrar na preparação de R8 não encerra o leilão.

Criado em RESULTADOS R7: a R7 já terminou, portanto duração 2 encerra após R9. A interface mostra o prazo incluindo rodada e temporada, usando o calendário recebido do servidor. Prazo que ultrapassa a última rodada continua na temporada seguinte, preservando a duração.

A liquidação voluntária exige estado RESULTS e a chave persistente `round_settled:Sx:Ry` do encerramento financeiro global, além do prazo por temporada/rodada. A chamada que conclui a rodada no orquestrador só chega a esse estado depois das partidas globais. Finalizar um jogo isolado, ler o mercado, recarregar, reconectar ou repetir uma operação não avança o prazo. Repetir a liquidação de um leilão fechado não move dinheiro.

Campos opcionais DurationRounds e ExpiresAtSeason foram acrescentados ao snapshot/DTO do leilão. Leilões antigos sem esses campos conservam a regra anterior. O motivo Voluntary=3 preserva os números existentes 0, 1 e 2 do enum.

## Proteções e economia

O jogador continua no clube vendedor até venda válida. Sem comprador, ou se a venda falhar na revalidação de caixa/elenco, o leilão fecha sem liberar o jogador nem criar receita fictícia.

A criação verifica o mínimo de 11 atletas, 1 goleiro e 10 de linha, considerando também as saídas já reservadas por outros leilões. A transferência revalida novamente ambos os elencos, caixa disponível e reservas do comprador. Lance efetivo zero permanece diferente de ausência de lance pelo HighestBidderClubId.

A venda usa ExecuteTransfer e o livro-caixa anterior: AuctionPurchase negativo no comprador, AuctionSale positivo no vendedor, valor idêntico e metadados de transferência. Os mecanismos anteriores de salário/contrato da transferência, reservas financeiras, incremento de lance e juros da 012D foram mantidos. Leilões automáticos de insolvência/recusa salarial continuam com mínimo zero; vencimento contratual conserva a modalidade anterior.

Não houve alteração em MatchEngine, nas fórmulas defensivas, de força, empréstimos, salários, patrocínio, inflação ou competição. As alterações no motor de mercado apenas encaminham o novo motivo de leilão e permitem sua transferência com a mesma reconciliação. A validação de centavos foi ampliada somente para leilões voluntários, mantendo a validação de reais inteiros nos automáticos.

## Persistência, concorrência e API

`POST /api/worlds/{worldId}/market/auction` recebe CoachId, PlayerId, MinimumBid opcional, DurationRounds opcional, IdempotencyKey e ExpectedRoundVersion. Valores omitidos usam os padrões definidos no contrato. A resposta contém o AuctionId.

A criação usa Finance, o mesmo lock da sala, autorização do clube controlado, restrição entre partidas/pronto, versão da sala, recibo persistente e publicação MarketUpdated do fluxo financeiro 012D. Repetições com a mesma chave e conteúdo recuperam o mesmo resultado, inclusive com versão antiga após perda de resposta. Reutilizar a chave com outra configuração é rejeitado. O cliente reutiliza o mecanismo de chave financeira quando perde a resposta ou recebe erro de servidor.

O snapshot persistido guarda as condições, a propriedade, o prazo e o recibo. Não depende de contador no Flutter. Não requer migração destrutiva de banco: os leilões já fazem parte do snapshot JSON existente.

## Validação executada

| Verificação | Resultado |
| --- | ---: |
| Build Release da solução | aprovado, zero avisos/erros |
| Domínio completo | 409 aprovados |
| Aplicação completa | 7 aprovados |
| Integração selecionada: leilões, economia 012D/ajuste, persistência, SignalR e cores 014C | 28 aprovados |
| Flutter analyze | sem problemas |
| Flutter completo | 120 aprovados |
| Flutter web Release | aprovado |

Os 444 testes .NET incluem 18 casos de domínio do novo leilão, replay após falha de SaveWorld e duas integrações novas. Os seis testes novos Flutter verificam padrões, duração, mínimo zero, centavos, nova abertura com padrões restaurados, prazo pós-resultados, chamada real da ação de plantel, idempotência e viewport 320/390/1366. Demais testes anteriores foram preservados.

A integração HTTP executou R7 e R8 com o MatchEngine real e SignalR: R7 não fechou o leilão de duração 2; preparação de R8 também não; conclusão de R8 fechou com venda reconciliada. Cobriu criação concorrente, consulta repetida de mercado e replay depois da venda. Outra integração recriou o repositório SQLite, recuperando mínimo zero/duração 3 e o prazo original.

Aceitação adicional com servidor real e PostgreSQL próprio em loopback 55438: quatro criações concorrentes produziram um único leilão padrão; condições alternativas zero/3 e 123,45/1 foram persistidas. Após a primeira rodada global, só o de uma rodada estava encerrado. API e PostgreSQL foram realmente encerrados e reiniciados. Depois da segunda rodada, o leilão padrão fechou, e o de três rodadas continuou aberto. A IA fez lances reais: arremate R$ 540.000, crédito do vendedor R$ 540.000 e débito do comprador R$ -540.000. Replay após a venda recuperou o mesmo leilão sem criar outro.

Evidências em `audit-auction-default-config/`: TRX, logs de build/análise/testes, baseline/hashes, `http-before-restart.json`, `http-restart-and-settlement.json`, `preservation.json` e `validation.json`. O script reproduzível é `scripts/smoke-auction-default-config.py` (cria mundo novo próprio no servidor local). Não executar repetidamente a etapa de continuação sobre o mesmo contexto já encerrado.

Os testes longos de 100 temporadas não foram repetidos: a validação desta tarefa cobre o fluxo novo e a regressão econômica existente; não há alteração dos coeficientes econômicos. Não se declara uma nova certificação estatística de 100 temporadas.

## Disponibilidade e pendências

Versão local atualizada em http://127.0.0.1:5156, API http://127.0.0.1:5155, relógio manual para revisão. Recarregar a página carrega o novo build. Os testes visuais desta tarefa são widgets reais em desktop/mobile; não foi acrescentada uma auditoria manual de navegador.

Sem pendências funcionais conhecidas para o escopo solicitado. Cobertura de cores e auditorias de carreira da missão 014C continuam com as ressalvas registradas nos relatórios daquela missão e não são reclassificadas por este PASS.
