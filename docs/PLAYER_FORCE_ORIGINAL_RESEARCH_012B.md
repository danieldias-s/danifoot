# Pesquisa original de força — 012B

30/10/2026, America/Sao_Paulo. **STATUS: PARTIAL. Nenhuma nova regra histórica foi comprovada por execução do original.** A 012 foi preservada; foram acrescentados pesquisa reproduzível, testes e plano de observação. O status não significa bloqueio dos testes do projeto.

## Preservação

Os sete relatórios 012 foram lidos integralmente. O Git continua em `0c7fc1b430ea2fdc5f474c5a794409922cbcdbe3`, checkpoint 011, sem remotos. **Não existia commit 012**: a implementação anterior estava no diretório de trabalho. Não se inventou um SHA para ela nem se fez reset/checkout.

A situação 012 foi preservada em [manifesto](audit-012b/baseline/checkpoint-012-manifest.json), [patch](audit-012b/baseline/checkpoint-012.patch) e cópias dos arquivos alterados/novos em `audit-012b/baseline/files`. Os artefatos volumosos 012 permanecem nos locais originais, com hashes no manifesto. Todo código de produção tem [manifesto próprio](audit-012b/baseline/production-manifest.json). O verificador final exige igualdade desses hashes, inclusive dos relatórios anteriores.

## O avanço da investigação

A API pública do GitHub permitiu fixar o repositório indicado no commit **`2f7ff68f50bf3af8a57d0baf5a4938f65abf0f03`**, de 22/08/2026, 07:48:17 em São Paulo. Foram examinados a árvore completa, os **oito commits**, todos os **três forks** expostos e a lista completa de issues/PRs, vazia na consulta. Não houve paginação pendente ou árvore truncada.

Os seis arquivos de texto foram lidos em diretório temporário ignorado pelo Git (`.tools/research-012b/upstream`). Seus SHA-256 e hashes Git de blob foram comparados à árvore fixada, sem copiar seu conteúdo para a implementação. O arquivo de evolução foi acrescentado no commit `56b82ad5bf050783cbb99e7010b7005e4a92f02e`; os commits posteriores não o completaram. Os três forks têm **o mesmo blob de evolução**, `3a5ffcdb21c83940c69fa6bfbe554296523dd325`, não três validações independentes.

Proveniência, linhas relevantes, caminhos e histórico: [provenance.json](audit-012b/research/provenance.json). Coletor: `scripts/research-player-force-012b.mjs`. Não se abriu issue, enviou mensagem ao pesquisador ou publicou código.

## Fontes primárias e alcance

