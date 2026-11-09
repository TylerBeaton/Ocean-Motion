using System;
using System.IO;
using OceanMotion.Subproject09.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OceanMotion.Subproject09.Integration.Editor
{
    public static class CurrentSceneChecks
    {
        public static void Run()
        {
            ArchivedSceneBaseline.Check();
            Check(BoatIkRangeSetup.ScenePath,false);
            Check(IkPoseRangeSetup.ScenePath,true);
            string prefix=PreviewChecks.Root+"Scenes/__cleanup_"+Guid.NewGuid().ToString("N");
            string boat=prefix+"_boat.unity",sweep=prefix+"_sweep.unity";
            try {
                BoatIkRangeSetup.BuildAt(boat);Check(boat,false);
                IkPoseRangeSetup.BuildAt(sweep,boat);Check(sweep,true);
            }
            finally {
                EditorSceneManager.OpenScene(BoatIkRangeSetup.ScenePath);
                AssetDatabase.DeleteAsset(sweep);AssetDatabase.DeleteAsset(boat);
            }
            IkPoseRangeChecks.CheckMapping();
            Debug.Log("SP09 CURRENT SCENES PASS: both retained scenes load and rebuild without retired probes; no missing scripts, OI5, USB OFF.");
        }
        private static void Check(string path,bool manual)
        {
            var scene=EditorSceneManager.OpenScene(path);
            int missing=0;
            foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
                missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            BoatIkChecks.Need(missing==0,"Missing scripts in "+path);
            var probes=UnityEngine.Object.FindObjectsByType<IndividualIkSweepProbe>(FindObjectsSortMode.None);
            BoatIkChecks.Need(probes.Length==1,"Expected one physical output component");var p=probes[0];
            BoatIkChecks.Need(p.extendedTravel && p.serial && !p.serial.enabled && p.Preview && p.Preview.geometry,"OI5/preview/USB gate failed");
            BoatIkChecks.Need(p.serial.messageListener==p.gameObject && p.serial.baudRate==115200,"Serial callback configuration failed");
            BoatIkChecks.Need(UnityEngine.Object.FindObjectsByType<SerialController>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1,"Duplicate serial output");
            if(manual) {
                BoatIkChecks.Need(p.source && !p.boatSource && p.GetComponent<IkAxisSweep>().extendedAngles &&
                    p.source.enforceSmallPoseEnvelope && p.source.smallAngleExtent==10,"Manual controls missing");
                BoatIkChecks.Need(UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==0,"Boat physics remains in static scene");
            } else {
                foreach(var root in scene.GetRootGameObjects())BoatIkChecks.Need(
                    root.name!="SP05_TelemetryCanvas" && root.name!="SP06 Motion Comparison","Retired UI returned to current boat scene");
                BoatIkChecks.Need(p.boatSource && !p.source && p.boatSource.pipeline && p.boatSource.telemetry && p.boatSource.waves,"Boat source references missing");
                BoatIkChecks.Need(p.boatSource.probe==p && p.boatSource.preview==p.Preview,"Boat output wiring mismatch");
            }
        }
    }
}
