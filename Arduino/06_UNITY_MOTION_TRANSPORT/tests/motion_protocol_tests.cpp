#include <cassert>
#include <cmath>
#include <cstdint>

#include "../MotionProtocol.h"
#include "../MotionReceiverState.h"
#include "../MotionFirmwareState.h"

int main()
{
    MotionPosePacket packet = {};
    MotionParseResult result = parseMotionPosePacket(
        "OM1,POSE,7,42,0.12500,-1.25000,2.50000,-3.75000",
        &packet);

    assert(result == MotionParseResult::Ok);
    assert(packet.transportSequence == 7u);
    assert(packet.sourceSequence == 42u);
    assert(std::fabs(packet.heaveMeters - 0.125f) < 0.000001f);
    assert(std::fabs(packet.pitchDegrees + 1.25f) < 0.000001f);
    assert(std::fabs(packet.yawDegrees - 2.5f) < 0.000001f);
    assert(std::fabs(packet.rollDegrees + 3.75f) < 0.000001f);

    assert(parseMotionPosePacket(
        "OM1,POSE,8,43,nan,0,0,0",
        &packet) == MotionParseResult::NonFinite);
    assert(parseMotionPosePacket(
        "OM1,POSE,9,44,0.25001,0,0,0",
        &packet) == MotionParseResult::OutOfRange);
    assert(parseMotionPosePacket(
        "OM1,POSE,10,45,0,10.001,0,0",
        &packet) == MotionParseResult::OutOfRange);
    assert(parseMotionPosePacket(
        "OM1,POSE,11,46,0,0,-5.001,0",
        &packet) == MotionParseResult::OutOfRange);
    assert(parseMotionPosePacket(
        "OM1,POSE,12,47,0,0,0,-10.001",
        &packet) == MotionParseResult::OutOfRange);
    assert(parseMotionPosePacket(
        "OM1,POSE,0,48,0,0,0,0",
        &packet) == MotionParseResult::InvalidFormat);
    assert(parseMotionPosePacket(
        "OM1,POSE,13,0,0,0,0,0",
        &packet) == MotionParseResult::InvalidFormat);
    assert(parseMotionPosePacket(
        "OM1,POSE,14,18446744073709551616,0,0,0,0",
        &packet) == MotionParseResult::InvalidFormat);
    assert(parseMotionPosePacket(
        "OM1,POSE,15,-1,0,0,0,0",
        &packet) == MotionParseResult::InvalidFormat);

    MotionReceiverState receiver;
    MotionPosePacket first = {2u, 10u, 0.1f, 1.0f, 2.0f, 3.0f};
    assert(receiver.accept(first) == MotionAcceptanceResult::Accepted);
    assert(receiver.accept(first) == MotionAcceptanceResult::Duplicate);

    MotionPosePacket conflictingSource = first;
    conflictingSource.sourceSequence = 11u;
    assert(receiver.accept(conflictingSource) ==
        MotionAcceptanceResult::SequenceConflict);

    MotionPosePacket conflictingPose = first;
    conflictingPose.heaveMeters = 0.2f;
    assert(receiver.accept(conflictingPose) ==
        MotionAcceptanceResult::SequenceConflict);

    MotionPosePacket older = first;
    older.transportSequence = 1u;
    assert(receiver.accept(older) == MotionAcceptanceResult::OutOfOrder);

    receiver.reset();
    assert(receiver.accept(first) == MotionAcceptanceResult::Accepted);

    MotionFirmwareState firmware;
    assert(firmware.acceptPose(first) ==
        MotionFirmwareResult::HandshakeRequired);
    assert(!firmware.isHandshakeComplete());
    assert(!firmware.isStreamActive());

    firmware.beginSession();
    assert(firmware.isHandshakeComplete());
    assert(firmware.acceptPose(first) == MotionFirmwareResult::Accepted);
    assert(firmware.isStreamActive());

    assert(firmware.acceptPose(conflictingPose) ==
        MotionFirmwareResult::SequenceError);
    assert(!firmware.isHandshakeComplete());
    assert(!firmware.isStreamActive());
    assert(firmware.acceptPose(first) ==
        MotionFirmwareResult::HandshakeRequired);

    firmware.beginSession();
    assert(firmware.acceptPose(first) == MotionFirmwareResult::Accepted);
    firmware.failClosed();
    assert(!firmware.isHandshakeComplete());
    assert(!firmware.isStreamActive());

    firmware.beginSession();
    assert(firmware.acceptPose(first) == MotionFirmwareResult::Accepted);
    firmware.stop();
    assert(firmware.isHandshakeComplete());
    assert(!firmware.isStreamActive());
    return 0;
}
