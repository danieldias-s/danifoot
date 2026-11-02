# API-Football e proveniência

O serviço 016B reutiliza ImportHttp016A, orçamento e cache persistentes. A chave é lida exclusivamente de APIFOOTBALL_API_KEY no processo do servidor. Não existe campo de chave do fornecedor na interface nem valor salvo no Git.

O piloto real NÃO foi autenticado: a variável estava ausente, por instrução anterior do usuário seguimos sem chave. Plano, saldo remoto, autorização comercial/armazenamento e cobertura de temporadas atuais não foram comprovados. As opções ApiFootballRights mantêm o bloqueio de acesso sem direitos configurados; não se deve habilitar permissões fictícias para passar o teste.

Endpoints usados pelo adaptador: countries; leagues por país/temporada; teams por liga/temporada; standings; players/squads por equipe; players com paginação para estatísticas. Identificadores vêm das respostas, nunca de palpites. A categoria da liga não é inferida do nome; descoberta sem categoria revisada gera REVIEW_REQUIRED. National teams são excluídas.

Cadastro de atleta exige identidade, clube, categoria, posição conhecida e temporada. Dados faltantes de posição não são preenchidos aleatoriamente. SQUADS exige temporada atual da liga; estatísticas só enriquecem IDs que continuam no elenco recebido, com clube/liga/temporada correspondentes. Elencos históricos não viram automaticamente elencos atuais.

As respostas de protocolo nos testes são sintéticas, com IsFixture=true e host fixture.invalid; não são evidência de cobertura ou autenticação externa. Os dois mundos locais reais usam exclusivamente fontes já salvas e jogadores fictícios explicitamente autorizados quando faltam elencos.

[Documentação oficial](https://www.api-football.com/documentation_v3), [planos](https://www.api-football.com/pricing), [quota e otimização](https://www.api-football.com/news/post/how-to-optimize-api-sports-calls-and-quota-usage), [termos](https://www.api-football.com/terms). O plano público gratuito anuncia 100 chamadas/dia; isso não comprova o plano desta conta. Direitos sobre logos também não são presumidos.
