/*
  Ocean Motion SP06.2 telemetry demonstration.

  Board: Arduino UNO R4 WiFi
  Serial: 115200 baud, newline-delimited ASCII
  LCD: 16x2 I2C display at 0x27
  Pitch servo: signal D6, external 5 V supply, shared ground
  Roll servo: signal D5, external 5 V supply, shared ground
  Common-cathode RGB LED: red D11, green D10, blue D9

  This is a two-servo milestone demonstration, not the final actuator
  architecture. The servos follow bounded pitch and roll telemetry. STOP,
  watchdog, and protocol faults centre both servos but do not remove power.
*/

#include <Arduino.h>
#include <Wire.h>
#include <LiquidCrystal_I2C.h>
#include <Servo.h>

#include <errno.h>
#include <math.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

namespace
{
    const unsigned long commandTimeoutMs = 250;
    const unsigned long errorResponseIntervalMs = 50;
    const unsigned long displayIntervalMs = 100;
    const size_t maximumBytesPerLoop = 64;
    const size_t maximumMessagesPerLoop = 4;

    const uint8_t pitchServoPin = 6;
    const uint8_t rollServoPin = 5;
    const uint8_t redPin = 11;
    const uint8_t greenPin = 10;
    const uint8_t bluePin = 9;

    const int servoCenterDegrees = 90;
    const int servoMinimumDegrees = 70;
    const int servoMaximumDegrees = 110;

    const float maximumHeaveMeters = 0.25f;
    const float maximumPitchDegrees = 10.0f;
    const float maximumYawDegrees = 5.0f;
    const float maximumRollDegrees = 10.0f;

    struct MotionPosePacket
    {
        uint32_t transportSequence;
        uint64_t sourceSequence;
        float heaveMeters;
        float pitchDegrees;
        float yawDegrees;
        float rollDegrees;
    };

    enum class ParseResult
    {
        Ok,
        InvalidFormat,
        NonFinite,
        OutOfRange
    };

    LiquidCrystal_I2C lcd(0x27, 16, 2);
    Servo pitchServo;
    Servo rollServo;

    char inputBuffer[160];
    size_t inputLength = 0;
    bool inputOverflow = false;

    bool handshakeComplete = false;
    bool streamActive = false;
    bool hasLatestPacket = false;
    MotionPosePacket latestPacket = {};

    unsigned long lastValidPoseTime = 0;
    unsigned long lastErrorResponseTime = 0;
    unsigned long lastDisplayTime = 0;
    bool hasSentErrorResponse = false;

    void readSerialMessages();
    void processMessage(const char* message);
    ParseResult parsePose(const char* message, MotionPosePacket* packet);
    bool packetsEqual(const MotionPosePacket& left,
                      const MotionPosePacket& right);
    void beginSession();
    void failClosed();
    void applyPose(const MotionPosePacket& packet);
    void showWaiting();
    void showStopped(const char* reason);
    void showFault(const char* reason);
    void centerServos();
    void setRgb(bool red, bool green, bool blue);
    void clearRow(uint8_t row);
    void printSigned(float value, uint8_t digits);
    void sendParseError(ParseResult result);
    void sendError(const char* message);
    bool sendLiteral(const char* message);
    void sendAcknowledgement(uint32_t transportSequence);
    void checkCommandWatchdog();
}

void setup()
{
    pinMode(redPin, OUTPUT);
    pinMode(greenPin, OUTPUT);
    pinMode(bluePin, OUTPUT);

    pitchServo.attach(pitchServoPin);
    rollServo.attach(rollServoPin);
    centerServos();

    Wire.begin();
    lcd.init();
    lcd.backlight();
    showWaiting();

    Serial.begin(115200);
}

void loop()
{
    checkCommandWatchdog();
    readSerialMessages();
    checkCommandWatchdog();
}

