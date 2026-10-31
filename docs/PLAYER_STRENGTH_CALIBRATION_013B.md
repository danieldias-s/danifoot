# Calibração inicial 1–50 — 013B

`InitialPlayerStrength`, versão relative-v1, produz estimativas do jogo, nunca uma nota individual oficial. Não existe multiplicador por gênero. A posição, o universo de dados comparáveis e os indicadores disponíveis determinam os grupos de comparação. Alterar a divisão manual do clube não altera sua força inicial.

Com ao menos três notas individuais comparáveis na posição, calcula-se percentil com tratamento de empates. A estimativa usa 12 + 37×percentil, ajuste de clube limitado a aproximadamente um ponto e ruído determinístico de até um ponto. Sem notas, minutos comparáveis podem servir como proxy, com confiança menor. O valor é arredondado e limitado a 1–50.

Sem dados individuais, a regra usa base 26, contexto relativo de clube limitado e variação determinística de ±7 por identidade. A confiança é 0,2. Não se atribui a todos os atletas a nota do clube. No piloto Wikidata não há estatísticas individuais suficientes: todas as forças iniciais são estimadas com esse aviso, e a UI só apresenta os valores finais após gerar a prévia.

O ruído depende de SHA-256 da identidade e seed. A estimativa registra Method, Origin, Confidence, InputHash, IndividualPercentile e ClubPercentile. O mesmo snapshot/seleção e seed reproduz os valores. As forças ficam congeladas no snapshot inicial; posteriormente somente as regras de evolução existentes atuam.

Os testes isolam gênero mantendo os mesmos IDs/desempenho/seed e obtêm valores iguais; verificam faixa, dispersão, valores altos, ausência de força 50 generalizada, troca manual de divisão e reprodução. Nenhum teto, peso, lesão, disciplina ou algoritmo de partida da 012C foi alterado. A calibração relativa ainda requer avaliação com uma base representativa real antes de lançamento.
