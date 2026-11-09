// Coordinated horn raise/lower exploration. Not a pure-heave IK solution.
// Supported unloaded plate; servo power OFF for upload. See README.md.
#include <Wire.h>
#include <Adafruit_PWMServoDriver.h>

Adafruit_PWMServoDriver pwm(0x40);
const uint8_t servoCount = 6;
const int neutralUs[servoCount] = {1500, 1600, 1550, 1600, 1500, 1500};
const int upUs[servoCount] = {2000, 1000, 2000, 1000, 2000, 1000};
const int downUs[servoCount] = {1000, 2000, 1000, 2000, 1000, 2000};
const int minimumUs = 1000;
const int maximumUs = 2000;
const unsigned long tickIntervalMs = 20;
const int rampStep = 5; // 0.5% of neutral-to-endpoint command travel per tick.
bool enabled[servoCount] = {};
bool driverReady = false;
bool configurationValid = false;
uint8_t selected = 0;
int progress = 0; // Per mille: -1000=down endpoints; 0=neutral; +1000=up endpoints.
int target = 0;
int raiseLimit = 50; // Initial 5% allowance in each direction.
int lowerLimit = 50;
int stepSize = 50; // Coarse 5%; fine 1%.
bool moving = false;
unsigned long lastTick = 0;
char command = 0;
bool invalidLine = false;

int pulseAt(uint8_t channel, int fraction) {
  int endpoint = fraction >= 0 ? upUs[channel] : downUs[channel];
  int magnitude = fraction >= 0 ? fraction : -fraction;
  return neutralUs[channel] + static_cast<long>(endpoint - neutralUs[channel]) * magnitude / 1000;
}

bool allEnabled() {
  if (!driverReady || !configurationValid) return false;
  for (uint8_t i = 0; i < servoCount; ++i) if (!enabled[i]) return false;
  return true;
}

bool validateConfiguration() {
  for (uint8_t i = 0; i < servoCount; ++i) {
    if (neutralUs[i] < minimumUs || neutralUs[i] > maximumUs ||
        upUs[i] < minimumUs || upUs[i] > maximumUs ||
        downUs[i] < minimumUs || downUs[i] > maximumUs ||
        (upUs[i] - neutralUs[i]) * (downUs[i] - neutralUs[i]) >= 0) return false;
  }
  return true;
}

void printPercent(int value) {
  if (value < 0) { Serial.print('-'); value = -value; }
  Serial.print(value / 10); Serial.print('.'); Serial.print(value % 10);
}

void status() {
  Serial.print("GROUP TEST progress_percent="); printPercent(progress);
  Serial.print(" target_percent="); printPercent(target);
  Serial.print(" moving="); Serial.print(moving);
  Serial.print(" all_enabled="); Serial.println(allEnabled());
  Serial.print("allowed_down_percent="); printPercent(-lowerLimit);
  Serial.print(" allowed_up_percent="); printPercent(raiseLimit);
  Serial.print(" step_percent="); printPercent(stepSize); Serial.println();
  Serial.print("driver_ready="); Serial.print(driverReady);
  Serial.print(" references_valid="); Serial.println(configurationValid);
  for (uint8_t i = 0; i < servoCount; ++i) {
    Serial.print("servo="); Serial.print(i);
    Serial.print(" enabled="); Serial.print(enabled[i]);
    Serial.print(" command_us="); Serial.println(pulseAt(i, progress));
  }
  Serial.println("Commanded progress only, not measured height or position.");
}

void holdPosition() {
  moving = false;
  target = progress;
}

void disableOutputs() {
  holdPosition();
  if (driverReady) {
    for (uint8_t i = 0; i < 16; ++i) pwm.setPWM(i, 0, 4096);
  }
  for (uint8_t i = 0; i < servoCount; ++i) enabled[i] = false;
}

