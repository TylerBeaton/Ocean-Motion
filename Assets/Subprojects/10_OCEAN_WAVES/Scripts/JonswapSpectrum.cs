using System;
using OceanMotion.Subproject05;
using UnityEngine;

namespace OceanMotion.Subproject10
{
    internal static class JonswapSpectrum
    {
        private const int ComponentCount = 32;
        private const double Gravity = 9.81;
        private const double PeakEnhancement = 3.3;
        private const double MinimumFrequencyRatio = 0.55;
        private const double MaximumFrequencyRatio = 2.15;
        private const double DirectionHalfWidthDegrees = 35.0;

        public static WaveComponent[] CreateWaves(
            float significantWaveHeight, float peakPeriod, float headingDegrees, int seed)
        {
            var random = new System.Random(seed);
            var waves = new WaveComponent[ComponentCount];
            double binWidth = (MaximumFrequencyRatio - MinimumFrequencyRatio) / ComponentCount;
            double totalWeight = 0.0;

            for (int index = 0; index < waves.Length; index++)
            {
                // Jitter within each frequency bin to avoid evenly spaced, recurring beat patterns.
                double frequencyRatio = MinimumFrequencyRatio + (index + random.NextDouble()) * binWidth;
                double weight = RelativeDensity(frequencyRatio) * binWidth;
                totalWeight += weight;

                double angularFrequency = 2.0 * Math.PI * frequencyRatio / peakPeriod;
                double waveNumber = angularFrequency * angularFrequency / Gravity;
                double azimuth = (headingDegrees + (2.0 * random.NextDouble() - 1.0) *
                    DirectionHalfWidthDegrees) * Math.PI / 180.0;

                waves[index] = new WaveComponent(
                    amplitude: (float)Math.Sqrt(2.0 * weight),
                    wavelength: (float)(2.0 * Math.PI / waveNumber),
                    direction: new Vector2((float)Math.Sin(azimuth), (float)Math.Cos(azimuth)),
                    speed: (float)(angularFrequency / waveNumber),
                    phaseOffsetRadians: (float)(2.0 * Math.PI * random.NextDouble())
                );
            }

            // Hm0 = 4 * sqrt(variance), and a sinusoid contributes amplitude squared / 2.
            // Normalize the retained frequency band to the requested variance.
            float scale = (float)(significantWaveHeight / (4.0 * Math.Sqrt(totalWeight)));
            for (int index = 0; index < waves.Length; index++)
            {
                waves[index].amplitude *= scale;
            }

            return waves;
        }

        private static double RelativeDensity(double frequencyRatio)
        {
            double width = frequencyRatio <= 1.0 ? 0.07 : 0.09;
            double offset = (frequencyRatio - 1.0) / width;
            double peak = Math.Pow(PeakEnhancement, Math.Exp(-0.5 * offset * offset));
            return Math.Pow(frequencyRatio, -5.0) *
                Math.Exp(-1.25 * Math.Pow(frequencyRatio, -4.0)) * peak;
        }
    }
}
