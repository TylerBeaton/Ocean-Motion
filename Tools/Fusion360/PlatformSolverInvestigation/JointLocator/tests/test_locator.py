import importlib.util
from pathlib import Path
import unittest
from types import SimpleNamespace as NS

SCRIPT = Path(__file__).resolve().parents[1] / 'JointLocator.py'


def load():
    if not SCRIPT.exists():
        return NS()
    spec = importlib.util.spec_from_file_location('locator', SCRIPT)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class Collection:
    def __init__(self, items):
        self.items = items
    @property
    def count(self):
        return len(self.items)
    def item(self, index):
        return self.items[index]


def joint(name, parent, index):
    return NS(name=name, parentComponent=parent,
              timelineObject=NS(index=index),
              occurrenceOne=NS(name='Pin:1', fullPathName='Pin:1'),
              occurrenceTwo=NS(name='Crank:1', fullPathName='Crank:1'))


def root():
    return NS(name='Hexapod Base (1)', joints=Collection([]),
              asBuiltJoints=Collection([]), allOccurrences=Collection([]))


class LocatorTests(unittest.TestCase):
    def test_fresh_root_enumeration_reports_both_kinds_and_exact_name_variants(self):
        mod = load()
        self.assertTrue(callable(getattr(mod, 'locate', None)), 'locate must exist')
        component = root()
        component.joints.items = [joint('Rigid 4', component, 16), joint('Rigid40', component, 88)]
        component.asBuiltJoints.items = [joint('Rigid10', component, 24)]
        report, entities = mod.locate(NS(rootComponent=component))
        self.assertEqual([r['name'] for r in report['matches']], ['Rigid 4', 'Rigid10'])
        first = report['matches'][0]
        self.assertEqual(first['parent_component'], 'Hexapod Base (1)')
        self.assertEqual(first['timeline_index'], 16)
        self.assertEqual(first['native_endpoints']['occurrenceOne']['name'], 'Pin:1')
        self.assertEqual(first['context_path'], '<root>')
        self.assertEqual(len(entities), 2)
        component.joints.items = []
        self.assertEqual(len(mod.locate(NS(rootComponent=component))[0]['matches']), 1)


    def test_repeated_contexts_and_getter_errors_preserve_native_evidence(self):
        mod = load()
        component = root()
        child = root()
        child.name = 'Leg Assembly'
        class BrokenProxy:
            @property
            def occurrenceOne(self):
                raise RuntimeError('jointOcc')
            occurrenceTwo = NS(name='Crank:1', fullPathName='Leg+Crank:1')
        native = joint('Rigid 7', child, 42)
        native.createForAssemblyContext = lambda occurrence: BrokenProxy()
        child.asBuiltJoints.items = [native]
        component.allOccurrences.items = [NS(component=child, fullPathName=f'Leg Assembly:{i}') for i in range(1, 7)]
        class BrokenRoot:
            name = 'Rigid4'
            parentComponent = component
            timelineObject = NS(index=16)
            @property
            def occurrenceOne(self):
                raise RuntimeError('endpoint unavailable')
            occurrenceTwo = NS(name='Base:1', fullPathName='Base:1')
        component.joints.items = [BrokenRoot()]
        report, entities = mod.locate(NS(rootComponent=component))
        self.assertEqual(len(report['matches']), 7)
        self.assertEqual(len({r['context_path'] for r in report['matches']}), 7)
        self.assertEqual(report['matches'][0]['timeline_index'], 16)
        self.assertIsNone(report['matches'][0]['native_endpoints']['occurrenceOne'])
        for record in report['matches'][1:]:
            self.assertEqual(record['native_endpoints']['occurrenceOne']['name'], 'Pin:1')
            self.assertIsNone(record['context_endpoints']['occurrenceOne'])
            self.assertTrue(any('jointOcc' in e['message'] for e in record['errors']))
        self.assertEqual(len(entities), 7)

    def test_collection_error_is_visible_not_empty_success(self):
        mod = load()
        component = root()
        class BrokenCollection:
            @property
            def count(self):
                raise RuntimeError('collection unavailable')
        component.joints = BrokenCollection()
        report, _ = mod.locate(NS(rootComponent=component))
        self.assertTrue(report['errors'])


    def test_export_precedes_optional_selection_and_selection_failures_are_isolated(self):
        import json
        import tempfile
        mod = load()
        self.assertTrue(callable(getattr(mod, 'execute', None)), 'execute must exist')
        component = root()
        component.joints.items = [joint('Rigid4', component, 16)]
        for choice, cancelled, mode, expected in [('', False, 'ok', 'skipped'), ('1', True, 'ok', 'skipped'), ('0', False, 'ok', 'invalid'), ('99', False, 'ok', 'invalid'), ('1', False, 'false', 'rejected'), ('1', False, 'throw', 'error'), ('1', False, 'ok', 'selected')]:
            with tempfile.TemporaryDirectory(dir=SCRIPT.parent) as folder:
                calls = []
                class UI:
                    def inputBox(self, *args):
                        paths = list(Path(folder).glob('joint-locator-*.json'))
                        self.assert_export = json.loads(paths[0].read_text())
                        return choice, cancelled
                    def add(self, entity):
                        calls.append(entity)
                        if mode == 'throw':
                            raise RuntimeError('selection unavailable')
                        return mode == 'ok'
                ui = UI()
                ui.activeSelections = ui
                path, result = mod.execute(NS(rootComponent=component), ui, Path(folder))
                self.assertEqual(result['status'], expected)
                self.assertEqual(json.loads(path.read_text()), ui.assert_export)
                self.assertEqual(len(calls), int(expected in ('selected', 'rejected', 'error')))


    def test_no_context_proxy_never_selects_native_fallback(self):
        import tempfile
        mod = load()
        component, child = root(), root()
        native = joint('Rigid7', child, 7)
        native.createForAssemblyContext = lambda occurrence: None
        child.joints.items = [native]
        component.allOccurrences.items = [NS(component=child, fullPathName='Leg:1')]
        ui = NS(inputBox=lambda *args: ('1', False))
        with tempfile.TemporaryDirectory(dir=SCRIPT.parent) as folder:
            path, result = mod.execute(NS(rootComponent=component), ui, Path(folder))
            self.assertEqual(result['status'], 'unavailable')
            self.assertTrue(path.exists())

    def test_entrypoint_uses_active_design_and_reports_result(self):
        import sys
        from unittest.mock import patch
        from types import ModuleType
        mod = load()
        design = NS(rootComponent=root())
        messages = []
        app = NS(activeProduct=design, userInterface=NS(messageBox=messages.append))
        adsk, core, fusion = ModuleType('adsk'), ModuleType('adsk.core'), ModuleType('adsk.fusion')
        core.Application = NS(get=lambda: app)
        fusion.Design = NS(cast=lambda value: value)
        adsk.core, adsk.fusion = core, fusion
        observed = []
        def execute(value, ui, directory):
            observed.append((value, directory))
            return directory / 'stand-in.json', {'status': 'skipped'}
        mod.execute = execute
        with patch.dict(sys.modules, {'adsk': adsk, 'adsk.core': core, 'adsk.fusion': fusion}):
            mod.run(None)
            app.activeProduct = None
            mod.run(None)
        self.assertEqual(observed, [(design, SCRIPT.parent / 'reports')])
        self.assertIn('skipped', messages[0])
        self.assertIn('Activate a Fusion Design', messages[1])

    def test_source_has_no_forbidden_cad_mutation_calls_or_setters(self):
        import ast
        tree = ast.parse(SCRIPT.read_text())
        forbidden = {'rollTo', 'moveToEnd', 'deleteMe', 'save', 'saveAs', 'computeAll', 'activate', 'clear', 'isSuppressed', 'isGrounded', 'transform', 'transform2'}
        self.assertFalse({n.attr for n in ast.walk(tree) if isinstance(n, ast.Attribute)} & forbidden)
        self.assertFalse([n for n in ast.walk(tree) if isinstance(n, ast.Attribute) and isinstance(n.ctx, ast.Store)])


if __name__ == '__main__':
    unittest.main()
