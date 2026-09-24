#ifndef OCEAN_MOTION_SP06_MOTION_HARDWARE_H
#define OCEAN_MOTION_SP06_MOTION_HARDWARE_H

#include <stdint.h>

#include "MotionProtocol.h"

struct MotionServoCalibration
{
    uint8_t channel;
    float minimumCommandDegrees;
    float centerCommandDegrees;
    float maximumCommandDegrees;
    uint16_t minimumPulse;
    uint16_t centerPulse;
    uint16_t maximumPulse;
};

struct MotionServoTarget
{
    uint8_t channel;
    uint16_t pulse;
};

struct MotionHardwareTargets
{
    MotionServoTarget pitch;
    MotionServoTarget roll;
};

namespace MotionHardware
{
inline MotionServoCalibration pitchCalibration()
{
    MotionServoCalibration calibration = {
        0u,
        -70.0f,
        0.0f,
        70.0f,
        235u,
        307u,
        379u};
    return calibration;
}

inline MotionServoCalibration rollCalibration()
{
    MotionServoCalibration calibration = {
        1u,
        -70.0f,
        0.0f,
        70.0f,
        235u,
        307u,
        379u};
    return calibration;
}

inline float clampCommand(
    float commandDegrees,
    const MotionServoCalibration& calibration)
{
    if (commandDegrees < calibration.minimumCommandDegrees)
        return calibration.minimumCommandDegrees;
    if (commandDegrees > calibration.maximumCommandDegrees)
        return calibration.maximumCommandDegrees;
    return commandDegrees;
}

inline uint16_t pulseForCommand(
    float commandDegrees,
    const MotionServoCalibration& calibration)
{
    const float clamped = clampCommand(commandDegrees, calibration);
    float pulse = static_cast<float>(calibration.centerPulse);

    if (clamped < calibration.centerCommandDegrees)
    {
        const float fraction =
            (clamped - calibration.minimumCommandDegrees) /
            (calibration.centerCommandDegrees -
                calibration.minimumCommandDegrees);
        pulse = static_cast<float>(calibration.minimumPulse) +
            fraction * static_cast<float>(
                calibration.centerPulse - calibration.minimumPulse);
    }
    else if (clamped > calibration.centerCommandDegrees)
    {
        const float fraction =
            (clamped - calibration.centerCommandDegrees) /
            (calibration.maximumCommandDegrees -
                calibration.centerCommandDegrees);
        pulse = static_cast<float>(calibration.centerPulse) +
            fraction * static_cast<float>(
                calibration.maximumPulse - calibration.centerPulse);
    }

    return static_cast<uint16_t>(pulse + 0.5f);
}

inline MotionServoTarget targetForCommand(
    float commandDegrees,
    const MotionServoCalibration& calibration)
{
    MotionServoTarget target = {
        calibration.channel,
        pulseForCommand(commandDegrees, calibration)};
    return target;
}

inline MotionServoTarget pitchTarget(float pitchDegrees)
{
    return targetForCommand(pitchDegrees, pitchCalibration());
}

inline MotionServoTarget rollTarget(float rollDegrees)
{
    return targetForCommand(rollDegrees, rollCalibration());
}

inline MotionHardwareTargets targetsForPose(const MotionPosePacket& packet)
{
    MotionHardwareTargets targets = {
        pitchTarget(packet.pitchDegrees),
        rollTarget(packet.rollDegrees)};
    return targets;
}

inline MotionHardwareTargets neutralTargets()
{
    MotionHardwareTargets targets = {
        pitchTarget(0.0f),
        rollTarget(0.0f)};
    return targets;
}

void begin();
void beginSession();
void applyPose(const MotionPosePacket& packet, unsigned long nowMs);
void stop(const char* reason);
void fault(const char* reason);
}

#endif
