# Distribuição de clubes — 013A

O servidor monta quatro divisões de exatamente oito clubes e 96 distritais. Clubes elegíveis pertencem aos países, categoria e temporada escolhidos. Chaves externas são qualificadas por país/categoria/fornecedor. Duplicações na seleção e jogadores repetidos entre elencos impedem confirmação.

No automático, posição 1–8 vai para DIV1, 9–16 para DIV2, 17–24 para DIV3 e 25–32 para DIV4. Clubes sem ranking ficam ao final em ordem estável de chave, com aviso explícito. O ranking só define o início: resultados, promoções e rebaixamentos continuam a cargo dos motores existentes. Um clube do top 32 não pode ser reservado aos distritais no modo automático; use personalizado para essa escolha.

No personalizado, a atribuição manual prevalece. É possível pesquisar, colocar, remover, mover e trocar clubes. Uma divisão cheia oferece a escolha de qual clube trocar; o clube removido recebe a vaga anterior quando existente. PREENCHER VAGAS POR RANKING ocupa as vagas livres sem substituir escolhas manuais. A confirmação exige a configuração completa.

O modo manual pode colocar qualquer clube elegível em qualquer divisão, sem alterar seu ranking externo. O servidor é a autoridade mesmo quando a requisição é enviada fora do Flutter. O complemento fictício depende de `AllowFictionalTeams=true`, habilitado pelo checkbox explícito do jogador; a prévia identifica todas essas equipes e seus atletas como fictícios.
