using System;
using OceanMotion.Subproject09.Core;
using UnityEngine;

namespace OceanMotion.Subproject09.Preview
{
    [Serializable] public sealed class PreviewNeutral
    {
        public double[] position_mm;
        public double[] rotation_xyzw;
    }
    [Serializable] public sealed class PreviewLeg
    {
        public int channel;
        public double[] shaft_point_mm, shaft_axis, reference_radial_direction, top_local_mm;
        public double axial_offset_mm, horn_radius_mm, rod_length_mm;
        public double[] angle_limits_deg;
    }
    [Serializable] public sealed class PreviewGeometry
    {
        public int schema_version;
        public string length_units, coordinate_frame;
        public bool hardware_output_enabled;
        public PreviewNeutral neutral_pose;
        public PreviewLeg[] legs;

        public static PreviewGeometry Parse(string json)
        {
            var data = JsonUtility.FromJson<PreviewGeometry>(json);
            if (data == null || data.schema_version != 1 || data.length_units != "mm" ||
                data.coordinate_frame != "fusion_z_up" || data.hardware_output_enabled || data.neutral_pose == null ||
                data.legs == null || data.legs.Length != 6)
                throw new IkGeometryException("Unsupported preview geometry; hardware output must be false.");
            return data;
        }
        public RotaryStewartModel Build()
        {
            var converted = new IkLeg[6];
            for (int i=0;i<6;i++)
            {
                var l=legs[i];
                if (l == null) throw new IkGeometryException("Missing leg.");
                // JsonUtility represents an explicit JSON null array as an empty array.
                var limits=l.angle_limits_deg;
                if (limits != null && limits.Length == 0) limits=null;
                converted[i]=new IkLeg(l.channel,IkVector.From(l.shaft_point_mm),IkVector.From(l.shaft_axis),
                    l.axial_offset_mm,IkVector.From(l.reference_radial_direction),IkVector.From(l.top_local_mm),
                    l.horn_radius_mm,l.rod_length_mm,limits);
            }
            return new RotaryStewartModel(converted,IkVector.From(neutral_pose.position_mm),
                IkMatrix.FromQuaternion(neutral_pose.rotation_xyzw));
        }
        public static Vector3 ToUnity(IkVector mm) => new Vector3((float)mm.X,(float)mm.Z,(float)mm.Y)*.001f;
        public Quaternion NeutralUnityRotation
        {
            get
            {
                var q=neutral_pose.rotation_xyzw;
                IkMatrix.FromQuaternion(q); // Validate before accessing components.
                return new Quaternion((float)-q[0],(float)-q[2],(float)-q[1],(float)q[3]).normalized;
            }
        }
    }
}
