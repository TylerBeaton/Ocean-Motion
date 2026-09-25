#include <cassert>
#include <cstdint>

#include "../MotionHardware.h"

int main()
{
    MotionServoTarget pitchMinimum = MotionHardware::pitchTarget(-70.0f);
    assert(pitchMinimum.channel == 0u);
    assert(pitchMinimum.pulse == 243u);

    MotionServoTarget pitchCenter = MotionHardware::pitchTarget(0.0f);
    assert(pitchCenter.channel == 0u);
    assert(pitchCenter.pulse == 315u);

    MotionServoTarget pitchMaximum = MotionHardware::pitchTarget(70.0f);
    assert(pitchMaximum.channel == 0u);
    assert(pitchMaximum.pulse == 379u);

    assert(MotionHardware::pitchTarget(-35.0f).pulse == 279u);
    assert(MotionHardware::pitchTarget(35.0f).pulse == 351u);
    assert(MotionHardware::pitchTarget(-100.0f).pulse == 243u);
    assert(MotionHardware::pitchTarget(100.0f).pulse == 379u);

    MotionServoTarget rollMinimum = MotionHardware::rollTarget(-70.0f);
    assert(rollMinimum.channel == 1u);
    assert(rollMinimum.pulse == 235u);

    MotionServoTarget rollCenter = MotionHardware::rollTarget(0.0f);
    assert(rollCenter.channel == 1u);
    assert(rollCenter.pulse == 299u);

    MotionServoTarget rollMaximum = MotionHardware::rollTarget(70.0f);
    assert(rollMaximum.channel == 1u);
    assert(rollMaximum.pulse == 371u);

    MotionPosePacket packet = {1u, 1u, 0.0f, -70.0f, 0.0f, 70.0f};
    MotionHardwareTargets targets = MotionHardware::targetsForPose(packet);
    assert(targets.pitch.channel == 0u);
    assert(targets.pitch.pulse == 243u);
    assert(targets.roll.channel == 1u);
    assert(targets.roll.pulse == 371u);

    MotionHardwareTargets neutral = MotionHardware::neutralTargets();
    assert(neutral.pitch.channel == 0u);
    assert(neutral.pitch.pulse == 315u);
    assert(neutral.roll.channel == 1u);
    assert(neutral.roll.pulse == 299u);
    return 0;
}
