# Pendências e limites — 015B

STATUS: PARTIAL. A implementação e as regressões foram concluídas, mas a auditoria visual real final está incompleta.

Bloqueio concreto: a ferramenta de navegador retornou “Local browser use is disabled in Work settings” após a primeira inspeção desktop. O proprietário informou que habilitaria o navegador; as tentativas seguintes continuaram impedidas. Nenhum contorno por outra automação foi utilizado.

Pendências obrigatórias: recarregar a build final identificada por browser-build-health.json; capturar rankings, mercado/leilão, compra direta, janela de confirmação, partida e mobile; verificar visualmente 360/390/768/1024/1440/1920 px e registrar diferenças. Revalidar o fechamento real do drawer no início automático, o desbloqueio final e o estado vazio Meu Clube. Quatro capturas desktop anteriores foram preservadas e identificadas como intermediárias, sem atribuí-las à build final.

Limites de desenho e evidência:

- A referência é uma ilustração de várias telas. A aplicação preserva funcionalidades adicionais e dados variáveis, portanto não reproduz cada pixel nem estatísticas da imagem.
- Não são utilizados os escudos e fotos da referência. O desenho usa ícones licenciados já disponíveis e elementos próprios.
- Em larguras menores, tabelas extensas precisam de rolagem horizontal; todos os atributos e ações continuam acessíveis.
- O calendário ainda apresenta alguns identificadores de competição do backend em inglês. Os dados e fases são os reais; não houve alteração esportiva.
- Mobile é validado por viewport responsivo no navegador real e por testes determinísticos, sem alegar validação em aparelho físico.
- O ambiente de revisão requer os dados DEV preparados neste computador; o runner não é um instalador de produção.

O teste manual do proprietário permanece pendente da sua confirmação. Não houve merge, push nem publicação em produção.
