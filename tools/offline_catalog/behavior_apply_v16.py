"""Catalog validation, atomic file replacements and recovery for behavior batches."""
from __future__ import annotations

from base64 import b64decode, b64encode
from pathlib import Path
import shutil
import subprocess
import tempfile

from behavior_classifier import atomic_write, decode_json, digest, encoded, safe_path


def validate_catalog(root, dll):
    dll = Path(dll).resolve(strict=True)
    if dll.name != 'Elifoot.Api.dll' or not dll.is_file():
        raise ValueError('Informe Elifoot.Api.dll do build local.')
    run = subprocess.run(
        ['dotnet', str(dll), 'offline-catalog-validate', f'--CatalogOffline:Root={root}'],
        cwd=root, capture_output=True, check=False, timeout=120,
    )
    try:
        report = decode_json(run.stdout)
    except ValueError as error:
        raise ValueError(f'Validador local não retornou um relatório JSON (saída {run.returncode}).') from error
    if run.returncode != 0 or report.get('valid') is not True:
        issues = list(report.get('issues', []))
        for catalog in report.get('catalogs', []):
            issues.extend(catalog.get('issues', []))
        messages = [entry['message'] for entry in issues if entry.get('isError')]
        raise ValueError('Validação do catálogo falhou: ' + '; '.join(messages[:10]))
    return {
        'valid': True, 'catalogCount': len(report['catalogs']),
        'readyCatalogCount': sum(catalog['ready'] for catalog in report['catalogs']),
    }


def manifest_path(relative):
    return relative.rsplit('/', 2)[0] + '/manifest.json'


def check_targets(root, files, use_after=False):
    for relative, (before, after, manifest_hash) in files.items():
        expected = after if use_after else before
        if (safe_path(root, relative).read_bytes() != expected or
                digest(safe_path(root, manifest_path(relative)).read_bytes()) != manifest_hash):
            raise ValueError(f'Clube ou manifest mudou durante a operação: {relative}.')


def validate_staged(root, files, validate):
    def ignore_research(path, names):
        folder = Path(path)
        ignored = {'_pesquisa', '_schemas', 'modelos', '.git'} if folder == root else set()
        for name in set(names) - ignored:
            entry = folder / name
            if entry.is_symlink() or entry.is_junction():
                raise ValueError(f'Link ou junction na cópia de validação: {entry.name}.')
        return ignored.intersection(names)

    with tempfile.TemporaryDirectory(prefix='elifoot-behavior-stage-') as directory:
        staged = Path(directory) / 'catalogos'
        shutil.copytree(root, staged, ignore=ignore_research)
        check_targets(staged, files)
        for relative, (_, after, _) in files.items():
            safe_path(staged, relative).write_bytes(after)
        validate(staged)


def journal_data(approval_ids, files):
    payload = {
        'version': 'behavior-transaction-1.0', 'approvalIds': approval_ids,
        'files': {
            path: {'before': b64encode(before).decode('ascii'), 'after': b64encode(after).decode('ascii'),
                   'manifestSha256': manifest_hash}
            for path, (before, after, manifest_hash) in files.items()
        },
    }
    return {**payload, 'transactionId': digest(encoded(payload))}


def rollback(root, files):
    conflicts = []
    for relative, (before, after, _) in reversed(list(files.items())):
        try:
            target = safe_path(root, relative)
            current = target.read_bytes()
            if current == before:
                continue
            if current != after:
                conflicts.append(relative)
                continue
            atomic_write(target, after, before)
        except (ValueError, OSError):
            conflicts.append(relative)
    if conflicts:
        raise ValueError('Recuperação pendente. Alteração externa preservada em: ' + ', '.join(conflicts))


def apply_changes(root, journal, ledger, approval_ids, files, changes, operator, validate, check_evidence):
    validate_staged(root, files, validate)
    check_evidence()
    check_targets(root, files)
    transaction = journal_data(approval_ids, files)
    atomic_write(journal, None, encoded(transaction))
    try:
        for relative, (before, after, manifest_hash) in files.items():
            check_targets(root, {relative: (before, after, manifest_hash)})
            if before != after:
                atomic_write(safe_path(root, relative), before, after)
        validation = validate(root)
        check_evidence()
        check_targets(root, files, use_after=True)
        receipt = {
            'status': 'APPLIED', 'transactionId': transaction['transactionId'],
            'approvalIds': approval_ids, 'changes': changes, 'validation': validation,
            'files': {
                path: {'beforeSha256': digest(before), 'afterSha256': digest(after)}
                for path, (before, after, _) in files.items()
            },
        }
        ledger.append('APPLY', receipt, operator)
    except BaseException:
        if not ledger.has_committed(transaction['transactionId'], approval_ids):
            rollback(root, files)
            journal.unlink()
        raise
    journal.unlink()
    return receipt


def recover(root, journal, ledger):
    if not journal.exists():
        return {'status': 'NO_PENDING_TRANSACTION'}
    record = decode_json(journal.read_bytes())
    transaction_id = record.pop('transactionId')
    if record.get('version') != 'behavior-transaction-1.0' or digest(encoded(record)) != transaction_id:
        raise ValueError('Diário de recuperação alterado.')
    applied = [ledger.applications.get(approval_id) for approval_id in record['approvalIds']]
    if any(applied):
        if not all(receipt and receipt['transactionId'] == transaction_id for receipt in applied):
            raise ValueError('Diário de recuperação não corresponde ao histórico.')
        status = 'ALREADY_COMMITTED'
    else:
        files = {
            path: (b64decode(data['before'], validate=True), b64decode(data['after'], validate=True),
                   data['manifestSha256'])
            for path, data in record['files'].items()
        }
        rollback(root, files)
        status = 'ROLLED_BACK'
    journal.unlink()
    return {'status': status, 'transactionId': transaction_id}
