# Cores da aparência clássica e das partidas

Entrega local em 04/11/2026. Branch isolada: `codex/classic-green-team-colors`.
Checkpoints de implementação: `36b770b` e `6064bca`. Sem merge ou publicação.

## Resultado

As telas clássicas usam painéis claros, títulos verdes, seleção em verde suave,
barra de janela azul e controles claros, conforme a referência fornecida.
Plantel, menus, táticas, reservas, mercado, estádio, classificação e rankings
compartilham essa apresentação. A versão atual mantém seu tema próprio.

| Elemento | Cor |
|---|---|
| Papel | `#F7F5E9` |
| Texto | `#102E1B` |
| Cabeçalhos | `#00542E` com texto branco |
| Seleção | `#B8DDA0` |
| Linhas alternadas | `#E4EED8` |
| Controles | `#E7E9E2` |

Nas partidas, o fundo de cada equipe usa sua cor principal cadastrada. O texto
usa a cor secundária. Quando necessário, somente a tinta renderizada recebe
ajuste para contraste mínimo de 4,5:1; o cadastro permanece intacto. Os placares
clássicos usam caixas pretas com números brancos. Fontes do placar e dos filtros
clássicos usam o arquivo já incluído no aplicativo, evitando nomes em branco
quando uma fonte externa não está disponível.

## Causa corrigida

Alguns clubes, inclusive fictícios e de saves antigos, possuem
`primaryColorHex` e `secondaryColorHex`, mas `colorPalette` nulo. A leitura
anterior descartava esses clubes e o placar não combinava os dados da partida
com as cores do cadastro. Agora essa combinação ocorre por ID do clube, tanto
nas partidas atuais quanto no calendário.

Paletas verificadas ou personalizadas continuam tendo prioridade. O fallback
dos campos antigos é apenas de apresentação e não inventa verificação de fonte.
Clubes sem cores válidas continuam neutros. Nenhuma cor foi pesquisada ou
deduzida pelo nome do clube.

## Validação

| Verificação | Evidência |
|---|---|
| Suite Flutter completa | 332 testes aprovados |
| Regressão das telas afetadas após revisão visual | 65 testes aprovados |
| Verificação final de partidas, fontes e contraste | 28 testes aprovados |
| Análise final | `No issues found` |
| Compilação web | Release concluído |
| Navegador | Capturas reais do cliente com API e PostgreSQL locais |
| Larguras | 360, 390, 430, 768, 1366 e 1920 px; documento sem excesso horizontal |
| Estado durante navegação visual final | Snapshot SHA-256 idêntico antes/depois; nenhuma escrita nas rotas do mundo |

Os testes incluem cores antigas sem mutação, precedência de paletas, clubes com
nomes iguais combinados por ID, contraste de texto, cabeçalhos compartilhados,
opção neutra e manutenção das telas atuais. A inspeção real usou a sala de QA
`979db9ee-0aed-47a0-974b-ebc22eb84d94`, em preparação, sem enviar táticas ou
avançar a rodada. Foram observados Atlético Paulistano (`#C8102E` / branco) e
Grêmio Guanabara (preto / `#CC0000`), ambos com paleta nula e cores antigas válidas.

A reconexão normal da sessão de QA aparece separada em `evidence.json`. O hash
final compara somente a navegação visual após a reconexão. A sala do proprietário
não foi acessada pelo treinador de teste. MatchEngine, economia, regras,
evolução, contratos e persistência do save não receberam alterações de código.

## Evidências e retomada

- `desktop-plantel.png`, `mobile-plantel.png`: aparência principal.
- `desktop-partidas.png`, `mobile-partidas.png`: cores individuais dos times.
- `desktop-classificacao.png`, `desktop-rankings.png`: tabelas e cabeçalhos.
- `evidence.json`: hashes, cores do cadastro e larguras reais.
- Logs nesta pasta: análise, testes completos, regressões e build final.

Prévia: <http://127.0.0.1:5232/#/plantel>. Atualize a página para carregar o build.
Se o computador desligar, execute `INICIAR-PREVIA.cmd` nesta pasta. O iniciador
reutiliza serviços abertos, aguarda a recuperação do PostgreSQL e verifica a
API e a página por HTTP. Usa o ambiente local preparado neste worktree; não
cria outro banco e não recria mundos.
