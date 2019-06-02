using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace MMRando.Utils
{

    public class ResourceUtils
    {

        public static void ApplyHack_File(string name, byte[] data)
        {
            BinaryReader hack_file = new BinaryReader(File.Open(name, FileMode.Open));
            int hack_len = (int)hack_file.BaseStream.Length;
            byte[] hack_content = new byte[hack_len];
            hack_file.Read(hack_content, 0, hack_len);
            hack_file.Close();
            int addr = 0;
            while (hack_content[addr] != 0xFF)
            {
                //Debug.WriteLine(addr.ToString("X4"));
                uint dest = ReadWriteUtils.Arr_ReadU32(hack_content, addr);
                addr += 4;
                uint len = ReadWriteUtils.Arr_ReadU32(hack_content, addr);
                addr += 4;
                ReadWriteUtils.Arr_Insert(hack_content, addr, (int)len, data, (int)dest);
                addr += (int)len;
            }
        }

        public static void ApplyHack(string name)
        {
            BinaryReader hack_file = new BinaryReader(File.Open(name, FileMode.Open));
            int hack_len = (int)hack_file.BaseStream.Length;
            byte[] hack_content = new byte[hack_len];
            hack_file.Read(hack_content, 0, hack_len);
            hack_file.Close();
            
            int addr = 0;
            while (hack_content[addr] != 0xFF)
            {
                uint dest = ReadWriteUtils.Arr_ReadU32(hack_content, addr);
                addr += 4;
                uint len = ReadWriteUtils.Arr_ReadU32(hack_content, addr);
                addr += 4;
                int f = RomUtils.GetFileIndexForWriting((int)dest);
                dest -= (uint)RomData.MMFileList[f].Addr;
                ReadWriteUtils.Arr_Insert(hack_content, addr, (int)len, RomData.MMFileList[f].Data, (int)dest);
                addr += (int)len;
            }
        }

        public static List<int[]> GetAddresses(string name)
        {
            List<int[]> Addrs = new List<int[]>();
            byte[] a;
            using (BinaryReader AddrFile = new BinaryReader(File.Open(name, FileMode.Open, FileAccess.Read)))
            {
                a = new byte[AddrFile.BaseStream.Length];
                AddrFile.Read(a, 0, a.Length);
            }
            int i = 0;
            while (a[i] != 0xFF)
            {
                int count = (int)ReadWriteUtils.Arr_ReadU32(a, i);
                int[] alist = new int[count];
                i += 4;
                for (int j = 0; j < count; j++)
                {
                    alist[j] = (int)ReadWriteUtils.Arr_ReadU32(a, i);
                    i += 4;
                }
                Addrs.Add(alist);
            }
            return Addrs;
        }

    }

}
