# Pendências e limites — 012D

STATUS: PARTIAL.

1. **Equilíbrio estratégico de longo prazo:** caixa agregado de R$ 18.107.128.451,84 em T100, contra abertura de R$ 138.800.000,00. O crédito funciona e foi reconciliado, mas perdeu relevância depois dos primeiros anos. A assimetria entre patrocínio por rodada e folha por partida já existia e foi preservada. Uma recalibração coordenada dessas receitas/despesas ainda precisa de experimentos próprios; não se afirma economia equilibrada.
2. **História:** os valores exatos R$ 500.000/R$ 25.000 e a fórmula de limite não foram autenticados em executável original. São adaptação autorizada, não engenharia reversa comprovada.
3. **Saves antigos:** composição de juros/principal anterior à migração pode ser desconhecida. Saldo, taxa e prestação são conservados, sem reconversão ou perdão. Contratos vigentes antigos são protegidos conservadoramente.
4. **Cobertura natural:** o cenário de 100 temporadas teve 0 recusas e 0 leilões sem comprador. Esses caminhos foram exercitados nos testes controlados e concorrentes, sem inventar ocorrências no cenário natural.
5. **Crédito superior:** faixas candidatas 1–3/2–4/2–5 foram conservadas como configuração da adaptação. A execução não demonstrou colapso por crédito, mas o excesso de caixa impede afirmar calibração estratégica definitiva dos tetos.
6. **Cliente legado:** chaves e versões continuam opcionais nos endpoints anteriores por compatibilidade. A interface atual envia ambas; consumidores antigos precisam reutilizar uma chave para garantir repetição entre requisições independentes.
7. **Pesquisa e escopo:** nenhum executável não autorizado foi obtido. Não houve publicação em produção nem alteração de outro projeto. O banco de desenvolvimento não foi usado nos testes; persistência foi validada em bancos descartáveis.

Validação efetiva: 410 testes .NET em Release, 61 testes Flutter, análise Flutter limpa, 100 temporadas com partidas reais e reconciliação independente. Logs iniciais com falhas de expectativas antigas/dependências de QA foram mantidos; `verification.json` aponta os resultados finais.
