# Subproject 00 — Unity RGB LED

## Demonstrates
Unity cycles a material color and sends matching RGB values to an Arduino UNO R4 WiFi, which drives an RGB LED.

## Pairing
- Unity scene: `Scenes/SP00_UnityToArduinoRgb.unity`
- Unity script: `Scripts/SP00_UnityToArduinoRgb.cs`
- Arduino sketch: `Arduino/00_UNITY_RGB_LED/00_UNITY_RGB_LED.ino`

## Interface
- Serial: 9600 baud, newline terminated
- Commands: `RGB,<red>,<green>,<blue>` and `OFF`

## Dependency and reuse
Independent introductory milestone. It validates Unity-to-Arduino serial communication but is not required by later rotation milestones.

## Notes
Uses Ardity for serial communication. RGB values are integers from 0 to 255.
