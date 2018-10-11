using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Google.Protobuf;
using Grpc.Core;
using UnityEngine;

namespace DataClient
{
    public class Client
    {
        private const int port = 8080;
        
        private byte[] magic = new byte[] { 72, 69, 76, 79 };

        // ManualResetEvent instances signal completion.  
        private static ManualResetEvent connectDone =
            new ManualResetEvent(false);

        private static ManualResetEvent sendDone =
            new ManualResetEvent(false);

        // The response from the remote device.  
        private static String response = String.Empty;

        private Socket client = null;

        public Client()
        {
            start_point:
            // Connect to a remote device.  
            try
            {
                // Establish the remote endpoint for the socket.  
                // The name of the   
                // remote device is "host.contoso.com".  
                IPHostEntry ipHostInfo = Dns.GetHostEntry(GetArg("-ip"));
                IPAddress ipAddress = ipHostInfo.AddressList[0];
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

                // Create a TCP/IP socket.  
                client = new Socket(ipAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.  
                var result = client.BeginConnect(remoteEP, null, client);
                //connectDone.WaitOne();
                bool success = result.AsyncWaitHandle.WaitOne(5, true);
                client.EndConnect(result);
                    

                
                
                // Send test data to the remote device.  
                //Send("1111111");
                //sendDone.WaitOne();

                // Write the response to the console.  
                Console.WriteLine("Response received : {0}", response);

                // Release the socket.  
                //client.Shutdown(SocketShutdown.Both);
                //client.Close();

            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
                throw new Exception();
            }
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            
            try
            {
                // Retrieve the socket from the state object.  
                client = (Socket) ar.AsyncState;

                // Complete the connection.  
                client.EndConnect(ar);

                Debug.Log("Socket connected to {0}" + 
                    client.RemoteEndPoint.ToString());

                // Signal that the connection has been made.  
                connectDone.Set();
               
            }
            catch (Exception e)
            {
                
                Debug.Log(e.ToString());
                //throw new Exception();
            }
        }

        public void Send(String data)
        {
            // Convert the string data to byte data using ASCII encoding.  
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            // Begin sending the data to the remote device.  
            client.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), client);
        }
        
        public void Send(VisualObservationStruct obs)
        {
            byte[] byteData = obs.observation.ToByteArray();
            byte[] img_width = BitConverter.GetBytes(obs.width);
            byte[] img_height = BitConverter.GetBytes(obs.height);
            byte[] img_channels = BitConverter.GetBytes(obs.channels);
            
            var s = new MemoryStream();
            s.Write(magic, 0, magic.Length);
            s.Write(img_width, 0, img_width.Length);
            s.Write(img_height, 0, img_height.Length);
            s.Write(img_channels, 0, img_channels.Length);
            s.Write(byteData, 0, byteData.Length);
            var b3 = s.ToArray();
            
            // Begin sending the data to the remote device.  
            client.BeginSend(b3, 0, b3.Length, 0,
                new AsyncCallback(SendCallback), client);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                client = (Socket) ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = client.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to server.", bytesSent);

                // Signal that all bytes have been sent.  
                sendDone.Set();
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
            }
        }
        
        public static string GetArg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == name && args.Length > i + 1)
                {
                    return args[i + 1];
                }
            }
            return null;
        }
    }
}