#include <cassert>
#include <string>
#include "../08_EXTENT_TEST.ino"

void submit(const std::string& text) {
  Serial.feed(text);
  while (Serial.available()) loop();
}

void finishReturn() {
  int iterations = 0;
  while (returning) {
    assert(++iterations <= 200);
    fakeTime += 20;
    updateReturn();
  }
}

int main() {
  pwm.beginSuccess = false;
  setup(); submit("e\n+\n"); assert(pwm.pulses.empty());
  pwm.beginSuccess = true;
  setup(); assert(driverReady && configurationValid && !allEnabled());
  assert(pwm.pulses.empty() && pwm.disabledChannels.size() == 16);
  for (int i = 0; i < 6; ++i) assert(!enabled[i]);

  submit("?\nv\n+\n-\nn\nl\nh\nw\n");
  assert(pwm.pulses.empty());
  for (int i = 0; i < 6; ++i) {
    submit(std::string(1, '0' + i) + "\ne\n");
    assert(pwm.pulses.size() == static_cast<size_t>(i + 1));
    assert(pwm.pulses.back() == std::make_pair(i, neutralUs[i]));
    if (i < 5) { submit("+\n"); assert(pwm.pulses.size() == static_cast<size_t>(i + 1)); }
  }
  assert(allEnabled());

  for (int channel = 0; channel < 6; ++channel) {
    submit(std::string(1, '0' + channel) + "\ng\n");
    size_t before = pwm.pulses.size();
    submit("+\n+\n");
    assert(pwm.pulses.size() == before + 2);
    assert(signedOffset() == 50);
    assert(pulseUs[channel] == neutralUs[channel] + 50 * raiseSign[channel]);
    for (int i = 0; i < 6; ++i) if (i != channel) assert(pulseUs[i] == neutralUs[i]);
    before = pwm.pulses.size();
    submit("+\ne\n"); // Bound and duplicate enable cannot move anything.
    assert(pwm.pulses.size() == before);
    submit(std::string(1, '0' + ((channel + 1) % 6)) + "\n");
    assert(selected == channel); // Must return before switching.
    submit("l\n"); assert(downEndpointUs[channel] == 0); // Wrong side rejected.
    submit("h\n"); assert(upEndpointUs[channel] == pulseUs[channel]);

    submit("n\n"); assert(returning);
    before = pwm.pulses.size();
    fakeTime += 19; updateReturn(); assert(pwm.pulses.size() == before);
    submit("+\nw\nf\n0\n"); assert(pwm.pulses.size() == before && selected == channel);
    ++fakeTime; updateReturn(); assert(signedOffset() == 45);
    finishReturn(); assert(signedOffset() == 0);

    submit("-\n-\nl\n"); assert(signedOffset() == -50);
    assert(downEndpointUs[channel] == pulseUs[channel]);
    int savedUp = upEndpointUs[channel];
    submit("h\n"); assert(upEndpointUs[channel] == savedUp); // Wrong side rejected.
    before = pwm.pulses.size();
    submit("-\n"); assert(pwm.pulses.size() == before);
    submit("w\n"); assert(pwm.pulses.size() == before); // Expansion is inert.
    submit("f\n-\n"); assert(signedOffset() == -55);
    submit("n\n"); finishReturn(); assert(pulseUs[channel] == neutralUs[channel]);
  }

  submit("0\ng\n");
  size_t before = pwm.pulses.size();
  for (int i = 0; i < 30; ++i) submit("w\n");
  assert(lowerWindowUs[0] == 1000 && upperWindowUs[0] == 2000);
  assert(pwm.pulses.size() == before);
  for (int i = 0; i < 20; ++i) submit("+\n");
  assert(pulseUs[0] == 2000);
  before = pwm.pulses.size(); submit("+\n"); assert(pwm.pulses.size() == before);
  submit("n\n"); finishReturn();
  for (int i = 0; i < 20; ++i) submit("-\n");
  assert(pulseUs[0] == 1000);
  before = pwm.pulses.size(); submit("-\n"); assert(pwm.pulses.size() == before);

  // Cancel gradual return; no later return tick can emit a pulse.
  submit("n\n"); assert(returning);
  int savedDown = downEndpointUs[0];
  submit("x\n"); assert(!returning && !allEnabled());
  before = pwm.pulses.size(); fakeTime += 1000; updateReturn();
  assert(pwm.pulses.size() == before && downEndpointUs[0] == savedDown);
  for (int i = 0; i < 6; ++i) assert(!enabled[i]);

  // Malformed, multi-command and unterminated input must not enable a channel.
  submit("0\nee\ne e\n6\n");
  submit(std::string(200, 'e') + "\n\r\n");
  assert(pwm.pulses.size() == before);
  submit("e"); assert(pwm.pulses.size() == before);
  configurationValid = false;
  submit("\n"); assert(pwm.pulses.size() == before);
  configurationValid = true;
  submit("e\n"); assert(pwm.pulses.size() == before + 1 && pulseUs[0] == neutralUs[0]);
  assert(!allEnabled());
}
