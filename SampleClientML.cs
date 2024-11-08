/* 
Copyright ｩ 2016 NaturalPoint Inc.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License. */


using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.IO;

using NatNetML;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;


/* SampleClientML.cs
 * 
 * This program is a sample console application which uses the managed NatNet assembly (NatNetML.dll) for receiving NatNet data
 * from a tracking server (e.g. Motive) and outputting them in every 200 mocap frames. This is provided mainly for demonstration purposes,
 * and thus, the program is designed at its simpliest approach. The program connects to a server application at a localhost IP address
 * (127.0.0.1) using Multicast connection protocol.
 *  
 * You may integrate this program into your applications if needed. This program is not designed to take account for latency/frame build up
 * when tracking a high number of assets. For more robust and comprehensive use of the NatNet assembly, refer to the provided WinFormSample project.
 * 
 *  Note: The NatNet .NET assembly is derived from the native NatNetLib.dll library, so make sure the NatNetLib.dll is linked to your application
 *        along with the NatNetML.dll file.  
 * 
 *  List of Output Data:
 *  ====================
 *      - Markers Data : Prints out total number of markers reconstructed in the scene.
 *      - Rigid Body Data : Prints out position and orientation data
 *      - Skeleton Data : Prints out only the position of the hip segment
 *      - Force Plate Data : Prints out only the first subsample data per each mocap frame
 *      - TODO update this with newer info
 */


namespace SampleClientML
{
    public class SampleClientML
    {
        static Socket gloveServer = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        static Socket pythonServer = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        // 假设你的Python脚本名为script.py并且位于C盘根目录下
        static Process pythonProcess;
        static string pythonScriptPath = @"C:\Users\SWCN\Desktop\yuzeping\Robot_Control\Recognition\UDP.py";
        private static Timer processMonitorTimer;

        const string robotCtrlCmdHead = "5a 5a";
        const string robotCtrlCmdEnd = "a5 a5";
        const string robotCtrlCmdInit = "08";
        const string robotCtrlCmdShutdown = "09";
        const string robotCtrlCmdCoord = "10";
        const string robotCtrlCmdElcGrpInt = "40";
        const string robotCtrlCmdElcGrpSet = "41";

        static bool resetZeroPoint = false;
        static int zeroPointX = 0;
        static int zeroPointY = 0;
        static int zeroPointZ = 0;

        const int robotBaseCoordX = 200;
        const int robotBaseCoordY = 0;
        const int robotBaseCoordZ = 380;

        // 定义一个变量来存储文件保存路径
        private static string fileSavePath = @"C:\Users\SWCN\Desktop";

        // 定义一个线程安全的队列用于存储接收到的数据
        private static ConcurrentQueue<string> receivedHexDataQueue = new ConcurrentQueue<string>();

        // 基本文件名，不包括路径和扩展名
        private static string baseFileName = "received_data";
        private static string hexFileName;
        //private static string decimalFileName;
        private static string packetFileName;
        private static bool csvHeaderFlag = false;
        private static bool robotApexFlag = false;

        /*  [NatNet] Network connection configuration    */
        private static NatNetML.NatNetClientML mNatNet;    // The client instance
        private static string mStrLocalIP = "127.0.0.1";   // Local IP address (string)
        private static string mStrServerIP = "127.0.0.1";  // Server IP address (string)
        private static NatNetML.ConnectionType mConnectionType = ConnectionType.Multicast; // Multicast or Unicast mode


        /*  List for saving each of datadescriptors */
        private static List<NatNetML.DataDescriptor> mDataDescriptor = new List<NatNetML.DataDescriptor>();

        /*  Lists and Hashtables for saving data descriptions   */
        private static Hashtable mHtSkelRBs = new Hashtable();
        private static Hashtable mAssetRBs = new Hashtable();
        private static List<RigidBody> mRigidBodies = new List<RigidBody>();
        private static List<Skeleton> mSkeletons = new List<Skeleton>();
        private static List<ForcePlate> mForcePlates = new List<ForcePlate>();
        private static List<Device> mDevices = new List<Device>();
        private static List<Camera> mCameras = new List<Camera>();
        private static List<AssetDescriptor> mAssets = new List<AssetDescriptor>();

        /*  boolean value for detecting change in asset */
        private static bool mAssetChanged = false;

        // 用于指示是否按下ESC键的全局标志
        private static volatile bool exitRequested = false;
        private static CancellationTokenSource gloveThreadCTS = new CancellationTokenSource();
        private static CancellationTokenSource pythonThreadCTS = new CancellationTokenSource();


