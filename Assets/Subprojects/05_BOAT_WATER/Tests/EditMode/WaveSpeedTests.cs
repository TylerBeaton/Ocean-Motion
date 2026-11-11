using System;
using NUnit.Framework;
using UnityEngine;

namespace OceanMotion.Subproject05.Tests
{
    public class WaveSpeedTests
    {
        private GameObject owner;
        private WaveField field;
        [SetUp] public void Setup() { owner=new GameObject("Wave speed test");field=owner.AddComponent<WaveField>();field.SetPreset(WavePreset.Crossed); }
        [TearDown] public void Cleanup() { UnityEngine.Object.DestroyImmediate(owner); }
        private static readonly Vector3[] Points={Vector3.zero,new Vector3(3,0,-2),new Vector3(-7,0,4)};
        private static float Height(Vector3 p,float time,WaveComponent a,WaveComponent b)
            =>WaveSampler.SampleHeight(new Vector2(p.x,p.z),time,0,a,b);

        [Test]
        public void DefaultOne_PreservesTheExistingSampler()
        {
            foreach(float time in new[]{0,1,1000})foreach(var p in Points) {
                Assert.That(field.WaveTimeAt(time),Is.EqualTo(time));
                Assert.That(field.SampleHeight(p,time),Is.EqualTo(Height(p,time,field.PrimaryWave,field.SecondaryWave)).Within(1e-6));
            }
        }
        [Test]
        public void AllQuarterSteps_SetTheTravelRateWithoutChangingAmplitudes()
        {
            float amplitude=field.PrimaryWave.amplitude;
            for(int step=0;step<=16;step++) {
                float time=1000+step*10,before=field.WaveTimeAt(time),speed=step*.25f;
                field.SetSpeedMultiplier(speed,time);
                Assert.That(field.WaveTimeAt(time),Is.EqualTo(before));
                Assert.That(field.WaveTimeAt(time+1),Is.EqualTo(before+speed));
                Assert.That(field.PrimaryWave.amplitude,Is.EqualTo(amplitude));
                foreach(var p in Points)Assert.That(field.SampleHeight(p,time+1),Is.EqualTo(Height(p,before+speed,field.PrimaryWave,field.SecondaryWave)).Within(1e-6));
            }
        }
        [Test]
        public void FreezeAndResume_PreserveSurfacePhaseAndGlobalPhysicsClock()
        {
            float timeScale=Time.timeScale,step=Time.fixedDeltaTime;
            field.SetSpeedMultiplier(4,1000);
            field.SetSpeedMultiplier(0,1002);
            Assert.That(field.WaveTimeAt(1100),Is.EqualTo(1008));
            foreach(var p in Points)Assert.That(field.SampleHeight(p,1002),Is.EqualTo(field.SampleHeight(p,1100)));
            field.SetSpeedMultiplier(.25f,1100);
            Assert.That(field.WaveTimeAt(1100),Is.EqualTo(1008));
            Assert.That(field.WaveTimeAt(1104),Is.EqualTo(1009));
            Assert.That(Time.timeScale,Is.EqualTo(timeScale));Assert.That(Time.fixedDeltaTime,Is.EqualTo(step));
        }
        [Test]
        public void SpeedChangeDuringPresetFade_IsContinuousAndDoesNotRetimeTheFade()
        {
            field.SetPreset(WavePreset.Bow);var a=field.PrimaryWave;var b=field.SecondaryWave;
            field.SetPreset(WavePreset.Side,2,1000);
            var before=new float[Points.Length];for(int i=0;i<Points.Length;i++)before[i]=field.SampleHeight(Points[i],1001);
            field.SetSpeedMultiplier(4,1001);
            for(int i=0;i<Points.Length;i++)Assert.That(field.SampleHeight(Points[i],1001),Is.EqualTo(before[i]).Within(1e-6));
            Assert.That(field.TransitionProgressAt(1001.5f),Is.EqualTo(.75f));
            foreach(var p in Points) {
                float expected=.25f*Height(p,1003,a,b)+.75f*Height(p,1003,field.PrimaryWave,field.SecondaryWave);
                Assert.That(field.SampleHeight(p,1001.5f),Is.EqualTo(expected).Within(1e-6));
            }
            field.SetSpeedMultiplier(0,1001.5f);
            Assert.That(field.TransitionProgressAt(1002),Is.EqualTo(1));
            foreach(var p in Points)Assert.That(field.SampleHeight(p,1002),Is.EqualTo(Height(p,1003,field.PrimaryWave,field.SecondaryWave)).Within(1e-6));
        }
        [Test]
        public void InvalidRatesOrClocks_DoNotMutateTheField()
        {
            float before=field.SampleHeight(Points[1],1000);
            foreach(float bad in new[]{-.25f,4.25f,1.1f,float.NaN,float.PositiveInfinity})
                Assert.Throws<ArgumentOutOfRangeException>(()=>field.SetSpeedMultiplier(bad,1000));
            foreach(float bad in new[]{-1,float.NaN,float.PositiveInfinity})
                Assert.Throws<ArgumentOutOfRangeException>(()=>field.SetSpeedMultiplier(2,bad));
            Assert.That(field.SpeedMultiplier,Is.EqualTo(1));
            Assert.That(field.SampleHeight(Points[1],1000),Is.EqualTo(before));
        }
    }
}
