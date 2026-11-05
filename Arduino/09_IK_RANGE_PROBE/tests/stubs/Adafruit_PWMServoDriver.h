#pragma once
#include <cassert>
#include <utility>
#include <vector>
struct Adafruit_PWMServoDriver {
  bool beginSuccess=true;
  int frequency=0,disabled=0;
  std::vector<std::pair<int,int> > pulses;
  explicit Adafruit_PWMServoDriver(int) {}
  bool begin() { return beginSuccess; }
  void setPWMFreq(int f) { frequency=f; }
  void setPWM(int i,int on,int off) { assert(i>=0 && i<16 && on==0 && off==4096); ++disabled; }
  void writeMicroseconds(int i,int us) { assert(i>=0 && i<6 && us>=1000 && us<=2000); pulses.push_back({i,us}); }
};
