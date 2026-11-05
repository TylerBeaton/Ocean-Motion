# Expanded independent IK range receiver — OI3

Current six-channel receiver; superseded local probe versions were pruned. Six independent signed pulse offsets around saved neutrals. Outputs startOFF. Hard±250us, active allowance25..250us in25us steps, exactper-Arm echo. HELLO resets active allowance25 but holds previous position; HOME can return retained offsets up to250 even after reset. Neutral-only ARM: `OI3,ARM,<session>,<allowance>`. Other OI2 records unchanged except prefixOI3. Oldprotocols refused.

No IK or collision checks on Arduino; Unity solves and bounds the complete pose. Hard1000..2000us and active allowance enforced on each whole packet. Shared1us/20msramp;250mswatchdogHOLD,noautoarm. User must observe newphysicalstages. Uploadservo supplyOFF/supportedplate. See `Assets/Subprojects/09_STEWART_IK/BOAT_IK_RANGE_TEST.md`. Oneupload adds runtime selection; no newupload per percentage/gain/limit.

Hosttests: `clang++ -std=c++11 -IArduino/09_IK_RANGE_PROBE/tests/stubs Arduino/09_IK_RANGE_PROBE/tests/range_test.cpp -o /tmp/ocean-range-test` thenrunbinary. NoUSBopened.
