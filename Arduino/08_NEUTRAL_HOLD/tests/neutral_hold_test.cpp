#include <cassert>
#include <string>
#include "../08_NEUTRAL_HOLD.ino"

void submit(const std::string& text) {
  Serial.feed(text);
  while (Serial.available()) loop();
}

int main() {
  // Failed driver initialization cannot enable an output.
  pwm.beginSuccess = false;
  setup();
  submit("e\n");
  assert(!driverReady && pwm.pulses.empty());

  pwm.beginSuccess = true;
  setup();
  assert(driverReady && configurationValid && pwm.frequency == 50);
  assert(pwm.pulses.empty() && pwm.disabledChannels.size() == 16);
  for (int i = 0; i < 16; ++i) assert(pwm.disabledChannels[i] == i);
  for (int i = 0; i < 6; ++i) assert(!enabled[i]);

  // Selection and help alone do not move anything; unterminated e is inert.
  submit("?\n5\r\n0\n");
  assert(selected == 0 && pwm.pulses.empty());
  submit("e");
  assert(pwm.pulses.empty());
  submit("\n");
  assert(enabled[0] && pwm.pulses.size() == 1);
  assert(pwm.pulses.back() == std::make_pair(0, 1500));

  // Add one exact reference at a time without rewriting previous channels.
  const int expected[6] = {1500, 1600, 1550, 1600, 1500, 1500};
  for (int i = 1; i < 6; ++i) {
    submit(std::string(1, '0' + i) + "\ne\n");
    assert(pwm.pulses.size() == static_cast<size_t>(i + 1));
    assert(pwm.pulses.back() == std::make_pair(i, expected[i]));
    for (int j = 0; j <= i; ++j) assert(enabled[j]);
  }
  submit("e\n?\n0\n?\n"); // Duplicate enable / selection / status are inert.
  assert(pwm.pulses.size() == 6);

  // Reject legacy motion, pasted commands, long lines and malformed input.
  submit("+\n-\np\ns\nr\na\nb\nc\n6\nE\ne e\neee\n");
  submit(std::string(200, 'e') + "\n\n\r\n");
  assert(pwm.pulses.size() == 6);

  submit("x\n");
  assert(pwm.disabledChannels.size() == 32);
  for (int i = 0; i < 6; ++i) assert(!enabled[i]);
  assert(pwm.pulses.size() == 6);

  // An invalid configuration blocks enabling; parser recovers after rejection.
  configurationValid = false;
  submit("e\n");
  assert(!enabled[0] && pwm.pulses.size() == 6);
  configurationValid = validateReferences();
  submit("2\ne\n");
  assert(enabled[2] && pwm.pulses.size() == 7);
  assert(pwm.pulses.back() == std::make_pair(2, 1550));
  for (int i = 0; i < 6; ++i) assert(enabled[i] == (i == 2));
}
