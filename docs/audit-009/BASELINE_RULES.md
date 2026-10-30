# Baseline 009 — antes da implementação

30/10/2026 (America/Sao_Paulo). A 008 foi conferida: 18 arquivos de código/testes do manifesto final sem divergência. Foram preservados 58 arquivos em baseline/ com SHA256.

- A consulta antiga usa SeasonGoals + CupGoals; o motor incrementa SeasonGoals para todo gol e CupGoals adicionalmente para a Taça: a consulta e o encerramento podem contar a Taça duas vezes.
- SeasonMatches/CareerMatches são incrementados no início e nas substituições, antes do encerramento. Não existem agregados históricos por competição/divisão, goleiro ou disciplina.
- Nova temporada zera contadores sazonais. Fixtures anteriores a duas temporadas são removidos. Transferência mantém PlayerId, mas a consulta antiga usa clube/divisão atuais.
- Eventos contêm minuto, tipo, clube, autor/segundo jogador. Substituição usa Primary=entrada e Secondary=saída. Segundo amarelo gera SecondYellowRedCard (um amarelo e um vermelho), além do primeiro amarelo anterior. Shootout é tipo distinto de Goal.
- Estado final guarda ativos, substituídos, expulsos e banco, sem snapshot de titulares/posições iniciais. A ordem dos eventos permite reconstruir entradas/saídas e goleiro; adicionar captura sem alterar RNG/decisões esportivas.
- Sem títulos de artilharia nem prêmio de 3 milhões por vencedor. Histórico antigo guarda um único nome, com desempate por força.
- EF usa snapshot JSON e projeções de livro-caixa/histórico. Preservar motor defensivo, mercado, estádio e carreira008.

Plano: captura imutável do elenco inicial e posição; consolidação após fim, agregados por jogador/temporada/competição/divisão/clube e guardas persistentes. Legados recuperam somente fatos demonstráveis e expõem lacunas. Sem inventar nem pagar premiações retroativas.
