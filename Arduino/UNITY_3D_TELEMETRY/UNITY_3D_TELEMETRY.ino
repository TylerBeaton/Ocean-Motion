/*
  Unity 3D telemetry display for Arduino UNO R4 WiFi.

  Expected newline-terminated commands from Unity:
    ROT,1.250,-0.500,3.000
    STOP

  Hardware:
    16x2 LCD with I2C interface at address 0x27
*/

#include <LiquidCrystal_I2C.h>
#include <math.h>
#include <stdlib.h>
#include <string.h>

LiquidCrystal_I2C lcd(0x27, 16, 2);

const unsigned long commandTimeoutMs = 1000;

char inputBuffer[64];
size_t inputLength = 0;
bool inputOverflow = false;

unsigned long lastCommandTime = 0;
bool telemetryActive = false;

void setup()
{
    Serial.begin(9600);

    lcd.init();
    lcd.clear();
    lcd.backlight();

    showStatus("Waiting for", "Unity data...");
    Serial.println("READY");
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

// Process ROT telemetry and STOP commands from Unity.
void processMessage(const char* message)
{
    if (strcmp(message, "STOP") == 0)
    {
        telemetryActive = false;
        showStatus("Unity stopped", "Waiting...");
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
        displayRotation(x, y, z);

        if (streamWasInactive)
        {
            Serial.println("ROT OK");
        }
    }
}

// Display Euler X and Y on the first row and Euler Z on the second row.
void displayRotation(float x, float y, float z)
{
    char xText[12];
    char yText[12];
    char zText[12];
    char line[17];

    formatAngle(x, xText, sizeof(xText));
    formatAngle(y, yText, sizeof(yText));
    formatAngle(z, zText, sizeof(zText));

    snprintf(line, sizeof(line), "X:%sY:%s", xText, yText);
    writeLcdLine(0, line);

    snprintf(line, sizeof(line), "Z:%s ROT", zText);
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
        showStatus("Unity timeout", "Waiting...");
    }
}
