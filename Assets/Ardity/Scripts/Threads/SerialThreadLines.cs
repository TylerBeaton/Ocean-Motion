/**
 * Ardity (Serial Communication for Arduino + Unity)
 * Author: Daniel Wilches <dwilches@gmail.com>
 *
 * This work is released under the Creative Commons Attributions license.
 * https://creativecommons.org/licenses/by/2.0/
 */

using UnityEngine;

using System.Collections;
using System.IO.Ports;
using System.Text;
using System.Threading;

public sealed class BoundedLineBuffer
{
    public const int MaxLineLength = 192;
    private const int MaxCompletedLines = 4;

    private readonly StringBuilder currentLine = new StringBuilder(MaxLineLength);
    private readonly Queue completedLines = new Queue();
    private bool discardingOverlongLine;

    public int BufferedCharacterCount => currentLine.Length;

    public void Append(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        for (int i = 0; i < text.Length; i++)
        {
            char character = text[i];
            if (character == '\n')
            {
                if (!discardingOverlongLine && completedLines.Count < MaxCompletedLines)
                {
                    int length = currentLine.Length;
                    if (length != 0 && currentLine[length - 1] == '\r')
                        length--;
                    completedLines.Enqueue(currentLine.ToString(0, length));
                }

                currentLine.Length = 0;
                discardingOverlongLine = false;
                continue;
            }

            if (discardingOverlongLine)
                continue;

            if (currentLine.Length == MaxLineLength)
            {
                currentLine.Length = 0;
                discardingOverlongLine = true;
                continue;
            }

            currentLine.Append(character);
        }
    }

    public string ReadLine()
    {
        return completedLines.Count == 0
            ? null
            : (string)completedLines.Dequeue();
    }
}

/**
 * This class contains methods that must be run from inside a thread and others
 * that must be invoked from Unity. Both types of methods are clearly marked in
 * the code, although you, the final user of this library, don't need to even
 * open this file unless you are introducing incompatibilities for upcoming
 * versions.
 * 
 * For method comments, refer to the base class.
 */
public class SerialThreadLines : AbstractSerialThread
{
    private const int MaxCharactersPerRead = 64;
    private BoundedLineBuffer lineBuffer = new BoundedLineBuffer();
    private readonly char[] readBuffer = new char[MaxCharactersPerRead];

    public SerialThreadLines(string portName,
                             int baudRate,
                             int delayBeforeReconnecting,
                             int maxUnreadMessages)
        : base(portName, baudRate, delayBeforeReconnecting, maxUnreadMessages, true)
    {
    }

    protected override void SendToWire(object message, SerialPort serialPort)
    {
        serialPort.WriteLine((string) message);
    }

    protected override void ResetReceiveBuffer()
    {
        // Drop completed lines, partial text, and overflow state together.
        lineBuffer = new BoundedLineBuffer();
    }

    protected override object ReadFromWire(SerialPort serialPort)
    {
        string completedLine = lineBuffer.ReadLine();
        if (completedLine != null)
            return completedLine;

        int availableCharacters = serialPort.BytesToRead;
        if (availableCharacters <= 0)
        {
            Thread.Sleep(1);
            return null;
        }

        int readCount = serialPort.Read(
            readBuffer,
            0,
            System.Math.Min(availableCharacters, MaxCharactersPerRead));
        if (readCount > 0)
            lineBuffer.Append(new string(readBuffer, 0, readCount));
        return lineBuffer.ReadLine();
    }
}
