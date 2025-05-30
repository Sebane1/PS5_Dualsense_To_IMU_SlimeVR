﻿using Everything_To_IMU_SlimeVR.Utility;
using System.Diagnostics;
using System.Numerics;

namespace Everything_To_IMU_SlimeVR.Tracking {
    internal class SensorOrientation : IDisposable {
        private Quaternion currentOrientation = Quaternion.Identity;
        private Vector3 accelerometerData = Vector3.Zero;
        private Vector3 gyroData = Vector3.Zero;
        private float _deltaTime = 0.02f; // Example time step (e.g., 50Hz)
        private float _previousTime;
        private bool _useCustomFusion;

        // Complementary filter parameters
        private float alpha = 0.98f; // Weighting factor for blending
        private IBodyTracker _bodyTracker;
        private int _index;
        private SensorType _sensorType;
        private Vector3 _accellerometerVectorCalibration;
        private Vector3 _gyroVectorCalibration;
        Stopwatch stopwatch = new Stopwatch();
        bool _calibratedRotation = false;
        private bool disposed;
        private Vector3 _magnetometer;
        private float gyroYawRate;
        private float gyroDriftCompensation;
        private float yawRadians;
        private float yawDegrees;
        private Vector3 _accelerometer;
        private Vector3 _gyro;

        public Quaternion CurrentOrientation { get => currentOrientation; set => currentOrientation = value; }
        public Vector3 AccelerometerData { get => accelerometerData; set => accelerometerData = value; }
        public Vector3 GyroData { get => gyroData; set => gyroData = value; }
        public float YawRadians { get => yawRadians; set => yawRadians = value; }
        public float YawDegrees { get => yawDegrees; set => yawDegrees = value; }

        public SensorOrientation(int index, SensorType sensorType) {
            _index = index;
            _sensorType = sensorType;
            stopwatch.Start();
            if (_sensorType == SensorType.Bluetooth) {
                Task.Run(() => {
                    while (!disposed) {
                        Update();
                        Thread.Sleep(16);
                    }
                });
            }
        }

        private JSL.MOTION_STATE GetReleventMotionState() {
            switch (_sensorType) {
                case SensorType.Bluetooth:
                    return JSL.JslGetMotionState(_index);
                case SensorType.ThreeDs:
                    return Forwarded3DSDataManager.DeviceMap.ElementAt(_index).Value;
            }
            return new JSL.MOTION_STATE();
        }
        public enum SensorType {
            Bluetooth = 0,
            ThreeDs = 1,
            Wiimote = 2,
            Nunchuck = 3
        }
        private void RefreshSensorData() {
            var sensorData = GetReleventMotionState();
            _accelerometer = new Vector3(sensorData.gravX, sensorData.gravY, sensorData.gravZ);
            _gyro = new Vector3(sensorData.accelX, sensorData.accelY, sensorData.accelZ);
        }

        public void Recalibrate() {
            RefreshSensorData();
            //_accellerometerVectorCalibration = -(_accelerometer);
            //_gyroVectorCalibration = -(_gyro);
        }
        // Update method to simulate gyroscope and accelerometer data fusion
        public void Update() {
            if (!disposed) {
                switch (_sensorType) {
                    case SensorType.Bluetooth:
                        var motionState = JSL.JslGetMotionState(_index);
                        currentOrientation = new Quaternion(-motionState.quatX, motionState.quatZ, motionState.quatY, motionState.quatW);
                        break;
                }
            }
        }

        // Get orientation from accelerometer data (pitch and roll)
        private Quaternion GetOrientationFromAccelerometer(Vector3 accelData) {
            // Normalize the accelerometer data (gravity vector)
            accelData = Vector3.Normalize(accelData);

            // Assuming gravity vector points along the Z-axis
            float pitch = (float)Math.Atan2(accelData.Y, accelData.Z);
            float roll = (float)Math.Atan2(-accelData.X, Math.Sqrt(accelData.Y * accelData.Y + accelData.Z * accelData.Z));

            // Create a quaternion based on the pitch and roll (ignore yaw for now)
            Quaternion orientation = Quaternion.CreateFromYawPitchRoll(0, pitch, roll);

            return orientation;
        }

        public bool IsValid(Quaternion quaternion) {
            bool isNaN = float.IsNaN(quaternion.X + quaternion.Y + quaternion.Z + quaternion.W);
            bool isZero = quaternion.X == 0 && quaternion.Y == 0 && quaternion.Z == 0 && quaternion.W == 0;
            return !(isNaN || isZero);
        }

        // Get delta rotation from gyroscope data
        private Quaternion GetDeltaRotationFromGyroscope(Vector3 gyroData, float deltaTime) {
            // Calculate the angle from the gyroscope angular velocity
            float angle = gyroData.Length() * deltaTime;

            if (angle > 0) {
                // Normalize the gyro data to get the rotation axis
                Vector3 axis = Vector3.Normalize(gyroData);

                // Calculate quaternion from the axis-angle representation
                float halfAngle = angle / 2.0f;
                float sinHalfAngle = (float)Math.Sin(halfAngle);
                float cosHalfAngle = (float)Math.Cos(halfAngle);

                gyroYawRate += gyroData.Z * deltaTime;
                yawRadians = gyroDriftCompensation * (yawRadians + gyroData.Z * deltaTime) + (1 - gyroDriftCompensation) * gyroYawRate;
                if (yawRadians > Math.PI) {
                    yawRadians -= (float)(2 * Math.PI);
                } else if (yawRadians < -Math.PI) {
                    yawRadians += (float)(2 * Math.PI);
                }
                yawDegrees = yawRadians.ConvertRadiansToDegrees();
                return new Quaternion(axis.X * sinHalfAngle, axis.Y * sinHalfAngle, axis.Z * sinHalfAngle, cosHalfAngle);
            } else {
                return Quaternion.Identity;
            }
        }

        public void Dispose() {
            disposed = true;
        }
    }
}
