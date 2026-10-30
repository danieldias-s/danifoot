# Limites conhecidos — 013B

STATUS GLOBAL: PARTIAL. Existe integração real funcional e persistida, mas não cobertura suficiente para o objetivo completo de mundos reais em oito países.

1. Carga real limitada à Espanha: um clube masculino/35 atletas utilizáveis e um feminino/seis. Os outros sete países não foram carregados. Faltam 127 clubes por categoria. Não foi confirmado um mundo real de 128 clubes.
2. Wikidata é comunitário: vínculos/posições podem estar desatualizados e não garantem o elenco oficial. Registros sem nome/posição foram reportados e descartados, sem invenção de atletas. O feminino está abaixo do mínimo e sem goleiro.
3. Não há ranking mundial real importado. O piloto usa ordem provisória por identificador, confiança zero. Elo derivado foi implementado/testado, mas não carregado com resultados reais. Nenhum dado Opta foi extraído.
4. API-Football, Sportmonks e football-data.org têm adapters nativos e mocks aprovados; faltam credenciais, licença compatível e homologação real. A documentação Sportmonks diverge sobre autenticação header/query; confirmar o modo suportado antes da ativação. Quotas baixas podem exigir cargas controladas menores/configuração de intervalo, nunca contorno de limites.
5. Força inicial é uma estimativa do jogo. No piloto faltam indicadores individuais e a confiança é baixa. Avaliar calibração numa base real representativa antes de lançamento.
6. Clubes de fornecedores diferentes exigem mapeamento revisado. Atletas sem correspondência canônica comprovada entre fornecedores precisam de revisão; não há fusão por nome. Vínculos de transferência ambíguos são rejeitados.
7. Bancos 013B são novos. Não foi entregue migração dos bancos persistidos 013A; eles permanecem intactos. Usar os runners 013B nesta branch. Política operacional de arquivamento/expurgo de snapshots/jobs/drafts continua pendente, respeitando os direitos de retenção.
8. Não houve ensaio em telefone físico, build nativo Windows/Android nem repetição dos benchmarks de 100 temporadas. Flutter foi testado por viewport e compilado para Web.
9. LocalDev continua sem homologação de identidade de produção. Integração final com a economia 012D depende do estado aprovado daquela missão. Nada foi publicado ou mesclado.

Esses limites não foram ocultados com fixtures no catálogo real, jogadores gerados sem consentimento ou ranking estimado apresentado como oficial. O relatório distingue infraestrutura, integração live, carga efetiva, testes sintéticos e pendências.
