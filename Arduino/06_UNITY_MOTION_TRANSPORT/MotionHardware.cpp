#include <Arduino.h>
#include <Wire.h>
#include <LiquidCrystal_I2C.h>
#include <Adafruit_PWMServoDriver.h>

#include "MotionHardware.h"

namespace
{
    const uint8_t redPin = 11;
    const uint8_t greenPin = 10;
    const uint8_t bluePin = 9;
    const unsigned long displayIntervalMs = 100;

    LiquidCrystal_I2C lcd(0x27, 16, 2);
    Adafruit_PWMServoDriver pwm(0x40);
    unsigned long lastDisplayTime = 0;

    void writeTarget(const MotionServoTarget& target)
    {
        pwm.setPWM(target.channel, 0, target.pulse);
    }

    void writeTargets(const MotionHardwareTargets& targets)
    {
        writeTarget(targets.pitch);
        writeTarget(targets.roll);
    }

    void centerServos()
    {
        writeTargets(MotionHardware::neutralTargets());
    }

    void setRgb(bool red, bool green, bool blue)
    {
        digitalWrite(redPin, red ? HIGH : LOW);
        digitalWrite(greenPin, green ? HIGH : LOW);
        digitalWrite(bluePin, blue ? HIGH : LOW);
    }

    void clearRow(uint8_t row)
    {
        lcd.setCursor(0, row);
        lcd.print("                ");
        lcd.setCursor(0, row);
    }

    void printSigned(float value, uint8_t digits)
    {
        if (value >= 0.0f)
            lcd.print('+');
        lcd.print(value, digits);
    }

    void showWaiting()
    {
        setRgb(true, true, false);
        clearRow(0);
        lcd.print("SP06.2 READY");
        clearRow(1);
        lcd.print("Waiting Unity");
    }

    void showStopped(const char* reason)
    {
        centerServos();
        setRgb(true, false, false);
        clearRow(0);
        lcd.print("TELEMETRY STOP");
        clearRow(1);
        lcd.print(reason);
    }

    void showFault(const char* reason)
    {
        centerServos();
        setRgb(true, false, false);
        clearRow(0);
        lcd.print("TELEMETRY FAULT");
        clearRow(1);
        lcd.print(reason);
    }
}

namespace MotionHardware
{
void begin()
{
    pinMode(redPin, OUTPUT);
    pinMode(greenPin, OUTPUT);
    pinMode(bluePin, OUTPUT);

    Wire.begin();
    pwm.begin();
    pwm.setPWMFreq(50);
    delay(10);

    lcd.init();
    lcd.backlight();

    centerServos();
    showWaiting();
}

void beginSession()
{
    centerServos();
    showWaiting();
}

void applyPose(const MotionPosePacket& packet, unsigned long nowMs)
{
    writeTargets(targetsForPose(packet));
    setRgb(false, true, false);

    if (nowMs - lastDisplayTime < displayIntervalMs)
        return;

    lastDisplayTime = nowMs;
    clearRow(0);
    lcd.print("P:");
    printSigned(packet.pitchDegrees, 1);
    lcd.print(" R:");
    printSigned(packet.rollDegrees, 1);

    clearRow(1);
    lcd.print("H:");
    printSigned(packet.heaveMeters, 3);
    lcd.print(" Y:");
    printSigned(packet.yawDegrees, 1);
}

void stop(const char* reason)
{
    showStopped(reason);
}

void fault(const char* reason)
{
    showFault(reason);
}
}
