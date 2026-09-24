#ifndef OCEAN_MOTION_SP06_MOTION_PROTOCOL_H
#define OCEAN_MOTION_SP06_MOTION_PROTOCOL_H

#include <errno.h>
#include <math.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

struct MotionPosePacket
{
    uint32_t transportSequence;
    uint64_t sourceSequence;
    float heaveMeters;
    float pitchDegrees;
    float yawDegrees;
    float rollDegrees;
};

enum class MotionParseResult
{
    Ok,
    InvalidFormat,
    NonFinite,
    OutOfRange
};

inline MotionParseResult parseMotionPosePacket(
    const char* message,
    MotionPosePacket* packet)
{
    if (message == nullptr || packet == nullptr ||
        strncmp(message, "OM1,POSE,", 9) != 0)
    {
        return MotionParseResult::InvalidFormat;
    }

    const char* cursor = message + 9;
    char* end = nullptr;

    if (*cursor < '0' || *cursor > '9')
        return MotionParseResult::InvalidFormat;
    errno = 0;
    unsigned long long transport = strtoull(cursor, &end, 10);
    if (end == cursor || *end != ',' ||
        errno == ERANGE || transport == 0 || transport > UINT32_MAX)
        return MotionParseResult::InvalidFormat;

    cursor = end + 1;
    if (*cursor < '0' || *cursor > '9')
        return MotionParseResult::InvalidFormat;
    errno = 0;
    unsigned long long source = strtoull(cursor, &end, 10);
    if (end == cursor || *end != ',' || errno == ERANGE || source == 0)
        return MotionParseResult::InvalidFormat;

    cursor = end + 1;
    float heave = strtof(cursor, &end);
    if (end == cursor || *end != ',')
        return MotionParseResult::InvalidFormat;

    cursor = end + 1;
    float pitch = strtof(cursor, &end);
    if (end == cursor || *end != ',')
        return MotionParseResult::InvalidFormat;

    cursor = end + 1;
    float yaw = strtof(cursor, &end);
    if (end == cursor || *end != ',')
        return MotionParseResult::InvalidFormat;

    cursor = end + 1;
    float roll = strtof(cursor, &end);
    if (end == cursor || *end != '\0')
        return MotionParseResult::InvalidFormat;

    if (!isfinite(heave) || !isfinite(pitch) ||
        !isfinite(yaw) || !isfinite(roll))
    {
        return MotionParseResult::NonFinite;
    }

    if (fabsf(heave) > 0.25f ||
        fabsf(pitch) > 70.0f ||
        fabsf(yaw) > 5.0f ||
        fabsf(roll) > 70.0f)
        return MotionParseResult::OutOfRange;

    packet->transportSequence = static_cast<uint32_t>(transport);
    packet->sourceSequence = static_cast<uint64_t>(source);
    packet->heaveMeters = heave;
    packet->pitchDegrees = pitch;
    packet->yawDegrees = yaw;
    packet->rollDegrees = roll;
    return MotionParseResult::Ok;
}

#endif
