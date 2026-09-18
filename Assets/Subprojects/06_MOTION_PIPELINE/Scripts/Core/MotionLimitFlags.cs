using System;

namespace OceanMotion.Subproject06
{
    [Flags]
    public enum MotionLimitFlags
    {
        None = 0,
        TranslationClamp = 1 << 0,
        RotationClamp = 1 << 1,
        TranslationVelocityLimit = 1 << 2,
        RotationVelocityLimit = 1 << 3,
        TranslationAccelerationLimit = 1 << 4,
        RotationAccelerationLimit = 1 << 5,
        InvalidInput = 1 << 6,
        InvalidDeltaTime = 1 << 7,
        InvalidSettings = 1 << 8,
        StaleInput = 1 << 9
    }
}
