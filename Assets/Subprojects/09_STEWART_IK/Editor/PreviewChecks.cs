using System;
using System.IO;
using System.Security.Cryptography;
using OceanMotion.Subproject09.Core;
using OceanMotion.Subproject09.Preview;
using UnityEditor;
using UnityEngine;

namespace OceanMotion.Subproject09.Editor
{
    public static class PreviewChecks
    {
        public const string Root="Assets/Subprojects/09_STEWART_IK/";
        [Serializable] private sealed class Fixture
        {
            public string name;
            public double[] translation_mm, rotation_flat, angles_deg;
            public bool expected_solution;
        }
        [Serializable] private sealed class Fixtures { public string geometry_sha256; public Fixture[] cases; }
        [Serializable] private sealed class Report
        {
            public bool passed, hardware_output_enabled;
            public string editor_version, geometry_sha256;
            public int python_parity_cases, continuity_samples;
            public double max_parity_error_deg, max_rod_residual_mm, max_continuity_step_deg;
        }
        private static void Require(bool condition,string message)
        { if (!condition) throw new InvalidOperationException("SP09: "+message); }

        [MenuItem("Ocean Motion/SP09/Run numerical checks")]
        public static void Run()
        {
            string config=File.ReadAllText(Root+"Measurements/offline-geometry.json");
            var model=PreviewGeometry.Parse(config).Build();
            var fixtures=JsonUtility.FromJson<Fixtures>(File.ReadAllText(Root+"Reports/unity-reference-fixtures.json"));
            string hash;
            using (var sha=SHA256.Create()) hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Root+"Measurements/offline-geometry.json"))).Replace("-","").ToLowerInvariant();
            Require(hash==fixtures.geometry_sha256,"Python fixtures refer to a different geometry snapshot.");
            var report=new Report { editor_version=Application.unityVersion,geometry_sha256=hash };
            foreach (var fixture in fixtures.cases)
            {
                var result=model.Solve(new IkPose(IkVector.From(fixture.translation_mm),IkMatrix.FromFlat(fixture.rotation_flat)));
                Require(result.KinematicSolutionFound==fixture.expected_solution,fixture.name+" result mismatch.");
                Require(!result.HardwareOutputEnabled,"Hardware output must remain false.");
                if (!fixture.expected_solution) Require(result.Legs.Length==0,"Invalid pose leaked partial solutions.");
                for (int i=0;i<result.Legs.Length;i++)
                {
                    double error=Math.Abs(result.Legs[i].AngleDegrees-fixture.angles_deg[i]);
                    Require(error<1e-8,fixture.name+" angle mismatch channel "+i);
                    report.max_parity_error_deg=Math.Max(report.max_parity_error_deg,error);
                    report.max_rod_residual_mm=Math.Max(report.max_rod_residual_mm,result.Legs[i].RodResidualMm);
                }
                report.python_parity_cases++;
            }
            double[] previous=null;
            for (int i=0;i<=180;i++)
            {
                double phase=2*Math.PI*i/180;
                Quaternion q=Quaternion.AngleAxis(1,new Vector3((float)Math.Cos(phase),0,(float)Math.Sin(phase)));
                var pose=IkPose.FromUnity(new IkVector(0,.0015*Math.Sin(phase),0),q.x,q.y,q.z,q.w);
                var result=model.Solve(pose,previous,previous==null ? (double?)null : 1);
                Require(result.KinematicSolutionFound,"Continuous Unity quaternion path failed.");
                var angles=result.AnglesDegrees;
                if (previous!=null) for (int j=0;j<6;j++)
                    report.max_continuity_step_deg=Math.Max(report.max_continuity_step_deg,Math.Abs(angles[j]-previous[j]));
                previous=angles; report.continuity_samples++;
            }
            foreach (Vector3 axis in new[] {Vector3.right,Vector3.up,Vector3.forward})
            {
                Quaternion q=Quaternion.AngleAxis(1,axis);
                var u=IkPose.FromUnity(new IkVector(axis.x*.001,axis.y*.001,axis.z*.001),q.x,q.y,q.z,q.w);
                var f=new IkVector(axis.x,axis.z,axis.y);
                Require((u.TranslationMm-f).Length<1e-10,"Metres/mm axis conversion failed.");
                Vector3 probe=new Vector3(.3f,.4f,.5f);
                Vector3 rotated=q*probe;
                var mapped=u.Rotation.Transform(new IkVector(probe.x,probe.z,probe.y));
                Require((mapped-new IkVector(rotated.x,rotated.z,rotated.y)).Length<1e-7,"Reflected rotation basis failed.");
            }
            bool refused=false;
            try { IkMatrix.FromQuaternion(0,0,0,0); } catch (IkGeometryException) { refused=true; }
            Require(refused,"Zero quaternion accepted.");
            refused=false;
            try { new IkVector(double.NaN,0,0); } catch (IkGeometryException) { refused=true; }
            Require(refused,"Nonfinite pose accepted.");
            var jump=model.Solve(IkPose.FromUnity(new IkVector(0,.001,0),0,0,0,1),model.Solve(IkPose.Neutral).AnglesDegrees,.01);
            Require(!jump.KinematicSolutionFound && jump.Legs.Length==0,"Continuity rejection failed.");
            report.passed=true;
            File.WriteAllText(Root+"Reports/unity-preview-checks.json",JsonUtility.ToJson(report,true)+"\n");
            Debug.Log("SP09 CORE PASS: 15 Python fixtures, 181 Unity poses, basis/invalid/continuity guards.");
        }
    }
}
