#ifndef OCEAN_MOTION_SP06_MOTION_RECEIVER_STATE_H
#define OCEAN_MOTION_SP06_MOTION_RECEIVER_STATE_H

#include "MotionProtocol.h"

enum class MotionAcceptanceResult
{
    Accepted,
    Duplicate,
    SequenceConflict,
    OutOfOrder
};

class MotionReceiverState
{
public:
    MotionReceiverState() : hasPacket_(false), latest_() {}

    void reset()
    {
        hasPacket_ = false;
        latest_ = MotionPosePacket{};
    }

    MotionAcceptanceResult accept(const MotionPosePacket& packet)
    {
        if (!hasPacket_ || packet.transportSequence > latest_.transportSequence)
        {
            latest_ = packet;
            hasPacket_ = true;
            return MotionAcceptanceResult::Accepted;
        }

        if (packet.transportSequence < latest_.transportSequence)
            return MotionAcceptanceResult::OutOfOrder;

        return packetsEqual(packet, latest_)
            ? MotionAcceptanceResult::Duplicate
            : MotionAcceptanceResult::SequenceConflict;
    }

    const MotionPosePacket& latest() const
    {
        return latest_;
    }

private:
    static bool packetsEqual(
        const MotionPosePacket& left,
        const MotionPosePacket& right)
    {
        return left.transportSequence == right.transportSequence &&
            left.sourceSequence == right.sourceSequence &&
            left.heaveMeters == right.heaveMeters &&
            left.pitchDegrees == right.pitchDegrees &&
            left.yawDegrees == right.yawDegrees &&
            left.rollDegrees == right.rollDegrees;
    }

    bool hasPacket_;
    MotionPosePacket latest_;
};

#endif