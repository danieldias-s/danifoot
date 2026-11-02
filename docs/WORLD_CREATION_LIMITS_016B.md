# Limites e consentimento

Um mundo aceita 1..8 países distintos e exatamente 128 clubes: quatro divisões de oito e 96 distritais na Taça. O catálogo global pode conter mais de oito ou 64 países; o limite oito é da criação de um mundo. Há teste com 80 países catalogados.

REAL_COMPLETE precisa de 128 clubes elegíveis e elencos suficientes para os alvos 17..23. REAL_CLUBS_SIMULATED pode usar clubes reais com complemento fictício de atletas explicitamente autorizado. Sem clubes fictícios, continuam necessários 128 reais.

Com distritais fictícios autorizados, ao menos 32 reais preenchem as divisões; só as 96 vagas distritais podem ser completadas. Mundo inteiramente fictício exige FictionalOnly + AllowFictionalTeams + AllowFictionalPlayers. Não se completa silenciosamente uma seleção sem cobertura.

Montagem automática e personalizada preservam as validações anteriores. Opt-in UseWorldCatalogRules mantém consumidores e saves antigos no fluxo anterior; o criador principal usa as novas regras. Regras de formato incompatíveis retornam erros antes da confirmação. Apenas CLASSIC_SYNC está implementado; outros modos são recusados.

Confirmar o mesmo draft de novo recupera o mesmo mundo. O snapshot de origem e os IDs de atletas não são recalculados ao reconectar ou atualizar o catálogo.
