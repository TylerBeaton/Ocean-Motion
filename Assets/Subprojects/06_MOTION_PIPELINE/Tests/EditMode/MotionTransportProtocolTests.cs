using NUnit.Framework;
using UnityEngine;

namespace OceanMotion.Subproject06.Tests
{
    public class MotionTransportProtocolTests
    {
        [Test]
        public void TryFormatPose_FormatsVersionedInvariantPacket()
        {
            var command = new MotionPoseCommand(
                new Vector3(0f, 0.125f, 0f),
                new Vector3(-1.25f, 2.5f, -3.75f),
                42,
                1.5,
                true);

            bool formatted = MotionTransportProtocol.TryFormatPose(
                command,
                7,
                out string packet);

            Assert.That(formatted, Is.True);
            Assert.That(
                packet,
                Is.EqualTo("OM1,POSE,7,42,0.12500,-1.25000,2.50000,-3.75000"));
        }

        [Test]
        public void TryFormatPose_RejectsInvalidCommand()
        {
            bool formatted = MotionTransportProtocol.TryFormatPose(
                default,
                1,
                out string packet);

            Assert.That(formatted, Is.False);
            Assert.That(packet, Is.Null);
        }

        [Test]
        public void TryFormatPose_RejectsNonFiniteValues()
        {
            var command = new MotionPoseCommand(
                new Vector3(0f, float.NaN, 0f),
                Vector3.zero,
                1,
                0d,
                true);

            bool formatted = MotionTransportProtocol.TryFormatPose(
                command,
                1,
                out string packet);

            Assert.That(formatted, Is.False);
            Assert.That(packet, Is.Null);
        }

        [TestCase(0u, 1ul, 0f, 0f, 0f, 0f)]
        [TestCase(1u, 0ul, 0f, 0f, 0f, 0f)]
        [TestCase(1u, 1ul, 0.25001f, 0f, 0f, 0f)]
        [TestCase(1u, 1ul, -0.25001f, 0f, 0f, 0f)]
        [TestCase(1u, 1ul, 0f, 70.001f, 0f, 0f)]
        [TestCase(1u, 1ul, 0f, 0f, -5.001f, 0f)]
        [TestCase(1u, 1ul, 0f, 0f, 0f, -70.001f)]
        public void TryFormatPose_RejectsPacketsOutsideFirmwareContract(
            uint transportSequence,
            ulong sourceSequence,
            float heave,
            float pitch,
            float yaw,
            float roll)
        {
            var command = new MotionPoseCommand(
                new Vector3(0f, heave, 0f),
                new Vector3(pitch, yaw, roll),
                sourceSequence,
                0d,
                true);

            bool formatted = MotionTransportProtocol.TryFormatPose(
                command,
                transportSequence,
                out string packet);

            Assert.That(formatted, Is.False);
            Assert.That(packet, Is.Null);
        }

        [Test]
        public void TryFormatPose_AcceptsExactFirmwareEnvelope()
        {
            var command = new MotionPoseCommand(
                new Vector3(0f, -0.25f, 0f),
                new Vector3(70f, -5f, -70f),
                1,
                0d,
                true);

            Assert.That(
                MotionTransportProtocol.TryFormatPose(command, 1, out _),
                Is.True);
        }

        [Test]
        public void TryParseAcknowledgement_ReturnsTransportSequence()
        {
            bool parsed = MotionTransportProtocol.TryParseAcknowledgement(
                "OM1,ACK,27",
                out uint sequence);

            Assert.That(parsed, Is.True);
            Assert.That(sequence, Is.EqualTo(27u));
        }


    }
}
