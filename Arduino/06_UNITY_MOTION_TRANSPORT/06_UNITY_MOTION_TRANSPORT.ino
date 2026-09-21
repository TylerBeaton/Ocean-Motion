/*
  Ocean Motion SP06 architecture-neutral motion transport receiver.

  Board: Arduino UNO R4 WiFi
  Serial: 115200 baud, newline-delimited ASCII

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

  This checkpoint intentionally has no Servo objects, actuator pins, PWM writes,
  motor drivers, or inverse kinematics. Valid pose packets are measured and
  acknowledged only. The controller-local watchdog stops packet acceptance state
  after 250 ms without a valid pose.
*/

// Implementation lives in MotionTransportFirmware.cpp so this sketch compiles
// without Arduino's host-specific automatic prototype generator.
