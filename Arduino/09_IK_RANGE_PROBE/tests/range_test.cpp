#include <cassert>
#include <string>
#include <random>
#include <iostream>
#include "../09_IK_RANGE_PROBE.ino"
void send(const std::string& s) { Serial.feed(s+"\n"); while(Serial.available())loop(); }
void arm(int cap=25) { setup(); send("OI3,HELLO,123"); send("OI3,ARM,123,"+std::to_string(cap)); assert(armed && outputsOn); }
std::string pose(uint32_t seq,const int* values) {
  std::string s="OI3,POSE,123,"+std::to_string(seq);
  for(int i=0;i<6;i++)s+=","+std::to_string(values[i]); return s;
}
void tick() {
  int old[6]; for(int i=0;i<6;i++)old[i]=progress[i];
  size_t n=pwm.pulses.size(); fakeTime+=20; updateMotion();
  for(int i=0;i<6;i++)assert(abs(progress[i])<=250 && abs(progress[i]-old[i])<=1);
  for(;n<pwm.pulses.size();n++)assert(abs(pwm.pulses[n].second-neutralUs[pwm.pulses[n].first])<=250);
}
int main() {
  setup(); assert(!armed && !outputsOn && pwm.pulses.empty());
  send("OI3,HELLO,123"); assert(ready && !outputsOn);
  send("OI3,ARM,123,25"); assert(armed && pwm.pulses.size()==6);
  uint32_t seq=0; const int asym[6]={25,-17,9,-25,3,0};
  for(int i=0;i<30;i++) { send(pose(++seq,asym)); tick(); }
  for(int i=0;i<6;i++)assert(progress[i]==asym[i]);
  std::mt19937 g(321); std::uniform_int_distribution<int>d(-25,25);
  for(int n=0;n<10000;n++) { int v[6];for(int&i:v)i=d(g);send(pose(++seq,v));tick(); }
  fakeTime+=251;updateMotion();assert(!armed && outputsOn);
  size_t written=pwm.pulses.size(); fakeTime+=200;updateMotion();assert(pwm.pulses.size()==written);
  send("OI3,HELLO,123"); assert(ready && !armed);
  send("OI3,HOME,123"); for(int i=0;i<30 && homing;i++){send("OI3,PING,123");tick();}
  assert(neutral() && !armed && !homing);
  for(const auto& bad:std::vector<std::string>{"OI3,POSE,123,1,0,0,0,0,0,26","OI3,POSE,123,1,0,0,0,0,0,-26","OI3,POSE,123,1,0,0,NaN,0,0,0","OI3,POSE,123,4294967296,0,0,0,0,0,0","OI3,POSE,999,1,0,0,0,0,0,0","OW3,ARM,123,1","OI3,POSE,123,1,0,0,0,0,0","OI3,POSE,123,1,0,0,0,0,0,0,0",std::string(120,'x'),""}) {
    arm();written=pwm.pulses.size();send(bad);assert(!armed && !ready && outputsOn && pwm.pulses.size()==written);
  }
  arm();send(pose(1,asym));tick();send(pose(1,asym));assert(!armed && !ready);
  send("OI3,HELLO,123");send("OI3,ARM,123,25");assert(!armed && Serial.output.find("NEUTRAL_REQUIRED")!=std::string::npos);
  send("OI3,OFF,123");assert(!outputsOn && neutral());
  arm();send(pose(1,asym));tick();send("OI3,HOLD,123");written=pwm.pulses.size();tick();assert(pwm.pulses.size()==written);
  for(int cap=25;cap<=250;cap+=25) {
    arm(cap); assert(activeLimit==cap); seq=0;
    std::uniform_int_distribution<int> values(-cap,cap);
    for(int n=0;n<1000;n++){int v[6];for(int&i:v)i=values(g);send(pose(++seq,v));tick();}
    send("OI3,HELLO,123");assert(activeLimit==25 && !armed);
    send("OI3,HOME,123");for(int n=0;n<260 && homing;n++){send("OI3,PING,123");tick();}assert(neutral());
  }
  for(const std::string& cap:{"0","26","101","NaN","4294967296"}) {
    setup();send("OI3,HELLO,123");send("OI3,ARM,123,"+cap);assert(!armed && !outputsOn);
  }
  pwm.beginSuccess=false;written=pwm.pulses.size();setup();send("OI3,HELLO,123");send("OI3,ARM,123,25");assert(!outputsOn && pwm.pulses.size()==written);
  std::cout<<"RANGE FIRMWARE PASS: independent six offsets, ramp, bounds, watchdog, home, sessions, malformed packets, no auto-arm.\n";
}
