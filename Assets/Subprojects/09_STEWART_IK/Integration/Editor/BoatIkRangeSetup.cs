using System;
using System.IO;
using System.Collections.Generic;
using OceanMotion.Subproject09.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OceanMotion.Subproject09.Integration.Editor
{
    public static class BoatIkRangeSetup
    {
        public const string ScenePath=PreviewChecks.Root+"Scenes/SP09_BoatIkRange.unity";
        [MenuItem("Ocean Motion/SP09/Open Boat IK Range Test")]
        public static void Open()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(File.Exists(ScenePath))EditorSceneManager.OpenScene(ScenePath);else Create();
        }
        public static void Create()=>BuildAt(ScenePath);
        // Build directly from the committed visual baseline; no superseded probe scenes are required.
        public static void BuildAt(string destination)
        {
            if(File.Exists(destination))throw new InvalidOperationException("Scene exists; open instead.");
            ArchivedSceneBaseline.Check();
            var scene=EditorSceneManager.OpenScene(BoatIkSceneBuilder.ScenePath);
            EditorSceneManager.SaveScene(scene,destination,true);scene=EditorSceneManager.OpenScene(destination);
            var old=UnityEngine.Object.FindFirstObjectByType<ProcessedBoatIkBridge>();
            if(!old)throw new InvalidOperationException("Visual baseline is missing its boat bridge.");
            var root=old.gameObject;var preview=old.preview;var pipeline=old.pipeline;
            var telemetry=old.telemetry;var waves=old.waves;
            UnityEngine.Object.DestroyImmediate(old);
            var source=root.AddComponent<BoatIndividualIkSource>();
            source.preview=preview;source.pipeline=pipeline;source.telemetry=telemetry;source.waves=waves;
            preview.geometry=AssetDatabase.LoadAssetAtPath<TextAsset>(PreviewChecks.Root+"Measurements/provisional-physical-geometry.json");
            preview.sourceDescription="Boat → same solved IK angles → six individual servo pulses";
            preview.outputDescription="Shared IK target; physical output status is in the right panel";
            var output=new GameObject("Expanded individual IK range - USB OFF until Connect");
            var serial=output.AddComponent<SerialController>();serial.enabled=false;
            serial.portName="/dev/cu.usbmodem-SET-ME";serial.baudRate=115200;
            serial.maxUnreadMessages=64;serial.messageListener=output;
            var probe=output.AddComponent<IndividualIkSweepProbe>();
            probe.extendedTravel=true;probe.serial=serial;probe.boatSource=source;source.probe=probe;
            waves.SetPreset(OceanMotion.Subproject05.WavePreset.Calm);
            TrimLegacyUi(scene);
            EditorSceneManager.SaveScene(scene);AssetDatabase.Refresh();
            Debug.Log("SP09 BOAT IK RANGE BUILT: OI5, USB OFF; "+destination);
        }
        public static void TrimCurrentScene()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath);
            TrimLegacyUi(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("SP09 CURRENT BOAT UI TRIM SAVED; runtime sources and physical output preserved.");
        }
        public static int TrimLegacyUi(UnityEngine.SceneManagement.Scene scene)
        {
            var roots=new List<GameObject>();var removed=new HashSet<GameObject>();
            foreach(var root in scene.GetRootGameObjects()) {
                if(root.name!="SP05_TelemetryCanvas" && root.name!="SP06 Motion Comparison")continue;
                if(root.activeSelf)throw new InvalidOperationException("Expected inactive legacy UI: "+root.name);
                roots.Add(root);
                foreach(var t in root.GetComponentsInChildren<Transform>(true))removed.Add(t.gameObject);
            }
            // Refuse the trim if a retained component still depends on an object in either subtree.
            foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true)) {
                if(removed.Contains(t.gameObject))continue;
                foreach(var component in t.GetComponents<Component>()) {
                    if(!component || component is Transform)continue;
                    using(var serialized=new SerializedObject(component)) {
                        var property=serialized.GetIterator();
                        while(property.Next(true)) {
                            if(property.propertyType!=SerializedPropertyType.ObjectReference)continue;
                            var reference=property.objectReferenceValue;
                            var target=reference is GameObject go ? go : reference is Component c ? c.gameObject : null;
                            if(target && removed.Contains(target))throw new InvalidOperationException(
                                "Retained component depends on legacy UI: "+component.name+" / "+property.propertyPath);
                        }
                    }
                }
            }
            foreach(var root in roots)UnityEngine.Object.DestroyImmediate(root);
            return removed.Count;
        }
    }
}
