"""Test control/cleanup semantics, not Autodesk's native solver."""
import importlib.util
import pathlib
import unittest

PATH = pathlib.Path(__file__).resolve().parents[1] / "reconnect.py"


def load():
    spec = importlib.util.spec_from_file_location("investigation_reconnect", PATH)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class Item:
    def __init__(self, name, log, suppressed=False):
        self.name, self.log = name, log
        self._suppressed, self._grounded = suppressed, False
        self._transform = "neutral"

    @property
    def isSuppressed(self):
        return self._suppressed

    @isSuppressed.setter
    def isSuppressed(self, value):
        self.log.append((self.name, "suppressed", value))
        self._suppressed = value

    @property
    def isGrounded(self):
        return self._grounded

    @isGrounded.setter
    def isGrounded(self, value):
        self.log.append((self.name, "grounded", value))
        self._grounded = value

    @property
    def transform2(self):
        return self._transform

    @transform2.setter
    def transform2(self, value):
        self.log.append((self.name, "transform", value))
        self._transform = value


class ReconnectTests(unittest.TestCase):
    def test_cleanup_failure_does_not_skip_other_constraints_or_ungrounding(self):
        module = load()
        log = []
        class StuckJoint(Item):
            @Item.isSuppressed.setter
            def isSuppressed(self, value):
                if not value:
                    raise RuntimeError("stuck joint")
                self._suppressed = value
        top = Item("top", log)
        joints = [StuckJoint("stuck", log), Item("other", log)]
        with self.assertRaises(RuntimeError) as caught:
            module.reconnect(top, joints, "target", lambda: None, lambda _: None)
        self.assertFalse(top.isGrounded, "Cleanup must still unground the platform")
        self.assertFalse(joints[1].isSuppressed, "Cleanup must attempt every original state")
        self.assertIn("DO NOT SAVE", str(caught.exception))

    def test_solver_failure_restores_flags_and_preserves_exception(self):
        module = load()
        log = []
        top = Item("top", log)
        joints = [Item("active", log), Item("already-suppressed", log, True)]
        def pump():
            raise RuntimeError("native solver failed")
        with self.assertRaisesRegex(RuntimeError, "native solver failed"):
            module.reconnect(top, joints, "target", pump, lambda _: None)
        self.assertFalse(top.isGrounded)
        self.assertEqual([j.isSuppressed for j in joints], [False, True])

    def test_reconnects_while_pinned_and_restores_original_flags(self):
        self.assertTrue(PATH.exists(), "Reconnect experiment implementation is missing")
        module = load()
        log = []
        top = Item("top", log)
        joints = [Item("active", log), Item("already-suppressed", log, True)]
        def pump():
            log.append(("pump", top.isGrounded, joints[0].isSuppressed))
        observations = []
        module.reconnect(top, joints, "target", pump, observations.append)
        self.assertEqual(log, [
            ("active", "suppressed", True), ("top", "transform", "target"),
            ("top", "grounded", True), ("active", "suppressed", False),
            ("pump", True, False), ("top", "grounded", False), ("pump", False, False)])
        self.assertEqual(observations, ["grounded", "released"])
        self.assertFalse(top.isGrounded)
        self.assertEqual([j.isSuppressed for j in joints], [False, True])


if __name__ == "__main__":
    unittest.main()
