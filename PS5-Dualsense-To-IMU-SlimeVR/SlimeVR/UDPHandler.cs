﻿using System.Net.Sockets;
using System.Numerics;
using System.Text;
using static PS5_Dualsense_To_IMU_SlimeVR.SlimeVR.FirmwareConstants;

namespace PS5_Dualsense_To_IMU_SlimeVR.SlimeVR {
    public class UDPHandler {
        private PacketBuilder packetBuilder;
        private int slimevrPort = 6969;
        UdpClient udpClient;


        public UDPHandler(string trackerLabel, int trackerId, byte[] macAddress) {
            packetBuilder = new PacketBuilder(trackerLabel, trackerId);
            udpClient = new UdpClient();
            udpClient.Connect("localhost", 6969);
            Handshake(BoardType.WRANGLER, ImuType.UNKNOWN, McuType.WRANGLER, macAddress);
            AddImu(ImuType.UNKNOWN, TrackerPosition.NONE, TrackerDataType.ROTATION);
        }

        public void Heartbeat() {
            Task.Run(async () => {
                while (true) {
                    await udpClient.SendAsync(packetBuilder.HeartBeat);
                    await Task.Delay(800); // At least 1 time per second (<1000ms)
                }
            });
        }

        public async void AddImu(ImuType imuType, TrackerPosition trackerPosition, TrackerDataType trackerDataType) {
            await udpClient.SendAsync(packetBuilder.BuildSensorInfoPacket(imuType, trackerPosition, trackerDataType));
        }

        public async void Handshake(BoardType boardType, ImuType imuType, McuType mcuType, byte[] macAddress) {
            Task.Run(() => {
                ListenForHandshake();
            });
            await udpClient.SendAsync(packetBuilder.BuildHandshakePacket(boardType, imuType, mcuType, macAddress));
            await Task.Delay(500);
            Heartbeat();
        }

        public async Task<bool> SetSensorRotation(Quaternion rotation) {
            await udpClient.SendAsync(packetBuilder.BuildRotationPacket(rotation));
            return true;
        }
        public async Task<bool> SetSensorAcceleration(Vector3 acceleration) {
            await udpClient.SendAsync(packetBuilder.BuildAccelerationPacket(acceleration));
            return true;
        }
        public async Task<bool> SetSensorGyro(Vector3 gyro) {
            await udpClient.SendAsync(packetBuilder.BuildGyroPacket(gyro));
            return true;
        }
        public async Task<bool> SetSensorFlexData(float flexResistance) {
            await udpClient.SendAsync(packetBuilder.BuildFlexDataPacket(flexResistance));
            return true;
        }

        public async Task<bool> SendPacket(byte[] packet) {
            await udpClient.SendAsync(packet);
            return true;
        }

        public async void ListenForHandshake() {
            var data = await udpClient.ReceiveAsync();
            string value = Encoding.UTF8.GetString(data.Buffer);
            if (value.Contains("Hey OVR =D 5")) {
                udpClient.Connect(data.RemoteEndPoint.Address.ToString(), 6969);
            }
        }

        public async Task<bool> SetSensorBattery(byte battery) {
            await udpClient.SendAsync(packetBuilder.BuildBatteryLevelPacket(battery));
            return true;
        }
    }
}
