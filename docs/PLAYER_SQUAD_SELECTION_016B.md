# Seleção inicial de jogadores

O ciclo segue a ordem final dos clubes: 23,23,22,22,21,21,20,20,19,19,18,18,17,17; depois reinicia. Elite configurável, padrão posições globais 1..20, exige ranking com confiança positiva e recebe 23. A exceção não altera a posição ordinal dos demais.

Atletas são deduplicados por ID canônico ou fonte/ID. Rating, minutos e aparições são convertidos em percentis dentro da própria posição. Pesos: 50%, 35%, 15%. Ausência de estatística é neutralidade 0,5, não zero inventado. Empates são estáveis por identidade. Isso seleciona o elenco; a calibração de força aprovada continua a mesma.

Para alvos 17..23, a seleção prioriza 2 goleiros, 4 defensores, 4 meios e 2 atacantes quando há disponibilidade; completa pelo restante da classificação. A validação final exige ao menos 11, 1 goleiro e 10 de linha e o alvo solicitado. A rotina de geração antiga ganhou parâmetro opcional de alvo, mantendo padrão 18 para consumidores anteriores. IDs dos complementos permanecem únicos inclusive além do 18º.

Sem autorização, faltas impedem confirmar. Com autorização, somente jogadores marcados FICTIONAL_GENERATED complementam o elenco. O limite 24 durante a carreira e as regras de evolução salarial não foram modificados.

Prévia masculina: 2.610 atletas em 128 clubes; contagens por tamanho: 17/18/19/20/21 = 16 clubes cada; 22 = 14; 23 = 34. Prévia feminina: 2.566 atletas, 1 vínculo real previamente verificado e 2.565 gerados; 17..22 = 18 clubes por tamanho, 23 = 20. A falta de estatísticas individuais reais permanece explícita.
