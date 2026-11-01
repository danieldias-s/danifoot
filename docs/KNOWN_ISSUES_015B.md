# Pendências e limites — 015B

STATUS: PARTIAL. A implementação e as regressões foram concluídas, mas a auditoria visual real final está incompleta.

Bloqueio concreto: a ferramenta de navegador retornou “Local browser use is disabled in Work settings” após a primeira inspeção desktop. O proprietário informou que habilitaria o navegador; as tentativas seguintes continuaram impedidas. Nenhum contorno por outra automação foi utilizado.

Pendências obrigatórias: recarregar a build final identificada por browser-build-health.json; capturar rankings, mercado/leilão, compra direta, janela de confirmação, partida e mobile; verificar visualmente 360/390/768/1024/1440/1920 px e registrar diferenças. Revalidar o fechamento real do drawer no início automático, o desbloqueio final e o estado vazio Meu Clube. Quatro capturas desktop anteriores foram preservadas e identificadas como intermediárias, sem atribuí-las à build final.

Limites de desenho e evidência:

- A referência é uma ilustração de várias telas. A aplicação preserva funcionalidades adicionais e dados variáveis, portanto não reproduz cada pixel nem estatísticas da imagem.
- Não são utilizados os escudos e fotos da referência. O desenho usa ícones licenciados já disponíveis e elementos próprios.
- Em larguras menores, tabelas extensas precisam de rolagem horizontal; todos os atributos e ações continuam acessíveis.
- O calendário ainda apresenta alguns identificadores de competição do backend em inglês. Os dados e fases são os reais; não houve alteração esportiva.
- A auditoria mobile em navegador deve usar viewport responsivo. A execução final segue pendente; testes determinísticos e renderizações Flutter não são apresentados como validação em aparelho físico.
- O ambiente de revisão requer os dados DEV preparados neste computador; o runner não é um instalador de produção.

O teste manual do proprietário permanece pendente da sua confirmação. Não houve merge, push nem publicação em produção.

## Teste manual posterior

O desenho da formação e o ajuste dos setores foram corrigidos após a captura do proprietário. A espera 1/3 era causada pela sala de auditoria com três identidades. Há uma sala individual nova ELIC18AA38C para o proprietário, preservando o save anterior. Validações e limites constam em PLAYTEST_FORMATION_SOLO_FIX_015B.md.

## Revisão mobile posterior

O layout pedido na imagem está implementado com barra de moral preservada. A suíte passou 180/180, e as prévias Flutter de respostas GET reais foram inspecionadas. As imagens novas em evidence-015b-mobile/automated-renders têm sua origem explicitamente identificada; não encerram a pendência de captura Chrome. MOBILE_REFERENCE_LAYOUT_015B.md documenta comportamentos, reprodução e acesso ao teste individual. O save foi consultado em R5, sem reinício.

## Nova tela ao vivo

A implementação e a regressão de 196 testes estão concluídas. O limite da auditoria Chrome permanece. Ver LIVE_MATCH_SCREEN_015B.md para dados não fornecidos pela API (cidade, posse/finalizações/escanteios/faltas, escudos oficiais), bilheteria apurada no encerramento, dados limitados do calendário e preservação do editor antes do save. Há 12 prévias Flutter identificadas; não se declara que são capturas reais de navegador.
