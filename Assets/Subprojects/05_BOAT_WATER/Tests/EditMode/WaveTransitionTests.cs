using System;
using NUnit.Framework;
using UnityEngine;

namespace OceanMotion.Subproject05.Tests
{
    public class WaveTransitionTests
    {
        private GameObject owner;
        private WaveField field;
        [SetUp] public void Setup() { owner=new GameObject("Wave transition test");field=owner.AddComponent<WaveField>(); }
        [TearDown] public void Cleanup() { UnityEngine.Object.DestroyImmediate(owner); }
        private static float Height(WaveComponent a,WaveComponent b,Vector3 p,float time)
            =>WaveSampler.SampleHeight(new Vector2(p.x,p.z),time,0,a,b);

        [TestCase(1f)] [TestCase(2f)] [TestCase(3f)]
        public void CalmToBow_IsLinearAtEverySamplePoint(float duration)
        {
            field.SetPreset(WavePreset.Calm);
            field.SetPreset(WavePreset.Bow,duration,1000);
            foreach(var p in new[]{Vector3.zero,new Vector3(3,0,-2),new Vector3(-8,0,5)}) {
                Assert.That(field.SampleHeight(p,1000),Is.EqualTo(0).Within(1e-6));
                float t=1000+duration/2;
                Assert.That(field.SampleHeight(p,t),Is.EqualTo(.5f*Height(field.PrimaryWave,field.SecondaryWave,p,t)).Within(1e-6));
                t=1000+duration;
                Assert.That(field.SampleHeight(p,t),Is.EqualTo(Height(field.PrimaryWave,field.SecondaryWave,p,t)).Within(1e-6));
            }
        }
        [Test]
        public void EveryPresetPair_PreservesPhaseAndBothEndpoints()
        {
            foreach(WavePreset from in Enum.GetValues(typeof(WavePreset)))foreach(WavePreset to in Enum.GetValues(typeof(WavePreset))) {
                field.SetPreset(from);var a=field.PrimaryWave;var b=field.SecondaryWave;
                field.SetPreset(to,2,1000);var c=field.PrimaryWave;var d=field.SecondaryWave;
                var p=new Vector3(3,0,-2);
                Assert.That(field.SampleHeight(p,1000),Is.EqualTo(Height(a,b,p,1000)).Within(1e-6));
                Assert.That(field.SampleHeight(p,1001),Is.EqualTo(.5f*(Height(a,b,p,1001)+Height(c,d,p,1001))).Within(1e-6));
                Assert.That(field.SampleHeight(p,1002),Is.EqualTo(Height(c,d,p,1002)).Within(1e-6));
            }
        }
        [Test]
        public void InterruptedTransition_RetargetsWithoutAHeightJump()
        {
            field.SetPreset(WavePreset.Bow);
            field.SetPreset(WavePreset.Side,2,1000);
            var p=new Vector3(4,0,7);float before=field.SampleHeight(p,1001);
            field.SetPreset(WavePreset.Crossed,3,1001);
            Assert.That(field.SampleHeight(p,1001),Is.EqualTo(before).Within(1e-6));
            Assert.That(field.SampleHeight(p,1004),Is.EqualTo(Height(field.PrimaryWave,field.SecondaryWave,p,1004)).Within(1e-6));
        }
        [Test]
        public void RepeatedAndRapidSelections_AreContinuousAndDoNotRestartSamePreset()
        {
            field.SetPreset(WavePreset.Calm);field.SetPreset(WavePreset.Bow,2,1000);
            field.SetPreset(WavePreset.Bow,2,1001);
            Assert.That(field.TransitionProgressAt(1002),Is.EqualTo(1));
            var p=new Vector3(3,0,2);
            for(int i=0;i<1000;i++) {
                float time=1003+i*.01f,before=field.SampleHeight(p,time);
                field.SetPreset((WavePreset)(i%4),2,time);
                Assert.That(field.SampleHeight(p,time),Is.EqualTo(before).Within(1e-5));
            }
        }
        [Test]
        public void LegacyInstantAndInvalidRequests_KeepTheirContracts()
        {
            field.SetPreset(WavePreset.Side);
            var p=new Vector3(2,0,3);float before=field.SampleHeight(p,1000);
            Assert.That(before,Is.EqualTo(Height(field.PrimaryWave,field.SecondaryWave,p,1000)).Within(1e-6));
            foreach(float bad in new[]{-1,31,float.NaN,float.PositiveInfinity})
                Assert.Throws<ArgumentOutOfRangeException>(()=>field.SetPreset(WavePreset.Calm,bad,1000));
            Assert.Throws<ArgumentOutOfRangeException>(()=>field.SetPreset((WavePreset)99,2,1000));
            Assert.That(field.SampleHeight(p,1000),Is.EqualTo(before));
            field.SetPreset(WavePreset.Calm);Assert.That(field.SampleHeight(p,1000),Is.Zero);
        }
    }
}
