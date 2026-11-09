// Manual assembled-configuration exploration, not an IK workspace or stall test.
// Support the unloaded plate; upload with servo supply OFF. See README.md.
#include <Wire.h>
#include <Adafruit_PWMServoDriver.h>

Adafruit_PWMServoDriver pwm(0x40);
const uint8_t servoCount = 6;
const int neutralUs[servoCount] = {1500, 1600, 1550, 1600, 1500, 1500};
const int raiseSign[servoCount] = {1, -1, 1, -1, 1, -1};
// Outer exploration guardrails, NOT validated servo/mechanical travel limits.
const int hardMinimumUs = 1000;
const int hardMaximumUs = 2000;
const unsigned long returnIntervalMs = 20;
const int returnStepUs = 5;
int pulseUs[servoCount] = {1500, 1600, 1550, 1600, 1500, 1500};
int lowerWindowUs[servoCount];
int upperWindowUs[servoCount];
int downEndpointUs[servoCount] = {};
int upEndpointUs[servoCount] = {};
bool enabled[servoCount] = {};
bool driverReady = false;
bool configurationValid = false;
bool returning = false;
unsigned long lastReturnTime = 0;
uint8_t selected = 0;
int stepUs = 25;
char command = 0;
bool invalidLine = false;

bool allEnabled() {
  for (uint8_t i = 0; i < servoCount; ++i) if (!enabled[i]) return false;
  return driverReady && configurationValid;
}

bool anyEnabledAwayFromNeutral() {
  for (uint8_t i = 0; i < servoCount; ++i) {
    if (enabled[i] && pulseUs[i] != neutralUs[i]) return true;
  }
  return false;
}

int signedOffset() {
  return (pulseUs[selected] - neutralUs[selected]) * raiseSign[selected];
}

void disableOutputs() {
  returning = false;
  if (driverReady) {
    for (uint8_t i = 0; i < 16; ++i) pwm.setPWM(i, 0, 4096);
  }
  for (uint8_t i = 0; i < servoCount; ++i) enabled[i] = false;
  // Preserve recorded endpoints in RAM so they can still be printed after x.
}

void status() {
  Serial.print("EXTENT TEST selected="); Serial.print(selected);
  Serial.print(" step_us="); Serial.print(stepUs);
  Serial.print(" offset_us="); Serial.print(signedOffset());
  Serial.print(" returning="); Serial.print(returning);
  Serial.print(" all_enabled="); Serial.println(allEnabled());
  Serial.print("driver_ready="); Serial.print(driverReady);
  Serial.print(" references_valid="); Serial.println(configurationValid);
  for (uint8_t i = 0; i < servoCount; ++i) {
    Serial.print("servo="); Serial.print(i);
    Serial.print(" enabled="); Serial.print(enabled[i]);
    Serial.print(" command_us="); Serial.print(pulseUs[i]);
    Serial.print(" neutral_us="); Serial.print(neutralUs[i]);
    Serial.print(" window="); Serial.print(lowerWindowUs[i]);
    Serial.print(".."); Serial.println(upperWindowUs[i]);
  }
}

void results() {
  Serial.println("Configuration-specific observations; NOT final IK limits. Zero endpoint means unrecorded.");
  Serial.println("servo,neutral_us,raise_sign,down_us,up_us,min_us,max_us");
  for (uint8_t i = 0; i < servoCount; ++i) {
    int low = 0;
    int high = 0;
    if (downEndpointUs[i] && upEndpointUs[i]) {
      low = downEndpointUs[i] < upEndpointUs[i] ? downEndpointUs[i] : upEndpointUs[i];
      high = downEndpointUs[i] > upEndpointUs[i] ? downEndpointUs[i] : upEndpointUs[i];
    }
    Serial.print(i); Serial.print(','); Serial.print(neutralUs[i]);
    Serial.print(','); Serial.print(raiseSign[i]);
    Serial.print(','); Serial.print(downEndpointUs[i]);
    Serial.print(','); Serial.print(upEndpointUs[i]);
    Serial.print(','); Serial.print(low);
    Serial.print(','); Serial.println(high);
  }
}

