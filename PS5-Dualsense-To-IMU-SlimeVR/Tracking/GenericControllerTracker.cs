﻿using PS5_Dualsense_To_IMU_SlimeVR.SlimeVR;
using System;
using System.Numerics;
using Wujek_Dualsense_API;

using Valve.VR;
using OVRSharp.Math;
namespace PS5_Dualsense_To_IMU_SlimeVR.Tracking {
    internal class GenericControllerTracker : IDisposable, IBodyTracker {
        private string _debug;
        private int _index;
        private int _id;
        private string macSpoof;
        private UDPHandler udpHandler;
        private Vector3 _rotationCalibration;
        private float _calibratedHeight;
        private bool _ready;
        private bool _disconnected;
        private string _rememberedStringId;
        private string _lastDualSenseId;
        private bool _simulateThighs = true;
        private FalseThighTracker _falseThighTracker;
        private float _lastHmdPositon;
        private Vector3 _rotation;
        private Vector3 _euler;
        private Vector3 _gyro;
        private Vector3 _acceleration;

        public GenericControllerTracker(int index, Color colour) {
            Initialize(index, colour);
        }
        public async void Initialize(int index, Color colour) {
            Task.Run(async () => {
                _index = index;
                _id = index + 1;
                var rememberedColour = colour;
                _rememberedStringId = index + " " + JSL.JslGetControllerType(index);
                JSL.JslSetLightColour(index, colour.ToArgb());
                await Task.Delay(10000);
                macSpoof = _rememberedStringId + "GenericController";
                udpHandler = new UDPHandler("GenericController" + _rememberedStringId, _id,
                 new byte[] { (byte)macSpoof[0], (byte)macSpoof[1], (byte)macSpoof[2], (byte)macSpoof[3], (byte)macSpoof[4], (byte)macSpoof[5] });
                var value = JSL.JslGetMotionState(index);
                _rotationCalibration = -(new Quaternion(value.quatX, value.quatY, value.quatZ, value.quatW)).QuaternionToEuler();
                if (_simulateThighs) {
                    _falseThighTracker = new FalseThighTracker(this);
                }
                _calibratedHeight = HmdReader.GetHMDHeight();
                _ready = true;
            });
        }

        private void Connection_ControllerDisconnected(object? sender, ConnectionStatus.Controller e) {
            _ready = false;
            _disconnected = true;
        }

        public async Task<bool> Update() {
            if (_ready) {
                var hmdHeight = HmdReader.GetHMDHeight();
                bool sitting = hmdHeight < _calibratedHeight / 2;
                var hmdRotation = HmdReader.GetHMDRotation();
                float hmdEuler = hmdRotation.GetYawFromQuaternion();
                if (!sitting) {
                    _lastHmdPositon = -hmdEuler;
                }
                var value = JSL.JslGetMotionState(_index);
                _rotation = (new Quaternion(value.quatX, value.quatY, value.quatZ, value.quatW)).QuaternionToEuler() + _rotationCalibration;
                _euler = _rotation;
                _debug =
                $"Device Id: {macSpoof}\r\n" +
                $"Euler Rotation:\r\n" +
                $"X:{_rotation.X}, Y:{_rotation.Y}, Z:{_rotation.Z}\r\n" +
                $"HMD Rotation:\r\n" +
                $"Y:{hmdEuler}\r\n"
                + _falseThighTracker.Debug;
                float finalY = !sitting ? _euler.Y : -_euler.Y;
                float finalZ = sitting ? -_euler.Z : -_euler.Z;
                await udpHandler.SetSensorBattery(100);
                if (!_simulateThighs) {
                    await udpHandler.SetSensorRotation(new Vector3(_euler.X, finalY, finalZ + _lastHmdPositon).ToQuaternion());
                } else {
                    await udpHandler.SetSensorRotation((new Vector3(_euler.X, finalY, finalZ + _lastHmdPositon)).ToQuaternion());
                    await _falseThighTracker.Update();
                }
            }
            return _ready;
        }

        public void Dispose() {
            _ready = false;
            _disconnected = true;
        }

        public string Debug { get => _debug; set => _debug = value; }
        public bool Ready { get => _ready; set => _ready = value; }
        public bool Disconnected { get => _disconnected; set => _disconnected = value; }
        public string RememberedDualsenseId { get => _rememberedStringId; set => _rememberedStringId = value; }
        public int Id { get => _id; set => _id = value; }
        public string MacSpoof { get => macSpoof; set => macSpoof = value; }
        public Vector3 Euler { get => _euler; set => _euler = value; }
        public Vector3 Gyro { get => _gyro; set => _gyro = value; }
        public Vector3 Acceleration { get => _acceleration; set => _acceleration = value; }
        public float LastHmdPositon { get => _lastHmdPositon; set => _lastHmdPositon = value; }
    }
}