        static void Main(string[] args)
        {


            bool debug = true;
            string strLocalIP = "127.0.0.1";   // Local IP address (string)
            string strServerIP = "127.0.0.1";  // Server IP address (string)
            NatNetML.ConnectionType connectionType = ConnectionType.Multicast; // Multicast or Unicast mode

            Console.WriteLine("SampleClientML managed client application starting...\n");
            if (args.Length == 0)
            {
                Console.WriteLine("  command line options: \n");
                Console.WriteLine("  SampleClientML [server_ip_address [client_ip_address [Unicast/Multicast]]] \n");
                Console.WriteLine("  Examples: \n");
                Console.WriteLine("    SampleClientML 127.0.0.1 127.0.0.1 Unicast \n");
                Console.WriteLine("    SampleClientML 127.0.0.1 127.0.0.1 m \n");
                Console.WriteLine("\n");
            }
            else
            {
                strServerIP = args[0];
                if (args.Length > 1)
                {
                    strLocalIP = args[1];
                    if (args.Length > 2)
                    {
                        connectionType = ConnectionType.Multicast; // Multicast or Unicast mode
                        string res = args[2].Substring(0, 1);
                        string res2 = res.ToLower();
                        if (res2 == "u")
                        {
                            connectionType = ConnectionType.Unicast;
                        }
                    }
                }
            }
            if (debug == true)
            {
                string cmdline = "SampleClientML " + strServerIP + " " + strLocalIP + " ";
                if (connectionType == ConnectionType.Multicast)
                {
                    cmdline += "Multicast";
                }
                else
                {
                    cmdline += "Unicast";
                }
                Console.WriteLine("Using: " + cmdline + "\n");
            }
            /*  [NatNet] Initialize client object and connect to the server  */
            // Initialize a NatNetClient object and connect to a server.
            connectToServer(strServerIP, strLocalIP, connectionType);


            Console.WriteLine("============================ SERVER DESCRIPTOR ================================\n");
            /*  [NatNet] Confirming Server Connection. Instantiate the server descriptor object and obtain the server description. */
            bool connectionConfirmed = fetchServerDescriptor();    // To confirm connection, request server description data

            if (connectionConfirmed)                         // Once the connection is confirmed.
            {
                Console.WriteLine("============================= DATA DESCRIPTOR =================================\n");
                Console.WriteLine("Now Fetching the Data Descriptor.\n");
                fetchDataDescriptor();                  //Fetch and parse data descriptor

                Console.WriteLine("============================= FRAME OF DATA ===================================\n");
                Console.WriteLine("Now Fetching the Frame Data\n");

                /*  [NatNet] Assigning a event handler function for fetching frame data each time a frame is received   */
                mNatNet.OnFrameReady += new NatNetML.FrameReadyEventHandler(fetchFrameData);

                Console.WriteLine("Success: Data Port Connected \n");

                Console.WriteLine("======================== STREAMING IN (PRESS ESC TO EXIT) =====================\n");
            }

            //绑定端口号和IP
            gloveServer.Bind(new IPEndPoint(IPAddress.Parse("192.168.1.102"), 1337));
            Console.WriteLine("gloveServer is Online");
            pythonServer.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1337));
            Console.WriteLine("pythonServer is Online");

            sendCtrlCmdToRobot(robotCtrlCmdInit);

            // 确定唯一的文件名
            hexFileName = GetUniqueFileName(fileSavePath, baseFileName, "hex");
            packetFileName = GetUniqueFileName(fileSavePath, baseFileName, "packet");


            // 创建线程时使用 lambda 表达式
            Thread gloveDataThread = new Thread(() => reciveDataFromGlove(gloveThreadCTS.Token));
            gloveDataThread.Start();

            Thread pythonDataThread = new Thread(() => reciveDataFromPython(pythonThreadCTS.Token));
            pythonDataThread.Start();

            //startPythonProcess();
            // 设置定时器以每30秒检查一次Python进程的状态
            //processMonitorTimer = new Timer(CheckPythonProcessStatus, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

            

            Console.WriteLine("MainInit is Completed");
            // 在后台线程中监听ESC键
            Task.Run(() =>
            {
                Console.WriteLine("Press ESC to stop");
                while (!exitRequested)
                {
                    if (Console.KeyAvailable) // 检查是否有按键事件
                    {
                        var key = Console.ReadKey(true).Key; // 读取按键，true参数意味着不在控制台显示按键字符
                        if (key == ConsoleKey.Escape)
                        {
                            exitRequested = true;
                        }
                    }
                }
            });

