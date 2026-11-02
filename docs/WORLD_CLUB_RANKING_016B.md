# Ordenação global disponível

A classificação considera todo o catálogo salvo da categoria e temporada antes de filtrar os países escolhidos. Não usa só os clubes de uma sala. Persiste WorldRank, WorldRankingScore, RankingSource, RankingDate e RankingConfidence em projeção por clube/temporada, separada dos saves.

Ordenação para criação: ClubInitialPower decrescente; score detalhado decrescente; posição global crescente; identificador estável ordinal. O score não altera a força dos atletas.

Rankings de fontes existentes conservam sua proveniência. Na ausência de ranking esportivo comprovado, a ordem estável tem confiança zero. O adaptador pode produzir uma estimativa doméstica por pontos/(3 x jogos), confiança 0,2 e método explícito. Esse indicador não ajusta diferenças de dificuldade entre ligas; sua combinação é uma estimativa do jogo, sem alegação de ranking oficial mundial.

Rankings UEFA cobrem o universo das suas competições, não todos os clubes do mundo: [fonte oficial](https://www.uefa.com/nationalassociations/uefarankings/). Não foi importado nesta missão um ranking universal licenciado. O corte de elite com confiança baixa continua uma aproximação documentada.

Uma atualização muda apenas a projeção do catálogo. O WorldCreationOrigin já confirmado guarda seu ranking, data, clubes e atletas originais.
