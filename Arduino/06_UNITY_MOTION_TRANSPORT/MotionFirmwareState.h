#ifndef OCEAN_MOTION_SP06_MOTION_FIRMWARE_STATE_H
#define OCEAN_MOTION_SP06_MOTION_FIRMWARE_STATE_H

#include "MotionReceiverState.h"

enum class MotionFirmwareResult
{
    Accepted,
    Duplicate,
    HandshakeRequired,
    SequenceError
};

class MotionFirmwareState
{
public:
    MotionFirmwareState()
        : handshakeComplete_(false), streamActive_(false), receiver_() {}

    void beginSession()
    {
        receiver_.reset();
        handshakeComplete_ = true;
        streamActive_ = false;
    }

    void failClosed()
    {
        receiver_.reset();
        handshakeComplete_ = false;
        streamActive_ = false;
    }

    void stop()
    {
        streamActive_ = false;
    }

    MotionFirmwareResult acceptPose(const MotionPosePacket& packet)
    {
        if (!handshakeComplete_)
            return MotionFirmwareResult::HandshakeRequired;

        MotionAcceptanceResult result = receiver_.accept(packet);
        if (result == MotionAcceptanceResult::Accepted)
        {
            streamActive_ = true;
            return MotionFirmwareResult::Accepted;
        }
        if (result == MotionAcceptanceResult::Duplicate)
            return MotionFirmwareResult::Duplicate;

        failClosed();
        return MotionFirmwareResult::SequenceError;
    }

    bool isHandshakeComplete() const
    {
        return handshakeComplete_;
    }

    bool isStreamActive() const
    {
        return streamActive_;
    }

private:
    bool handshakeComplete_;
    bool streamActive_;
    MotionReceiverState receiver_;
};

#endif