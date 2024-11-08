from pymycobot import MyCobot
import time


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

def UDP_power_off(mc):
    try:
        mc.power_off()
        return 0
    except:
        return 99
        
def UDP_init(mc):
    try:
        mc.power_on()
        time.sleep(0.1)
        mc.set_fresh_mode(1)
        time.sleep(0.1)
        mc.set_tool_reference([0,0,0,0,0,(-135)])
        time.sleep(0.1)
        mc.set_end_type(1)
        time.sleep(0.1)
        mc.send_coords([200,0,350,0,0,0],50,1)
        time.sleep(0.1)
        mc.init_eletric_gripper()
        mc.init_eletric_gripper()
        mc.init_eletric_gripper()
        mc.init_eletric_gripper()
        mc.init_eletric_gripper()
        time.sleep(0.1)
        return 0
    except:
        return 99

def UDP_shutdown(mc):
    try:
        mc.set_fresh_mode(0)
        time.sleep(0.1)
        mc.send_angles([0,(-130),143,36,90,(-140)],100)
        time.sleep(3)
        mc.set_eletric_gripper(1)
        mc.set_eletric_gripper(1)
        mc.set_eletric_gripper(1)
        mc.set_eletric_gripper(1)
        mc.set_eletric_gripper(1)
        time.sleep(0.1)
        mc.power_off()
        return 0
    except:
        return 99

def UDP_send_angles(recv_data_hex,mc):
    try:
        sign_bit = int(recv_data_hex[20:22],16)
        angle1 = int(recv_data_hex[6:8],16)
        angle2 = int(recv_data_hex[8:10],16)
        angle3 = int(recv_data_hex[10:12],16)
        angle4 = int(recv_data_hex[12:14],16)
        angle5 = int(recv_data_hex[14:16],16)
        angle6 = int(recv_data_hex[16:18],16)
        speed = int(recv_data_hex[18:20],16)
        
        if ( sign_bit >> 0 ) & 1 == 1:
            angle1 = -angle1
        if ( sign_bit >> 1 ) & 1 == 1:
            angle2 = -angle2
        if ( sign_bit >> 2 ) & 1 == 1:
            angle3 = -angle3
        if ( sign_bit >> 3 ) & 1 == 1:
            angle4 = -angle4
        if ( sign_bit >> 4 ) & 1 == 1:
            angle5 = -angle5
        if ( sign_bit >> 5 ) & 1 == 1:
            angle6 = -angle6   
        
        if speed > 100:
            speed = 100
        elif speed < 0:
            speed = 0   
        
        mc.send_angles([angle1,angle2,angle3,angle4,angle5,angle6],speed)
        return 0
    
    except ValueError:
        return 1
    except:
        return 99
        
def UDP_send_coords(recv_data_hex,mc):
    try:
        sign_bit = int(recv_data_hex[28:30],16)
        x = int(recv_data_hex[6:10],16)
        y = int(recv_data_hex[10:14],16)
        z = int(recv_data_hex[14:18],16)
        Rx = int(recv_data_hex[18:20],16)
        Ry = int(recv_data_hex[20:22],16)
        Rz = int(recv_data_hex[22:24],16)
        speed = int(recv_data_hex[24:26],16)
        method = int(recv_data_hex[26:28],16)
        
        if ( sign_bit >> 0 ) & 1 == 1:
            x = -x
        if ( sign_bit >> 1 ) & 1 == 1:
            y = -y
        if ( sign_bit >> 2 ) & 1 == 1:
            z = -z
        if ( sign_bit >> 3 ) & 1 == 1:
            Rx = -Rx
        if ( sign_bit >> 4 ) & 1 == 1:
            Ry = -Ry
        if ( sign_bit >> 5 ) & 1 == 1:
            Rz = -Rz
        
        if speed > 100:
            speed = 100
        elif speed < 0:
            speed = 0
        
        if method != 0:
            method = 1
        
        mc.send_coords([x,y,z,Rx,Ry,Rz],speed,method)
        return 0
        
    except ValueError:
        return 1
    except:
        return 99

def UDP_init_eletric_gripper(mc):
    try:
        mc.init_eletric_gripper()
        mc.init_eletric_gripper()
        mc.init_eletric_gripper()
        mc.init_eletric_gripper()
        mc.init_eletric_gripper()
        return 0
    except:
        return 99
        
def UDP_set_eletric_gripper(recv_data_hex,mc):
    try:
        gripper = int(recv_data_hex[6:8],16)
        mc.set_eletric_gripper(gripper)
        mc.set_eletric_gripper(gripper)
        mc.set_eletric_gripper(gripper)
        mc.set_eletric_gripper(gripper)
        mc.set_eletric_gripper(gripper)
        return 0
    except:
        return 99

