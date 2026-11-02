#pragma once
#include <stdint.h>
#include <string>

unsigned long fakeTime = 0;
unsigned long millis() { return fakeTime; }

struct WireStub {
  void begin() {}
} Wire;

struct SerialStub {
  std::string input;
  size_t cursor = 0;
  void begin(int) {}
  int available() { return cursor < input.size(); }
  char read() { return input[cursor++]; }
  void feed(const std::string& text) { input += text; }
  template <class T> void print(T) {}
  template <class T> void println(T) {}
  void println() {}
} Serial;
