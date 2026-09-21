using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OceanMotion.Subproject06.Editor
{
    public static class MotionSerialTransportChecks
    {
        [MenuItem("Ocean Motion/Subproject 06/Check Serial Transport")]
        public static void Run()
        {
            Type transportType = FindType(
                "OceanMotion.Subproject06.MotionSerialTransport");
            Require(transportType != null, "MotionSerialTransport is not implemented.");

            GameObject testObject = new GameObject("SP06 Serial Transport Check");
            testObject.SetActive(false);
            try
            {
                BoatTelemetryPoseSource source =
                    testObject.AddComponent<BoatTelemetryPoseSource>();
                MotionPipelineController controller =
                    testObject.AddComponent<MotionPipelineController>();
                SerialController serialController =
                    testObject.AddComponent<SerialController>();
                serialController.enabled = false;
                serialController.baudRate = 115200;
                Component transport = testObject.AddComponent(transportType);

                Set(transport, "controller", controller);
                Set(transport, "serialController", serialController);
                Invoke(transport, "Awake");

                Require(
                    (int)GetProperty(transport, "RequiredBaudRate") == 115200,
                    "Serial transport baud contract is not 115200.");
                Invoke(transport, "OnConnectionEvent", true);
                var session = (MotionTransportSession)GetPrivateField(
                    transport,
                    "session");
                session.TryCreateHandshakeMessage(
                    double.MaxValue,
                    out _);
                session.NotifyHandshakeWritten();
                Invoke(transport, "OnMessageArrived", MotionTransportProtocol.ReadyMessage);
                Require(
                    (bool)GetProperty(transport, "IsConnected"),
                    "Connection callback did not update transport state.");
                Require(
                    (bool)GetProperty(transport, "IsFirmwareReady"),
                    "READY message did not update transport state.");
                Require(
                    (uint)GetProperty(transport, "AcknowledgedPoseCount") == 0u,
                    "Transport counted an acknowledgement before receiving one.");

                Invoke(transport, "OnDisable");
                Require(!session.IsConnected, "Disabled transport retained its session.");
                Invoke(transport, "OnEnable");
                Require(session.IsConnected && !session.IsFirmwareReady,
                    "Re-enabled transport did not require a fresh handshake on the existing port.");
                Require(session.TryCreateHandshakeMessage(double.MaxValue, out string resumedHello) &&
                    resumedHello == MotionTransportProtocol.HelloMessage,
                    "Re-enabled transport cannot restart HELLO.");
                Invoke(transport, "OnDisable");
                Invoke(transport, "OnConnectionEvent", false);
                Invoke(transport, "OnEnable");
                Require(!session.IsConnected,
                    "Re-enable invented a connection after the port disconnected.");
                Invoke(transport, "OnConnectionEvent", true);

                var worker = new SerialThreadLines("unused", 115200, 1000, 1);
                MethodInfo outputGate = typeof(AbstractSerialThread).GetMethod(
                    "IsOutputEnabledForConnection",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Require(
                    !(bool)outputGate.Invoke(worker, null),
                    "Line transport bypassed its connection-event gate.");
                var binaryWorker = new SerialThreadBinaryDelimited(
                    "unused", 115200, 1000, 1, 90);
                Require(
                    (bool)outputGate.Invoke(binaryWorker, null),
                    "Custom-delimiter output is blocked waiting for an event it never emits.");
                worker.SendMessage("old-pose");
                worker.SendLatestMessage("new-pose");
                var outputQueue = (System.Collections.Queue)typeof(AbstractSerialThread)
                    .GetField("outputQueue", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(worker);
                Require(
                    outputQueue.Count == 1 && (string)outputQueue.Peek() == "new-pose",
                    "Latest-state delivery retained an old pose.");
                serialController.SendLatestSerialMessage("test");

                Require(
                    typeof(AbstractSerialThread).GetMethod("ReadSentMessage") != null,
                    "Ardity does not expose successful writes for handshake gating.");

                var pollingWorker = new SerialThreadLines("unused", 115200, 1000, 1);
                FieldInfo inputQueueField = typeof(AbstractSerialThread).GetField(
                    "inputQueue",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo outputGateField = typeof(AbstractSerialThread).GetField(
                    "outputEnabledForConnection",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Require(
                    inputQueueField != null && outputGateField != null,
                    "Ardity polling gate fields are unavailable.");
                ((System.Collections.Queue)inputQueueField.GetValue(pollingWorker))
                    .Enqueue(SerialController.SERIAL_DEVICE_CONNECTED);
                SetProtected(serialController, "serialThread", pollingWorker);
                serialController.messageListener = null;
                Require(
                    ReferenceEquals(
                        serialController.ReadSerialMessage(),
                        SerialController.SERIAL_DEVICE_CONNECTED),
                    "Polling mode did not return the connection event.");
                Require(
                    !(bool)outputGateField.GetValue(pollingWorker),
                    "Polling mode opened output before the caller handled the event.");
                serialController.SendLatestSerialMessage("polling-test");
                Require(
                    (bool)outputGateField.GetValue(pollingWorker),
                    "Polling mode did not open output when the caller next sent.");

                Type lineBufferType = FindType("BoundedLineBuffer");
                Require(lineBufferType != null, "Bounded serial line buffer is missing.");
                object lineBuffer = Activator.CreateInstance(lineBufferType);
                Invoke(lineBuffer, "Append", new string('X', 10000));
                Require(
                    (int)GetProperty(lineBuffer, "BufferedCharacterCount") <= 192,
                    "Unterminated serial input grew beyond the line bound.");
                Invoke(lineBuffer, "Append", "\nOM1,ACK,1\n");
                Require(
                    (string)InvokeWithResult(lineBuffer, "ReadLine") == "OM1,ACK,1",
                    "Line buffer did not resynchronize after overlong input.");

                serialController.messageListener = testObject;
                testObject.SetActive(true);
                Invoke(serialController, "OnDisable");
                Require(
                    !(bool)GetProperty(transport, "IsConnected"),
                    "SerialController disable did not notify its listener after teardown.");
                CheckSilentListenerTeardown(serialController);

                serialController.baudRate = 9600;
                Require(
                    !(bool)GetProperty(transport, "IsConfigurationValid"),
                    "Transport accepted a baud rate other than 115200.");

                CheckReconnectBuffer("OM1,ACK,9\nOM1,REA");
                CheckReconnectBuffer(new string('X', 193));

                Debug.Log("SP06_SERIAL_TRANSPORT_CHECKS: passed");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testObject);
            }
        }

        private static void CheckSilentListenerTeardown(SerialController serialController)
        {
            GameObject unavailableListener = new GameObject(
                "SP06 Unavailable Serial Listener");
            unavailableListener.SetActive(false);
            bool missingReceiverError = false;

            void CaptureLog(string condition, string stackTrace, LogType type)
            {
                if (condition.Contains("SendMessage OnConnectionEvent has no receiver"))
                    missingReceiverError = true;
            }

            try
            {
                serialController.messageListener = unavailableListener;
                SetProtected(
                    serialController,
                    "serialThread",
                    new SerialThreadLines("unused", 115200, 1000, 1));
                Application.logMessageReceived += CaptureLog;
                Invoke(serialController, "OnDisable");
            }
            finally
            {
                Application.logMessageReceived -= CaptureLog;
                UnityEngine.Object.DestroyImmediate(unavailableListener);
            }

            Require(!missingReceiverError,
                "SerialController teardown required an unavailable listener.");
        }

        private static void CheckReconnectBuffer(string staleInput)
        {
            // Null port cannot open hardware; connection preparation still runs.
            var worker = new SerialThreadLines(null, 115200, 1000, 1);
            ((BoundedLineBuffer)GetPrivateField(worker, "lineBuffer")).Append(staleInput);
            try
            {
                typeof(AbstractSerialThread).GetMethod(
                    "AttemptConnection", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(worker, null);
                throw new InvalidOperationException("Null port unexpectedly connected.");
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException is ArgumentException ||
                    exception.InnerException is System.IO.IOException)
            {
                // Expected: no physical port is used by this check.
            }

            var buffer = (BoundedLineBuffer)GetPrivateField(worker, "lineBuffer");
            Require(buffer.BufferedCharacterCount == 0 && buffer.ReadLine() == null,
                "Reconnect retained partial text or completed lines.");
            buffer.Append("OM1,READY\n");
            Require(buffer.ReadLine() == "OM1,READY" && buffer.ReadLine() == null,
                "Reconnect retained overflow state or corrupted the first fresh response.");
        }

        private static object GetProperty(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(name);
            Require(property != null, $"Missing {name} property.");
            return property.GetValue(target);
        }

        private static void Invoke(object target, string method, params object[] arguments)
        {
            MethodInfo targetMethod = target.GetType().GetMethod(
                method,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Require(targetMethod != null, $"Missing {method} method.");
            targetMethod.Invoke(target, arguments);
        }

        private static object InvokeWithResult(
            object target,
            string method,
            params object[] arguments)
        {
            MethodInfo targetMethod = target.GetType().GetMethod(
                method,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Require(targetMethod != null, $"Missing {method} method.");
            return targetMethod.Invoke(target, arguments);
        }

        private static void Set(object target, string field, object value)
        {
            FieldInfo targetField = target.GetType().GetField(
                field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(targetField != null, $"Missing {field} field.");
            targetField.SetValue(target, value);
        }

        private static void SetProtected(object target, string field, object value)
        {
            FieldInfo targetField = target.GetType().GetField(
                field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(targetField != null, $"Missing {field} field.");
            targetField.SetValue(target, value);
        }

        private static object GetPrivateField(object target, string field)
        {
            FieldInfo targetField = target.GetType().GetField(
                field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(targetField != null, $"Missing {field} field.");
            return targetField.GetValue(target);
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
