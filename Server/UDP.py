import socket
import struct
import csv
import numpy as np
import time
from tensorflow.keras.models import load_model

# 创建UDP socket
udp_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
# 绑定端口
udp_socket.bind(("192.168.1.102", 1337))
print("UDP server is running and listening for incoming messages...")

# 加载预训练模型
model = load_model('C:\\Users\\SWCN\\Desktop\\yuzeping\\Robot_Control\\Recognition\\model_Conv1D1_BiLSTM2_20240223.h5')

# 初始化数据缓冲区
data_buffer = []

# 打开CSV文件以写入数据
with open("data.csv", mode="w", newline="") as csv_file:
    csv_writer = csv.writer(csv_file)
    # 写入CSV文件的标题行
    header = [
        "Start", "DN", "SN", "Time", "S1", "S2", "S3", "S4", "S5", "S6", "S7", "S8", "S9", "S10",
        "Magnetometer_x", "Magnetometer_y", "Magnetometer_z",
        "Gyroscope_x", "Gyroscope_y", "Gyroscope_z",
        "Accelerometer_x", "Accelerometer_y", "Accelerometer_z"
    ]
    csv_writer.writerow(header)

    def sliding_window(data, window_size=200):
        """
        使用滑动窗口方法来处理数据。
        """
        if len(data) >= window_size:
            return data[-window_size:]
        else:
            return np.zeros((window_size, len(data[0])), dtype=np.float32)  # 确保数据形状正确

    while True:
        data, addr = udp_socket.recvfrom(1024)
        try:
            # 接收数据
            # 解析数据
            unpacked_data = struct.unpack("<HBBIH" + "H" * 10 + "f" * 3 + "f" * 3 + "f" * 3 + "H", data)
            # 解析数据为对应的类型
            # print(data)
            start, dn, sn, time1, time2 = unpacked_data[:5]
            print(start)
            s_values = unpacked_data[5:15]
            magnetometer_values = unpacked_data[15:18]
            gyroscope_values = unpacked_data[18:21]
            accelerometer_values = unpacked_data[21:24]
            end = unpacked_data[24:]
            time2_seconds = time2 / 1000
            # 合并时间单位为秒
            combined_time_seconds = time1 + time2_seconds
            print("Start:", start, dn, sn, combined_time_seconds, s_values, magnetometer_values, gyroscope_values,
                  accelerometer_values, end)
            print(f"Received invalid data from {addr}: {data.hex()}")
            # row = [start, dn, sn, time1, time2] + list(s_values) + list(magnetometer_values) + list(gyroscope_values) + list(
            #     accelerometer_values)
            # row = [start,dn,sn,time1,time2,s_values,magnetometer_values,gyroscope_values,accelerometer_values,end]
            row = [start, dn, sn, combined_time_seconds,
                   s_values[0], s_values[1], s_values[2], s_values[3], s_values[4], s_values[5], s_values[6],
                   s_values[7], s_values[8], s_values[9],
                   magnetometer_values[0], magnetometer_values[1], magnetometer_values[2],
                   gyroscope_values[0], gyroscope_values[1], gyroscope_values[2],

                   accelerometer_values[0], accelerometer_values[1], accelerometer_values[2],
                   end]
            csv_writer.writerow(row)  # 写入CSV文件

            # 更新数据缓冲区
            data_buffer.append(row[4:14])  # 假设真正的传感器数据从第四个元素开始

            if len(data_buffer) >= 200:
                # 应用滑动窗口
                window_data = np.array(sliding_window(data_buffer))
                window_data = window_data.reshape(1, 200, -1)  # 确保输入数据的形状符合模型要求
                
                # 使用模型进行预测
                prediction = model.predict(window_data)
                print("识别结果:", prediction)
                print("识别结果:", np.argmax(prediction, axis=1)[0])

                # 移除旧数据，以便新的滑动窗口
                data_buffer.pop(0)

                #if np.argmax(prediction, axis=1)[0] != 0:
                #    time.sleep(0.1)
                #time.sleep(0.1)

        except struct.error:
            print(f"Received invalid data from {addr}: {data.hex()}")

# 关闭socket
udp_socket.close()
