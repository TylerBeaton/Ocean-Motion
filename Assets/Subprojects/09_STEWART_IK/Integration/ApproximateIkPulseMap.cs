using System;
using OceanMotion.Subproject09.Core;

namespace OceanMotion.Subproject09.Integration
{
    /// <summary>Provisional differential mapping; not measured angular calibration.</summary>
    public sealed class ApproximateIkPulseMap
    {
        public const int LimitUs=25;
        public static readonly int[] NeutralUs={1500,1600,1550,1600,1500,1500};
        private static readonly int[] RaiseSigns={1,-1,1,-1,1,-1};
        private readonly double[] signedUsPerDegree=new double[6];
        public ApproximateIkPulseMap(RotaryStewartModel model,double usPerDegree=11)
        {
            if (!IkMath.Finite(usPerDegree) || usPerDegree<=0 || usPerDegree>30)
                throw new IkGeometryException("Invalid provisional pulse scale.");
            var neutral=model.Solve(IkPose.Neutral);
            if (!neutral.KinematicSolutionFound) throw new IkGeometryException("Neutral IK failed.");
            for (int i=0;i<6;i++) {
                var leg=model.Legs[i];
                // Positive mathematical rotation is right-hand about Q-P. Convert using
                // the actual neutral radial vector, not the sign of an Euler display angle.
                double rise=IkVector.Cross(leg.ShaftAxis,neutral.Legs[i].Horn-leg.CircleCentreMm).Z;
                if (!IkMath.Finite(rise) || Math.Abs(rise)<1)
                    throw new IkGeometryException("Ambiguous horn raise direction, channel "+i);
                signedUsPerDegree[i]=RaiseSigns[i]*Math.Sign(rise)*usPerDegree;
            }
        }
        public bool TryOffsets(IkResult result,out int[] offsets,out string error)
            =>TryOffsets(result,LimitUs,out offsets,out error);
        public bool TryOffsets(IkResult result,int limitUs,out int[] offsets,out string error)
        {
            offsets=null; error=null;
            if(limitUs<25 || limitUs>250 || limitUs%25!=0) { error="Unsupported pulse allowance"; return false; }
            if (result==null || !result.KinematicSolutionFound) { error="No complete IK solution"; return false; }
            var values=new int[6]; var seen=new bool[6];
            foreach (var leg in result.Legs) {
                int i=leg.Channel;
                if (i<0 || i>=6 || seen[i]) { error="Invalid IK channel set"; return false; }
                seen[i]=true;
                double delta=leg.NeutralDeltaDegrees*signedUsPerDegree[i];
                if (!IkMath.Finite(delta) || Math.Abs(delta)>limitUs) {
                    error="Whole pose rejected: channel "+i+" needs "+delta.ToString("F1")+"us (limit ±"+limitUs+"us)"; return false;
                }
                values[i]=(int)Math.Round(delta,MidpointRounding.AwayFromZero);
            }
            offsets=values; return true;
        }
    }
}