            // 主循环
            Task.Run(() =>
            {
                while (!exitRequested)
                {
                    // 主循环的内容
                    // Exception handler for updated assets list.
                    if (mAssetChanged == true)
                    {
                        Console.WriteLine("\n===============================================================================\n");
                        Console.WriteLine("Change in the list of all assets. Refetching the descriptions");

                        /*  Clear out existing lists */
                        mDataDescriptor.Clear();
                        mHtSkelRBs.Clear();
                        mAssetRBs.Clear();
                        mRigidBodies.Clear();
                        mSkeletons.Clear();
                        mForcePlates.Clear();
                        mDevices.Clear();
                        mCameras.Clear();
                        mAssets.Clear();

                        /* [NatNet] Re-fetch the updated list of descriptors  */
                        fetchDataDescriptor();
                        Console.WriteLine("===============================================================================\n");
                        mAssetChanged = false;
                    }
                }

                // 清理资源，准备退出
                CleanUpResources();
                Console.WriteLine("Exiting...");
            }).Wait(); // 等待主循环任务完成




        }

        static void startPythonProcess()
        {
            
            try
            {
                pythonProcess = new Process();
                pythonProcess.StartInfo.FileName = "python"; // 或者完整路径，例如 @"C:\Python39\python.exe"
                pythonProcess.StartInfo.Arguments = pythonScriptPath;
                pythonProcess.StartInfo.CreateNoWindow = false; // 创建新窗口
                pythonProcess.StartInfo.UseShellExecute = true; // 使用shell启动进程
                pythonProcess.Start(); // 启动Python脚本
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting Python script: {ex.Message}");
            }
        }

        static void CheckPythonProcessStatus(object state)
        {
            if (pythonProcess == null || pythonProcess.HasExited)
            {
                Console.WriteLine("Restarting Python...");
                startPythonProcess();
            }
        }

        static void CleanUpResources()
        {
            // 在这里添加退出前需要执行的清理代码
            // 例如断开网络连接、保存数据到文件等
            /*  [NatNet] Disabling data handling function   */
            mNatNet.OnFrameReady -= fetchFrameData;

            /*  Clearing Saved Descriptions */
            mDataDescriptor.Clear();
            mHtSkelRBs.Clear();
            mAssetRBs.Clear();
            mRigidBodies.Clear();
            mSkeletons.Clear();
            mForcePlates.Clear();
            mDevices.Clear();
            mCameras.Clear();
            mAssets.Clear();
            mNatNet.Disconnect();
            gloveServer.Close();
            pythonServer.Close();
            gloveThreadCTS.Cancel();
            pythonThreadCTS.Cancel();

            if (pythonProcess != null && !pythonProcess.HasExited)
            {
                pythonProcess.Kill(); // 确保Python进程被终止
                pythonProcess.Dispose(); // 释放与Process关联的所有资源
            }
        }



        /// <summary>
        /// [NatNet] parseFrameData will be called when a frame of Mocap
        /// data has is received from the server application.
        ///
        /// Note: This callback is on the network service thread, so it is
        /// important to return from this function quickly as possible 
        /// to prevent incoming frames of data from buffering up on the
        /// network socket.
        ///
        /// Note: "data" is a reference structure to the current frame of data.
        /// NatNet re-uses this same instance for each incoming frame, so it should
        /// not be kept (the values contained in "data" will become replaced after
        /// this callback function has exited).
        /// </summary>
        /// <param name="data">The actual frame of mocap data</param>
        /// <param name="client">The NatNet client instance</param>
        static void fetchFrameData(NatNetML.FrameOfMocapData data, NatNetML.NatNetClientML client)
        {

            /*  Exception handler for cases where assets are added or removed.
                Data description is re-obtained in the main function so that contents
                in the frame handler is kept minimal. */
            if ((data.bTrackingModelsChanged == true ||
                data.nRigidBodies != mRigidBodies.Count ||
                data.nSkeletons != mSkeletons.Count ||
                data.nForcePlates != mForcePlates.Count ||
                data.nDevices != mDevices.Count ||
                data.nAssets != mAssets.Count))
            {
                mAssetChanged = true;
            }

            /*  Processing and ouputting frame data every 200th frame.
                This conditional statement is included in order to simplify the program output */
            if (data.iFrame % 20 == 0)
            {
                if (data.bRecording == false)
                    Console.WriteLine("Frame #{0} Received:", data.iFrame);
                else if (data.bRecording == true)
                    Console.WriteLine("[Recording] Frame #{0} Received:", data.iFrame);

                processFrameData(data);
            }
        }

        static void processFrameData(NatNetML.FrameOfMocapData data)
        {
            /*  Parsing Rigid Body Frame Data   */
            for (int i = 0; i < mRigidBodies.Count; i++)
            {
                int rbID = mRigidBodies[i].ID;              // Fetching rigid body IDs from the saved descriptions

                for (int j = 0; j < data.nRigidBodies; j++)
                {
                    if (rbID == data.RigidBodies[j].ID)      // When rigid body ID of the descriptions matches rigid body ID of the frame data.
                    {
                        NatNetML.RigidBody rb = mRigidBodies[i];                // Saved rigid body descriptions
                        NatNetML.RigidBodyData rbData = data.RigidBodies[j];    // Received rigid body descriptions

                        if (rbData.Tracked == true)
                        {
                            Console.WriteLine("\tRigidBody ({0}):", rb.Name);
                            Console.WriteLine("\t\tpos ({0}, {1:N3}, {2:N3})", rbData.x, rbData.y, rbData.z);


                            // Rigid Body Euler Orientation
                            float[] quat = new float[4] { rbData.qx, rbData.qy, rbData.qz, rbData.qw };
                            float[] eulers = new float[3];

                            eulers = NatNetClientML.QuatToEuler(quat, NATEulerOrder.NAT_XYZr); //Converting quat orientation into XYZ Euler representation.
                            double xrot = RadiansToDegrees(eulers[0]);
                            double yrot = RadiansToDegrees(eulers[1]);
                            double zrot = RadiansToDegrees(eulers[2]);

                            Console.WriteLine("\t\tori ({0:N3}, {1:N3}, {2:N3})", xrot, yrot, zrot);
                            motiveCoordToRobot(rbData);
                        }
                        else
                        {
                            Console.WriteLine("\t{0} is not tracked in current frame", rb.Name);
                        }
                    }
                }
            }

            /* Parsing Skeleton Frame Data  */
            for (int i = 0; i < mSkeletons.Count; i++)      // Fetching skeleton IDs from the saved descriptions
            {
                int sklID = mSkeletons[i].ID;

                for (int j = 0; j < data.nSkeletons; j++)
                {
                    if (sklID == data.Skeletons[j].ID)      // When skeleton ID of the description matches skeleton ID of the frame data.
                    {
                        NatNetML.Skeleton skl = mSkeletons[i];              // Saved skeleton descriptions
                        NatNetML.SkeletonData sklData = data.Skeletons[j];  // Received skeleton frame data

                        Console.WriteLine("\tSkeleton ({0}):", skl.Name);
                        Console.WriteLine("\t\tSegment count: {0}", sklData.nRigidBodies);

                        /*  Now, for each of the skeleton segments  */
                        for (int k = 0; k < sklData.nRigidBodies; k++)
                        {
                            NatNetML.RigidBodyData boneData = sklData.RigidBodies[k];

                            /*  Decoding skeleton bone ID   */
                            int skeletonID = HighWord(boneData.ID);
                            int rigidBodyID = LowWord(boneData.ID);
                            int uniqueID = skeletonID * 1000 + rigidBodyID;
                            int key = uniqueID.GetHashCode();

                            NatNetML.RigidBody bone = (RigidBody)mHtSkelRBs[key];   //Fetching saved skeleton bone descriptions

                            //Outputting only the hip segment data for the purpose of this sample.
                            if (k == 0)
                                Console.WriteLine("\t\t{0:N3}: pos({1:N3}, {2:N3}, {3:N3})", bone.Name, boneData.x, boneData.y, boneData.z);
                        }
                    }
                }
            }

            /*  Parsing Force Plate Frame Data  */
            for (int i = 0; i < mForcePlates.Count; i++)
            {
                int fpID = mForcePlates[i].ID;                  // Fetching force plate IDs from the saved descriptions

                for (int j = 0; j < data.nForcePlates; j++)
                {
                    if (fpID == data.ForcePlates[j].ID)         // When force plate ID of the descriptions matches force plate ID of the frame data.
                    {
                        NatNetML.ForcePlate fp = mForcePlates[i];                // Saved force plate descriptions
                        NatNetML.ForcePlateData fpData = data.ForcePlates[i];    // Received forceplate frame data

                        Console.WriteLine("\tForce Plate ({0}):", fp.Serial);

                        // Here we will be printing out only the first force plate "subsample" (index 0) that was collected with the mocap frame.
                        for (int k = 0; k < fpData.nChannels; k++)
                        {
                            Console.WriteLine("\t\tChannel {0}: {1}", fp.ChannelNames[k], fpData.ChannelData[k].Values[0]);
                        }
                    }
                }
            }

            /*  Parsing Asset Frame Data   */
            for (int i = 0; i < mAssets.Count; i++)
            {
                int assetID = mAssets[i].AssetID;               // Fetching IDs from the saved descriptions

                for (int j = 0; j < data.nAssets; j++)
                {
                    if (assetID == data.Assets[j].AssetID)      // When ID of the descriptions matches ID of the frame data.
                    {
                        NatNetML.AssetDescriptor asset = mAssets[i];                // Saved asset descriptions
                        NatNetML.AssetData assetData = data.Assets[j];              // Received asset descriptions

                        Console.WriteLine("\tAsset ({0}):", asset.Name);
                        Console.WriteLine("\t\tSegment count: {0}", assetData.nRigidBodies);

                        /*  For each segment  */
                        for (int k = 0; k < assetData.nRigidBodies; k++)
                        {
                            NatNetML.RigidBodyData segmentData = assetData.RigidBodies[k];

                            /*  Decoding segment ID   */
                            int rigidBodyID = segmentData.ID;
                            int uniqueID = assetID * 1000 + rigidBodyID;
                            int key = uniqueID.GetHashCode();

                            NatNetML.RigidBody segment = (RigidBody)mAssetRBs[key];   //Fetching saved segment descriptions

                            //Outputting each segment
                            Console.WriteLine("\t\t{0:N3}: pos({1:N3}, {2:N3}, {3:N3})", segment.Name, segmentData.x, segmentData.y, segmentData.z);
                        }
                    }
                }
            }

            /* Optional Precision Timestamp (NatNet 4.1 or later) */
            if (data.PrecisionTimestampSeconds != 0)
            {
                int hours = (int)(data.PrecisionTimestampSeconds / 3600);
                int minutes = (int)(data.PrecisionTimestampSeconds / 60) % 60;
                int seconds = (int)(data.PrecisionTimestampSeconds) % 60;

                Console.WriteLine("Precision Timestamp HH:MM:SS : {0:00}:{1:00}:{2:00}", hours, minutes, seconds);
                Console.WriteLine("Precision Timestamp Seconds : {0}", data.PrecisionTimestampSeconds);
                Console.WriteLine("Precision Timestamp Fractional Seconds : {0}", data.PrecisionTimestampFractionalSeconds);
            }

            Console.WriteLine("\n");
        }

        static void connectToServer(string serverIPAddress, string localIPAddress, NatNetML.ConnectionType connectionType)
        {
            /*  [NatNet] Instantiate the client object  */
            mNatNet = new NatNetML.NatNetClientML();

            /*  [NatNet] Checking verions of the NatNet SDK library  */
            int[] verNatNet = new int[4];           // Saving NatNet SDK version number
            verNatNet = mNatNet.NatNetVersion();
            Console.WriteLine("NatNet SDK Version: {0}.{1}.{2}.{3}", verNatNet[0], verNatNet[1], verNatNet[2], verNatNet[3]);

            /*  [NatNet] Connecting to the Server    */

            NatNetClientML.ConnectParams connectParams = new NatNetClientML.ConnectParams();
            connectParams.ConnectionType = connectionType;
            connectParams.ServerAddress = serverIPAddress;
            connectParams.LocalAddress = localIPAddress;

            Console.WriteLine("\nConnecting...");
            Console.WriteLine("\tServer IP Address: {0}", serverIPAddress);
            Console.WriteLine("\tLocal IP address : {0}", localIPAddress);
            Console.WriteLine("\tConnection Type  : {0}", connectionType);
            Console.WriteLine("\n");

            mNatNet.Connect(connectParams);
        }

        static bool fetchServerDescriptor()
        {
            NatNetML.ServerDescription m_ServerDescriptor = new NatNetML.ServerDescription();
            int errorCode = mNatNet.GetServerDescription(m_ServerDescriptor);

            if (errorCode == 0)
            {
                Console.WriteLine("Success: Connected to the server\n");
                parseSeverDescriptor(m_ServerDescriptor);
                return true;
            }
            else
            {
                Console.WriteLine("Error: Failed to connect. Check the connection settings.");
                Console.WriteLine("Program terminated (Enter ESC to exit)");
                return false;
            }
        }

        static void parseSeverDescriptor(NatNetML.ServerDescription server)
        {
            Console.WriteLine("Server Info:");
            Console.WriteLine("\tHost               : {0}", server.HostComputerName);
            Console.WriteLine("\tApplication Name   : {0}", server.HostApp);
            Console.WriteLine("\tApplication Version: {0}.{1}.{2}.{3}", server.HostAppVersion[0], server.HostAppVersion[1], server.HostAppVersion[2], server.HostAppVersion[3]);
            Console.WriteLine("\tNatNet Version     : {0}.{1}.{2}.{3}\n", server.NatNetVersion[0], server.NatNetVersion[1], server.NatNetVersion[2], server.NatNetVersion[3]);
        }

        static void fetchDataDescriptor()
        {
            /*  [NatNet] Fetch Data Descriptions. Instantiate objects for saving data descriptions and frame data    */
            bool result = mNatNet.GetDataDescriptions(out mDataDescriptor);
            if (result)
            {
                Console.WriteLine("Success: Data Descriptions obtained from the server.");
                parseDataDescriptor(mDataDescriptor);
            }
            else
            {
                Console.WriteLine("Error: Could not get the Data Descriptions");
            }
            Console.WriteLine("\n");
        }

        static void parseDataDescriptor(List<NatNetML.DataDescriptor> description)
        {
            //  [NatNet] Request a description of the Active Model List from the server. 
            //  This sample will list only names of the data sets, but you can access 
            int numDataSet = description.Count;
            Console.WriteLine("Total {0} data sets in the capture:", numDataSet);

            for (int i = 0; i < numDataSet; ++i)
            {
                int dataSetType = description[i].type;
                // Parse Data Descriptions for each data sets and save them in the delcared lists and hashtables for later uses.
                switch (dataSetType)
                {
                    case ((int)NatNetML.DataDescriptorType.eMarkerSetData):
                        NatNetML.MarkerSet mkset = (NatNetML.MarkerSet)description[i];
                        Console.WriteLine("\tMarkerSet ({0})", mkset.Name);
                        break;


                    case ((int)NatNetML.DataDescriptorType.eRigidbodyData):
                        NatNetML.RigidBody rb = (NatNetML.RigidBody)description[i];
                        Console.WriteLine("\tRigidBody ({0})", rb.Name);

                        // Saving Rigid Body Descriptions
                        mRigidBodies.Add(rb);
                        break;


                    case ((int)NatNetML.DataDescriptorType.eSkeletonData):
                        NatNetML.Skeleton skl = (NatNetML.Skeleton)description[i];
                        Console.WriteLine("\tSkeleton ({0}), Bones:", skl.Name);

                        //Saving Skeleton Descriptions
                        mSkeletons.Add(skl);

                        // Saving Individual Bone Descriptions
                        for (int j = 0; j < skl.nRigidBodies; j++)
                        {

                            Console.WriteLine("\t\t{0}. {1}", j + 1, skl.RigidBodies[j].Name);
                            int uniqueID = skl.ID * 1000 + skl.RigidBodies[j].ID;
                            int key = uniqueID.GetHashCode();
                            mHtSkelRBs.Add(key, skl.RigidBodies[j]); //Saving the bone segments onto the hashtable
                        }
                        break;


                    case ((int)NatNetML.DataDescriptorType.eForcePlateData):
                        NatNetML.ForcePlate fp = (NatNetML.ForcePlate)description[i];
                        Console.WriteLine("\tForcePlate ({0})", fp.Serial);

                        // Saving Force Plate Channel Names
                        mForcePlates.Add(fp);

                        for (int j = 0; j < fp.ChannelCount; j++)
                        {
                            Console.WriteLine("\t\tChannel {0}: {1}", j + 1, fp.ChannelNames[j]);
                        }
                        break;

                    case ((int)NatNetML.DataDescriptorType.eDeviceData):
                        NatNetML.Device dd = (NatNetML.Device)description[i];
                        Console.WriteLine("\tDeviceData ({0})", dd.Serial);

                        // Saving Device Data Channel Names
                        mDevices.Add(dd);

                        for (int j = 0; j < dd.ChannelCount; j++)
                        {
                            Console.WriteLine("\t\tChannel {0}: {1}", j + 1, dd.ChannelNames[j]);
                        }
                        break;

                    case ((int)NatNetML.DataDescriptorType.eCameraData):
                        // Saving Camera Names
                        NatNetML.Camera camera = (NatNetML.Camera)description[i];
                        Console.WriteLine("\tCamera: ({0})", camera.Name);

                        // Saving Force Plate Channel Names
                        mCameras.Add(camera);
                        break;

                    case ((int)NatNetML.DataDescriptorType.eAssetData):
                        NatNetML.AssetDescriptor asset = (NatNetML.AssetDescriptor)description[i];
                        Console.WriteLine("\tAsset ({0}), Segments:", asset.Name);

                        // Saving Asset Description
                        mAssets.Add(asset);

                        // Saving Individual Segment Descriptions
                        for (int j = 0; j < asset.nRigidBodies; j++)
                        {

                            Console.WriteLine("\t\t{0}. {1}", j + 1, asset.RigidBodies[j].Name);
                            int uniqueID = asset.AssetID * 1000 + asset.RigidBodies[j].ID;
                            int key = uniqueID.GetHashCode();
                            mAssetRBs.Add(key, asset.RigidBodies[j]); //Saving the segments onto the hashtable
                        }
                        break;


                    default:
                        // When a Data Set does not match any of the descriptions provided by the SDK.
                        Console.WriteLine("\tError: Invalid Data Set - dataSetType = " + dataSetType);
                        break;
                }
            }
        }

        static double RadiansToDegrees(double dRads)
        {
            return dRads * (180.0f / Math.PI);
        }

        static int LowWord(int number)
        {
            return number & 0xFFFF;
        }

        static int HighWord(int number)
        {
            return ((number >> 16) & 0xFFFF);
        }



        static void SendDataToPython(byte[] data, int length)
        {
            try
            {
                IPEndPoint pythonEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1338); // Python服务器的端点
                // 使用指定长度的数据发送到Python服务器
                pythonServer.SendTo(data, length, SocketFlags.None, pythonEndpoint);
                //Console.WriteLine("Data sent to Python server.");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Could not send data to Python server: {e.Message}");
            }
        }
        static void reciveDataFromPython(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    EndPoint point = new IPEndPoint(IPAddress.Any, 0); // 用来保存发送方的ip和端口号
                    byte[] buffer = new byte[1024];
                    int length = pythonServer.ReceiveFrom(buffer, ref point); // 接收数据报

                    // 将接收到的字节数据转换为16进制字符串
                    string hex = BitConverter.ToString(buffer, 0, length).Replace("-", " ");
                    Console.WriteLine($"{point} received hex data from python: {hex}");

                    // 执行基于接收到的16进制数据的不同命令
                    ExecuteCommand(hex);
                }
                catch (SocketException ex)
                {
                    // 当socket被关闭时会触发这个异常
                    Console.WriteLine("Python Socket has been closed. Exiting receive loop.");
                    break; // 跳出循环，结束这个方法，从而结束线程的执行
                }
                catch (Exception ex)
                {
                    // 处理其他类型的异常
                    Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                    break; // 在这里也跳出循环，或者根据实际情况决定是否要继续
                }
            }
        }

        static void ExecuteCommand(string hexData)
        {
            // 命令的16进制代码
            const string command1Hex = "A1 B2 C3";
            const string command2Hex = "D4 E5 F6";

            if (hexData.Equals(command1Hex))
            {
                // 执行命令1
                Console.WriteLine("Executing Command 1");
            }
            else if (hexData.Equals(command2Hex))
            {
                // 执行命令2
                Console.WriteLine("Executing Command 2");
            }
            else
            {
                // 未知命令
                Console.WriteLine("Received unknown command");
            }
        }

        static void motiveCoordToRobot(NatNetML.RigidBodyData motiveCoord)
        {
            if (resetZeroPoint)
            {
                zeroPointX = (int)(motiveCoord.x * 1000);
                zeroPointY = (int)(motiveCoord.y * 1000);
                zeroPointZ = (int)(motiveCoord.z * 1000);
                Console.WriteLine("Zero Point");
                Console.WriteLine("\tNow zeroPoint({0},{1},{2})", zeroPointX, zeroPointY, zeroPointZ);
                resetZeroPoint = false;
            }
            int intMotiveCoordX = (int)(motiveCoord.x * 1000) - zeroPointX;
            int intMotiveCoordY = (int)(motiveCoord.y * 1000) - zeroPointY;
            int intMotiveCoordZ = (int)(motiveCoord.z * 1000) - zeroPointZ;
            int intRobotCoordX = robotBaseCoordX + intMotiveCoordX;
            int intRobotCoordY = robotBaseCoordY + intMotiveCoordY;
            int intRobotCoordZ = robotBaseCoordZ + intMotiveCoordZ;
            string robotCoordX = (Math.Abs(intRobotCoordX)).ToString("X4").Insert(2, " ");
            string robotCoordY = (Math.Abs(intRobotCoordY)).ToString("X4").Insert(2, " ");
            string robotCoordZ = (Math.Abs(intRobotCoordZ)).ToString("X4").Insert(2, " ");
            byte byteRobotCoordSign = 0x00;
            if (intRobotCoordX < 0)
            {
                // 将对应的位设置为1
                byteRobotCoordSign |= (byte)(1 << 0);
            }
            if (intRobotCoordY < 0)
            {
                // 将对应的位设置为1
                byteRobotCoordSign |= (byte)(1 << 1);
            }
            if (intRobotCoordZ < 0)
            {
                // 将对应的位设置为1
                byteRobotCoordSign |= (byte)(1 << 2);
            }
            string robotCoordSign = byteRobotCoordSign.ToString("X2");
            Console.WriteLine(robotCoordX);
            Console.WriteLine(robotCoordY);
            Console.WriteLine(robotCoordZ);
            sendCtrlCmdToRobot(robotCtrlCmdCoord, robotCoordX, robotCoordY, robotCoordZ, robotCoordSign);
        }

        static void sendCtrlCmdToRobot(string CtrlCmd)
        {
            string HexCtrlCmd = robotCtrlCmdHead + " " + CtrlCmd + " " + robotCtrlCmdEnd;
            sendDataToRobot(HexCtrlCmd);
        }
        static void sendCtrlCmdToRobot(string CtrlCmd, string cmdData)
        {
            string HexCtrlCmd = robotCtrlCmdHead + " " + CtrlCmd + " " + cmdData + " " + robotCtrlCmdEnd;
            sendDataToRobot(HexCtrlCmd);
        }

        static void sendCtrlCmdToRobot(string CtrlCmd, string coordX, string coordY, string coordZ ,string robotCoordSign)
        {
            string HexCtrlCmd = robotCtrlCmdHead + " " + CtrlCmd + " " + coordX + " " + coordY + " " + coordZ + " " + "00"+ " " + "00" + " " + "00" + " " + "20 00" + " " + robotCoordSign + " " + robotCtrlCmdEnd;
            sendDataToRobot(HexCtrlCmd);
        }
        /// <summary>
        /// 向特定ip的主机的端口发送数据报
        /// </summary>
        static void sendDataToRobot(string msg)
        {
            EndPoint point = new IPEndPoint(IPAddress.Parse("192.168.1.150"), 1337);
            byte[] hexdata = HexStringToByteArray(msg);
            gloveServer.SendTo(hexdata, point);
        }
        /// <summary>
        /// 接收发送给本机ip对应端口号的数据报
        /// </summary>
        
        static void reciveDataFromGlove(CancellationToken token)
        {
            Task.Run(() => ProcessDataAsync(token)); // 在后台启动数据处理任务

            while (!token.IsCancellationRequested)
            {
                try
                {
                    EndPoint point = new IPEndPoint(IPAddress.Any, 0); // 用来保存发送方的ip和端口号
                    byte[] buffer = new byte[1024];
                    int length = gloveServer.ReceiveFrom(buffer, ref point); // 接收数据报

                    SendDataToPython(buffer, length); // 直接将接收到的数据发送给Python

                    // 检查发送方的 IP 地址
                    IPEndPoint ipEndPoint = point as IPEndPoint;
                    if (ipEndPoint != null && ipEndPoint.Address.ToString() != "192.168.1.150")
                    {
                        // 将接收到的字节数据转换为16进制字符串
                        string hex = BitConverter.ToString(buffer, 0, length).Replace("-", " ");
                        receivedHexDataQueue.Enqueue(hex);// 将16进制数据添加到队列中
                                                          //Console.WriteLine($"{point} received hex data: {hex}");
                    }
                }
                catch (SocketException ex)
                {
                    // 当socket被关闭时会触发这个异常
                    Console.WriteLine("Socket has been closed. Exiting receive loop.");
                    break; // 跳出循环，结束这个方法，从而结束线程的执行
                }
                catch (Exception ex)
                {
                    // 处理其他类型的异常
                    Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                    break; // 在这里也跳出循环，或者根据实际情况决定是否要继续
                }
            }
        }

        static async Task ProcessDataAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (receivedHexDataQueue.TryDequeue(out string hexData))
                {
                    // 这里处理数据
                    await ProcessAndSaveData(hexData);
                }
                else
                {
                    await Task.Delay(5); // 如果队列为空，则等待一段时间再尝试
                }
            }
        }

        public static byte[] HexStringToByteArray(string s)
        {
            try
            {
                s = s.Replace(" ", "");
                byte[] buffer = new byte[s.Length / 2];
                for (int i = 0; i < s.Length; i += 2)
                    buffer[i / 2] = Convert.ToByte(s.Substring(i, 2), 16);
                return buffer;
            }
            catch (System.FormatException)
            {
                Console.WriteLine("Error");
                return new byte[] { 0xff, 0xee };
            }

        }

        // 定时执行的方法
        private static readonly object fileLock = new object();
        private static Task ProcessAndSaveData(string hexData)
        {
            // 构建CSV内容
            StringBuilder hexCsvContent = new StringBuilder();
            StringBuilder packetCsvContent = new StringBuilder();

            if (csvHeaderFlag == false)
            {
                packetCsvContent.AppendLine(BuildCsvHeader());
                csvHeaderFlag = true;
            }

            hexCsvContent.AppendLine(hexData);
            string packetData = ParseDataPacket(hexData);
            packetCsvContent.AppendLine(packetData);

            lock(fileLock)
            {
                try
                {
                    // 写入文件
                    File.AppendAllText(Path.Combine(fileSavePath, hexFileName), hexCsvContent.ToString());
                    File.AppendAllText(Path.Combine(fileSavePath, packetFileName), packetCsvContent.ToString());
                }
                catch(IOException) 
                {
                    Console.WriteLine("无法写入文件");
                }
            }
            return Task.CompletedTask;
            
        }




        private static string BuildCsvHeader()
        {
            var sb = new StringBuilder();
            sb.Append("DN, SN, Timestamp, ");

            for (int i = 1; i <= 10; i++)
            {
                sb.Append($"SensorData{i}, ");
            }

            for (int i = 1; i <= 9; i++)
            {
                sb.Append($"FloatSensorData{i}, ");
            }

            sb.Length -= 2; // 移除最后的逗号和空格
            return sb.ToString();
        }
        private static string ParseDataPacket(string hexData)
        {
            try
            {
                var bytes = ConvertHexStringToByteArray(hexData);

                // 检查数据包的开始和结束标志（兼容 C# 7.3）
                if (bytes.Length != 68 || bytes[0] != 0x5a || bytes[1] != 0x5a || bytes[bytes.Length - 2] != 0xa5 || bytes[bytes.Length - 1] != 0xa5)
                {                    
                    throw new InvalidOperationException("Invalid start or end sequence");
                }

                // 提取DN号和SN号
                byte dnNumber = bytes[2];
                byte snNumber = bytes[3];

                // 提取秒级时间戳
                int secondTimestamp = BitConverter.ToInt32(bytes, 4);

                // 提取毫秒级时间戳
                short millisecondTimestamp = BitConverter.ToInt16(bytes, 8);


                // 提取传感器数据
                var sensorData = new short[10];
                int sensorDataSum = 0;
                for (int i = 0; i < 10; i++)
                {
                    sensorData[i] = BitConverter.ToInt16(bytes, 10 + i * 2);
                    sensorDataSum += sensorData[i]; // 累加每个传感器的值
                }
                // 计算平均值
                double sensorDataAverage = (double)sensorDataSum / 10;
                // 根据平均值执行不同的操作
                if (sensorDataAverage < 900 && robotApexFlag == false)
                {
                    // 均值小于900时执行的代码
                    sendCtrlCmdToRobot(robotCtrlCmdElcGrpSet,"01");
                    robotApexFlag = true;
                    if (resetZeroPoint == false)
                    {
                        resetZeroPoint = true;
                    }
                }
                else if (sensorDataAverage > 1000 && robotApexFlag == true)
                {
                    // 均值大于1000时执行的代码
                    sendCtrlCmdToRobot(robotCtrlCmdElcGrpSet,"00");
                    robotApexFlag = false;
                }

                // 提取高精度float型传感器数据
                var floatSensorData = new float[9];
                for (int i = 0; i < 9; i++)
                {
                    floatSensorData[i] = BitConverter.ToSingle(bytes, 30 + i * 4);
                }

                // 构建解析后的数据字符串
                var sb = new StringBuilder();
                sb.Append($"{dnNumber}, {snNumber}, {secondTimestamp}.{millisecondTimestamp:000}, ");

                foreach (var data in sensorData)
                {
                    sb.Append($"{data}, ");
                }

                foreach (var data in floatSensorData)
                {
                    sb.Append($"{data}, ");
                }

                sb.Length -= 2; // 移除最后的逗号和空格
                return sb.ToString();
            }

            catch
            {
                Console.WriteLine("Warning: A data packet cannot be parsed correctly. Please check the data packet format or detect network conditions.");
                return "Parse failed";
            }
        }


        private static byte[] ConvertHexStringToByteArray(string hexString)
        {
            // 移除可能存在的非16进制字符（如空格）
            hexString = hexString.Replace(" ", string.Empty);

            // 检查字符串长度是否为偶数（每两个字符表示一个字节）
            if (hexString.Length % 2 != 0)
                throw new ArgumentException("Hex string length must be even.");

            byte[] bytes = new byte[hexString.Length / 2];
            for (int i = 0; i < hexString.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
            }
            return bytes;
        }


        private static string GetUniqueFileName(string directory, string baseFileName, string type)
        {
            string fileName;
            int index = 0;

            do
            {
                fileName = $"{baseFileName}_{type}{(index == 0 ? "" : $"({index})")}.csv";
                index++;
            } while (File.Exists(Path.Combine(directory, fileName)));

            return fileName;
        }


    } // End. ManagedClient class
} // End. NatNetML Namespace
