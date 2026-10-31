# Limitações conhecidas — 013C

- Cobertura de dados reais continua PARTIAL: nenhum elenco atual completo. O modo A bloqueia a criação com a carga disponível. A aprovação funcional da 013C não transforma elenco simulado em real.
- As 232 identidades femininas verificadas não possuem participação atual certificada na carga. Essa limitação é exibida separadamente. O único vínculo atual utilizável entra no mundo feminino ensaiado; os demais registros ficam como referências.
- A seleção de oito países permite escolher clubes desses países; não garante uma cota por país. No masculino, não há argentinos elegíveis para a temporada atual no recorte preservado. O mundo ensaiado usa sete dos oito países selecionados.
- Não há ranking oficial/Opta licenciado. O masculino utiliza estimativa local com grafos domésticos desconectados e confiança limitada a 0,35. O feminino usa desempate técnico determinístico por seed, com confiança zero. Nenhum ranking masculino é atribuído ao feminino.
- Geradores e estimativas são regras de jogo. Não representam habilidades individuais reais nem garantem equilíbrio financeiro para toda configuração. A regressão de 100 temporadas utiliza os protocolos herdados 012C/012D/014A, incluindo mundos de fixtures; os mundos reais 013C foram exercitados por três rodadas e reinício, não por 100 temporadas.
- A proveniência adicionada cobre os atletas iniciais e é mantida em seus eventos posteriores. Atletas novos criados pelo gerador legado durante temporadas futuras conservam o tratamento legado; não se atribui a eles identidade real de catálogo.
- Testes sintéticos de contrato não contam como cobertura real. A aceitação com o bundle real é registrada separadamente em audit-013c/actual-worlds.json.
- Os runners são locais, específicos de D:\elifoot_98_integration e das portas próprias 55436/5143. Autenticação LocalDev é configuração local, sem homologação de produção. O relógio normal é padrão; -ManualClock é exclusivo da aceitação controlada.
- Licenças e evidências de origem foram preservadas sem nova coleta nesta missão. Não há autorização adicional para mídia ou dados proprietários; uma publicação pública exige respeitar as permissões efetivas de cada snapshot.

Não houve push, publicação em produção, merge no principal ou alteração dos worktrees originais. A base integrada d838cc5 foi escolhida explicitamente pelo usuário, substituindo a instrução de base desatualizada do anexo.
