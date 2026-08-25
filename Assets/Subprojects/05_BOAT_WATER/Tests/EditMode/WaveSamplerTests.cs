using NUnit.Framework;
using UnityEngine;

namespace OceanMotion.Subproject05.Tests
{
    public class WaveSamplerTests
    {
        [Test]
        public void SampleHeight_ZeroAmplitude_ReturnsBaseHeight()
        {
            var wave = new WaveComponent(
                amplitude: 0f,
                wavelength: 8f,
                direction: Vector2.right,
                speed: 1f,
                phaseOffsetRadians: 0f
            );

            float height = WaveSampler.SampleHeight(
                new Vector2(3f, -2f),
                time: 4f,
                baseHeight: 1.25f,
                wave
            );

            Assert.That(height, Is.EqualTo(1.25f).Within(0.00001f));
        }

        [Test]
        public void SampleHeight_QuarterWavelength_ReturnsWaveCrest()
        {
            var wave = new WaveComponent(
                amplitude: 0.2f,
                wavelength: 8f,
                direction: Vector2.right,
                speed: 1f,
                phaseOffsetRadians: 0f
            );

            float height = WaveSampler.SampleHeight(
                new Vector2(2f, 0f),
                time: 0f,
                baseHeight: 0.5f,
                wave
            );

            Assert.That(height, Is.EqualTo(0.7f).Within(0.00001f));
        }

        [Test]
        public void SampleHeight_TwoComponents_AddBothDisplacements()
        {
            var primary = new WaveComponent(
                amplitude: 0.2f,
                wavelength: 8f,
                direction: Vector2.right,
                speed: 1f,
                phaseOffsetRadians: 0f
            );
            var secondary = new WaveComponent(
                amplitude: 0.1f,
                wavelength: 8f,
                direction: Vector2.up,
                speed: 0.5f,
                phaseOffsetRadians: 0f
            );

            float height = WaveSampler.SampleHeight(
                new Vector2(2f, 2f),
                time: 0f,
                baseHeight: 0.5f,
                primary,
                secondary
            );

            Assert.That(height, Is.EqualTo(0.8f).Within(0.00001f));
        }

        [Test]
        public void SampleHeight_AfterTravelTime_AdvancesByConfiguredSpeed()
        {
            var wave = new WaveComponent(
                amplitude: 0.2f,
                wavelength: 8f,
                direction: Vector2.right,
                speed: 2f,
                phaseOffsetRadians: 0f
            );

            float height = WaveSampler.SampleHeight(
                Vector2.zero,
                time: 1f,
                baseHeight: 0f,
                wave
            );

            Assert.That(height, Is.EqualTo(-0.2f).Within(0.00001f));
        }

        [Test]
        public void WaveField_SampleHeight_UsesBothConfiguredComponents()
        {
            var gameObject = new GameObject("Wave Field Test");

            try
            {
                WaveField field = gameObject.AddComponent<WaveField>();
                field.BaseHeightOffset = 0.5f;
                field.PrimaryWave = new WaveComponent(
                    0.2f,
                    8f,
                    Vector2.right,
                    1f,
                    0f
                );
                field.SecondaryWave = new WaveComponent(
                    0.1f,
                    8f,
                    Vector2.up,
                    0.5f,
                    0f
                );

                float height = field.SampleHeight(
                    new Vector3(2f, 10f, 2f),
                    simulationTime: 0f
                );

                Assert.That(height, Is.EqualTo(0.8f).Within(0.00001f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void WaveSurface_UpdateSurface_MatchesSharedWaveFieldAtEveryVertex()
        {
            var gameObject = new GameObject("Wave Surface Test");

            try
            {
                WaveField field = gameObject.AddComponent<WaveField>();
                field.PrimaryWave = new WaveComponent(
                    0.2f,
                    8f,
                    Vector2.right,
                    1f,
                    0f
                );
                field.SecondaryWave = new WaveComponent(
                    0.05f,
                    6f,
                    Vector2.up,
                    0.5f,
                    0.25f
                );

                WaveSurface surface = gameObject.AddComponent<WaveSurface>();
                surface.Field = field;
                surface.Size = new Vector2(4f, 4f);
                surface.Resolution = new Vector2Int(3, 3);
                surface.RebuildMesh();
                surface.UpdateSurface(simulationTime: 1.5f);

                Mesh mesh = gameObject.GetComponent<MeshFilter>().sharedMesh;

                foreach (Vector3 localVertex in mesh.vertices)
                {
                    Vector3 worldVertex = gameObject.transform.TransformPoint(localVertex);
                    float expectedHeight = field.SampleHeight(
                        worldVertex,
                        simulationTime: 1.5f
                    );

                    Assert.That(
                        worldVertex.y,
                        Is.EqualTo(expectedHeight).Within(0.00001f)
                    );
                }
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