| Fonte | O que permite verificar | O que não fornece |
| --- | --- | --- |
| [README fixado](https://github.com/trsthales/desvendando-elifoot-98/blob/2f7ff68f50bf3af8a57d0baf5a4938f65abf0f03/README.md#L129) | Descrição atribuída a FUN_17fb_4a41 e referência ao avaliador | Implementação de AvaliarRendimento, seus estados e origem da base |
| [Arquivo técnico fixado](https://github.com/trsthales/desvendando-elifoot-98/blob/2f7ff68f50bf3af8a57d0baf5a4938f65abf0f03/src/player_evolution.c#L37) | Condições sobre avaliação recebida e limites | Quem calcula/preenche desempenhoPartida e quem chama a função |
| [Roadmap fixado](https://github.com/trsthales/desvendando-elifoot-98/blob/2f7ff68f50bf3af8a57d0baf5a4938f65abf0f03/ROADMAP.md#L34) | Evolução ainda prevista para aprofundamento | Protocolo ou medições completas dessa evolução |
| [Publicação que aponta para o mesmo repositório](https://www.reddit.com/r/linuxbrasil/comments/1vublc9/fiz_engenharia_reversa_do_elifoot_98_com_ghidra/) | O autor da publicação reconhece que a avaliação individual não foi totalmente mapeada | Confirmação independente ou novo algoritmo |
| [Relato de laboratório](https://www.tabnews.com.br/trsthales/cheat-engine-nativo-no-linux-como-inspecionei-a-memoria-do-elifoot-98-no-debian-12) | Relata escrita de força de titular ao avançar rodada, vinculada à função estudada | Hash do executável e ensaio reproduzível cobrindo banco/lesões |
| [Experiência histórica de Eidy Tasaka](https://jogoveio.com.br/elifoot-98-pc/) | Descreve desenvolvimento de atletas baratos e perdas grandes por contusão | Coeficientes, duração, sorteio ou permanência da perda |
| [Esporte Clube 8-Bit, experiência histórica](https://ec8bit.wordpress.com/page/2/) | Relata desenvolver jogadores baratos antes de utilizá-los | Comparação controlada de reservas e escalação |

Essas fontes foram acessadas em 30/10/2026. As postagens do pesquisador e forks não são amostras independentes do original. Os arquivos são pseudocódigo/modelos simplificados, inclusive com chamadas à biblioteca C para sorteio; não se confundem com exportação integral do código nativo. Não foram transcritos para produção.

## Respostas às perguntas técnicas

| Pergunta | Resultado da inspeção | Classe |
| --- | --- | --- |
| AvaliarRendimento | Apenas chamada no README, sem definição nos seis textos, histórico posterior ou forks examinados | ORIGINAL_PARTIALLY_SUPPORTED; algoritmo desconhecido |
| NivelBaseTime | Parâmetro recebido; não há cálculo nem ligação publicada à divisão/média do elenco | ORIGINAL_PARTIALLY_SUPPORTED; origem desconhecida |
| Probabilidade de ganho | Condicional a avaliação positiva; não existe frequência publicada dessa avaliação | ORIGINAL_PARTIALLY_SUPPORTED |
| Probabilidade de queda | Há teste condicional de tolerância; não revela frequência de avaliações negativas | ORIGINAL_PARTIALLY_SUPPORTED |
| Titulares | O relato dinâmico observado pelo autor usa um titular. A seleção de titulares em cálculo de setores é outra função | ORIGINAL_PARTIALLY_SUPPORTED |
| Banco | Percorrer slots do elenco não demonstra avaliação positiva para todos. Relato histórico sugere crescimento sem utilização, sem quantificação | ORIGINAL_PARTIALLY_SUPPORTED |
| Goleiro | Publica guarda adicional de 50% para ganho elegível. Não demonstra evolução efetiva duas vezes mais lenta em todas as circunstâncias | ORIGINAL_PARTIALLY_SUPPORTED |
| Base e limites | As duas descrições públicas discordam justamente na fronteira; análise reproduzível abaixo | ORIGINAL_PARTIALLY_SUPPORTED e contraditório |
| Divisão/classificação | Nenhuma transformação numérica publicada dessas variáveis para avaliação/base | Não confirmado; qualquer fórmula nova seria GAME_DESIGN_PROPOSAL |
| Lesão/entrada | Não há rotina quantificada dessas perdas nos arquivos examinados | Não confirmado; alternativas em relatório próprio |

**ORIGINAL_VERIFIED:** nenhum novo algoritmo completo de evolução/lesão nesta missão. **GAME_DESIGN_PROPOSAL:** não ativada. O comportamento local já aprovado continua identificado como política do projeto, não como prova do original.

### Probabilidades: o que a documentação não autoriza deduzir

Uma chance adicional condicionada a uma avaliação não equivale a chance de evolução por rodada. É necessário conhecer a distribuição da avaliação, elegibilidade e teste de limite. Só com avaliação positiva e limite elegível seria possível aplicar a guarda descrita para goleiros. A frase de comentário associando desempenho a vitória/destaque não é implementação do avaliador. Não autoriza +1 para todo vencedor, nem −1 para todo derrotado. Também não demonstra que banco e titulares tenham a mesma distribuição de avaliações.

### Divergência formal dos limites

O ensaio compara predicados escritos independentemente para as duas descrições públicas, com força/base sintéticas 1–50, três avaliações, duas posições e dois resultados de sorteio: **30.000 combinações, 220 contraexemplos**. Não executa código proprietário, não escolhe qual descrição está certa e não inventa o avaliador.

Exemplos com base 20: avaliação positiva em força 25 produz 26 no modelo do README e 25 no modelo do arquivo C; avaliação negativa aceita pelo sorteio em força 15 produz 14 versus 15. O conflito permanece mesmo sem discutir a origem da base. [Contraexemplos completos](audit-012b/regression/force-012b/published-model-counterexamples.json).

## Executável original no ambiente

A busca limitada abrangeu o projeto recursivamente, nomes contendo Elifoot no topo de Downloads/Desktop/Documents/pastas usuais de instalação e registros de desinstalação. Só surgiu um executável do projeto moderno, `artifacts/player-rankings-009/api/Elifoot.Api.exe`, com cabeçalho PE, e um prompt Markdown em Downloads. Não apareceu cópia original cuja versão/licença pudesse ser verificada. Isso **não prova ausência em todo o computador**, pois não se vasculharam todos os arquivos pessoais/discos.

[Inventário e limites](audit-012b/research/local-original-inventory.json): **zero downloads de binários e zero execuções do original**. Não se utilizaram remakes, páginas VNC, geradores de registro ou cópias sem autorização como substitutos da observação do original.

## Decisão técnica

Nenhuma mudança em evolução, lesão, fórmula defensiva, IA, mercado, persistência ou multiplayer. A pesquisa disponível continua insuficiente para preencher os pontos ausentes com fidelidade verificável. As combinações exigidas foram executadas no clone e preparadas para comparação futura, com campos do original vazios, em [testes diferenciais](PLAYER_FORCE_DIFFERENTIAL_TESTS_012B.md). O [mapa de lacunas](PLAYER_FORCE_GAPS_012B.md) define exatamente a evidência necessária para encerrar cada pendência.
