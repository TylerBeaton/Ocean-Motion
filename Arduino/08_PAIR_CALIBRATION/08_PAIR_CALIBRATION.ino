// Standalone bench experiment; no Unity/IK input. See README before powering servos.
#include <Wire.h>
#include <Adafruit_PWMServoDriver.h>

Adafruit_PWMServoDriver pwm(0x40);
// User reconfirmed shared-side mapping after replacing faulty servo 5.
const uint8_t pairs[3][2] = {{0, 5}, {1, 2}, {3, 4}};
uint8_t channels[2] = {0, 5};
char activePair = 'A';
const int fineStepUs = 5;
const int coarseStepUs = 25;
int stepUs = coarseStepUs;
// Initial exploration windows, NOT measured mechanical limits.
const int minimumUs = 1400;
const int maximumUs = 1650; // Stage 2: channel 1's observed level is 1600 us.
const int pairWindowUs = 50;
int pulseUs[2] = {1500, 1500}; // Nominal reference only; not calibrated level.
int neutralUs[2] = {0, 0};
int direction[2] = {1, -1}; // Hypothesis: verify disconnected before pairing.
bool enabled[2] = {false, false};
bool pairMode = false;
int selected = 0;
int offsetUs = 0;
char command = 0;
bool invalidLine = false;

bool inWindow(int pulse) { return pulse >= minimumUs && pulse <= maximumUs; }

void writeServo(int index) {
  pwm.writeMicroseconds(channels[index], pulseUs[index]);
}

void status() {
  Serial.print(pairMode ? "PAIRED" : "INDIVIDUAL");
  Serial.print(" pair="); Serial.print(activePair);
  Serial.print(" selected="); Serial.print(channels[selected]);
  Serial.print(" offset_us="); Serial.print(offsetUs);
  Serial.print(" step_us="); Serial.println(stepUs);
  for (int i = 0; i < 2; ++i) {
    Serial.print("servo="); Serial.print(channels[i]);
    Serial.print(" enabled="); Serial.print(enabled[i]);
    Serial.print(" command_us="); Serial.print(pulseUs[i]);
    Serial.print(" level_us="); Serial.print(neutralUs[i]);
    Serial.print(" direction="); Serial.println(direction[i]);
  }
}

void disableOutputs() {
  for (int i = 0; i < 16; ++i) pwm.setPWM(i, 0, 4096);
  enabled[0] = enabled[1] = false;
  neutralUs[0] = neutralUs[1] = 0;
  pairMode = false;
  offsetUs = 0;
}

void execute(char c) {
  if (c == 'x') {
    disableOutputs(); // Removes PWM, NOT supply power. Support platform first.
  } else if (c == '?') {
    Serial.println("a/b/c choose pair A=[0,5] B=[1,2] C=[3,4] (outputs must be off)");
    Serial.println("0..5 select within pair; e enable at 1500us; +/- step; s save level;");
    Serial.println("g coarse 25us; f fine 5us; r reverse direction; p pair mode; x PWM off; ? status");
    Serial.print("Exploration bounds: "); Serial.print(minimumUs);
    Serial.print(".."); Serial.print(maximumUs);
    Serial.print("us absolute, pair offset +/-"); Serial.print(pairWindowUs);
    Serial.println("us. NOT measured extents.");
  } else if (c == 'g' || c == 'f') {
    stepUs = c == 'g' ? coarseStepUs : fineStepUs; // Works in either mode; no motion.
  } else if (c == 'a' || c == 'A' || c == 'b' || c == 'B' || c == 'c' || c == 'C') {
    if (enabled[0] || enabled[1]) {
      Serial.println("Support platform, then x before changing pairs."); return;
    }
    activePair = c >= 'a' && c <= 'c' ? c - 'a' + 'A' : c;
    channels[0] = pairs[activePair - 'A'][0];
    channels[1] = pairs[activePair - 'A'][1];
    pulseUs[0] = pulseUs[1] = 1500;
    neutralUs[0] = neutralUs[1] = 0;
    direction[0] = 1; direction[1] = -1;
    selected = 0;
    offsetUs = 0;
    pairMode = false;
  } else if (c == '+' || c == '-') {
    int delta = (c == '+' ? stepUs : -stepUs);
    if (pairMode) {
      int next = offsetUs + delta;
      int a = neutralUs[0] + direction[0] * next;
      int b = neutralUs[1] + direction[1] * next;
      if (next < -pairWindowUs || next > pairWindowUs || !inWindow(a) || !inWindow(b)) {
        Serial.println("BLOCKED: exploration window reached."); return;
      }
      pulseUs[0] = a; pulseUs[1] = b; offsetUs = next;
      writeServo(0); writeServo(1);
    } else {
      int next = pulseUs[selected] + direction[selected] * delta;
      if (!enabled[selected] || !inWindow(next)) {
        Serial.println("BLOCKED: disabled or outside exploration window."); return;
      }
      pulseUs[selected] = next;
      neutralUs[selected] = 0; // Any individual movement requires re-saving level.
      writeServo(selected);
    }
  } else if (pairMode) {
    Serial.println("Pair mode: use +/-, f/g step size, or x. No automatic return to level."); return;
  } else if (c >= '0' && c <= '5') {
    if (c - '0' != channels[0] && c - '0' != channels[1]) {
      Serial.println("Select a servo belonging to the active pair."); return;
    }
    selected = (c - '0' == channels[0] ? 0 : 1);
  } else if (c == 'e') {
    if (enabled[selected]) { Serial.println("Already enabled."); return; }
    pulseUs[selected] = 1500;
    enabled[selected] = true;
    writeServo(selected);
  } else if (c == 's') {
    if (!enabled[selected]) { Serial.println("Enable selected servo first."); return; }
    neutralUs[selected] = pulseUs[selected];
  } else if (c == 'r') {
    direction[selected] = -direction[selected]; // No movement.
  } else if (c == 'p') {
    if (!enabled[0] || !enabled[1] || neutralUs[0] == 0 || neutralUs[1] == 0) {
      Serial.println("Save level for BOTH servos first."); return;
    }
    pairMode = true;
    offsetUs = 0; // Current commands already equal saved neutral; no movement.
  } else {
    Serial.println("Unknown command; send ? for help."); return;
  }
  status();
}

void setup() {
  Serial.begin(115200);
  Wire.begin();
  pwm.begin();
  disableOutputs();
  pwm.setPWMFreq(50);
}

void loop() {
  // Exactly one character per line. Reject pasted multi-command lines.
  while (Serial.available()) {
    char c = Serial.read();
    if (c == '\r') continue;
    if (c == '\n') {
      if (invalidLine) Serial.println("Rejected: send one command per line.");
      else if (command) execute(command);
      command = 0; invalidLine = false;
    } else if (command) invalidLine = true;
    else command = c;
  }
}
