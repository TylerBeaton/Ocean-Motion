#pragma once
#include <cassert>
#include <utility>
#include <vector>

struct Adafruit_PWMServoDriver {
  bool beginSuccess = true;
  int frequency = 0;
  std::vector<int> disabledChannels;
  std::vector<std::pair<int, int> > pulses;
  explicit Adafruit_PWMServoDriver(int) {}
  bool begin() { return beginSuccess; }
  void setPWMFreq(int value) { frequency = value; }
  void setPWM(int channel, int on, int off) {
    assert(channel >= 0 && channel < 16 && on == 0 && off == 4096);
    disabledChannels.push_back(channel);
  }
  void writeMicroseconds(int channel, int pulse) {
    assert(channel >= 0 && channel < 6 && pulse >= 1400 && pulse <= 1650);
    pulses.push_back(std::make_pair(channel, pulse));
  }
};
