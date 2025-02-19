﻿using PS5_Dualsense_To_IMU_SlimeVR.SlimeVR;
using PS5_Dualsense_To_IMU_SlimeVR.Utility;
using System;
using System.Numerics;
using Wujek_Dualsense_API;

namespace PS5_Dualsense_To_IMU_SlimeVR.Tracking {
    internal class DualsenseTracker {
        private string _debug;
        private int _index;
        private int _id;
        private SensorOrientation sensorOrientation;
        private string macSpoof;
        private UDPHandler udpHandler;
        private Vector3 rotationCalibration;
        private bool _ready;
        private bool _disconnected;
        private string _rememberedDualsenseId;
        private Dualsense dualsense;
        private string _lastDualSenseId;

        public DualsenseTracker(int index, string dualsenseId, Color colour) {
            _lastDualSenseId = dualsenseId;
            Initialize(index, dualsenseId, colour);
        }
        public async void Initialize(int index, string dualsenseId, Color colour) {
            Task.Run(async () => {
                _index = index;
                _id = index + 1;
                var rememberedColour = colour;
                _rememberedDualsenseId = dualsenseId;
                dualsense = new Dualsense(_rememberedDualsenseId);
                dualsense.Connection.ControllerDisconnected += Connection_ControllerDisconnected;
                dualsense.Start();
                dualsense.SetLightbar(rememberedColour.R, rememberedColour.G, rememberedColour.B);
                sensorOrientation = new SensorOrientation(dualsense);
                await Task.Delay(10000);
                macSpoof = dualsense.DeviceID.Split("&")[3];
                udpHandler = new UDPHandler("Dualsense5", _id,
                 new byte[] { (byte)macSpoof[0], (byte)macSpoof[1], (byte)macSpoof[2], (byte)macSpoof[3], (byte)macSpoof[4], (byte)macSpoof[5] });
                rotationCalibration = -sensorOrientation.CurrentOrientation.QuaternionToEuler();
                _ready = true;
            });
        }

        private void Connection_ControllerDisconnected(object? sender, ConnectionStatus.Controller e) {
            _ready = false;
            _disconnected = true;
        }

        public async Task<bool> Update() {
            if (_ready) {
                Vector3 euler = sensorOrientation.CurrentOrientation.QuaternionToEuler() + rotationCalibration;
                Vector3 gyro = sensorOrientation.GyroData;
                Vector3 acceleration = sensorOrientation.AccelerometerData;
                _debug =
                $"Device Id: {macSpoof}\r\n" +
                $"Quaternion Rotation:\r\n" +
                $"X:{sensorOrientation.CurrentOrientation.X}, " +
                $"Y:{sensorOrientation.CurrentOrientation.Y}, " +
                $"Z:{sensorOrientation.CurrentOrientation.Z}, " +
                $"W:{sensorOrientation.CurrentOrientation.W}\r\n" +
                $"Euler Rotation:\r\n" +
                $"X:{euler.X}, Y:{euler.Y}, Z:{euler.Z}" +
                $"\r\nGyro:\r\n" +
                $"X:{gyro.X}, Y:{gyro.Y}, Z:{gyro.Z}" +
                $"\r\nAcceleration:\r\n" +
                $"X:{acceleration.X}, Y:{acceleration.Y}, Z:{acceleration.Z}";

                await udpHandler.SetSensorRotation(new Vector3(-euler.X, euler.Y, euler.Z).ToQuaternion());
                await udpHandler.SetSensorBattery(dualsense.Battery.Level / 100f);
            }
            return _ready;
        }

        public string Debug { get => _debug; set => _debug = value; }
        public bool Ready { get => _ready; set => _ready = value; }
        public bool Disconnected { get => _disconnected; set => _disconnected = value; }
        public string RememberedDualsenseId { get => _rememberedDualsenseId; set => _rememberedDualsenseId = value; }
    }
}