namespace
{
    void readSerialMessages()
    {
        size_t bytesRead = 0;
        size_t messagesRead = 0;

        while (Serial.available() > 0 &&
               bytesRead < maximumBytesPerLoop &&
               messagesRead < maximumMessagesPerLoop)
        {
            char incoming = static_cast<char>(Serial.read());
            bytesRead++;

            if (incoming == '\n')
            {
                messagesRead++;

                if (inputOverflow)
                {
                    failClosed();
                    showFault("INPUT OVERFLOW");
                    sendError("OM1,ERR,OVERFLOW");
                }
                else
                {
                    inputBuffer[inputLength] = '\0';
                    processMessage(inputBuffer);
                }

                inputLength = 0;
                inputOverflow = false;
            }
            else if (incoming != '\r')
            {
                if (inputLength < sizeof(inputBuffer) - 1)
                    inputBuffer[inputLength++] = incoming;
                else
                    inputOverflow = true;
            }
        }
    }

    void processMessage(const char* message)
    {
        if (strcmp(message, "OM1,HELLO") == 0)
        {
            beginSession();
            showWaiting();
            sendLiteral("OM1,READY");
            return;
        }

        if (strcmp(message, "OM1,STOP") == 0)
        {
            streamActive = false;
            showStopped("STOPPED");
            sendLiteral("OM1,STOPPED");
            return;
        }

        MotionPosePacket parsed = {};
        ParseResult parseResult = parsePose(message, &parsed);
        if (parseResult != ParseResult::Ok)
        {
            failClosed();
            showFault("PROTOCOL ERROR");
            sendParseError(parseResult);
            return;
        }

        if (!handshakeComplete)
        {
            showFault("NEED HANDSHAKE");
            sendError("OM1,ERR,HANDSHAKE");
            return;
        }

        if (hasLatestPacket &&
            parsed.transportSequence < latestPacket.transportSequence)
        {
            failClosed();
            showFault("SEQUENCE ERROR");
            sendError("OM1,ERR,SEQUENCE");
            return;
        }

        if (hasLatestPacket &&
            parsed.transportSequence == latestPacket.transportSequence)
        {
            if (packetsEqual(parsed, latestPacket))
            {
                sendAcknowledgement(parsed.transportSequence);
                return;
            }

            failClosed();
            showFault("SEQUENCE ERROR");
            sendError("OM1,ERR,SEQUENCE");
            return;
        }

        latestPacket = parsed;
        hasLatestPacket = true;
        streamActive = true;
        lastValidPoseTime = millis();

        applyPose(parsed);
        sendAcknowledgement(parsed.transportSequence);
    }

    ParseResult parsePose(const char* message, MotionPosePacket* packet)
    {
        if (message == nullptr || packet == nullptr ||
            strncmp(message, "OM1,POSE,", 9) != 0)
        {
            return ParseResult::InvalidFormat;
        }

        const char* cursor = message + 9;
        char* end = nullptr;

        if (*cursor < '0' || *cursor > '9')
            return ParseResult::InvalidFormat;

        errno = 0;
        unsigned long long transport = strtoull(cursor, &end, 10);
        if (end == cursor || *end != ',' || errno == ERANGE ||
            transport == 0 || transport > UINT32_MAX)
        {
            return ParseResult::InvalidFormat;
        }

        cursor = end + 1;
        if (*cursor < '0' || *cursor > '9')
            return ParseResult::InvalidFormat;

        errno = 0;
        unsigned long long source = strtoull(cursor, &end, 10);
        if (end == cursor || *end != ',' || errno == ERANGE || source == 0)
            return ParseResult::InvalidFormat;

        cursor = end + 1;
        float heave = strtof(cursor, &end);
        if (end == cursor || *end != ',')
            return ParseResult::InvalidFormat;

        cursor = end + 1;
        float pitch = strtof(cursor, &end);
        if (end == cursor || *end != ',')
            return ParseResult::InvalidFormat;

        cursor = end + 1;
        float yaw = strtof(cursor, &end);
        if (end == cursor || *end != ',')
            return ParseResult::InvalidFormat;

        cursor = end + 1;
        float roll = strtof(cursor, &end);
        if (end == cursor || *end != '\0')
            return ParseResult::InvalidFormat;

        if (!isfinite(heave) || !isfinite(pitch) ||
            !isfinite(yaw) || !isfinite(roll))
        {
            return ParseResult::NonFinite;
        }

        if (fabsf(heave) > maximumHeaveMeters ||
            fabsf(pitch) > maximumPitchDegrees ||
            fabsf(yaw) > maximumYawDegrees ||
            fabsf(roll) > maximumRollDegrees)
        {
            return ParseResult::OutOfRange;
        }

        packet->transportSequence = static_cast<uint32_t>(transport);
        packet->sourceSequence = static_cast<uint64_t>(source);
        packet->heaveMeters = heave;
        packet->pitchDegrees = pitch;
        packet->yawDegrees = yaw;
        packet->rollDegrees = roll;
        return ParseResult::Ok;
    }

