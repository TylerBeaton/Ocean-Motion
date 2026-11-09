#include <cassert>
#include <string>
#include <random>
#include <iostream>
#include "../09_IK_RANGE_PROBE.ino"
void send(const std::string& s) { Serial.feed(s+"\n"); while(Serial.available())loop(); }
void arm(int cap=25,int speed=50) { setup(); send("OI5,HELLO,123"); send("OI5,ARM,123,"+std::to_string(cap)+","+std::to_string(speed)); assert(armed && outputsOn); assert(Serial.output.find("OI5,ARMED,123,"+std::to_string(cap)+","+std::to_string(speed))!=std::string::npos); }
std::string pose(uint32_t seq,const int* values) {
  std::string s="OI5,POSE,123,"+std::to_string(seq);
  for(int i=0;i<6;i++)s+=","+std::to_string(values[i]); return s;
}
void tick() {
  int old[6]; for(int i=0;i<6;i++)old[i]=progress[i];
  int step=homing ? 1 : motionStep;
  size_t n=pwm.pulses.size(); fakeTime+=20; updateMotion();
  for(int i=0;i<6;i++)assert(abs(progress[i])<=500 && abs(progress[i]-old[i])<=step);
  for(;n<pwm.pulses.size();n++)assert(abs(pwm.pulses[n].second-neutralUs[pwm.pulses[n].first])<=500);
}
int main() {
  setup(); assert(!armed && !outputsOn && pwm.pulses.empty());
  send("OI5,HELLO,123"); assert(ready && !outputsOn);
  send("OI5,ARM,123,25,50"); assert(armed && pwm.pulses.size()==6);
  uint32_t seq=0; const int asym[6]={25,-17,9,-25,3,0};
  for(int i=0;i<30;i++) { send(pose(++seq,asym)); tick(); }
  for(int i=0;i<6;i++)assert(progress[i]==asym[i]);
  std::mt19937 g(321); std::uniform_int_distribution<int>d(-25,25);
  for(int n=0;n<10000;n++) { int v[6];for(int&i:v)i=d(g);send(pose(++seq,v));tick(); }
  fakeTime+=251;updateMotion();assert(!armed && outputsOn);
  size_t written=pwm.pulses.size(); fakeTime+=200;updateMotion();assert(pwm.pulses.size()==written);
  send("OI5,HELLO,123"); assert(ready && !armed);
  send("OI5,HOME,123"); for(int i=0;i<30 && homing;i++){send("OI5,PING,123");tick();}
  assert(neutral() && !armed && !homing);
  for(const auto& bad:std::vector<std::string>{"OI5,POSE,123,1,0,0,0,0,0,26","OI5,POSE,123,1,0,0,0,0,0,-26","OI5,POSE,123,1,0,0,NaN,0,0,0","OI5,POSE,123,4294967296,0,0,0,0,0,0","OI5,POSE,999,1,0,0,0,0,0,0","OW3,ARM,123,1","OI5,POSE,123,1,0,0,0,0,0","OI5,POSE,123,1,0,0,0,0,0,0,0",std::string(120,'x'),""}) {
    arm();written=pwm.pulses.size();send(bad);assert(!armed && !ready && outputsOn && pwm.pulses.size()==written);
  }
  arm();send(pose(1,asym));tick();send(pose(1,asym));assert(!armed && !ready);
  send("OI5,HELLO,123");send("OI5,ARM,123,25,50");assert(!armed && Serial.output.find("NEUTRAL_REQUIRED")!=std::string::npos);
  send("OI5,OFF,123");assert(!outputsOn && neutral());
  arm();send(pose(1,asym));tick();send("OI5,HOLD,123");written=pwm.pulses.size();tick();assert(pwm.pulses.size()==written);
  for(int speed=50;speed<=500;speed+=50)for(int cap=25;cap<=500;cap+=25) {
    arm(cap,speed); assert(activeLimit==cap && motionStep==speed/50); seq=0;
    std::uniform_int_distribution<int> values(-cap,cap);
    for(int n=0;n<1000;n++){int v[6];for(int i=0;i<6;i++){std::uniform_int_distribution<int> channelValues(std::max(-cap,1000-neutralUs[i]),std::min(cap,2000-neutralUs[i]));v[i]=channelValues(g);}send(pose(++seq,v));tick();if(!armed){std::cerr<<"random fault speed="<<speed<<" cap="<<cap<<" tail="<<Serial.output.substr(Serial.output.size()>200 ? Serial.output.size()-200 : 0)<<"\n";assert(armed);}}
    send("OI5,HOME,123");for(int n=0;n<510 && homing;n++){send("OI5,PING,123");tick();}if(!neutral())std::cerr<<"HOME failure speed="<<speed<<" cap="<<cap<<" ready="<<ready<<" homing="<<homing<<" progress0="<<progress[0]<<" tail="<<Serial.output.substr(Serial.output.size()>300 ? Serial.output.size()-300 : 0)<<"\n";assert(neutral());
    send("OI5,HELLO,123");assert(activeLimit==25 && motionStep==1 && !armed);
    send("OI5,HOME,123");for(int n=0;n<510 && homing;n++){send("OI5,PING,123");tick();}assert(neutral());
  }
  for(const std::string& cap:{"0","26","101","525","NaN","4294967296"}) {
    setup();send("OI5,HELLO,123");send("OI5,ARM,123,"+cap+",50");assert(!armed && !outputsOn);
  }
  for(const std::string& speed:{"0","49","51","501","550","NaN","4294967296"}) {
    setup();send("OI5,HELLO,123");written=pwm.pulses.size();
    send("OI5,ARM,123,250,"+speed);assert(!armed && !outputsOn && !ready && pwm.pulses.size()==written);
  }
  arm(250,500);send(pose(1,asym));tick();assert(!neutral());
  written=pwm.pulses.size();send("OI5,ARM,123,250,100");assert(!armed && !ready && motionStep==10 && pwm.pulses.size()==written);
  setup();send("OI5,HELLO,123");send("OI5,ARM,123,250");assert(!armed && !outputsOn);
  setup();send("OI3,HELLO,123");assert(!ready && !armed && !outputsOn);
  arm(250,500);send(pose(1,asym));tick();fakeTime+=251;updateMotion();assert(!armed && outputsOn);
  // Fractional half ties must not invent a 3us jump on a 2us/tick reversal.
  arm(500,100);const int halfTie[6]={-1,4,0,0,0,0},reverseTie[6]={2,4,0,0,0,0};
  send(pose(1,halfTie));tick();assert(position[0]==-.5 && progress[0]==0);
  send(pose(2,reverseTie));tick();assert(armed && progress[0]==2);
  // Absolute pulse boundaries remain independent of the selected offset cap.
  for(int ch=0;ch<6;ch++) {
    const int high=std::min(500,2000-neutralUs[ch]);
    int v[6]={};v[ch]=high;arm(500,500);send(pose(1,v));assert(armed);
    for(int n=0;n<60;n++){send(pose(n+2,v));tick();}assert(progress[ch]==high);
    send("OI5,HOME,123");int homeTicks=0;
    while(homing && homeTicks<510){send("OI5,PING,123");tick();homeTicks++;}
    assert(neutral() && homeTicks==high); // HOME is exactly 1us/tick even at 500us/s.
    if(high<500){arm(500);v[ch]=high+1;written=pwm.pulses.size();send(pose(1,v));assert(!armed && !ready && pwm.pulses.size()==written);}
  }
  // HELLO retains large physical commands; HOME still works after allowance reset to25.
  arm(500,500);const int far[6]={500,400,450,400,500,500};
  for(uint32_t n=1;n<=60;n++){send(pose(n,far));tick();}assert(progress[0]==500);
  send("OI5,HELLO,123");assert(activeLimit==25 && motionStep==1 && progress[0]==500);
  send("OI5,HOME,123");for(int n=0;n<510 && homing;n++){send("OI5,PING,123");tick();}assert(neutral());
  setup();send("OI4,HELLO,123");assert(!ready && !armed && !outputsOn);
  pwm.beginSuccess=false;written=pwm.pulses.size();setup();send("OI5,HELLO,123");send("OI5,ARM,123,25,50");assert(!outputsOn && pwm.pulses.size()==written);
  std::cout<<"RANGE FIRMWARE PASS: independent six offsets, all 10 motion speeds at all 20 pulse caps, fixed slow HOME, bounds, watchdog, home, sessions, malformed packets, no auto-arm.\n";
}
