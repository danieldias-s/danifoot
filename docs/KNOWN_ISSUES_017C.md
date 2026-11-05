# Limites conhecidos — 017C

- A aprovação visual definitiva da história na aparência clássica depende
  das referências prometidas pelo proprietário. A funcionalidade está integrada.
- Saves antigos só podem recuperar o que preservaram. Partidas/valores/forças,
  motivos e momentos antigos ausentes permanecem parciais ou não registrados.
  Não há reconstrução fictícia de anos anteriores nem de carreira real externa.
- Não existe empréstimo de jogadores na base. Esse cenário é não aplicável;
  implementar negociação nova contrariaria a missão. O banco de empréstimos
  financeiros permanece independente.
- Não há novas métricas, notas ou prêmios de goleiros/defensores. Os rankings
  e as regras de elegibilidade anteriores continuam vigentes.
- O estado completo de 100 temporadas tem aproximadamente 625 MB. A consulta
  redundante do JSON no save foi corrigida e a recuperação/regravação passou,
  mas a arquitetura de snapshot acumulado ainda exige memória e tempo. A
  prévia usa timeout SQL de 180 segundos; uma normalização geral fica fora da
  missão. Na reconexão via navegador, a carga e os salvamentos desse mundo
  levaram minutos. A sala curta de QA foi usada para verificar os fluxos de UI.
- O teste de celular usa Chrome em viewport de 390×1000. Não equivale a uma
  execução em hardware Android/iOS nativo.
- Nenhum merge, push ou publicação em produção foi realizado.
