#pragma once
#include <stdint.h>
#include <string>
#include <sstream>
unsigned long fakeTime=0;
unsigned long millis() { return fakeTime; }
struct WireStub { void begin() {} } Wire;
struct SerialStub {
  std::string input,output; size_t cursor=0;
  void begin(int) {}
  int available() { return cursor<input.size(); }
  char read() { return input[cursor++]; }
  void feed(const std::string& value) { input+=value; }
  template<class T> void print(T value) { std::ostringstream s; s<<value; output+=s.str(); }
  template<class T> void println(T value) { print(value); output+='\n'; }
  void println() { output+='\n'; }
} Serial;
