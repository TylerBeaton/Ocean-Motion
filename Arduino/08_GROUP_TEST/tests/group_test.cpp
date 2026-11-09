#include <cassert>
#include <string>
#include "../08_GROUP_TEST.ino"

void submit(const std::string& text) {
  Serial.feed(text);
  while (Serial.available()) loop();
}

void finishMotion() {
  int ticks = 0;
  while (moving) {
    assert(++ticks <= 400);
    size_t before = pwm.pulses.size();
    int previous = progress;
    fakeTime += 20;
    updateMotion();
    assert(pwm.pulses.size() == before + 6);
    for (int i = 0; i < 6; ++i) {
      assert(pwm.pulses[before + i] == std::make_pair(i, pulseAt(i, progress)));
      int change = pulseAt(i, progress) - pulseAt(i, previous);
      assert(change >= -3 && change <= 3);
    }
  }
}

int main() {
  pwm.beginSuccess = false; setup(); submit("e\n+\n");
  assert(pwm.pulses.empty());
  pwm.beginSuccess = true; setup();
  assert(pwm.pulses.empty() && pwm.disabledChannels.size() == 16);
  assert(driverReady && configurationValid && !allEnabled());
  submit("+\n-\nn\nw\n"); assert(!moving && progress == 0);

  // Every interpolated target stays inside individually observed pulse guardrails.
  for (int p = -1000; p <= 1000; ++p) {
    for (int i = 0; i < 6; ++i) {
      assert(pulseAt(i, p) >= 1000 && pulseAt(i, p) <= 2000);
      if (p == 0) assert(pulseAt(i, p) == neutralUs[i]);
      if (p == 1000) assert(pulseAt(i, p) == upUs[i]);
      if (p == -1000) assert(pulseAt(i, p) == downUs[i]);
    }
  }
  for (int i = 0; i < 6; ++i) {
    submit(std::string(1, '0' + i) + "\ne\n");
    assert(pwm.pulses.size() == static_cast<size_t>(i + 1));
    assert(pwm.pulses.back() == std::make_pair(i, neutralUs[i]));
    if (i < 5) { submit("+\n"); assert(!moving); }
  }
  assert(allEnabled());
  size_t before = pwm.pulses.size();
  submit("e\nw\n?\n"); assert(pwm.pulses.size() == before && raiseLimit == 50);
  submit("+\n"); assert(target == 50 && progress == 0 && moving);
  fakeTime += 19; updateMotion(); assert(pwm.pulses.size() == before);
  ++fakeTime; updateMotion(); assert(progress == 5 && pwm.pulses.size() == before + 6);
  int savedTarget = target;
  submit("+\n-\nn\nw\n0\ne\n"); assert(target == savedTarget && selected == 5);
  submit("s\n"); assert(!moving && target == progress);
  before = pwm.pulses.size(); fakeTime += 100; updateMotion(); assert(pwm.pulses.size() == before);
  submit("n\n"); finishMotion(); assert(progress == 0);

  // Reach every permitted boundary before expanding; no write on expansion/block.
  for (int percent = 5; percent <= 100; percent += 5) {
    submit("+\n"); finishMotion(); assert(progress == percent * 10);
    before = pwm.pulses.size();
    submit("+\n"); assert(!moving && pwm.pulses.size() == before);
    submit("w\n"); assert(pwm.pulses.size() == before);
    assert(raiseLimit == (percent == 100 ? 1000 : (percent + 5) * 10));
    assert(lowerLimit == 50);
  }
  for (int i = 0; i < 6; ++i) assert(pulseAt(i, progress) == upUs[i]);
  submit("n\n"); finishMotion(); assert(progress == 0);
  for (int percent = 5; percent <= 100; percent += 5) {
    submit("-\n"); finishMotion(); assert(progress == -percent * 10);
    before = pwm.pulses.size(); submit("-\n"); assert(!moving && pwm.pulses.size() == before);
    submit("w\n"); assert(pwm.pulses.size() == before);
    assert(lowerLimit == (percent == 100 ? 1000 : (percent + 5) * 10));
  }
  for (int i = 0; i < 6; ++i) assert(pulseAt(i, progress) == downUs[i]);
  submit("n\n"); finishMotion();
  submit("f\n+\n"); finishMotion(); assert(progress == 10);
  submit("g\n-\n"); finishMotion(); assert(progress == -40);
  submit("n\n"); finishMotion();

  before = pwm.pulses.size();
  submit("ee\n+ +\n"); submit(std::string(200, '+') + "\n");
  assert(!moving && pwm.pulses.size() == before);
  configurationValid = false; submit("+\ne\n"); assert(!moving);
  configurationValid = true;
  submit("+\n"); fakeTime += 20; updateMotion();
  assert(progress == 5 && moving);
  submit("x\n"); assert(!moving && !allEnabled());
  before = pwm.pulses.size(); fakeTime += 1000; updateMotion();
  submit("e\n+\nn\n"); assert(pwm.pulses.size() == before && !moving);
  for (int i = 0; i < 6; ++i) assert(!enabled[i]);
}
