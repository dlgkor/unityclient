using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using UnityEngine;

public class Packet
{
    public ushort packetID { get; set; }
    public ushort dataSize { get; set; }
    public bool recv_head;
    public byte[] data { get; set; }

    private int readCursor;

    public Packet() {
        packetID = 0;
        dataSize = 0;
        data = null;
        recv_head = false;
        readCursor = 0;
    }

    public Packet(byte[] _data)
    {
        packetID = BitConverter.ToUInt16(_data, 0);
        dataSize = BitConverter.ToUInt16(_data, 2);
        data = new byte[dataSize];
        Array.Copy(_data, 4, data, 0, dataSize);
    }

    public void WriteDataSize()
    {
        dataSize = (ushort)data.Length;
    }

    public byte[] Serialize()
    {
        using (MemoryStream memoryStream = new MemoryStream())
        {
            byte[] _packetID = BitConverter.GetBytes(packetID);
            byte[] _dataSize = BitConverter.GetBytes(dataSize);

            memoryStream.Write(_packetID, 0, _packetID.Length);
            memoryStream.Write(_dataSize, 0, _dataSize.Length);
            memoryStream.Write(data, 0, data.Length);

            return memoryStream.ToArray();
        }
    }

    public byte[] Serialize(int clientid)
    {
        using (MemoryStream memoryStream = new MemoryStream())
        {
            byte[] _clientid = BitConverter.GetBytes(clientid);
            byte[] _packetID = BitConverter.GetBytes(packetID);
            byte[] _dataSize = BitConverter.GetBytes(dataSize);

            memoryStream.Write(_clientid, 0, _clientid.Length);
            memoryStream.Write(_packetID, 0, _packetID.Length);
            memoryStream.Write(_dataSize, 0, _dataSize.Length);
            memoryStream.Write(data, 0, data.Length);

            return memoryStream.ToArray();
        }
    }
}

public class PacketComposer
{
    private Packet packet;
    private byte[] clipPacket;
    private int clipReadCur;
    private int maxClipSize;
    private int clipSize;

    public PacketComposer(int maxClipSize)
    {
        packet = new Packet();
        this.maxClipSize = maxClipSize;
        clipPacket = new byte[maxClipSize];
        clipReadCur = 0;
        clipSize = 0;
    }

    public void addClip(byte[] src, int srcSize)
    {
        Array.Copy(src, 0, clipPacket, clipSize, srcSize);
        clipSize += srcSize;
    }

    public Packet Compose()
    {
        //Compose and return packet

        if (clipSize == 0)
            return null;

        if (!packet.recv_head)
        {
            if(clipSize - clipReadCur < 4)
            {
                return null;
            }

            packet.packetID = BitConverter.ToUInt16(clipPacket, clipReadCur);
            clipReadCur += 2;
            packet.dataSize = BitConverter.ToUInt16(clipPacket, clipReadCur);
            clipReadCur += 2;

            packet.recv_head = true;
        }

        if (clipSize - clipReadCur < packet.dataSize)
            return null;

        packet.data = new byte[packet.dataSize];
        Array.Copy(clipPacket,clipReadCur,packet.data,0,packet.dataSize);
        clipReadCur += packet.dataSize;

        packet.recv_head = false;

        return packet;
    }
    public void ClearHandledData()
    {
        if(clipSize - clipReadCur != 0)
            Array.Copy(clipPacket, clipReadCur, clipPacket, 0, clipSize - clipReadCur);

        clipSize -= clipReadCur;
        clipReadCur = 0;
        //Clear handled data
    }
}