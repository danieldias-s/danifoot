# Cache, fallback e isolamento — 013B

A 013B reutiliza CatalogDbContext/CatalogSyncService, snapshots imutáveis e ativação transacional da 013A. Não há sistema paralelo de cache. O banco real usado é `elifoot_013b_catalog`; mundos e testes usam bancos diferentes. PostgreSQL temporário próprio em 127.0.0.1:55434, dados em artifacts/013b/pgdata. A instância/bancos 013A e os bancos da 012D não foram acessados.

Os adapters compartilham cache curto de respostas válidas por fornecedor e respeitam intervalo mínimo, orçamento por carga, máximo de páginas e headers de quota/Retry-After. Respostas com erro não se tornam dados válidos. O sinal maxlag do Wikidata causa espera/fallback; o limite solicitado não foi elevado para contornar a carga da fonte.

Timeout: fontes legadas mantêm a configuração 013A; carga nativa padrão 90 segundos, configurável de 5–120; Wikidata 45. Há no máximo um retry. Lease compartilhado de dez minutos cobre a execução limitada. Jobs, datas e versões continuam persistidos por país/categoria/liga/temporada/tipo/fornecedor. Erros de autenticação/licença são distintos dos demais erros de rede. Não há token nos logs.

O usuário pode usar imediatamente os dados salvos. Direitos expirados/incompatíveis impedem uso, mesmo se o snapshot existir. Fontes com retenção limitada são recusadas pelo catálogo permanente. Uma falha de país não apaga os demais; uma atualização parcial não destrói um elenco completo.

Foram feitos backups pg_dump do catálogo 013B antes e depois da carga real em artifacts/013b/backups. O piloto foi consultado pela API reiniciada sem fontes externas configuradas, com os mesmos quatro snapshots. Nenhum serviço permanente foi instalado. Os scripts Start/Stop controlam apenas a instância da 013B.
