// Stage 2 foundation: hold recorded horn references, one enabled channel at a time.
// Upload with servo power OFF and all pushrods disconnected. See README.md.
#include <Wire.h>
#include <Adafruit_PWMServoDriver.h>

Adafruit_PWMServoDriver pwm(0x40);
const uint8_t servoCount = 6;
// Visually aligned horn references from 2026-10-31; NOT measured joint angles.
const uint16_t neutralUs[servoCount] = {1500, 1600, 1550, 1600, 1500, 1500};
const uint16_t minimumUs = 1400;
const uint16_t maximumUs = 1650;
bool enabled[servoCount] = {false, false, false, false, false, false};
uint8_t selected = 0;
bool driverReady = false;
bool configurationValid = false;
char command = 0;
bool invalidLine = false;

bool validateReferences() {
  for (uint8_t i = 0; i < servoCount; ++i) {
    if (neutralUs[i] < minimumUs || neutralUs[i] > maximumUs) return false;
  }
  return true;
}

void disableOutputs() {
  if (driverReady) {
    for (uint8_t i = 0; i < 16; ++i) pwm.setPWM(i, 0, 4096);
  }
  for (uint8_t i = 0; i < servoCount; ++i) enabled[i] = false;
}

void status() {
  Serial.print("NEUTRAL HOLD selected="); Serial.print(selected);
  Serial.print(" driver_ready="); Serial.print(driverReady);
  Serial.print(" references_valid="); Serial.println(configurationValid);
  bool allEnabled = true;
  for (uint8_t i = 0; i < servoCount; ++i) {
    Serial.print("servo="); Serial.print(i);
    Serial.print(" enabled="); Serial.print(enabled[i]);
    Serial.print(" reference_us="); Serial.println(neutralUs[i]);
    allEnabled = allEnabled && enabled[i];
  }
  Serial.print("all_enabled="); Serial.println(allEnabled);
  Serial.println("Reported enable state is commanded PWM, not position/power feedback.");
}

void execute(char c) {
  if (c == '?') {
    Serial.println("0..5 select (no movement); e enable selected at saved reference; x ALL PWM off; ? status");
    Serial.println("Neutral holding only. No pair movement or automatic enabling.");
  } else if (c >= '0' && c <= '5') {
    selected = c - '0'; // Previously enabled channels keep holding.
  } else if (c == 'x') {
    disableOutputs(); // Does not remove supply power; support the plate first.
  } else if (c == 'e') {
    if (!driverReady || !configurationValid) {
      Serial.println("BLOCKED: driver unavailable or invalid references."); return;
    }
    if (enabled[selected]) {
      Serial.println("Already enabled; reference unchanged."); return;
    }
    pwm.writeMicroseconds(selected, neutralUs[selected]);
    enabled[selected] = true;
  } else {
    Serial.println("Rejected: neutral holding only; send ? for commands."); return;
  }
  status();
}

void setup() {
  Serial.begin(115200);
  Wire.begin();
  driverReady = pwm.begin();
  disableOutputs();
  if (driverReady) pwm.setPWMFreq(50);
  configurationValid = validateReferences();
}

void loop() {
  // One character per line; reject pasted multi-command lines and overflow.
  uint8_t bytesRead = 0;
  while (Serial.available() && bytesRead < 32) {
    ++bytesRead;
    char c = Serial.read();
    if (c == '\r') continue;
    if (c == '\n') {
      if (invalidLine) Serial.println("Rejected: send one command per line.");
      else if (command) execute(command);
      command = 0;
      invalidLine = false;
    } else if (command) {
      invalidLine = true;
    } else {
      command = c;
    }
  }
}
