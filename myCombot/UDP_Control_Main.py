import UDP_mc_Function
import socket
import time
from pymycobot import MyCobot

test_option = 1

UDP_IP = "192.168.1.150"
UDP_PORT = 1337


udp_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
udp_socket.bind((UDP_IP, UDP_PORT))
mc = MyCobot('/dev/ttyAMA0',115200)
mc.power_on()
time.sleep(0.1)
mc.send_angles([0,0,0,0,0,0],50)
time.sleep(0.1)



#send_data = input("Send data:")
#udp_socket.sendto(send_data.encode("utf-8"),("192.168.1.101",12351))

while True:
    recv_data, send_addr = udp_socket.recvfrom(1024)
    recv_data_hex = recv_data.hex()
    
    if recv_data_hex.startswith('5a5a') and recv_data_hex.endswith('a5a5'):
    
        if len(recv_data_hex)>8:
        
            control_command = recv_data_hex[4:6]
            #print(control_command)
            
            if control_command == '00':
                if len(recv_data_hex)>10 or len(recv_data_hex)<10:
                    UDP_mc_Function.UDP_Message(f"ERROR:Command{control_command} Format Error",send_addr,udp_socket)
                else:
                    UDP_mc_Function.UDP_Message(f"Command{control_command}: Power On.",send_addr,udp_socket)
                    command_status = UDP_mc_Function.UDP_power_on(mc)
                    if command_status == 0:
                        UDP_mc_Function.UDP_Message(f"Command{control_command}: Completed.",send_addr,udp_socket)
                    else:
                        UDP_mc_Function.UDP_Message("ERROR: Unexpected Error.",send_addr,udp_socket)
                        
            elif control_command == '01':
                if len(recv_data_hex)>10 or len(recv_data_hex)<10:
                    UDP_mc_Function.UDP_Message(f"ERROR:Command{control_command} Format Error",send_addr,udp_socket)
                else:
                    UDP_mc_Function.UDP_Message(f"Command{control_command}: Power Off.",send_addr,udp_socket)
                    command_status = UDP_mc_Function.UDP_power_off(mc)
                    if command_status == 0:
                        UDP_mc_Function.UDP_Message(f"Command{control_command}: Completed.",send_addr,udp_socket)
                    else:
                        UDP_mc_Function.UDP_Message("ERROR: Unexpected Error.",send_addr,udp_socket)
                        
            elif control_command == '08':
                if len(recv_data_hex)>10 or len(recv_data_hex)<10:
                    UDP_mc_Function.UDP_Message(f"ERROR:Command{control_command} Format Error",send_addr,udp_socket)
                else:
                    UDP_mc_Function.UDP_Message(f"Command{control_command}: Init.",send_addr,udp_socket)
                    command_status = UDP_mc_Function.UDP_init(mc)
                    if command_status == 0:
                        UDP_mc_Function.UDP_Message(f"Command{control_command}: Completed.",send_addr,udp_socket)
                    else:
                        UDP_mc_Function.UDP_Message("ERROR: Unexpected Error.",send_addr,udp_socket)
            
            elif control_command == '09':
                if len(recv_data_hex)>10 or len(recv_data_hex)<10:
                    UDP_mc_Function.UDP_Message(f"ERROR:Command{control_command} Format Error",send_addr,udp_socket)
                else:
                    UDP_mc_Function.UDP_Message(f"Command{control_command}: Shutdown.",send_addr,udp_socket)
                    command_status = UDP_mc_Function.UDP_shutdown(mc)
                    if command_status == 0:
                        UDP_mc_Function.UDP_Message(f"Command{control_command}: Completed.",send_addr,udp_socket)
                    else:
                        UDP_mc_Function.UDP_Message("ERROR: Unexpected Error.",send_addr,udp_socket)
                        
            elif control_command == '10':
                if len(recv_data_hex)>34 or len(recv_data_hex)<34:
                    UDP_mc_Function.UDP_Message(f"ERROR:Command{control_command} Format Error",send_addr,udp_socket)
                else:
                    UDP_mc_Function.UDP_Message(f"Command{control_command}: Send Coords.",send_addr,udp_socket)
                    command_status = UDP_mc_Function.UDP_send_coords(recv_data_hex,mc)
                    if command_status == 0:
                        UDP_mc_Function.UDP_Message(f"Command{control_command}: Completed.",send_addr,udp_socket)
                    elif command_status == 1:
                        UDP_mc_Function.UDP_Message("ERROR: Value Error.",send_addr,udp_socket)
                    else:
                        UDP_mc_Function.UDP_Message("ERROR: Unexpected Error.",send_addr,udp_socket)
            
       
            elif control_command == '20':
                if len(recv_data_hex)>26 or len(recv_data_hex)<26:
                    UDP_mc_Function.UDP_Message(f"ERROR:Command{control_command} Format Error",send_addr,udp_socket)
                else:
                    UDP_mc_Function.UDP_Message(f"Command{control_command}: Send Angles.",send_addr,udp_socket)
                    command_status = UDP_mc_Function.UDP_send_angles(recv_data_hex,mc)
                    if command_status == 0:
                        UDP_mc_Function.UDP_Message(f"Command{control_command}: Completed.",send_addr,udp_socket)
                    elif command_status == 1:
                        UDP_mc_Function.UDP_Message("ERROR: Value Error.",send_addr,udp_socket)
                    else:
                        UDP_mc_Function.UDP_Message("ERROR: Unexpected Error.",send_addr,udp_socket)
            
            elif control_command == '40':
                if len(recv_data_hex)>10 or len(recv_data_hex)<10:
                    UDP_mc_Function.UDP_Message(f"ERROR:Command{control_command} Format Error",send_addr,udp_socket)
                else:
                    UDP_mc_Function.UDP_Message(f"Command{control_command}: Init Eletric Gripper.",send_addr,udp_socket)
                    command_status = UDP_mc_Function.UDP_init_eletric_gripper(mc)
                    if command_status == 0:
                        UDP_mc_Function.UDP_Message(f"Command{control_command}: Completed.",send_addr,udp_socket)
                    else:
                        UDP_mc_Function.UDP_Message("ERROR: Unexpected Error.",send_addr,udp_socket)
                        
            elif control_command == '41':
                if len(recv_data_hex)>12 or len(recv_data_hex)<12:
                    UDP_mc_Function.UDP_Message(f"ERROR:Command{control_command} Format Error",send_addr,udp_socket)
                else:
                    UDP_mc_Function.UDP_Message(f"Command{control_command}: Set Eletric Gripper.",send_addr,udp_socket)
                    command_status = UDP_mc_Function.UDP_set_eletric_gripper(recv_data_hex,mc)
                    if command_status == 0:
                        UDP_mc_Function.UDP_Message(f"Command{control_command}: Completed.",send_addr,udp_socket)
                    else:
                        UDP_mc_Function.UDP_Message("ERROR: Unexpected Error.",send_addr,udp_socket)
            
                
            else:
                UDP_mc_Function.UDP_Message("ERROR: Invalid Command.",send_addr,udp_socket)
                
        else:
            UDP_mc_Function.UDP_Message("ERROR: Data too short.",send_addr,udp_socket)
    else:
        UDP_mc_Function.UDP_Message("ERROR : Data Corrupted or Wrong Data Format.",send_addr,udp_socket)
            
            
    if test_option == True:
        print("Recieve data:", recv_data_hex)
        print("From address:", str(send_addr))
        print("Len:",len(recv_data_hex))


    


    

    #udp_socket.close()
