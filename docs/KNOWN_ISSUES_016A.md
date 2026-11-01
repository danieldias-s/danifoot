# Pendências e limites — 016A

1. **API-Football não conectada**, conforme a decisão de continuar sem chave. IDs/ligas dessa API ainda precisam de descoberta e revisão. Plano, cobertura da temporada e quota real não foram verificados. Importação autenticada real não foi declarada aprovada.
2. **Elencos atuais INCOMPLETE** nos seis clubes. Cinco consultas Wikidata chegaram ao teto de 500 linhas; não foram promovidas a elencos completos. O feminino tem 113 históricos normalizados e 13 IDs com posição/nome pendentes. Mais cobertura exige consulta adicional delimitada e evidência atual suficiente, ou fornecedor autorizado.
3. **Publicação/licenças da API-Football não autorizadas**. Flags de armazenamento, uso em jogo e redistribuição exigem evidência própria; permanecem desabilitadas. Sem fotos/logos novos, scraping, merge, push ou produção.
4. **Versões históricas de DEV** geradas durante a descoberta do defeito de precisão foram preservadas. Há 113 jogadores distintos e 113 na versão ativa, mesmo com 226 linhas históricas de snapshots. O replay corrigido não aumenta esses números.
5. **Auditoria antiga do piloto** registrou a contagem do objeto raiz em vez das linhas SPARQL. O código foi corrigido e testado; os logs antigos não foram reescritos. Payload/cache e SQL sustentam as contagens publicadas.
6. **Limite da evidência visual**: navegador Chromium embutido em desktop e viewport mobile, não aparelho físico. A auditoria desta missão não aprova retroativamente a 015B. A base exigida 456cd40 foi respeitada; melhorias posteriores da 015B não foram mescladas.
7. Saves pré-existentes não são atualizados pelo catálogo. A importação preserva snapshots de clube/jogador e paletas existentes. Revisões entre fontes diferentes continuam exigindo vínculo de identidade confiável.

Para retomar importação API-Football: configurar a chave somente no servidor, documentar os direitos realmente disponíveis, reiniciar apenas a API 016A, consultar conta, revisar IDs/ligas/temporadas, executar DRY RUN e então APPLY em DEV. Não inserir chave em Flutter, Git, relatório ou chat.

O título da aba ainda herda a identificação visual 015B da base exigida. A build e API auditadas são da 016A: portas 5172/5171, health próprio e hash da build registrados na evidência. Isso não representa execução do worktree 015B.
