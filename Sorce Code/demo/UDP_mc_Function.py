from pymycobot import MyCobot


def UDP_Message(Message,address,udp_socket):
    print(Message)
    udp_socket.sendto(Message.encode("utf-8"),address)
    return True

def UDP_power_on(mc):
    try:
        mc.power_on()
        return 0
    except:
        return 99

def UDP_send_angles(recv_data_hex,mc):
    try:
        angles1 = int(recv_data_hex[6:8])
        angles2 = int(recv_data_hex[8:10])
        angles3 = int(recv_data_hex[10:12])
        angles4 = int(recv_data_hex[12:14])
        angles5 = int(recv_data_hex[14:16])
        angles6 = int(recv_data_hex[16:18])
        speed = int(recv_data_hex[18:20])
        
        mc.send_angles([angles1,angles2,angles3,angles4,angles5,angles6],speed)
        return 0
    
    except ValueError:
        return 1
    except:
        return 99
