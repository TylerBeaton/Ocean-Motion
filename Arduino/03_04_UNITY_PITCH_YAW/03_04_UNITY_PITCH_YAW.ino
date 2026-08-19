/*
  Unity calibrated pitch-and-yaw commands for Arduino UNO R4 WiFi.

  Expected newline-terminated commands from Unity:
    SERVO,90,90
    STOP

  Unity owns joint calibration, wrapping, inversion, mechanical limits,
  and angle-to-servo scaling. Arduino validates the final commands,
  writes them directly, and provides the local safety timeout.

  Hardware:
    16x2 LCD with I2C interface at address 0x27
    Yaw servo signal connected to pin 9
    Pitch servo signal connected to pin 10
    Servos powered from an appropriate external 5 V supply
    External supply ground connected to Arduino ground
*/

#include <LiquidCrystal_I2C.h>
#include <stdlib.h>
#include <string.h>
#include <Servo.h>

LiquidCrystal_I2C lcd(0x27, 16, 2);

const unsigned long commandTimeoutMs = 1000;

const int pitchServoPin = 10;
const int yawServoPin = 9;

const int pitchMinimumCommand = 0;
const int pitchNeutralCommand = 90;
const int pitchMaximumCommand = 180;

const int yawMinimumCommand = 0;
const int yawNeutralCommand = 90;
const int yawMaximumCommand = 180;

char inputBuffer[64];
size_t inputLength = 0;
bool inputOverflow = false;

unsigned long lastCommandTime = 0;
bool telemetryActive = false;

Servo pitchServo;
Servo yawServo;
int currentPitchCommand = pitchNeutralCommand;
int currentYawCommand = yawNeutralCommand;

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

bool parseServoCommands(
    const char* message,
    int* pitchCommand,
    int* yawCommand
)
{
    if (strncmp(message, "SERVO,", 6) != 0)
    {
        return false;
    }

    const char* cursor = message + 6;
    char* end;

    long parsedPitch = strtol(cursor, &end, 10);
    if (end == cursor || *end != ',')
    {
        return false;
    }

    cursor = end + 1;
    long parsedYaw = strtol(cursor, &end, 10);
    if (end == cursor || *end != '\0')
    {
        return false;
    }

    *pitchCommand = (int)constrain(
        parsedPitch,
        (long)pitchMinimumCommand,
        (long)pitchMaximumCommand
    );
    *yawCommand = (int)constrain(
        parsedYaw,
        (long)yawMinimumCommand,
        (long)yawMaximumCommand
    );

    return true;
}

void moveServosToNeutral()
{
    currentPitchCommand = pitchNeutralCommand;
    currentYawCommand = yawNeutralCommand;
    pitchServo.write(currentPitchCommand);
    yawServo.write(currentYawCommand);
}

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

    int pitchCommand;
    int yawCommand;

    if (parseServoCommands(message, &pitchCommand, &yawCommand))
    {
        bool streamWasInactive = !telemetryActive;

        lastCommandTime = millis();
        telemetryActive = true;

        currentPitchCommand = pitchCommand;
        currentYawCommand = yawCommand;
        pitchServo.write(pitchCommand);
        yawServo.write(yawCommand);

        displayServoCommands(pitchCommand, yawCommand);

        if (streamWasInactive)
        {
            Serial.println("SERVO OK");
        }
    }
}

void displayServoCommands(int pitchCommand, int yawCommand)
{
    char line[17];

    snprintf(
        line,
        sizeof(line),
        "PC:%3d YC:%3d",
        pitchCommand,
        yawCommand
    );
    writeLcdLine(0, line);
    writeLcdLine(1, "Calibrated cmd");
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
