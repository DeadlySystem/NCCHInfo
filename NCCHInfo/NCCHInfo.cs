using System;
using System.Runtime.InteropServices;

namespace NCCHInfo
{
    [StructLayout(LayoutKind.Sequential, Pack = 1), ByteOrder(ByteOrder.LittleEndian)]
    public struct NCCHInfoHeader
    {
        public UInt32 reserved;
        public UInt32 version;
        public UInt32 entries;
        public UInt32 reserved2;

        public NCCHInfoHeader(UInt32 entries)
        {
            this.reserved = 0xFFFFFFFF;
            this.version = 0xF0000004;
            this.entries = entries;
            this.reserved2 = 0x00000000;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1), ByteOrder(ByteOrder.LittleEndian)]
    public struct NCCHInfoEntry
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16), ByteOrder(ByteOrder.BigEndian)]
        public byte[] counter;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16), ByteOrder(ByteOrder.Default)]
        public byte[] keyY;
        public UInt32 size; //In MB, rounded up
        public UInt32 reserved;
        public UInt32 uses9xcrypto; //0 or 1
        public UInt32 uses7xcrypto; //0 or 1
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8), ByteOrder(ByteOrder.Default)]
        public byte[] titleID;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 112), ByteOrder(ByteOrder.Default)]
        public byte[] outputname; //UTF-8 Encoding
    }
}