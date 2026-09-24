/*
  Ocean Motion SP06.2 modular telemetry demonstration.

  Board: Arduino UNO R4 WiFi
  Serial: 115200 baud, newline-delimited ASCII
  LCD: 16x2 I2C display at 0x27
  PCA9685: I2C address 0x40, external 5 V servo supply, shared ground
  Pitch servo: PCA9685 channel 0
  Roll servo: PCA9685 channel 1
  Common-cathode RGB LED: red D11, green D10, blue D9

  Host commands:
    OM1,HELLO
    OM1,POSE,<transportSeq>,<sourceSeq>,<heave>,<pitch>,<yaw>,<roll>
    OM1,STOP

  Device responses:
    OM1,READY
    OM1,ACK,<transportSeq>
    OM1,STOPPED
    OM1,WATCHDOG
    OM1,ERR,<reason>

  MotionTransportFirmware.cpp owns transport and safety state. MotionHardware.cpp
  owns the two-servo PCA9685 output, LCD telemetry, and RGB status. STOP,
  watchdog, and protocol faults centre both servos but do not remove power.
  The 235-to-379 pulse range is experimental and requires unloaded endpoint
  validation before use with a mechanism or payload.
*/

// Implementation lives in MotionTransportFirmware.cpp so this sketch compiles
// without Arduino's host-specific automatic prototype generator.
