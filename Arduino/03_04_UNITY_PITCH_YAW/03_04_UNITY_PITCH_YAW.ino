/*
  Unity pitch-and-yaw servo test for Arduino UNO R4 WiFi.

  Expected newline-terminated commands from Unity:
    ROT,1.250,-0.500,3.000
    STOP

  Mapping:
    Unity world X pitch -90..+90 degrees -> pitch servo 0..180
    Unity world Y yaw   -90..+90 degrees -> yaw servo 180..0

  Hardware:
    16x2 LCD with I2C interface at address 0x27
    Yaw servo signal connected to pin 9
    Pitch servo signal connected to pin 10
    Servos powered from an appropriate external 5 V supply
    External supply ground connected to Arduino ground
*/

#include <LiquidCrystal_I2C.h>
#include <math.h>
#include <stdlib.h>
#include <string.h>
#include <Servo.h>

LiquidCrystal_I2C lcd(0x27, 16, 2);

const unsigned long commandTimeoutMs = 1000;

const int pitchServoPin = 10;
const int yawServoPin = 9;
const int servoNeutralAngle = 90;
const int servoMinimumAngle = 0;
const int servoMaximumAngle = 180;
const float inputMinimumDegrees = -90.0f;
const float inputMaximumDegrees = 90.0f;
const bool invertPitchServo = false;
const bool invertYawServo = true;

char inputBuffer[64];
size_t inputLength = 0;
bool inputOverflow = false;

unsigned long lastCommandTime = 0;
bool telemetryActive = false;

Servo pitchServo;
Servo yawServo;
int currentPitchServoAngle = servoNeutralAngle;
int currentYawServoAngle = servoNeutralAngle;

void setup()
{
    Serial.begin(9600);

    lcd.init();
    lcd.clear();
    lcd.backlight();

    showStatus("Waiting for", "Unity data...");
    Serial.println("READY");

    pitchServo.attach(pitchServoPin);
    yawServo.attach(yawServoPin);
    moveServosToNeutral();
}

void loop()
{
    readSerialMessages();
    checkConnectionTimeout();
}

// Read complete newline-terminated commands without blocking.
void readSerialMessages()
{
    while (Serial.available() > 0)
    {
        char incomingCharacter = Serial.read();

        if (incomingCharacter == '\n')
        {
            if (!inputOverflow)
            {
                inputBuffer[inputLength] = '\0';
                processMessage(inputBuffer);
            }

            inputLength = 0;
            inputOverflow = false;
        }
        else if (incomingCharacter != '\r')
        {
            if (inputLength < sizeof(inputBuffer) - 1)
            {
                inputBuffer[inputLength] = incomingCharacter;
                inputLength++;
            }
            else
            {
                inputOverflow = true;
            }
        }
    }
}

// Parse ROT without relying on embedded scanf float support.
bool parseTelemetry(
    const char* message,
    float* x,
    float* y,
    float* z
)
{
    if (strncmp(message, "ROT,", 4) != 0)
    {
        return false;
    }

    const char* cursor = message + 4;
    char* end;

    *x = strtof(cursor, &end);
    if (end == cursor || *end != ',')
    {
        return false;
    }

    cursor = end + 1;
    *y = strtof(cursor, &end);
    if (end == cursor || *end != ',')
    {
        return false;
    }

    cursor = end + 1;
    *z = strtof(cursor, &end);

    return
        end != cursor &&
        *end == '\0' &&
        isfinite(*x) &&
        isfinite(*y) &&
        isfinite(*z);
}

float wrapToSignedDegrees(float angle)
{
    float wrappedAngle = fmod(angle, 360.0f);

    if (wrappedAngle >= 180.0f)
    {
        wrappedAngle -= 360.0f;
    }
    else if (wrappedAngle < -180.0f)
    {
        wrappedAngle += 360.0f;
    }

    return wrappedAngle;
}

int mapRotationToServo(float unityDegrees, bool invert)
{
    float signedAngle = wrapToSignedDegrees(unityDegrees);
    float limitedAngle = constrain(
        signedAngle,
        inputMinimumDegrees,
        inputMaximumDegrees
    );

    float normalizedAngle =
        (limitedAngle - inputMinimumDegrees) /
        (inputMaximumDegrees - inputMinimumDegrees);

    if (invert)
    {
        normalizedAngle = 1.0f - normalizedAngle;
    }

    float servoAngle =
        servoMinimumAngle +
        normalizedAngle *
        (servoMaximumAngle - servoMinimumAngle);

    return round(servoAngle);
}

void moveServosToNeutral()
{
    currentPitchServoAngle = servoNeutralAngle;
    currentYawServoAngle = servoNeutralAngle;
    pitchServo.write(currentPitchServoAngle);
    yawServo.write(currentYawServoAngle);
}

// Process ROT telemetry and STOP commands from Unity.
void processMessage(const char* message)
{
    if (strcmp(message, "STOP") == 0)
    {
        telemetryActive = false;
        moveServosToNeutral();
        showStatus("Unity stopped", "Servos neutral");
        Serial.println("STOPPED");
        return;
    }

    float x;
    float y;
    float z;

    if (parseTelemetry(message, &x, &y, &z))
    {
        bool streamWasInactive = !telemetryActive;

        lastCommandTime = millis();
        telemetryActive = true;

        int pitchServoAngle =
            mapRotationToServo(x, invertPitchServo);
        int yawServoAngle =
            mapRotationToServo(y, invertYawServo);

        currentPitchServoAngle = pitchServoAngle;
        currentYawServoAngle = yawServoAngle;
        pitchServo.write(pitchServoAngle);
        yawServo.write(yawServoAngle);

        displayPitchYaw(x, y);

        if (streamWasInactive)
        {
            Serial.println("ROT OK");
        }
    }
}

// Display pitch/yaw input on row 1 and servo outputs on row 2.
void displayPitchYaw(float pitch, float yaw)
{
    char pitchText[12];
    char yawText[12];
    char line[17];

    formatAngle(pitch, pitchText, sizeof(pitchText));
    formatAngle(yaw, yawText, sizeof(yawText));

    snprintf(line, sizeof(line), "P:%sY:%s", pitchText, yawText);
    writeLcdLine(0, line);

    snprintf(
        line,
        sizeof(line),
        "PS:%3d YS:%3d",
        currentPitchServoAngle,
        currentYawServoAngle
    );
    writeLcdLine(1, line);
}

// Format each angle into a fixed six-character LCD field.
void formatAngle(float value, char* output, size_t outputSize)
{
    dtostrf(value, 6, 2, output);

    if (strlen(output) > 6)
    {
        strncpy(output, "######", outputSize);
        output[outputSize - 1] = '\0';
    }
}

void showStatus(const char* firstLine, const char* secondLine)
{
    writeLcdLine(0, firstLine);
    writeLcdLine(1, secondLine);
}

// Overwrite all 16 columns so old characters never remain onscreen.
void writeLcdLine(byte row, const char* text)
{
    lcd.setCursor(0, row);

    for (byte column = 0; column < 16; column++)
    {
        char character = text[column];
        lcd.print(character == '\0' ? ' ' : character);

        if (character == '\0')
        {
            for (column++; column < 16; column++)
            {
                lcd.print(' ');
            }
            break;
        }
    }
}

void checkConnectionTimeout()
{
    if (
        telemetryActive &&
        millis() - lastCommandTime > commandTimeoutMs
    )
    {
        telemetryActive = false;
        moveServosToNeutral();
        showStatus("Unity timeout", "Servos neutral");
    }
}
