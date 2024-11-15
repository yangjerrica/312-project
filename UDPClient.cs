﻿/*
    Author(s):  Long Qian
    Created on: 2019-03-29
    (C) Copyright 2015-2018 Johns Hopkins University (JHU), All Rights Reserved.

    --- begin cisst license - do not edit ---
    This software is provided "as is" under an open source license, with
    no warranty.  The complete license can be found in license.txt and
    http://www.cisst.org/cisst/license.txt.
    --- end cisst license ---
*/
using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.UI; // For Slider

#if !UNITY_EDITOR && UNITY_METRO
using Windows.Networking.Sockets;
using Windows.Networking.Connectivity;
using Windows.Networking;
#else
using System.Net;
using System.Net.Sockets;
using System.Threading;
#endif


namespace DVRK
{

    public class UDPClient : MonoBehaviour
    {

        public int port = 8051;
        public string PyIPAddress = "10.0.0.201";
        private UdpClient udpClient;
        private Queue<string> receivedUDPPacketQueue = new Queue<string>();

        // Progress bar UI element
        public Slider progressBar;
        private int maxQueueSize = 100; // Max size for progress tracking, you can change this value as needed.

        public string GetLatestUDPPacket()
        {
            string message = "";
            while (receivedUDPPacketQueue.Count > 0)
            {
                message = receivedUDPPacketQueue.Dequeue();
            }
            return message;
        }

        private string objectName;

        private void Awake()
        {
            objectName = name;
        }

#if !UNITY_EDITOR && UNITY_METRO

        DatagramSocket socket;
        
        async void Start() {
            socket = new DatagramSocket();
            socket.MessageReceived += Socket_MessageReceived;
            HostName IP = null;
            try
            {
                var icp = NetworkInformation.GetInternetConnectionProfile();

                IP = Windows.Networking.Connectivity.NetworkInformation.GetHostNames()
                .SingleOrDefault(
                    hn =>
                        hn.IPInformation?.NetworkAdapter != null && hn.IPInformation.NetworkAdapter.NetworkAdapterId
                        == icp.NetworkAdapter.NetworkAdapterId);

                await socket.BindEndpointAsync(IP, port.ToString());
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
                Debug.Log(SocketError.GetStatus(e.HResult).ToString());
                return;
            }
            Debug.Log(objectName + ": DatagramSocket setup done...");
        }

        private async void Socket_MessageReceived(Windows.Networking.Sockets.DatagramSocket sender,
            Windows.Networking.Sockets.DatagramSocketMessageReceivedEventArgs args)
        {
            //Debug.Log("Received message: ");
            //Read the message that was received from the UDP echo client.
            Stream streamIn = args.GetDataStream().AsStreamForRead();
            StreamReader reader = new StreamReader(streamIn);
            string message = await reader.ReadLineAsync();

            //Debug.Log("Message: " + message);

            receivedUDPPacketQueue.Enqueue(message);
            UpdateProgressBar();
        }

        private void OnDestroy() {
            if (socket != null) {
                socket.MessageReceived -= Socket_MessageReceived;
                socket.Dispose();
                Debug.Log(objectName + ": Socket disposed");
            }
        }

#else

        Thread receiveThread;
        UdpClient client;

        private bool shouldTerminate = true;

        public void Start()
        {
            shouldTerminate = false;
            receiveThread = new Thread(new ThreadStart(ReceiveData));
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }


        void OnDestroy()
        {
            shouldTerminate = true;
            client.Close(); // trigger the exception in thread
            receiveThread.Abort();
            while (receiveThread.IsAlive)
            {
                Debug.Log(objectName + ": Still alive");
            }
            Debug.Log(objectName + ": Receive thread stopped");
        }

        // Method to send data to Python
        //public void SendJointData(string message)
        //{
        //    try
        //    {
        //        Debug.Log(objectName + ": Starting UDP, send joint data");
        //        udpClient = new UdpClient();
        //        byte[] data = Encoding.UTF8.GetBytes(message);
        //        udpClient.Send(data, data.Length, PyIPAddress, port);
        //    }
        //    catch (SocketException e)
        //    {
        //        Debug.LogError("Socket error while sending data: " + e.Message);
        //    }
        //}


        // receive thread
        private void ReceiveData()
        {
            client = new UdpClient(port);
            Debug.Log(objectName + ": Receive thread starts");
            while (!shouldTerminate)
            {
                try
                {
                    IPEndPoint anyIP = new IPEndPoint(IPAddress.Parse("10.0.0.201"), 0);
                    byte[] data = client.Receive(ref anyIP);
                    string text = Encoding.UTF8.GetString(data);
                    // Debug.Log("EditorUDPClient: Packet >> " + text);

                    receivedUDPPacketQueue.Enqueue(text);
                }
                catch (Exception err)
                {
                    Debug.Log(err.ToString());
                }
            }
        }



#endif
    }
}