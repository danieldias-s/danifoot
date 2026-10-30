# Limites conhecidos — 012C

STATUS: PASS dos requisitos implementados/testados. Sem falha funcional conhecida pendente na suíte final.

1. É uma adaptação autorizada. As missões 012/012B continuam válidas como pesquisa/caracterização, com seus limites de evidência histórica. Não se obteve a fórmula original exata.
2. O calendário é uma convenção nova determinística: 4/3 dias, 21 de transição e adiamento global por elenco mínimo lesionado. Não reproduz um calendário civil anual completo. Houve 82 adiamentos em 100 temporadas. A política preserva sincronismo, confrontos e IA de mercado, mas deve ser observada em jogos humanos com elencos muito curtos. Suspensões e regras emergenciais legadas não foram redesenhadas.
3. A distribuição perde oferta de atletas fortes a longo prazo (5 força ≥40 no fim do ensaio). Jogadores usados frequentemente tendem a manter/perder; distritais só disputam Taça e não têm recuperação de força por liga. Esses efeitos são apresentados no relatório de balanceamento. A regra permite crescimento até 50, confirmado em teste controlado, mas a IA não prioriza descanso para desenvolver atletas fortes. Nenhum teto/peso foi alterado.
4. Uma seed com todos IA e oito probes força 1 não cobre todas as estratégias humanas ou seeds. Recomenda-se ensaio adicional de elenco voltado à reserva e monitoramento da oferta por divisão antes de eventual ajuste autorizado da curva de ganho/perda. Não há justificativa para mudar os pesos de duração aprovados.
5. Histórico, episódios, chaves de partidas e snapshots são retidos para garantir auditoria/idempotência e crescem com temporadas. Não se implementou expurgo/paginação nova; avaliar arquivamento compatível antes de mundos de duração extrema. A política de retenção da missão 012 permanece.
6. Saves antigos e importação de pacotes preservam o modo legado; não existe migração automática nem botão para converter uma sala em andamento. Novas salas pela API usam ClassicAdapted por padrão e podem solicitar LegacySeasonal. Versão desconhecida é recusada para impedir fórmula silenciosamente diferente.
7. A UI mostra dias, data virtual, teto e motivos; a autoridade continua no servidor. Os testes multiplayer incluem requisições concorrentes, snapshots e 100 rodadas/8 técnicos. Não foi feito novo ensaio manual com oito dispositivos físicos nem publicação em produção.

NEXT_RECOMMENDATIONS: acompanhar equilíbrio em estratégia humana, avaliar retenção do histórico e só então planejar migração explícita de saves. Nada foi publicado e nenhum outro projeto foi modificado.
