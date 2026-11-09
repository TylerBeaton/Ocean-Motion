// Six independent pulse-offset receiver for provisional micro IK tests. No IK on board.
#include <Wire.h>
#include <Adafruit_PWMServoDriver.h>
#include <stdint.h>
#include <string.h>
#include <math.h>

Adafruit_PWMServoDriver pwm(0x40);
const int neutralUs[6]={1500,1600,1550,1600,1500,1500};
const int limit=500;
int activeLimit=25;
int motionStep=1; // 1..10 us per 20ms = 50..500 us/s; neutral-only ARM configuration.
const unsigned long tickMs=20,watchdogMs=250;
bool driverReady=false,ready=false,armed=false,outputsOn=false,homing=false;
uint32_t session=0,lastSequence=0;
int progress[6]={},target[6]={};
double position[6]={};
unsigned long lastPacket=0,lastTick=0;
char line[96]; uint8_t used=0; bool overflow=false;

bool neutral() { for (uint8_t i=0;i<6;i++) if (progress[i]!=0) return false; return true; }
void hold() {
  armed=homing=false;
  for (uint8_t i=0;i<6;i++) { target[i]=progress[i]; position[i]=progress[i]; }
}
void off() {
  hold(); if (driverReady) for (uint8_t i=0;i<16;i++) pwm.setPWM(i,0,4096);
  outputsOn=false;
  for (uint8_t i=0;i<6;i++) progress[i]=target[i]=position[i]=0; // Not physical homing.
}
void reply(const char* kind) {
  Serial.print("OI5,"); Serial.print(kind); Serial.print(','); Serial.println(session);
}
void printVector(const int* values) {
  for (uint8_t i=0;i<6;i++) { if (i) Serial.print(','); Serial.print(values[i]); }
}
void fault(const char* reason) {
  hold(); ready=false;
  Serial.print("OI5,ERR,"); Serial.print(session); Serial.print(','); Serial.println(reason);
}
bool number(const char* s,uint32_t& value) {
  if (!s || !*s) return false;
  uint32_t result=0;
  for (;*s;s++) {
    if (*s<'0' || *s>'9') return false;
    uint32_t digit=*s-'0'; if (result>(UINT32_MAX-digit)/10) return false;
    result=result*10+digit;
  }
  value=result; return true;
}
bool fraction(const char* s,int& result) {
  bool negative=s && *s=='-'; if (negative) ++s;
  uint32_t value;
  if (!number(s,value) || value>static_cast<uint32_t>(limit)) return false;
  result=negative ? -static_cast<int>(value) : static_cast<int>(value); return true;
}
void execute(char* message) {
  if (!strcmp(message,"?")) {
    Serial.print("OI5,STATUS,"); Serial.print(session); Serial.print(",driver_ready="); Serial.print(driverReady);
    Serial.print(",armed="); Serial.print(armed); Serial.print(",pwm_on="); Serial.print(outputsOn);
    Serial.print(",progress="); printVector(progress); Serial.print(",target="); printVector(target);
    Serial.print(",motion_speed_us_per_s="); Serial.print(motionStep*50);
    Serial.print(",home_speed_us_per_s=50,active_limit_us="); Serial.print(activeLimit); Serial.println(",hard_offset_limit_us=500"); return;
  }
  char* fields[11]; int count=1; fields[0]=message;
  for (char* p=message;*p;p++) if (*p==',') {
    if (count==11) { fault("FIELDS"); return; }
    *p=0; fields[count++]=p+1;
  }
  if (count<3 || strcmp(fields[0],"OI5")) { fault("FORMAT"); return; }
  uint32_t token;
  if (!number(fields[2],token) || token==0) { fault("SESSION"); return; }
  if (!strcmp(fields[1],"HELLO") && count==3) {
    hold(); activeLimit=25; motionStep=1; session=token; lastSequence=0; ready=driverReady;
    if (ready) { Serial.print("OI5,READY,"); Serial.print(session); Serial.print(','); printVector(progress); Serial.println(); } else fault("DRIVER"); return;
  }
  if (token==session && !strcmp(fields[1],"HOLD") && count==3) { hold(); reply("HOLD"); return; }
  if (token==session && !strcmp(fields[1],"OFF") && count==3) { off(); reply("OFF"); return; }
  if (!ready || token!=session) { fault("SESSION"); return; }
  if (!strcmp(fields[1],"ARM") && count==5) {
    uint32_t requestedLimit,requestedSpeed;
    if (!number(fields[3],requestedLimit) || (requestedLimit<25 || requestedLimit>500 || requestedLimit%25!=0)) { fault("LIMIT"); return; }
    if (!number(fields[4],requestedSpeed) || requestedSpeed<50 || requestedSpeed>500 || requestedSpeed%50!=0) { fault("SPEED"); return; }
    if (armed || homing || !neutral()) { fault("NEUTRAL_REQUIRED"); return; }
    if (!outputsOn) for (uint8_t i=0;i<6;i++) pwm.writeMicroseconds(i,neutralUs[i]);
    activeLimit=requestedLimit; motionStep=requestedSpeed/50; outputsOn=true; armed=true; lastSequence=0; lastPacket=lastTick=millis();
    Serial.print("OI5,ARMED,"); Serial.print(session); Serial.print(','); Serial.print(activeLimit); Serial.print(','); Serial.println(motionStep*50); return;
  }
  if (!strcmp(fields[1],"HOME") && count==3) {
    if (!outputsOn) { fault("OUTPUTS_OFF"); return; }
    hold(); homing=true; for (uint8_t i=0;i<6;i++) target[i]=0;
    lastPacket=lastTick=millis(); reply("HOMING"); return;
  }
  if (!strcmp(fields[1],"PING") && count==3 && homing) { lastPacket=millis(); return; }
  if (!strcmp(fields[1],"POSE") && count==10) {
    uint32_t seq; int requested[6];
    if (!armed || !number(fields[3],seq) || seq==0 || seq<=lastSequence) { fault("POSE"); return; }
    for (uint8_t i=0;i<6;i++) {
      if (!fraction(fields[i+4],requested[i]) || abs(requested[i])>activeLimit) { fault("POSE"); return; }
      if(neutralUs[i]+requested[i]<1000 || neutralUs[i]+requested[i]>2000) { fault("BOUND");return; }
    }
    for (uint8_t i=0;i<6;i++) target[i]=requested[i];
    lastSequence=seq; lastPacket=millis();
    Serial.print("OI5,ACK,"); Serial.print(session); Serial.print(','); Serial.print(seq); Serial.print(',');
    printVector(progress); Serial.println(); return;
  }
  fault("COMMAND");
}
void updateMotion() {
  unsigned long now=millis();
  if ((armed || homing) && now-lastPacket>watchdogMs) { hold(); reply("WATCHDOG"); }
  if ((!armed && !homing) || !outputsOn || now-lastTick<tickMs) return;
  lastTick=now;
  const int rampStep=homing ? 1 : motionStep;
  double peak=0,nextPosition[6]; int next[6],pulses[6];
  for (uint8_t i=0;i<6;i++) if (fabs(target[i]-position[i])>peak) peak=fabs(target[i]-position[i]);
  double divisor=peak>rampStep ? peak/rampStep : 1;
  // One shared interpolation factor. Fractional state avoids starving smaller components.
  for (uint8_t i=0;i<6;i++) {
    nextPosition[i]=position[i]+(target[i]-position[i])/divisor;
    // A consistent tie direction preserves integer-step bounds across zero.
    // lround(-0.5)->-1 then lround(1.5)->2 can otherwise turn a 2us step into 3us.
    next[i]=static_cast<int>(floor(nextPosition[i]+0.5));
    if (next[i] < -limit || next[i]>limit || abs(next[i]-progress[i])>rampStep) { fault("RAMP"); return; }
  }
  for (uint8_t i=0;i<6;i++) {
    pulses[i]=neutralUs[i]+next[i];
    if (pulses[i]<1000 || pulses[i]>2000 || abs(pulses[i]-neutralUs[i])>limit) { fault("BOUND"); return; }
  }
  for (uint8_t i=0;i<6;i++) pwm.writeMicroseconds(i,pulses[i]);
  for (uint8_t i=0;i<6;i++) { progress[i]=next[i]; position[i]=nextPosition[i]; }
  if (homing && neutral()) { hold(); reply("NEUTRAL"); }
}
void setup() {
  activeLimit=25; motionStep=1; ready=armed=outputsOn=homing=false; session=lastSequence=0; used=0; overflow=false;
  for (uint8_t i=0;i<6;i++) progress[i]=target[i]=position[i]=0;
  Serial.begin(115200); Wire.begin(); driverReady=pwm.begin(); off();
  if (driverReady) pwm.setPWMFreq(50);
  Serial.println("OI5,BOOT,0,OUTPUTS_OFF");
}
void loop() {
  updateMotion();
  for (uint8_t count=0;Serial.available() && count<64;count++) {
    char c=Serial.read(); if (c=='\r') continue;
    if (c=='\n') {
      if (overflow || used==0) fault("LINE");
      else { line[used]=0; execute(line); }
      used=0; overflow=false;
    } else if (c<' ' || c>'~') overflow=true;
    else if (used<sizeof(line)-1) line[used++]=c;
    else overflow=true;
  }
}
