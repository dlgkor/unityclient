using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine.UIElements;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor.Sprites;
using System.IO;
using UnityEditor.PackageManager;

public class Client : MonoBehaviour
{
    public static int dataBufferSize = 4096;
    private delegate void PacketHandler(Packet _packet);
    private static Dictionary<int, PacketHandler> packetHandlers;

    public string IP = "192.168.0.16";
    public int Port = 8888;

    TCP tcp;
    UDP udp;
    int clientID;

    private static Client instance = null;

    private void ClientIDPacket(Packet _packet)
    {
        clientID = BitConverter.ToInt32(_packet.data);
        Debug.Log($"clientID: {clientID}");
        udp = new UDP();
        udp.Connect(((IPEndPoint)tcp.socket.Client.LocalEndPoint).Port);
        Debug.Log($"UDP port: {((IPEndPoint)tcp.socket.Client.LocalEndPoint).Port}");
    }

    private void helloserver(Packet _packet)
    {
        //move this function to packetmanger class
        Debug.Log(Encoding.ASCII.GetString(_packet.data));
    }

    void Start()
    {
        packetHandlers = new Dictionary<int, PacketHandler>(){
            {0x1000, ClientIDPacket},
            {0x1001, helloserver}
        };

        clientID = -1;
        tcp = new TCP();
        tcp.Connect();
    }

    private void Awake()
    {
        if(instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    public static Client Instance { get { 
            if(instance == null)
            {
                return null;
            }
            return instance; 
        } 
    }


    private void Update()
    {
        if (tcp != null)
        {
            string msg = "tcp Test";
            Packet _packet = new Packet();
            _packet.packetID = 0x1001;
            _packet.data = Encoding.ASCII.GetBytes(msg);
            _packet.WriteDataSize();
            tcp.SendPacket(_packet);
        }   

        if(udp != null) {
            string msg = "udp Test";
            Packet _Packet = new Packet();
            _Packet.packetID = 0x1001;
            _Packet.data = Encoding.ASCII.GetBytes(msg);
            _Packet.WriteDataSize();
            udp.SendPacket(_Packet);
        }
    }
    

    private void OnApplicationQuit()
    {
        tcp.DisConnect();
        udp.Disconnect();
    }

    public class TCP
    {
        public TcpClient socket;
        private NetworkStream stream;
        private byte[] receiveBuffer;
        private PacketComposer packetComposer;

        public TCP()
        {
            socket = null;
            stream = null;
            receiveBuffer = new byte[dataBufferSize];
            packetComposer = null;
        }

        public void Connect()
        {
            packetComposer = new PacketComposer(dataBufferSize * 2);

            socket = new TcpClient()
            {
                ReceiveBufferSize = dataBufferSize,
                SendBufferSize = dataBufferSize
            };

            socket.BeginConnect(Instance.IP, Instance.Port, connectCallBack, socket);
        }

        private void connectCallBack(IAsyncResult _result)
        {
            socket.EndConnect(_result);
            if (!socket.Connected)
            {
                return;
            }

            stream = socket.GetStream();
            Debug.Log("connected to Server");

            stream.BeginRead(receiveBuffer, 0, dataBufferSize ,receiveCallBack, null);
            //receiveBuffer가 new로 할당되어 있어야 작동함
            //null이면 read 안함
        }

        public void SendPacket(Packet _packet)
        {
            try
            {
                if (stream != null)
                {
                    byte[] packetBytes = _packet.Serialize();
                    stream.BeginWrite(packetBytes, 0, packetBytes.Length, null, null);
                }
            }
            catch (Exception _ex)
            {
                Debug.Log($"Error sending data to server via TCP: {_ex}");
            }
        }

        private void receiveCallBack(IAsyncResult _result)
        {
            int _byteLength =  stream.EndRead(_result);

            if (_byteLength <= 0)
            {
                // TODO: disconnect
                return;
            }

            packetComposer.addClip(receiveBuffer,_byteLength);

            while (true)
            {
                Packet _packet = packetComposer.Compose();
                if (_packet == null)
                {
                    break;
                }

                if (packetHandlers.ContainsKey(_packet.packetID))
                {
                    packetHandlers[_packet.packetID](_packet);
                }
            }
            packetComposer.ClearHandledData();

            stream.BeginRead(receiveBuffer, 0, dataBufferSize, receiveCallBack, null);
        }

        public void DisConnect()
        {
            stream.Close();
            socket.Close();
        }
    }

    public class UDP
    {
        public UdpClient socket;
        public IPEndPoint endPoint;

        //public int clientid { get; set; }

        public UDP()
        {
            socket = null;
            endPoint = new IPEndPoint(IPAddress.Parse(Instance.IP), Instance.Port);
        }

        public void Connect(int _localport)
        {
            socket = new UdpClient(_localport);

            socket.Connect(endPoint);
            socket.BeginReceive(ReceiveCallback, null);

            Debug.Log("Connected");
        }

        public void SendPacket(Packet _packet)
        {
            try
            {
                if (socket != null)
                {
                    byte[] packetBytes = _packet.Serialize(Instance.clientID);
                    Debug.Log($"Sending UDP Packet clientID:{Instance.clientID}, size: {packetBytes.Length}");
                    socket.BeginSend(packetBytes, packetBytes.Length, null, null);
                }
            }
            catch (Exception _ex)
            {
                Debug.Log($"Error sending data to server via UDP: {_ex}");
            }
        }

        private void ReceiveCallback(IAsyncResult _result)
        {
            try
            {
                Debug.Log("receivecallback");

                byte[] _data = socket.EndReceive(_result, ref endPoint);
                socket.BeginReceive(ReceiveCallback, null);

                Debug.Log("endreceive");

                if (_data.Length < 4)
                {
                    //disconnect
                }

                HandleData(_data);
            }
            catch
            {
                //disconnect
            }
        }

        private void HandleData(byte[] _data)
        {
            Debug.Log("handling data");
            Packet _packet = new Packet(_data);
            packetHandlers[_packet.packetID](_packet);
        }

        public void Disconnect()
        {
            socket.Close();
        }
    }
}
