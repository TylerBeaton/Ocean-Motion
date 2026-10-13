"""Fresh, read-only Fusion joint inventory; optional explicit UI selection."""
import json
from pathlib import Path
import uuid

TARGETS = ('Rigid 4', 'Rigid 10', 'Rigid 7')


def _normalized(name):
    return ''.join(name.split()).casefold()


def _read(errors, field, operation):
    try:
        return operation()
    except Exception as exc:
        errors.append({'field': field, 'message': f'{type(exc).__name__}: {exc}'})
        return None


def _items(owner, attribute, errors, label):
    collection = _read(errors, label, lambda: getattr(owner, attribute))
    if collection is None:
        return
    count = _read(errors, label + '.count', lambda: collection.count)
    for index in range(count or 0):
        item = _read(errors, f'{label}[{index}]', lambda: collection.item(index))
        if item is not None:
            yield index, item


def _endpoints(entity, errors, prefix):
    result = {}
    for key in ('occurrenceOne', 'occurrenceTwo'):
        occurrence = _read(errors, prefix + '.' + key, lambda: getattr(entity, key))
        result[key] = None if occurrence is None else {
            'name': _read(errors, prefix + '.' + key + '.name', lambda: occurrence.name),
            'path': _read(errors, prefix + '.' + key + '.fullPathName', lambda: occurrence.fullPathName)}
    return result


def locate(design):
    root = design.rootComponent
    report = {'targets': list(TARGETS), 'matches': [], 'errors': []}
    entities = []
    contexts = [('<root>', root, None)]
    for index, occurrence in _items(root, 'allOccurrences', report['errors'], 'allOccurrences'):
        path = _read(report['errors'], f'occurrence[{index}].fullPathName', lambda: occurrence.fullPathName)
        component = _read(report['errors'], f'occurrence[{index}].component', lambda: occurrence.component)
        if component is not None:
            contexts.append((path or f'<unreadable occurrence[{index}]>', component, occurrence))
    for path, component, occurrence in contexts:
        for kind in ('joints', 'asBuiltJoints'):
            for index, entity in _items(component, kind, report['errors'], path + '.' + kind):
                name = _read(report['errors'], f'{path}.{kind}[{index}].name', lambda: entity.name)
                if name is None or _normalized(name) not in {_normalized(n) for n in TARGETS}:
                    continue
                errors = []
                record = {'id': len(entities) + 1, 'name': name, 'kind': kind,
                          'collection_index': index, 'context_path': path,
                          'parent_component': _read(errors, 'parentComponent.name', lambda: entity.parentComponent.name),
                          'timeline_index': _read(errors, 'timelineObject.index', lambda: entity.timelineObject.index),
                          'native_endpoints': _endpoints(entity, errors, 'native'), 'errors': errors}
                selectable = entity
                if occurrence is not None:
                    selectable = _read(errors, 'createForAssemblyContext', lambda: entity.createForAssemblyContext(occurrence))
                    if selectable is None:
                        errors.append({'field': 'context', 'message': 'No contextual entity available; selection disabled for this row.'})
                    record['context_endpoints'] = None if selectable is None else _endpoints(selectable, errors, 'context')
                else:
                    record['context_endpoints'] = record['native_endpoints']
                record['selectable'] = selectable is not None
                report['matches'].append(record)
                entities.append(selectable)
    report['match_count'] = len(report['matches'])
    return report, entities


def execute(design, ui, output_directory):
    report, entities = locate(design)
    output_directory.mkdir(parents=True, exist_ok=True)
    path = output_directory / f'joint-locator-{uuid.uuid4().hex}.json'
    with path.open('x', encoding='utf-8') as stream:
        json.dump(report, stream, indent=2, ensure_ascii=False)
        stream.write('\n')
    # Persist the diagnostic first. UI failures can never erase or rewrite it.
    try:
        rows = '\n'.join(f"{r['id']}: {r['name']} | {r['kind']} | {r['context_path']} | timeline {r['timeline_index']}" for r in report['matches'])
        choice, cancelled = ui.inputBox(
            f'Report saved: {path}\n\n{rows}\n\nOptional UI-only selection: enter ONE row ID from this run.\nBlank or Cancel = diagnostic only. Existing selections are retained.',
            'JointLocator — optional selection', '')
        if cancelled or not choice.strip():
            return path, {'status': 'skipped'}
        text = choice.strip()
        if not text.isascii() or not text.isdigit() or not 1 <= int(text) <= len(entities):
            return path, {'status': 'invalid', 'message': 'Not a row ID from this run.'}
        entity = entities[int(text) - 1]
        if entity is None:
            return path, {'status': 'unavailable', 'message': 'No contextual entity; no native fallback selected.'}
        added = ui.activeSelections.add(entity)
        return path, {'status': 'selected' if added else 'rejected', 'id': int(text)}
    except Exception as exc:
        return path, {'status': 'error', 'message': f'{type(exc).__name__}: {exc}'}


def run(context):
    import adsk.core
    import adsk.fusion
    app = adsk.core.Application.get()
    ui = app.userInterface
    try:
        design = adsk.fusion.Design.cast(app.activeProduct)
        if design is None:
            ui.messageBox('Activate a Fusion Design before running JointLocator.')
            return
        path, selection = execute(design, ui, Path(__file__).resolve().parent / 'reports')
        ui.messageBox(f'JointLocator report: {path}\nSelection: {json.dumps(selection)}\nNo CAD edits or saves requested.')
    except Exception as exc:
        ui.messageBox(f'JointLocator stopped: {type(exc).__name__}: {exc}\nNo CAD edits or saves requested.')