    bool packetsEqual(const MotionPosePacket& left,
                      const MotionPosePacket& right)
    {
        return left.transportSequence == right.transportSequence &&
               left.sourceSequence == right.sourceSequence &&
               left.heaveMeters == right.heaveMeters &&
               left.pitchDegrees == right.pitchDegrees &&
               left.yawDegrees == right.yawDegrees &&
               left.rollDegrees == right.rollDegrees;
    }

    void beginSession()
    {
        handshakeComplete = true;
        streamActive = false;
        hasLatestPacket = false;
        latestPacket = MotionPosePacket{};
        centerServos();
    }

    void failClosed()
    {
        handshakeComplete = false;
        streamActive = false;
        hasLatestPacket = false;
        latestPacket = MotionPosePacket{};
        centerServos();
    }

    void applyPose(const MotionPosePacket& packet)
    {
        // Pitch and roll are constrained to +/-10 degrees. Map both axes to
        // the previously verified, unloaded demo range of 70 to 110.
        int pitchServoAngle = static_cast<int>(
            servoCenterDegrees + packet.pitchDegrees * 2.0f);
        pitchServoAngle = constrain(pitchServoAngle,
                                    servoMinimumDegrees,
                                    servoMaximumDegrees);

        int rollServoAngle = static_cast<int>(
            servoCenterDegrees + packet.rollDegrees * 2.0f);
        rollServoAngle = constrain(rollServoAngle,
                                   servoMinimumDegrees,
                                   servoMaximumDegrees);

        pitchServo.write(pitchServoAngle);
        rollServo.write(rollServoAngle);
        setRgb(false, true, false);

        // Serial remains at 20 Hz; the LCD is deliberately limited to 10 Hz.
        unsigned long now = millis();
        if (now - lastDisplayTime < displayIntervalMs)
            return;

        lastDisplayTime = now;

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

    void centerServos()
    {
        pitchServo.write(servoCenterDegrees);
        rollServo.write(servoCenterDegrees);
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

    void sendParseError(ParseResult result)
    {
        switch (result)
        {
            case ParseResult::NonFinite:
                sendError("OM1,ERR,NONFINITE");
                break;

            case ParseResult::OutOfRange:
                sendError("OM1,ERR,RANGE");
                break;

            default:
                sendError("OM1,ERR,FORMAT");
                break;
        }
    }

    void sendError(const char* message)
    {
        unsigned long now = millis();

        if (hasSentErrorResponse &&
            now - lastErrorResponseTime < errorResponseIntervalMs)
        {
            return;
        }

        if (sendLiteral(message))
        {
            lastErrorResponseTime = now;
            hasSentErrorResponse = true;
        }
    }

    bool sendLiteral(const char* message)
    {
        if (!Serial)
            return false;

        size_t expectedBytes = strlen(message) + 2;
        return Serial.println(message) == expectedBytes;
    }

    void sendAcknowledgement(uint32_t transportSequence)
    {
        if (!Serial)
            return;

        Serial.print("OM1,ACK,");
        Serial.println(transportSequence);
    }

    void checkCommandWatchdog()
    {
        if (streamActive &&
            millis() - lastValidPoseTime > commandTimeoutMs)
        {
            streamActive = false;
            showStopped("WATCHDOG");
            sendLiteral("OM1,WATCHDOG");
        }
    }
}
