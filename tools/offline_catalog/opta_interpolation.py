"""Explicit local Opta interpolation; anchors and provenance are supplied by the operator."""
import argparse
from datetime import date
import math
from pathlib import Path
from behavior_classifier import encoded, load_json, safe_path


def interpolate(target_rank, above, below):
    if type(target_rank) is not int or target_rank < 1:
        raise ValueError('Posição mundial conhecida e positiva obrigatória.')
    for anchor in [above, below]:
        if anchor.get('source') != 'OPTA' or type(anchor.get('rank')) is not int or anchor['rank'] < 1:
            raise ValueError('Vizinhos precisam ser posições originais OPTA.')
        rating = anchor.get('rating')
        if type(rating) not in (float, int) or not math.isfinite(rating) or not 0 <= rating <= 100:
            raise ValueError('Nota Opta deve estar entre zero e cem.')
        reference = anchor.get('referenceDate')
        if not isinstance(reference,str) or date.fromisoformat(reference).isoformat() != reference:
            raise ValueError('Data de referência deve usar YYYY-MM-DD.')
        if any(not isinstance(anchor.get(key),str) or not anchor[key].strip() for key in ['evidence','clubId']):
            raise ValueError('Identidade e evidência dos vizinhos são obrigatórias.')
    if above['referenceDate'] != below['referenceDate'] or above['clubId'] == below['clubId']:
        raise ValueError('Exija dois vizinhos distintos na mesma data.')
    if not above['rank'] < target_rank < below['rank'] or above['rating'] < below['rating']:
        raise ValueError('Sem extrapolação ou inversão de posições/notas.')
    rating = above['rating'] + (target_rank-above['rank']) * (below['rating']-above['rating']) / (below['rank']-above['rank'])
    return {'historicalRanking': {'rating': rating, 'source': 'OPTA_INTERPOLATED', 'referenceDate': above['referenceDate']},
            'evidence': {'targetWorldRank': target_rank, 'above': above, 'below': below,
                         'method': 'linear-opta-1.0', 'classification': 'ESTIMATED',
                         'warning': 'Estimativa baseada nas evidências declaradas; não é nota oficial nem verificação externa automática.'}}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input',required=True)
    parser.add_argument('--root',required=True)
    parser.add_argument('--output',required=True,help='Caminho dentro de _pesquisa, relativo à raiz')
    args=parser.parse_args()
    request=load_json(args.input)
    result=interpolate(request['targetWorldRank'],request['above'],request['below'])
    if not args.output.startswith('_pesquisa/'):
        raise ValueError('Saída somente em _pesquisa; nunca alterar automaticamente catálogo.')
    output=safe_path(args.root,args.output);output.parent.mkdir(parents=True,exist_ok=True)
    with output.open('xb') as stream:stream.write(encoded(result))
    print(output)


if __name__=='__main__':
    try: main()
    except (ValueError,KeyError,TypeError,OSError) as error:raise SystemExit(str(error)) from error
