using System;
using System.Collections.Generic;

namespace OceanMotion.Subproject09.Core
{
    public sealed class IkGeometryException : ArgumentException
    {
        public IkGeometryException(string message) : base(message) { }
    }

    public static class IkMath
    {
        public const double Tau = 2 * Math.PI;
        public const double Degrees = 180 / Math.PI;
        public static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        public static void RequireFinite(double x, string name)
        {
            if (!Finite(x)) throw new IkGeometryException(name + " must be finite.");
        }
        public static double Unwrap(double angle, double reference) =>
            angle + Tau * Math.Round((reference - angle) / Tau);
    }

    public readonly struct IkVector
    {
        public readonly double X, Y, Z;
        public IkVector(double x, double y, double z)
        {
            IkMath.RequireFinite(x, "X"); IkMath.RequireFinite(y, "Y"); IkMath.RequireFinite(z, "Z");
            X = x; Y = y; Z = z;
        }
        public static IkVector From(double[] values)
        {
            if (values == null || values.Length != 3)
                throw new IkGeometryException("Vector requires three numbers.");
            return new IkVector(values[0], values[1], values[2]);
        }
        public double Length
        {
            get
            {
                double scale = Math.Max(Math.Abs(X), Math.Max(Math.Abs(Y), Math.Abs(Z)));
                if (scale == 0) return 0;
                double x = X / scale, y = Y / scale, z = Z / scale;
                return scale * Math.Sqrt(x*x + y*y + z*z);
            }
        }
        public IkVector Unit
        {
            get
            {
                double length = Length;
                if (length < 1e-12 || !IkMath.Finite(length))
                    throw new IkGeometryException("Zero/invalid axis or radial direction.");
                return this * (1 / length);
            }
        }
        public double[] ToArray() => new[] { X, Y, Z };
        public static IkVector operator +(IkVector a, IkVector b) => new IkVector(a.X+b.X, a.Y+b.Y, a.Z+b.Z);
        public static IkVector operator -(IkVector a, IkVector b) => new IkVector(a.X-b.X, a.Y-b.Y, a.Z-b.Z);
        public static IkVector operator *(IkVector a, double s) => new IkVector(a.X*s, a.Y*s, a.Z*s);
        public static double Dot(IkVector a, IkVector b) => a.X*b.X + a.Y*b.Y + a.Z*b.Z;
        public static IkVector Cross(IkVector a, IkVector b) => new IkVector(
            a.Y*b.Z-a.Z*b.Y, a.Z*b.X-a.X*b.Z, a.X*b.Y-a.Y*b.X);
    }

