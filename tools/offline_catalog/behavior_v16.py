"""Experimental offline proposals. Never writes catalogs, worlds or saves."""
import argparse
from bisect import bisect_left, bisect_right
from collections import defaultdict
import math
from behavior_classifier import encoded, load_json, safe_path

VERSION = 'discipline-gameplay-1.6-experimental'
DEFAULT_CONFIG = {
    'version': VERSION, 'minimumMinutes': 900,
    'weights': {'fouls': 1, 'yellowCards': 2, 'redCards': 5},
    'targets': {'D1_D2': [20,20,25,20,10,5], 'D3_D4': [15,15,20,25,15,10], 'DISTRICT': [10,10,15,30,20,15]},
}


def classify(rows, config=None):
    config = config or DEFAULT_CONFIG
    if config.get('version') != VERSION or set(config) != set(DEFAULT_CONFIG):
        raise ValueError('Versão/configuração experimental desconhecida.')
    def number(value):
        return type(value) in (int,float) and math.isfinite(value) and value >= 0
    if type(config['minimumMinutes']) is not int or config['minimumMinutes'] < 900:
        raise ValueError('A classificação automática exige pelo menos 900 minutos.')
    if set(config['weights']) != {'fouls','yellowCards','redCards'} or not all(number(v) for v in config['weights'].values()):
        raise ValueError('Pesos devem ser finitos e não negativos.')
    if set(config['targets']) != {'D1_D2','D3_D4','DISTRICT'} or any(
        len(target)!=6 or not all(number(v) for v in target) or not math.isclose(sum(target),100)
        for target in config['targets'].values()):
        raise ValueError('Metas agregadas precisam de seis percentuais somando cem.')
    if not isinstance(rows,list) or len(rows)>100000:
        raise ValueError('Informe até 100000 amostras.')
    result=[]; cohorts=defaultdict(list); identities=set()
    for row in rows:
        identity=row.get('playerCatalogId')
        if not isinstance(identity,str) or not identity.strip() or identity in identities:
            raise ValueError('Identidades devem ser únicas e não vazias.')
        identities.add(identity)
        if row.get('position') not in ['G','D','M','A'] or type(row.get('initialDivision')) is not int or row['initialDivision'] not in range(1,6):
            raise ValueError('Posição ou divisão inicial inválida.')
        if not isinstance(row.get('comparisonGroup'),str) or not row['comparisonGroup'].strip():
            raise ValueError('Grupo de pesquisa comparável e documentado obrigatório.')
        base=row.get('baseBehavior')
        if base is not None and (type(base) is not int or base not in range(6)):
            raise ValueError('Valor-base inválido.')
        metrics={key:row.get(key) for key in ['minutes','fouls','yellowCards','redCards']}
        if any(value is not None and not number(value) for value in metrics.values()):
            raise ValueError('Estatísticas devem ser números finitos não negativos ou nulos.')
        bracket='D1_D2' if row['initialDivision']<=2 else 'D3_D4' if row['initialDivision']<=4 else 'DISTRICT'
        entry={'playerCatalogId':identity,'position':row['position'],'initialDivision':row['initialDivision'],
               'comparisonGroup':row['comparisonGroup'],'baseBehavior':base,'historicalStatistics':metrics,
               'index':None,'percentile':None,'effectiveBehaviorProposal':None,'status':'REVISAO',
               'classification':'GAMEPLAY_ESTIMATE','reason':'Dados ausentes ou menos de 900 minutos.'}
        if all(value is not None for value in metrics.values()) and metrics['minutes']>=config['minimumMinutes']:
            entry['index']=sum(metrics[key]*90/metrics['minutes']*weight for key,weight in config['weights'].items())
            cohorts[(row['comparisonGroup'],bracket,row['position'])].append(entry)
        result.append(entry)
    for (_,bracket,_), group in cohorts.items():
        indexes=sorted(entry['index'] for entry in group)
        thresholds=[]; total=0
        for target in config['targets'][bracket]:
            total+=target; thresholds.append(total/100)
        for entry in group:
            # Equal indexes retain equal outcomes; do not force quotas by arbitrary ID order.
            percentile=(bisect_left(indexes,entry['index'])+bisect_right(indexes,entry['index']))/(2*len(indexes))
            behavior=next((i for i,limit in enumerate(thresholds) if percentile<=limit),5)
            entry.update(percentile=percentile,effectiveBehaviorProposal=behavior,status='SUGERIDO',
                         reason='Proposta experimental por posição e grupo; não é fato histórico nem quota por clube.',
                         cohortSize=len(group),approximateTargets=config['targets'][bracket])
    return {'version':VERSION,'config':config,'results':sorted(result,key=lambda entry:entry['playerCatalogId']),
            'application':'REVIEW_REQUIRED_NO_AUTOMATIC_WORLD_OR_CATALOG_WRITE'}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input',required=True)
    parser.add_argument('--root',required=True)
    parser.add_argument('--output',required=True)
    parser.add_argument('--config')
    args=parser.parse_args()
    result=classify(load_json(args.input),load_json(args.config) if args.config else None)
    if not args.output.startswith('_pesquisa/'):
        raise ValueError('Exporte somente sob _pesquisa; sem modificar catálogo ou save.')
    output=safe_path(args.root,args.output); output.parent.mkdir(parents=True,exist_ok=True)
    with output.open('xb') as stream: stream.write(encoded(result))
    print(output)


if __name__=='__main__':
    try: main()
    except (ValueError,KeyError,TypeError,OSError) as error: raise SystemExit(str(error)) from error
