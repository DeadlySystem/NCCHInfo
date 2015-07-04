using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace NCCHInfo
{
    public class Parser
    {
        private const int MEDIAUNITSIZE = 0x200;
        private const int BYTES_PER_MEGABYTE = 1024 * 1024;




        public List<NCCHInfoEntry> parse(string filename)
        {
            //Open the file
            FileStream file = File.OpenRead(filename);
            BinaryReader reader = new BinaryReader(file, Encoding.ASCII);

            //Read the signature
            reader.BaseStream.Position = 0x100;
            string signature = new string(reader.ReadChars(4));

            List<NCCHInfoEntry> result;

            //Process contents based on signature
            if(signature.Equals("NCSD"))
            {
                //Console.WriteLine("Detected NCSD file");
                result = this.parseNCSD(reader);
            }
            else if(signature.Equals("NCCH"))
            {
                //Console.WriteLine("Detected NCCH file");
                result = this.parseNCCH(reader);
            }
            else
            {
                result = new List<NCCHInfoEntry>();
                Console.WriteLine("File does not have a valid signature.");
            }

            //Close the stream
            reader.Close();

            return result;
        }




        private List<NCCHInfoEntry> parseNCSD(BinaryReader reader)
        {
            Console.WriteLine("Parsing NCSD in file \"{0}\"", (reader.BaseStream as FileStream).Name);
            reader.BaseStream.Position = 0;

            byte[] headerbytes = reader.ReadBytes(Marshal.SizeOf(typeof(NCSDHeader)));
            NCSDHeader header = StructMarshaling.BytesToStruct<NCSDHeader>(headerbytes);

            string titleID = ByteArrayTo3DSIdentifier(header.titleId);

            List<NCCHInfoEntry> entries = new List<NCCHInfoEntry>();

            for(uint i = 0; i <= header.partitionTable.GetUpperBound(0); ++i)
            {
                PartitionEntry currententry = header.partitionTable[i];
                if(currententry.offset != 0)
                {
                    List<NCCHInfoEntry> newentries = parseNCCH(reader, currententry.offset * MEDIAUNITSIZE, i, titleID, false);
                    entries.AddRange(newentries);
                }
            }

            return entries;
        }




        private List<NCCHInfoEntry> parseNCCH(BinaryReader reader, uint offset = 0, uint idx = 0, string titleID = "", bool standalone = true)
        {
            string indent = standalone ? "  " : "    ";

            if(standalone)
            {
                Console.WriteLine("Parsing NCCH in file " + (reader.BaseStream as FileStream).Name);
            }
            else
            {
                Console.WriteLine("  Parsing {0} NCCH", GetPartitionName(idx));
            }

            //Read the NCCH header
            reader.BaseStream.Position = offset;
            byte[] headerbytes = reader.ReadBytes(Marshal.SizeOf(typeof(NCCHHeader)));
            NCCHHeader header = StructMarshaling.BytesToStruct<NCCHHeader>(headerbytes);

            if(titleID.Equals(""))
            {
                titleID = ByteArrayTo3DSIdentifier(header.titleId);
            }

            //Find keyY
            byte[] keyY = new byte[16];
            Array.Copy(header.signature, 0, keyY, 0, keyY.Length);

            //Print info
            if(!standalone)
            {
                Console.WriteLine(indent + "NCCH offset: {0:X8}", offset);
            }
            Console.WriteLine(indent + "Product code: " + Encoding.ASCII.GetString(header.productCode).TrimEnd((char) 0x00));
            if(!standalone)
            {
                Console.WriteLine(indent + "Partition number: {0}", idx);
            }
            Console.WriteLine(indent + "KeyY: {0}", ToHexString(keyY));
            Console.WriteLine(indent + "Title ID: {0}", ByteArrayTo3DSIdentifier(header.titleId));
            Console.WriteLine(indent + "Format version: {0}", header.formatVersion);

            bool uses7xCrypto = (header.flags[3] != 0x00);
            bool usesSeedCrypto = (header.flags[3] == 0x20);

            if(uses7xCrypto)
            {
                Console.WriteLine(indent + "Uses 7.x NCCH crypto");
            }
            if(usesSeedCrypto)
            {
                Console.WriteLine(indent + "Uses 9.x SEED crypto");
            }

            Console.WriteLine();

            List<NCCHInfoEntry> entries = new List<NCCHInfoEntry>();
            NCCHInfoEntry entry;
            if(header.exhdrSize != 0)
            {
                entry = parseNCCHSection(header, NCCHSection.ExHeader, keyY, false, false, true, indent);
                byte[] namebytes = Encoding.UTF8.GetBytes(genOutName(titleID, GetPartitionName(idx), "exheader"));
                Array.Copy(namebytes, 0, entry.outputname, 0, namebytes.Length);
                entries.Add(entry);
                Console.WriteLine();
            }

            if(header.exefsSize != 0)
            {
                //We need generate two xorpads for exefs if it uses 7.x crypto, since only a part of it uses the new crypto.
                entry = parseNCCHSection(header, NCCHSection.ExeFS, keyY, false, false, true, indent);
                byte[] namebytes = Encoding.UTF8.GetBytes(genOutName(titleID, GetPartitionName(idx), "exefs_norm"));
                Array.Copy(namebytes, 0, entry.outputname, 0, namebytes.Length);
                entries.Add(entry);
                if(uses7xCrypto)
                {
                    entry = parseNCCHSection(header, NCCHSection.ExeFS, keyY, uses7xCrypto, usesSeedCrypto, false, indent);
                    namebytes = Encoding.UTF8.GetBytes(genOutName(titleID, GetPartitionName(idx), "exefs_7x"));
                    Array.Copy(namebytes, 0, entry.outputname, 0, namebytes.Length);
                    entries.Add(entry);
                }
                Console.WriteLine();
            }

            if(header.romfsSize != 0)
            {
                entry = parseNCCHSection(header, NCCHSection.RomFS, keyY, uses7xCrypto, usesSeedCrypto, true, indent);
                byte[] namebytes = Encoding.UTF8.GetBytes(genOutName(titleID, GetPartitionName(idx), "romfs"));
                Array.Copy(namebytes, 0, entry.outputname, 0, namebytes.Length);
                entries.Add(entry);
                Console.WriteLine();
            }

            Console.WriteLine();

            return entries;
        }



        private NCCHInfoEntry parseNCCHSection(NCCHHeader header, NCCHSection type, byte[] keyY, bool uses7xCrypto, bool usesSeedCrypto, bool print, string indent)
        {
            NCCHInfoEntry entry = new NCCHInfoEntry();
            entry.keyY = keyY;
            entry.reserved = 0x00000000;
            entry.uses7xcrypto = (UInt32) (uses7xCrypto ? 0x00000001 : 0x00000000);
            entry.uses9xcrypto = (UInt32) (usesSeedCrypto ? 0x00000001 : 0x00000000);
            entry.outputname = new byte[112];

            string sectionname;
            uint offset;
            uint sectionsize;

            switch(type)
            {
                case NCCHSection.ExHeader:
                    sectionname = "ExHeader";
                    offset = 0x200; //Always 0x200
                    sectionsize = header.exhdrSize * MEDIAUNITSIZE;
                    break;
                case NCCHSection.ExeFS:
                    sectionname = "ExeFS";
                    offset = header.exefsOffset * MEDIAUNITSIZE;
                    sectionsize = header.exefsSize * MEDIAUNITSIZE;
                    break;
                case NCCHSection.RomFS:
                    sectionname = "RomFS";
                    offset = header.romfsOffset * MEDIAUNITSIZE;
                    sectionsize = header.romfsSize * MEDIAUNITSIZE;
                    break;
                default:
                    Console.Error.WriteLine("Illegal NCCH Section type provided!");
                    Environment.Exit(1);
                    return entry; //Needed to compile
            }

            entry.counter = getNcchAesCounter(header, type);
            entry.titleID = new byte[8];
            Array.Copy(header.programId, 0, entry.titleID, 0, 8);

            //Compute section size in MB, rounding up to the next MB
            uint sectionMb = sectionsize / BYTES_PER_MEGABYTE; //Rounding down in this step
            uint remainder = sectionsize % BYTES_PER_MEGABYTE;
            if(remainder != 0)
            {
                ++sectionMb;
            }
            entry.size = sectionMb;

            if(print)
            {
                Console.WriteLine(String.Format(indent + "{0} offset:  {1:X8}", sectionname, offset));
                Console.WriteLine(String.Format(indent + "{0} counter: {1}", sectionname, ToHexString(entry.counter)));
                Console.WriteLine(String.Format(indent + "{0} Megabytes(rounded up): {1}", sectionname, sectionMb));
            }


            //TODO
            //return struct.pack('<16s16sIIIIQ', str(counter), str(keyY), sectionMb, 0, usesSeedCrypto, uses7xCrypto, titleId)

            return entry;
        }



        private string genOutName(string titleId, string partitionName, string sectionName)
        {
            string outName = String.Format("/{0}.{1}.{2}.xorpad", titleId, partitionName, sectionName);
            if(outName.Length > 112)
            {
                Console.Error.WriteLine("Output file name too large. This shouldn't happen.");
                Environment.Exit(1);
            }

            //Add padding so the entry is 160 bytes (48 bytes are set before filename)
            return outName;
            //TODO: Technically, this will be a byte[]... return outName + (b'\x00'*(112-len(outName)))
        }


        private byte[] getNcchAesCounter(NCCHHeader header, NCCHSection type) //Function based on code from ctrtool's source: https://github.com/Relys/Project_CTR
        {
            byte[] counter = new byte[16];
            if(header.formatVersion == 2 || header.formatVersion == 0)
            {
                for(int i = 0; i < 8; ++i)
                {
                    counter[i] = header.titleId[header.titleId.Length - 1 - i];
                }
                counter[8] = (byte) type;
            }
            else if(header.formatVersion == 1)
            {
                UInt32 x = 0;
                switch(type)
                {
                    case NCCHSection.ExHeader:
                        x = 0x200; //ExHeader is always 0x200 bytes into the NCCH
                        break;
                    case NCCHSection.ExeFS:
                        x = header.exefsOffset * MEDIAUNITSIZE;
                        break;
                    case NCCHSection.RomFS:
                        x = header.romfsOffset * MEDIAUNITSIZE;
                        break;
                }
                for(int i = 0; i < 8; ++i)
                {
                    counter[i] = header.titleId[i];
                }
                counter[12] = (byte) ((x >> 24) & 0xFF);
                counter[13] = (byte) ((x >> 16) & 0xFF);
                counter[14] = (byte) ((x >> 8) & 0xFF);
                counter[15] = (byte) (x & 0xFF);
            }
            return counter;
        }


        private string ByteArrayTo3DSIdentifier(byte[] array)
        {
            StringBuilder result = new StringBuilder();
            for(int i = array.Length - 1; i >= 0; --i)
            {
                result.AppendFormat("{0:X2}", array[i]);
            }
            return result.ToString();
        }

        private string ToHexString(byte[] array)
        {
            StringBuilder result = new StringBuilder();
            for(int i = 0; i < array.Length; ++i)
            {
                result.AppendFormat("{0:X2}", array[i]);
            }
            return result.ToString();
        }




        private string GetPartitionName(uint partitionindex)
        {
            switch(partitionindex)
            {
                case 0: return "Main";
                case 1: return "Manual";
                case 2: return "DownloadPlay";
                case 3: return "Partition4";
                case 4: return "Partition5";
                case 5: return "Partition6";
                case 6: return "Partition7";
                case 7: return "UpdateData";
                default: return "UNKNOWN";
            }
        }





        //Structs

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct NCSDHeader
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public Byte[] signature;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public Char[] magic;
            public UInt32 mediaSize;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public Byte[] titleId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public Byte[] padding0;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public PartitionEntry[] partitionTable;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
            public Byte[] padding1;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public Byte[] flags;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public Byte[] ncchIdTable;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
            public Byte[] padding2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct PartitionEntry
        {
            public UInt32 offset;
            public UInt32 size;
        }


        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct NCCHHeader
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public Byte[] signature;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public Char[] magic;
            public UInt32 ncchSize;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public Byte[] titleId;
            public UInt16 makerCode;
            public Byte formatVersion;
            public Byte formatVersion2;
            public UInt32 padding0;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public Byte[] programId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public Byte[] padding1;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public Byte[] logoHash;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public Byte[] productCode;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public Byte[] exhdrHash;
            public UInt32 exhdrSize;
            public UInt32 padding2;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public Byte[] flags;
            public UInt32 plainRegionOffset;
            public UInt32 plainRegionSize;
            public UInt32 logoOffset;
            public UInt32 logoSize;
            public UInt32 exefsOffset;
            public UInt32 exefsSize;
            public UInt32 exefsHashSize;
            public UInt32 padding4;
            public UInt32 romfsOffset;
            public UInt32 romfsSize;
            public UInt32 romfsHashSize;
            public UInt32 padding5;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public Byte[] exefsHash;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public Byte[] romfsHash;
        }

        enum NCCHSection : byte
        {
            ExHeader = 1,
            ExeFS = 2,
            RomFS = 3
        }
    }
}