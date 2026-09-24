#include <Arduino.h>

#include "MotionProtocol.h"
#include "MotionFirmwareState.h"
#include "MotionHardware.h"

namespace
{
    const unsigned long commandTimeoutMs = 250;
    const unsigned long errorResponseIntervalMs = 50;
    const size_t maximumBytesPerLoop = 64;
    const size_t maximumMessagesPerLoop = 4;

    char inputBuffer[160];
    size_t inputLength = 0;
    bool inputOverflow = false;

    MotionFirmwareState protocolState;
    unsigned long lastValidPoseTime = 0;
    unsigned long lastErrorResponseTime = 0;
    bool hasSentErrorResponse = false;

    void readSerialMessages();
    void processMessage(const char* message);
    void sendParseError(MotionParseResult result);
    void sendError(const char* message);
    bool sendLiteral(const char* message);
    void sendAcknowledgement(uint32_t transportSequence);
    void checkCommandWatchdog();
}

void setup()
{
    MotionHardware::begin();
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
                    protocolState.failClosed();
                    MotionHardware::fault("INPUT OVERFLOW");
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
            protocolState.beginSession();
            MotionHardware::beginSession();
            sendLiteral("OM1,READY");
            return;
        }

        if (strcmp(message, "OM1,STOP") == 0)
        {
            protocolState.stop();
            MotionHardware::stop("STOPPED");
            sendLiteral("OM1,STOPPED");
            return;
        }

        MotionPosePacket parsed = {};
        MotionParseResult result = parseMotionPosePacket(message, &parsed);
        if (result != MotionParseResult::Ok)
        {
            protocolState.failClosed();
            MotionHardware::fault("PROTOCOL ERROR");
            sendParseError(result);
            return;
        }

        MotionFirmwareResult acceptance = protocolState.acceptPose(parsed);
        if (acceptance == MotionFirmwareResult::HandshakeRequired)
        {
            MotionHardware::fault("NEED HANDSHAKE");
            sendError("OM1,ERR,HANDSHAKE");
            return;
        }
        if (acceptance == MotionFirmwareResult::Duplicate)
        {
            sendAcknowledgement(parsed.transportSequence);
            return;
        }
        if (acceptance != MotionFirmwareResult::Accepted)
        {
            MotionHardware::fault("SEQUENCE ERROR");
            sendError("OM1,ERR,SEQUENCE");
            return;
        }

        unsigned long now = millis();
        lastValidPoseTime = now;
        MotionHardware::applyPose(parsed, now);
        sendAcknowledgement(parsed.transportSequence);
    }

    void sendParseError(MotionParseResult result)
    {
        switch (result)
        {
            case MotionParseResult::NonFinite:
                sendError("OM1,ERR,NONFINITE");
                break;
            case MotionParseResult::OutOfRange:
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
        if (protocolState.isStreamActive() &&
            millis() - lastValidPoseTime > commandTimeoutMs)
        {
            protocolState.stop();
            MotionHardware::stop("WATCHDOG");
            sendLiteral("OM1,WATCHDOG");
        }
    }
}