void execute(char c) {
  if (c == 'x') {
    disableOutputs(); status(); return;
  }
  if (c == '?') {
    Serial.println("0..5 select; e enable at neutral; +/- raise/lower selected; g 25us; f 5us;");
    Serial.println("l record down endpoint; h record up endpoint; n gradual neutral return;");
    Serial.println("w expand selected exploration window by 50us each way (NO movement); v results; x ALL PWM off");
    status(); return;
  }
  if (c == 'v') { results(); return; }
  if (returning) {
    Serial.println("Returning: only ?, v or x accepted until neutral."); return;
  }
  if (c >= '0' && c <= '5') {
    if (anyEnabledAwayFromNeutral()) {
      Serial.println("BLOCKED: n to neutral before switching channels."); return;
    }
    selected = c - '0';
  } else if (c == 'e') {
    if (!driverReady || !configurationValid || anyEnabledAwayFromNeutral()) {
      Serial.println("BLOCKED: driver/configuration unavailable or a channel away from neutral."); return;
    }
    if (enabled[selected]) { Serial.println("Already enabled."); return; }
    pwm.writeMicroseconds(selected, neutralUs[selected]);
    pulseUs[selected] = neutralUs[selected];
    enabled[selected] = true;
  } else if (c == 'f' || c == 'g') {
    stepUs = c == 'f' ? 5 : 25; // No movement.
  } else if (c == '+' || c == '-') {
    if (!allEnabled()) { Serial.println("BLOCKED: enable all six at neutral first."); return; }
    int next = pulseUs[selected] + (c == '+' ? stepUs : -stepUs) * raiseSign[selected];
    if (next < lowerWindowUs[selected] || next > upperWindowUs[selected]) {
      Serial.println("BLOCKED: software exploration window, NOT a measured physical limit."); return;
    }
    pwm.writeMicroseconds(selected, next);
    pulseUs[selected] = next;
  } else if (c == 'n') {
    if (!allEnabled()) { Serial.println("BLOCKED: all six must be enabled."); return; }
    returning = pulseUs[selected] != neutralUs[selected];
    lastReturnTime = millis();
  } else if (c == 'l' || c == 'h') {
    if (!allEnabled() || (c == 'l' && signedOffset() > 0) ||
        (c == 'h' && signedOffset() < 0)) {
      Serial.println("BLOCKED: l requires down/neutral, h requires up/neutral, all six enabled."); return;
    }
    if (c == 'l') downEndpointUs[selected] = pulseUs[selected];
    else upEndpointUs[selected] = pulseUs[selected];
    results(); return;
  } else if (c == 'w') {
    if (!allEnabled()) { Serial.println("BLOCKED: enable all six first."); return; }
    int low = lowerWindowUs[selected] - 50;
    int high = upperWindowUs[selected] + 50;
    lowerWindowUs[selected] = low < hardMinimumUs ? hardMinimumUs : low;
    upperWindowUs[selected] = high > hardMaximumUs ? hardMaximumUs : high;
    Serial.println("Window expanded without motion. Inspect each next step; guardrails are NOT safe travel limits.");
  } else {
    Serial.println("Rejected: send ? for extent-test commands."); return;
  }
  status();
}

void updateReturn() {
  if (!returning || millis() - lastReturnTime < returnIntervalMs) return;
  lastReturnTime = millis();
  int difference = neutralUs[selected] - pulseUs[selected];
  int delta = difference > returnStepUs ? returnStepUs :
              difference < -returnStepUs ? -returnStepUs : difference;
  pulseUs[selected] += delta;
  pwm.writeMicroseconds(selected, pulseUs[selected]);
  if (pulseUs[selected] == neutralUs[selected]) {
    returning = false;
    Serial.println("Returned to commanded neutral; inspect actual platform.");
    status();
  }
}

void setup() {
  Serial.begin(115200);
  Wire.begin();
  driverReady = pwm.begin();
  disableOutputs();
  if (driverReady) pwm.setPWMFreq(50);
  configurationValid = true;
  for (uint8_t i = 0; i < servoCount; ++i) {
    lowerWindowUs[i] = neutralUs[i] - 50;
    upperWindowUs[i] = neutralUs[i] + 50;
    if (lowerWindowUs[i] < hardMinimumUs || upperWindowUs[i] > hardMaximumUs ||
        (raiseSign[i] != 1 && raiseSign[i] != -1)) configurationValid = false;
  }
}

void loop() {
  uint8_t bytesRead = 0;
  while (Serial.available() && bytesRead < 32) {
    ++bytesRead;
    char c = Serial.read();
    if (c == '\r') continue;
    if (c == '\n') {
      if (invalidLine) Serial.println("Rejected: send one command per line.");
      else if (command) execute(command);
      command = 0; invalidLine = false;
    } else if (command) invalidLine = true;
    else command = c;
  }
  updateReturn();
}
