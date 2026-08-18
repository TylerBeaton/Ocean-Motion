// ------------------------------------------------------------
// Unity-controlled RGB LED for Arduino UNO R4
// Expected commands:
// RGB,255,128,0
// OFF
// ------------------------------------------------------------

// Pins are flipped! Red is usually 9 and blue is usually 11

const int redPin = 11;
const int greenPin = 10;
const int bluePin = 9;

// Keep this set to the value that works with your wiring.
const bool commonAnode = false;

// Adjust these values to calibrate the physical LED.

// These are the settings I used to calibrate it by approximation.
const float redGain = 255.0f / 255.0f;
const float greenGain = 24.0f / 255.0f;
const float blueGain = 32.0f / 255.0f;

// Unity sends fifteen updates per second, or once every 70 ms.
const unsigned long transitionDurationMs = 220;

// Turn the LED off if Unity stops sending commands.
const unsigned long commandTimeoutMs = 1000;

// Serial message buffer.
char inputBuffer[32];
size_t inputLength = 0;

// Current displayed colour.
float currentRed = 0;
float currentGreen = 0;
float currentBlue = 0;

// Colour at the beginning of the current transition.
float startRed = 0;
float startGreen = 0;
float startBlue = 0;

// Colour received from Unity.
float targetRed = 0;
float targetGreen = 0;
float targetBlue = 0;

unsigned long transitionStart = 0;
unsigned long lastCommandTime = 0;

bool commandStreamActive = false;

void setup()
{
    Serial.begin(9600);

    pinMode(redPin, OUTPUT);
    pinMode(greenPin, OUTPUT);
    pinMode(bluePin, OUTPUT);

    transitionStart = millis();

    turnOffLed();
}

void loop()
{
    readSerialMessages();
    updateLedTransition();
    checkConnectionTimeout();
}

// ------------------------------------------------------------
// Read complete newline-terminated commands without blocking.
// ------------------------------------------------------------

void readSerialMessages()
{
    while (Serial.available() > 0)
    {
        char incomingCharacter = Serial.read();

        if (incomingCharacter == '\n')
        {
            inputBuffer[inputLength] = '\0';

            processMessage(inputBuffer);

            inputLength = 0;
        }
        else if (
            incomingCharacter != '\r' &&
            inputLength < sizeof(inputBuffer) - 1
        )
        {
            inputBuffer[inputLength] = incomingCharacter;
            inputLength++;
        }
    }
}

// ------------------------------------------------------------
// Process RGB and OFF commands.
// ------------------------------------------------------------

void processMessage(const char* message)
{
    if (strcmp(message, "OFF") == 0)
    {
        turnOffLed();
        commandStreamActive = false;

        Serial.println("LED turned off");
        return;
    }

    int red;
    int green;
    int blue;

    int valuesRead = sscanf(
        message,
        "RGB,%d,%d,%d",
        &red,
        &green,
        &blue
    );

    if (valuesRead == 3)
    {
        lastCommandTime = millis();
        commandStreamActive = true;

        beginColorTransition(
            constrain(red, 0, 255),
            constrain(green, 0, 255),
            constrain(blue, 0, 255)
        );
    }
}

// ------------------------------------------------------------
// Start fading toward a newly received colour.
// ------------------------------------------------------------

void beginColorTransition(int red, int green, int blue)
{
    // Calculate the current point in the existing transition.
    updateLedTransition();

    startRed = currentRed;
    startGreen = currentGreen;
    startBlue = currentBlue;

    targetRed = red;
    targetGreen = green;
    targetBlue = blue;

    transitionStart = millis();
}

// ------------------------------------------------------------
// Smoothly interpolate toward the latest target colour.
// ------------------------------------------------------------

void updateLedTransition()
{
    unsigned long elapsed = millis() - transitionStart;

    float amount =
        (float)elapsed / (float)transitionDurationMs;

    amount = constrain(amount, 0.0f, 1.0f);

    currentRed =
        startRed + (targetRed - startRed) * amount;

    currentGreen =
        startGreen + (targetGreen - startGreen) * amount;

    currentBlue =
        startBlue + (targetBlue - startBlue) * amount;

    setLedColor(
        round(currentRed),
        round(currentGreen),
        round(currentBlue)
    );
}

// ------------------------------------------------------------
// Shut down if communication from Unity stops.
// ------------------------------------------------------------

void checkConnectionTimeout()
{
    if (
        commandStreamActive &&
        millis() - lastCommandTime > commandTimeoutMs
    )
    {
        turnOffLed();
        commandStreamActive = false;
    }
}

// ------------------------------------------------------------
// Apply channel calibration, polarity, and PWM output.
// ------------------------------------------------------------

void setLedColor(int red, int green, int blue)
{
    red = constrain(red, 0, 255);
    green = constrain(green, 0, 255);
    blue = constrain(blue, 0, 255);

    // Balance the physical brightness of each LED channel.
    red = round(red * redGain);
    green = round(green * greenGain);
    blue = round(blue * blueGain);

    red = constrain(red, 0, 255);
    green = constrain(green, 0, 255);
    blue = constrain(blue, 0, 255);

    if (commonAnode)
    {
        red = 255 - red;
        green = 255 - green;
        blue = 255 - blue;
    }

    analogWrite(redPin, red);
    analogWrite(greenPin, green);
    analogWrite(bluePin, blue);
}

// ------------------------------------------------------------
// Immediately turn off every channel.
// ------------------------------------------------------------

void turnOffLed()
{
    currentRed = 0;
    currentGreen = 0;
    currentBlue = 0;

    startRed = 0;
    startGreen = 0;
    startBlue = 0;

    targetRed = 0;
    targetGreen = 0;
    targetBlue = 0;

    transitionStart = millis();

    setLedColor(0, 0, 0);
}