void startTarget(int requested) {
  if (!allEnabled()) { Serial.println("BLOCKED: enable all six at neutral first."); return; }
  if (requested < -lowerLimit || requested > raiseLimit) {
    Serial.println("BLOCKED: staged group allowance. Inspect completed boundary before w."); return;
  }
  target = requested;
  moving = target != progress;
  lastTick = millis();
}

void execute(char c) {
  if (c == 'x') { disableOutputs(); status(); return; }
  if (c == 's') { holdPosition(); status(); return; } // Hold PWM, no recenter.
  if (c == '?') {
    Serial.println("0..5 select; e enable selected at neutral; +/- group raise/lower;");
    Serial.println("g 5% step; f 1% step; w extend completed boundary by 5% (NO movement);");
    Serial.println("n gradual neutral; s pause and HOLD current commands; x ALL PWM off; ? status");
    status(); return;
  }
  if (moving) { Serial.println("Motion active: wait, s to hold, x PWM off, or ?."); return; }
  if (c >= '0' && c <= '5') {
    selected = c - '0'; // Selection never moves or disables other channels.
  } else if (c == 'e') {
    if (!driverReady || !configurationValid || progress != 0) {
      Serial.println("BLOCKED: enabling requires valid references and neutral progress."); return;
    }
    if (!enabled[selected]) {
      pwm.writeMicroseconds(selected, neutralUs[selected]);
      enabled[selected] = true;
    }
  } else if (c == 'g' || c == 'f') {
    stepSize = c == 'g' ? 50 : 10;
  } else if (c == '+' || c == '-') {
    startTarget(progress + (c == '+' ? stepSize : -stepSize));
  } else if (c == 'n') {
    startTarget(0);
  } else if (c == 'w') {
    if (!allEnabled()) { Serial.println("BLOCKED: enable all six first."); return; }
    if (progress == raiseLimit && raiseLimit < 1000) {
      raiseLimit += 50;
    } else if (progress == -lowerLimit && lowerLimit < 1000) {
      lowerLimit += 50;
    } else {
      Serial.println("BLOCKED: complete current boundary first, or already at 100% guardrail."); return;
    }
    Serial.println("Allowance extended WITHOUT movement. Newly allowed travel is unverified.");
  } else {
    Serial.println("Rejected: send ? for group-test commands."); return;
  }
  status();
}

void updateMotion() {
  if (!moving || millis() - lastTick < tickIntervalMs) return;
  lastTick = millis();
  if (!allEnabled()) { holdPosition(); return; }
  int difference = target - progress;
  int delta = difference > rampStep ? rampStep : difference < -rampStep ? -rampStep : difference;
  int next = progress + delta;
  int pulses[servoCount];
  // Validate the complete update before writing any channel.
  for (uint8_t i = 0; i < servoCount; ++i) {
    pulses[i] = pulseAt(i, next);
    if (pulses[i] < minimumUs || pulses[i] > maximumUs) {
      holdPosition(); Serial.println("BLOCKED: group target outside pulse guardrails."); return;
    }
  }
  for (uint8_t i = 0; i < servoCount; ++i) pwm.writeMicroseconds(i, pulses[i]);
  progress = next;
  if (progress == target) {
    moving = false;
    Serial.println("Group target commands reached. Inspect actual platform before continuing.");
    status();
  }
}

void setup() {
  Serial.begin(115200);
  Wire.begin();
  driverReady = pwm.begin();
  disableOutputs();
  if (driverReady) pwm.setPWMFreq(50);
  configurationValid = validateConfiguration();
}

void loop() {
  uint8_t bytesRead = 0;
  while (Serial.available() && bytesRead < 32) {
    ++bytesRead;
    char c = Serial.read();
    if (c == '\r') continue;
    if (c == '\n') {
      if (invalidLine) Serial.println("Rejected: one command per line.");
      else if (command) execute(command);
      command = 0; invalidLine = false;
    } else if (command) invalidLine = true;
    else command = c;
  }
  updateMotion();
}