    public sealed class IkMatrix
    {
        private readonly double[] values;
        public static readonly IkMatrix Identity = new IkMatrix(new double[] {1,0,0, 0,1,0, 0,0,1});
        private static readonly IkMatrix SwapYZ = new IkMatrix(new double[] {1,0,0, 0,0,1, 0,1,0});
        private IkMatrix(double[] values) { this.values = (double[])values.Clone(); }
        public double this[int row, int col] => values[row * 3 + col];
        public IkVector Transform(IkVector v) => new IkVector(
            values[0]*v.X+values[1]*v.Y+values[2]*v.Z,
            values[3]*v.X+values[4]*v.Y+values[5]*v.Z,
            values[6]*v.X+values[7]*v.Y+values[8]*v.Z);
        public IkMatrix Multiply(IkMatrix b)
        {
            var result = new double[9];
            for (int r = 0; r < 3; ++r)
                for (int c = 0; c < 3; ++c)
                    for (int k = 0; k < 3; ++k) result[r*3+c] += this[r,k]*b[k,c];
            return new IkMatrix(result);
        }
        public static IkMatrix FromFlat(double[] input)
        {
            if (input == null || input.Length != 9)
                throw new IkGeometryException("Rotation requires nine matrix entries.");
            foreach (double value in input) IkMath.RequireFinite(value, "Rotation");
            var m = new IkMatrix(input);
            var rows = new[] {new IkVector(input[0],input[1],input[2]),
                new IkVector(input[3],input[4],input[5]), new IkVector(input[6],input[7],input[8])};
            for (int i = 0; i < 3; ++i)
                for (int j = 0; j < 3; ++j)
                    if (Math.Abs(IkVector.Dot(rows[i],rows[j]) - (i == j ? 1 : 0)) > 1e-8)
                        throw new IkGeometryException("Rotation must be orthonormal.");
            if (Math.Abs(IkVector.Dot(rows[0], IkVector.Cross(rows[1], rows[2])) - 1) > 1e-8)
                throw new IkGeometryException("Rotation must be proper (no reflection).");
            return m;
        }
        public static IkMatrix FromQuaternion(double x, double y, double z, double w)
        {
            foreach (double value in new[] {x,y,z,w}) IkMath.RequireFinite(value, "Quaternion");
            double scale = Math.Max(Math.Max(Math.Abs(x),Math.Abs(y)),Math.Max(Math.Abs(z),Math.Abs(w)));
            if (scale == 0) throw new IkGeometryException("Quaternion must be nonzero.");
            x /= scale; y /= scale; z /= scale; w /= scale;
            double length = Math.Sqrt(x*x+y*y+z*z+w*w);
            x /= length; y /= length; z /= length; w /= length;
            return new IkMatrix(new[] {
                1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w),
                2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w),
                2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)});
        }
        public static IkMatrix FromQuaternion(double[] q)
        {
            if (q == null || q.Length != 4) throw new IkGeometryException("Quaternion requires x,y,z,w.");
            return FromQuaternion(q[0],q[1],q[2],q[3]);
        }
        public static IkMatrix UnityToFusionRotation(double x, double y, double z, double w) =>
            SwapYZ.Multiply(FromQuaternion(x,y,z,w)).Multiply(SwapYZ);
    }

    public sealed class IkPose
    {
        public IkVector TranslationMm { get; }
        public IkMatrix Rotation { get; }
        public static readonly IkPose Neutral = new IkPose(new IkVector(0,0,0), IkMatrix.Identity);
        public IkPose(IkVector translationMm, IkMatrix rotation)
        {
            TranslationMm = translationMm;
            Rotation = rotation ?? throw new IkGeometryException("Rotation required.");
        }
        public static IkPose FromUnity(IkVector positionMeters, double qx, double qy, double qz, double qw) =>
            new IkPose(new IkVector(positionMeters.X*1000,positionMeters.Z*1000,positionMeters.Y*1000),
                IkMatrix.UnityToFusionRotation(qx,qy,qz,qw));
    }

    public sealed class IkLeg
    {
        public int Channel { get; }
        public IkVector ShaftPointMm { get; }
        public IkVector ShaftAxis { get; }
        public double AxialOffsetMm { get; }
        public IkVector ReferenceRadial { get; }
        public IkVector TopLocalMm { get; }
        public double HornRadiusMm { get; }
        public double RodLengthMm { get; }
        private readonly double[] angleLimits;
        public IkVector CircleCentreMm => ShaftPointMm + ShaftAxis*AxialOffsetMm;
        public IkLeg(int channel, IkVector shaftPointMm, IkVector shaftAxis, double axialOffsetMm,
            IkVector referenceRadial, IkVector topLocalMm, double hornRadiusMm, double rodLengthMm,
            double[] angleLimitsDeg = null)
        {
            if (channel < 0 || channel > 5) throw new IkGeometryException("Channel must be 0..5.");
            IkMath.RequireFinite(axialOffsetMm,"Axial offset");
            IkMath.RequireFinite(hornRadiusMm,"Horn radius"); IkMath.RequireFinite(rodLengthMm,"Rod length");
            if (hornRadiusMm <= 0 || rodLengthMm <= 0) throw new IkGeometryException("Lengths must be positive.");
            Channel = channel; ShaftPointMm = shaftPointMm; ShaftAxis = shaftAxis.Unit;
            AxialOffsetMm = axialOffsetMm;
            ReferenceRadial = (referenceRadial-ShaftAxis*IkVector.Dot(referenceRadial,ShaftAxis)).Unit;
            TopLocalMm = topLocalMm; HornRadiusMm = hornRadiusMm; RodLengthMm = rodLengthMm;
            if (angleLimitsDeg != null)
            {
                if (angleLimitsDeg.Length != 2) throw new IkGeometryException("Angle limits require min,max.");
                foreach (double value in angleLimitsDeg) IkMath.RequireFinite(value,"Angle limit");
                if (angleLimitsDeg[0] >= angleLimitsDeg[1]) throw new IkGeometryException("Angle limits must be ordered.");
                angleLimits = (double[])angleLimitsDeg.Clone();
            }
        }
        public IkVector HornPoint(double angle) => CircleCentreMm +
            (ReferenceRadial*Math.Cos(angle)+IkVector.Cross(ShaftAxis,ReferenceRadial)*Math.Sin(angle))*HornRadiusMm;
        public bool WithinLimits(double angle) => angleLimits == null ||
            (angle*IkMath.Degrees >= angleLimits[0] && angle*IkMath.Degrees <= angleLimits[1]);
        internal bool Candidates(IkVector top, out double plus, out double minus, out double condition, out string error)
        {
            plus = minus = condition = 0; error = null;
            IkVector delta = top-CircleCentreMm;
            double a = IkVector.Dot(delta,ReferenceRadial);
            double b = IkVector.Dot(delta,IkVector.Cross(ShaftAxis,ReferenceRadial));
            double amplitude = Math.Sqrt(a*a+b*b);
            double rhs = (IkVector.Dot(delta,delta)+HornRadiusMm*HornRadiusMm-RodLengthMm*RodLengthMm)/(2*HornRadiusMm);
            if (!IkMath.Finite(amplitude) || !IkMath.Finite(rhs)) error = "Non-finite geometry calculation.";
            else if (amplitude < 1e-10) error = Math.Abs(rhs) < 1e-10 ? "Indeterminate horn angle." : "Unreachable shaft-axis target.";
            if (error != null) return false;
            double ratio = rhs/amplitude;
            if (Math.Abs(ratio) > 1+1e-12) { error = "Rod sphere does not intersect horn circle."; return false; }
            ratio = Math.Max(-1,Math.Min(1,ratio));
            condition = Math.Sqrt(Math.Max(0,1-ratio*ratio));
            if (condition < 1e-6) { error = "Tangent/ill-conditioned individual leg."; return false; }
            double phase = Math.Atan2(b,a), opening = Math.Acos(ratio);
            plus = phase+opening; minus = phase-opening;
            return true;
        }
    }

    public sealed class IkLegSolution
    {
        public readonly int Channel, Branch;
        public readonly double AngleDegrees, NeutralDeltaDegrees, RodResidualMm, ConditionSine;
        public readonly IkVector Horn, Top;
        internal IkLegSolution(int channel, int branch, double angle, double delta,
            double residual, double condition, IkVector horn, IkVector top)
        {
            Channel=channel; Branch=branch; AngleDegrees=angle; NeutralDeltaDegrees=delta;
            RodResidualMm=residual; ConditionSine=condition; Horn=horn; Top=top;
        }
    }

    public sealed class IkResult
    {
        public bool KinematicSolutionFound => Errors.Length == 0 && Legs.Length == 6;
        public bool HardwareOutputEnabled => false;
        public readonly IkLegSolution[] Legs;
        public readonly string[] Errors;
        internal IkResult(List<IkLegSolution> legs, List<string> errors)
        {
            Errors = errors.ToArray();
            Legs = errors.Count == 0 ? legs.ToArray() : Array.Empty<IkLegSolution>();
        }
        public double[] AnglesDegrees
        {
            get
            {
                if (!KinematicSolutionFound) return Array.Empty<double>();
                var angles = new double[6];
                for (int i=0;i<6;++i) angles[i]=Legs[i].AngleDegrees;
                return angles;
            }
        }
    }

    public sealed class RotaryStewartModel
    {
        public IkLeg[] Legs { get; }
        public IkVector NeutralPositionMm { get; }
        public IkMatrix NeutralRotation { get; }
        private readonly double[] neutralAngles = new double[6];
        private readonly int[] branches = new int[6];
        public RotaryStewartModel(IkLeg[] legs, IkVector neutralPositionMm, IkMatrix neutralRotation)
        {
            if (legs == null || legs.Length != 6) throw new IkGeometryException("Exactly six legs required.");
            Legs = new IkLeg[6];
            foreach (IkLeg leg in legs)
            {
                if (leg == null || Legs[leg.Channel] != null) throw new IkGeometryException("Unique channels 0..5 required.");
                Legs[leg.Channel] = leg;
            }
            NeutralPositionMm=neutralPositionMm;
            NeutralRotation=neutralRotation ?? throw new IkGeometryException("Neutral rotation required.");
            for (int i=0;i<6;++i)
            {
                IkLeg leg=Legs[i];
                if (!leg.Candidates(TopPoint(leg,IkPose.Neutral),out double plus,out double minus,out _,out string error))
                    throw new IkGeometryException("Neutral channel "+i+": "+error);
                plus=IkMath.Unwrap(plus,0); minus=IkMath.Unwrap(minus,0);
                bool p=leg.WithinLimits(plus), m=leg.WithinLimits(minus);
                if (!p && !m) throw new IkGeometryException("No neutral branch within channel "+i+" angle limits.");
                if (p && m && Math.Abs(Math.Abs(plus)-Math.Abs(minus)) < 1e-10)
                    throw new IkGeometryException("Ambiguous neutral branch for channel "+i+".");
                branches[i]=p && (!m || Math.Abs(plus)<Math.Abs(minus)) ? 1 : -1;
                neutralAngles[i]=branches[i]==1 ? plus : minus;
            }
        }
        public IkVector TopPoint(IkLeg leg, IkPose pose) => NeutralPositionMm+
            NeutralRotation.Transform(pose.TranslationMm+pose.Rotation.Transform(leg.TopLocalMm));
        public IkResult Solve(IkPose pose, double[] previousAnglesDegrees=null, double? maxStepDegrees=null)
        {
            if (pose == null) throw new IkGeometryException("Pose required.");
            if (previousAnglesDegrees != null && previousAnglesDegrees.Length != 6)
                throw new IkGeometryException("Six previous angles required.");
            if (previousAnglesDegrees != null)
                foreach (double value in previousAnglesDegrees) IkMath.RequireFinite(value,"Previous angle");
            if (maxStepDegrees.HasValue && (!IkMath.Finite(maxStepDegrees.Value) || maxStepDegrees.Value<=0))
                throw new IkGeometryException("Max step must be finite and positive.");
            var rows=new List<IkLegSolution>(6); var errors=new List<string>();
            for (int i=0;i<6;++i)
            {
                IkLeg leg=Legs[i];
                try
                {
                    IkVector top=TopPoint(leg,pose);
                    if (!leg.Candidates(top,out double plus,out double minus,out double condition,out string error))
                        throw new IkGeometryException(error);
                    double previous=previousAnglesDegrees==null ? neutralAngles[i] : previousAnglesDegrees[i]/IkMath.Degrees;
                    double angle=IkMath.Unwrap(branches[i]==1 ? plus : minus,previous);
                    if (!leg.WithinLimits(angle)) throw new IkGeometryException("Locked branch outside angle limits.");
                    if (maxStepDegrees.HasValue && Math.Abs((angle-previous)*IkMath.Degrees)>maxStepDegrees.Value)
                        throw new IkGeometryException("Solution exceeds continuity step.");
                    IkVector horn=leg.HornPoint(angle);
                    double residual=Math.Abs((horn-top).Length-leg.RodLengthMm);
                    if (!IkMath.Finite(residual) || residual>1e-7) throw new IkGeometryException("Rod reconstruction residual too large.");
                    rows.Add(new IkLegSolution(i,branches[i],angle*IkMath.Degrees,
                        (angle-neutralAngles[i])*IkMath.Degrees,residual,condition,horn,top));
                }
                catch (IkGeometryException error) { errors.Add("Channel "+i+": "+error.Message); }
            }
            return new IkResult(rows,errors);
        }
    }
}
