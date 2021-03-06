﻿using PersonaEditorLib.Extension;
using PersonaEditorLib.FileStructure.Text;
using PersonaEditorLib.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PersonaEditorLib.FileStructure.Text
{
    public class BMD : IPersonaFile
    {
        public BMD()
        {

        }

        public BMD(byte[] data)
        {
            using (MemoryStream MS = new MemoryStream(data))
                Open(MS);
        }

        public bool Open(string filepath)
        {
            using (FileStream FS = new FileStream(filepath, FileMode.Open, FileAccess.Read))
                return Open(FS);

        }

        public bool Open(Stream stream)
        {
            try
            {
                Name.Clear();
                Msg.Clear();

                stream.Position = 0x8;
                byte[] temp = new byte[4];
                stream.Read(temp, 0, 4);
                IsLittleEndian = Encoding.ASCII.GetString(temp) == "MSG1";
                stream.Position = 0;
                using (BinaryReader reader = Utilities.IO.OpenReadFile(stream, IsLittleEndian))
                    ParseMSG1(reader);

                return true;
            }
            catch (Exception e)
            {
                Name.Clear();
                Msg.Clear();

                Logging.Write("PTPfactory", e.ToString());
                return false;
            }
        }

        public bool Open(PTP PTP, Encoding New)
        {
            Name.Clear();
            Msg.Clear();
            foreach (var a in PTP.names)
                Name.Add(new Names(a.Index, a.NewName.GetTextBaseList(New).GetByteArray().ToArray()));

            foreach (var a in PTP.msg)
            {
                int Index = a.Index;
                string Name = a.Name;
                MSGs.MsgType Type = a.Type == "MSG" ? MSGs.MsgType.MSG : MSGs.MsgType.SEL;
                int CharacterIndex = a.CharacterIndex;

                byte[] MsgBytes = a.GetNew(New);

                Msg.Add(new MSGs(Index, Name, Type, CharacterIndex, MsgBytes.ToArray()));
            }

            return true;
        }

        private void ParseMSG1(BinaryReader BR)
        {
            try
            {
                byte[] buffer;

                int MSG_PointBlock_Pos = 0x20;
                BR.BaseStream.Position = 24;
                int MSG_count = BR.ReadInt32();
                BR.BaseStream.Position = MSG_PointBlock_Pos;
                List<int[]> MSGPosition = new List<int[]>();

                for (int i = 0; i < MSG_count; i++)
                {
                    int[] temp = new int[2];
                    temp[0] = BR.ReadInt32();
                    temp[1] = BR.ReadInt32();
                    MSGPosition.Add(temp);
                }

                int Name_Block_Position = BR.ReadInt32();
                int Name_Count = BR.ReadInt32();
                BR.BaseStream.Position = Name_Block_Position + MSG_PointBlock_Pos;

                List<long> NamePosition = new List<long>();
                for (int i = 0; i < Name_Count; i++)
                    NamePosition.Add(BR.ReadInt32());

                for (int i = 0; i < NamePosition.Count; i++)
                {
                    BR.BaseStream.Position = NamePosition[i] + MSG_PointBlock_Pos;
                    byte Byte = BR.ReadByte();
                    List<byte> Bytes = new List<byte>();
                    while (Byte != 0)
                    {
                        Bytes.Add(Byte);
                        Byte = BR.ReadByte();
                    }
                    Name.Add(new Names(i, Bytes.ToArray()));
                }

                for (int i = 0; i < MSGPosition.Count; i++)
                {
                    BR.BaseStream.Position = MSG_PointBlock_Pos + MSGPosition[i][1];
                    buffer = BR.ReadBytes(24);
                    string MSG_Name = System.Text.Encoding.Default.GetString(buffer).Trim('\0');
                    if (string.IsNullOrEmpty(MSG_Name))
                        MSG_Name = "<EMPTY>";

                    byte[] MSG_bytes = new byte[0];
                    MSGs.MsgType Type;
                    int CharacterIndex = 0xFFFF;

                    if (MSGPosition[i][0] == 0)
                    {
                        Type = MSGs.MsgType.MSG;
                        int count = BR.ReadUInt16();
                        CharacterIndex = BR.ReadUInt16();
                        if (count > 0)
                        {
                            BR.BaseStream.Position = BR.BaseStream.Position + 4 * count;
                            int size = BR.ReadInt32();
                            MSG_bytes = BR.ReadBytes(size);
                        }
                    }
                    else if (MSGPosition[i][0] == 1)
                    {
                        Type = MSGs.MsgType.SEL;
                        BR.BaseStream.Position += 2;
                        int count = BR.ReadUInt16();
                        if (count > 0)
                        {
                            BR.BaseStream.Position += 4 * count + 4;
                            int size = BR.ReadInt32();
                            MSG_bytes = BR.ReadBytes(size);
                        }
                    }
                    else
                    {
                        Logging.Write("PersonaEditorLib", "Error: Unknown message type!");

                        return;
                    }

                    MSGs MSG = new MSGs(i, MSG_Name, Type, CharacterIndex, MSG_bytes);

                    Msg.Add(MSG);
                }
            }
            catch (Exception e)
            {
                Logging.Write("PersonaEditorLib", "Error: Parse MSG1 error!");
                Logging.Write("PersonaEditorLib", e);
                Name.Clear();
                Msg.Clear();
            }
        }

        public class Names
        {
            public Names(int Index, byte[] NameBytes)
            {
                this.Index = Index;
                this.NameBytes = NameBytes;
            }

            public int Index { get; set; }
            public byte[] NameBytes { get; set; }
        }

        public class MSGs
        {
            public enum MsgType
            {
                MSG = 0,
                SEL = 1
            }

            public MSGs(int index, string name, MsgType type, int characterIndex, byte[] msgBytes)
            {
                Index = index;
                Type = type;
                Name = name;
                CharacterIndex = characterIndex;
                MsgBytes = msgBytes;
            }

            public int Index { get; set; }
            public MsgType Type { get; set; }
            public string Name { get; set; }
            public int CharacterIndex { get; set; }
            public byte[] MsgBytes { get; set; }
        }

        public List<MSGs> Msg { get; } = new List<MSGs>();
        public List<Names> Name { get; } = new List<Names>();

        static class GetNewBMD
        {
            public static void Get(IList<MSGs> msg, IList<Names> name, BinaryWriter BW)
            {
                byte[] buffer;

                List<List<int>> MSG_pos = new List<List<int>>();
                List<int> NAME_pos = new List<int>();
                List<int> LastBlock = new List<int>();

                buffer = new byte[4] { 7, 0, 0, 0 };
                BW.Write(buffer);
                BW.Write((int)0x0);

                buffer = Encoding.ASCII.GetBytes("MSG1");

                BW.Write(BitConverter.ToInt32(buffer, 0));

                BW.Write((int)0x0);
                BW.Write((int)0x0);
                BW.Write((int)0x0);
                BW.Write(msg.Count);
                BW.Write((ushort)0);
                BW.Write((ushort)0x2);

                foreach (var MSG in msg)
                {
                    if (MSG.Type == MSGs.MsgType.MSG)
                        BW.Write((int)0x0);
                    else if (MSG.Type == MSGs.MsgType.SEL)
                        BW.Write((int)0x1);
                    else
                        return;


                    LastBlock.Add((int)BW.BaseStream.Position);
                    BW.Write((int)0x0);
                }

                LastBlock.Add((int)BW.BaseStream.Position);
                BW.Write((int)0x0);
                BW.Write(name.Count);
                BW.Write((int)0x0);
                BW.Write((int)0x0);

                foreach (var MSG in msg)
                {
                    var split = MSG.MsgBytes.SplitSourceBytes();

                    // List<PTP.MSG.MSGstr> MSGStrings = new List<PTP.MSG.MSGstr>();
                    // MSGStrings.ParseString(MSG.MsgBytes);

                    List<int> MSG_o = new List<int>();
                    MSG_o.Add((int)BW.BaseStream.Position);

                    BW.WriteString(MSG.Name, 24);

                    if (MSG.Type == MSGs.MsgType.MSG)
                    {
                        BW.Write((ushort)split.Count);

                        if (MSG.CharacterIndex == -1) { BW.Write((ushort)0xFFFF); }
                        else { BW.Write((ushort)MSG.CharacterIndex); }
                    }
                    else if (MSG.Type == MSGs.MsgType.SEL)
                    {
                        BW.Write((ushort)0);
                        BW.Write((ushort)split.Count);
                        BW.Write((int)0x0);
                    }

                    int Size = 0;

                    foreach (var String in split)
                    {
                        LastBlock.Add((int)BW.BaseStream.Position);
                        BW.Write((int)0x0);
                        Size += String.Length;
                    }
                    MSG_o.Add(Size);

                    BW.Write((int)0x0);

                    foreach (var String in split)
                    {
                        List<byte> NewString = new List<byte>();
                        NewString.AddRange(String.ToArray());

                        MSG_o.Add((int)BW.BaseStream.Position);
                        BW.Write(NewString.ToArray());
                    }

                    while (BW.BaseStream.Length % 4 != 0)
                    {
                        BW.Write((byte)0);
                    }

                    MSG_pos.Add(MSG_o);
                }

                long Name_Block_pos = BW.BaseStream.Length;
                BW.BaseStream.Position = 0x20;
                for (int i = 0; i < msg.Count; i++)
                {
                    BW.BaseStream.Position += 4;
                    BW.Write((int)MSG_pos[i][0] - 0x20);
                }
                BW.Write((int)Name_Block_pos - 0x20);
                for (int i = 0; i < msg.Count; i++)
                {
                    BW.BaseStream.Position = MSG_pos[i][0];

                    if (msg[i].Type == MSGs.MsgType.MSG)
                    {
                        BW.BaseStream.Position += 28;
                    }
                    else if (msg[i].Type == MSGs.MsgType.SEL)
                    {
                        BW.BaseStream.Position += 32;
                    }

                    var split = msg[i].MsgBytes.SplitSourceBytes();

                    for (int k = 0; k < split.Count; k++)
                    {
                        BW.Write((int)MSG_pos[i][k + 2] - 0x20);
                    }
                    BW.Write((int)MSG_pos[i][1]);
                }


                BW.BaseStream.Position = Name_Block_pos;
                for (int i = 0; i < name.Count; i++)
                {
                    LastBlock.Add((int)BW.BaseStream.Position);
                    BW.Write((int)0);
                }

                foreach (var NAME in name)
                {
                    NAME_pos.Add((int)BW.BaseStream.Position);
                    if (NAME.NameBytes.Length == 0)
                        BW.Write(new byte[] { 0x20 });
                    else
                        BW.Write(NAME.NameBytes);

                    BW.Write((byte)0);
                }
                BW.BaseStream.Position = Name_Block_pos;
                for (int i = 0; i < name.Count; i++)
                {
                    BW.Write((int)NAME_pos[i] - 0x20);
                }
                BW.BaseStream.Position = BW.BaseStream.Length;
                while (BW.BaseStream.Length % 4 != 0)
                {
                    BW.Write((byte)0);
                }

                int LastBlockPos = (int)BW.BaseStream.Position;
                byte[] LastBlockBytes = getLastBlock(LastBlock);
                BW.Write(LastBlockBytes);

                BW.BaseStream.Position = 0x10;
                BW.Write((int)LastBlockPos);
                BW.Write((int)LastBlockBytes.Length);

                BW.BaseStream.Position = 0x4;
                BW.Write((int)BW.BaseStream.Length);

                BW.BaseStream.Position = 0;

                // buffer = new byte[BW.BaseStream.Length];
                // BW.BaseStream.Read(buffer, 0, (int)BW.BaseStream.Length);

                // return new MemoryStream(buffer);
            }

            //static byte[] getLastBlock(List<int> Addresses)
            //{
            //    int sum = 0;
            //    List<byte> returned = new List<byte>();

            //    for (int i = 0; i < Addresses.Count; i++)
            //    {
            //        int reloc = Addresses[i] - sum - 0x20;
            //        int amount = getSeq(ref Addresses, i);
            //        Encode(reloc, ref returned, ref sum);
            //        if (amount > 1)
            //        {
            //            reloc = 7;
            //            reloc |= ((amount - 2) / 2) << 4;
            //            if (amount % 2 == 1)
            //            {
            //                reloc |= 8;
            //            }

            //            if (amount > 30)
            //            {
            //                Encode(reloc, ref returned, ref sum);
            //            }

            //            returned.Add((byte)reloc);
            //            i += amount;
            //            sum += amount * 4;
            //        }
            //    }

            //    return returned.ToArray();
            //}

            static byte[] getLastBlock(List<int> Addresses)
            {
                int sum = 0;
                List<byte> returned = new List<byte>();

                for (int i = 0; i < Addresses.Count; i++)
                {
                    int reloc = Addresses[i] - sum - 0x20;
                    int amount = getSeq(ref Addresses, i);
                    Encode(reloc, ref returned, ref sum);
                    if (amount > 1)
                    {
                        reloc = 7;
                        reloc |= ((amount - 2) / 2) << 4;
                        if (amount % 2 == 1)
                        {
                            reloc |= 8;
                        }

                        returned.Add((byte)reloc);
                        i += amount;
                        sum += amount * 4;
                    }
                }

                return returned.ToArray();
            }

            static int getSeq(ref List<int> Addresses, int index)
            {
                if (index < Addresses.Count - 1)
                {
                    if (Addresses[index + 1] - Addresses[index] == 4)
                        return getSeq(ref Addresses, index + 1) + 1;
                    else
                        return 0;
                }
                return 0;
            }

            static void Encode(int reloc, ref List<byte> LastBlock, ref int sum)
            {
                if (reloc % 2 == 0)
                {
                    int temp = reloc >> 1;
                    if (temp <= 0xFF)
                    {
                        LastBlock.Add((byte)temp);
                    }
                    else
                    {
                        byte item = (byte)((reloc & 0xff) + 1);
                        byte num2 = (byte)((reloc & 0xff00) >> 8);
                        LastBlock.Add(item);
                        LastBlock.Add(num2);
                    }

                }
                else
                {
                    byte item = (byte)((reloc & 0xff) + 1);
                    byte num2 = (byte)((reloc & 0xff00) >> 8);
                    LastBlock.Add(item);
                    LastBlock.Add(num2);
                }
                sum += reloc;
            }
        }

        public bool IsLittleEndian { get; set; } = true;

        #region IPersonaFile

        public FileType Type => FileType.BMD;

        public List<ObjectFile> SubFiles { get; } = new List<ObjectFile>();

        public Dictionary<string, object> GetProperties
        {
            get
            {
                Dictionary<string, object> returned = new Dictionary<string, object>();

                returned.Add("Names Count", Name.Count);
                returned.Add("MSG Count", Msg.Count);
                returned.Add("Type", Type);

                return returned;
            }
        }

        #endregion IPersonaFile

        #region IFile

        public int Size()
        {
            return Get().Length;
        }

        public byte[] Get()
        {
            byte[] returned = new byte[0];
            using (MemoryStream MS = new MemoryStream())
            {
                BinaryWriter writer = Utilities.IO.OpenWriteFile(MS, IsLittleEndian);
                GetNewBMD.Get(Msg, Name, writer);
                returned = MS.ToArray();
            }
            return returned;
        }

        #endregion IFile        
    }
}