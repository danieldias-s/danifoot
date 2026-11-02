# Pendências e condições reais

1. API-Football sem chave: não se autenticou conta nem verificou plano, quotas remotas, cobertura atual, licenças ou execução mundial real. A implementação está preparada; o catálogo disponível nesta auditoria é salvo, não uma carga atual recém-validada.
2. Chrome detectado após instalação da extensão, mas navegação ainda bloqueada pela configuração efetiva de uso local. Capturas reais desktop/mobile pendentes. Não declarar aprovação visual ou manual.
3. Ligas sem categoria exigem revisão administrativa fundamentada. Evita misturar times masculinos e femininos, mas impede automatismo total sem mapeamentos verificados.
4. Ranking esportivo mundial universal não integrado. Há estimativa explícita e fallback estável com confiança zero. A comparação entre ligas e o corte de elite são aproximações do jogo, sem coeficiente mundial oficial.
5. Estatísticas reais individuais ausentes no catálogo local atual; o algoritmo foi testado com protocolo sintético por posição, com ausência neutra. A seleção real atual requer o piloto autenticado.
6. Atualização incremental por liga/clube/página e fila por país estão prontas; priorização refinada de elencos escolhidos/incompletos dentro da mesma fila e uma tela completa para edição administrativa de força continuam pendentes.
7. Cobertura de cores parcial. Somente fonte local já verificada foi utilizada, sem criar cores ou direitos sobre escudos.
8. Sincronização de salas reutiliza locks locais existentes. Várias instâncias de API em paralelo precisariam coordenação distribuída adicional; não é configuração desta DEV.
9. O banco de revisão contém cópias de saves anteriores e salas criadas pelas tentativas de auditoria. Elas foram preservadas, sem limpeza destrutiva. A sala manual final e sua identidade estão em TESTE_MANUAL_CATALOGO_016B.md.
10. Uma comparação byte a byte dos cinco saves originais encontrou um timestamp updatedAtUtc diferente. Auditoria estrutural confirmou que só esse timestamp mudou na sala antiga Revisão visual; rodada, versão e demais conteúdos permanecem iguais ao backup inicial.

Nenhum item acima foi mascarado como PASS. Não houve publicação, merge ou modificação de outro worktree.
