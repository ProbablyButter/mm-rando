﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MMRando
{

    public partial class MainRandomizerForm
    {

        private void WriteAudioSeq()
        {
            if (!cBGM.Checked) { return; };
            foreach (SequenceInfo s in SequenceList)
            {
                s.Name = MusicDirectory + s.Name;
            };
            ROMFuncs.ApplyHack(ModsDirectory + "fix-music");
            ROMFuncs.ApplyHack(ModsDirectory + "inst24-swap-guitar");
            ROMFuncs.RebuildAudioSeq(SequenceList);
        }

        private void WriteLinkAppearance()
        {
            if (cLink.SelectedIndex == 0)
            {
                WriteTunicColour();
            }
            else if (cLink.SelectedIndex < 4)
            {
                int i = cLink.SelectedIndex;
                BinaryReader b = new BinaryReader(File.Open(ObjsDirectory + "link-" + i.ToString(), FileMode.Open));
                byte[] obj = new byte[b.BaseStream.Length];
                b.Read(obj, 0, obj.Length);
                b.Close();
                if (i < 3)
                {
                    WriteTunicColour(obj, i);
                };
                ROMFuncs.ApplyHack(ModsDirectory + "fix-link-" + i.ToString());
                ROMFuncs.InsertObj(obj, 0x11);
                if (i == 3)
                {
                    b = new BinaryReader(File.Open(ObjsDirectory + "kafei", FileMode.Open));
                    obj = new byte[b.BaseStream.Length];
                    b.Read(obj, 0, obj.Length);
                    b.Close();
                    WriteTunicColour(obj, i);
                    ROMFuncs.InsertObj(obj, 0x1C);
                    ROMFuncs.ApplyHack(ModsDirectory + "fix-kafei");
                };
            };
            List<int[]> Others = ROMFuncs.GetAddresses(AddrsDirectory + "tunic-forms");
            ROMFuncs.UpdateFormTunics(Others, bTunic.BackColor);
        }

        private void WriteTunicColour()
        {
            Color t = bTunic.BackColor;
            byte[] c = { t.R, t.G, t.B };
            List<int[]> locs = ROMFuncs.GetAddresses(AddrsDirectory + "tunic-colour");
            for (int i = 0; i < locs.Count; i++)
            {
                ROMFuncs.WriteROMAddr(locs[i], c);
            };
        }

        private void WriteTunicColour(byte[] obj, int i)
        {
            Color t = bTunic.BackColor;
            byte[] c = { t.R, t.G, t.B };
            List<int[]> locs = ROMFuncs.GetAddresses(AddrsDirectory + "tunic-" + i.ToString());
            for (int j = 0; j < locs.Count; j++)
            {
                ROMFuncs.WriteFileAddr(locs[j], c, obj);
            };
        }

        private void WriteTatlColour()
        {
            if (cTatl.SelectedIndex != 5)
            {
                byte[] c = new byte[8];
                List<int[]> locs = ROMFuncs.GetAddresses(AddrsDirectory + "tatl-colour");
                for (int i = 0; i < locs.Count; i++)
                {
                    ROMFuncs.Arr_WriteU32(c, 0, Values.TatlColours[cTatl.SelectedIndex, i << 1]);
                    ROMFuncs.Arr_WriteU32(c, 4, Values.TatlColours[cTatl.SelectedIndex, (i << 1) + 1]);
                    ROMFuncs.WriteROMAddr(locs[i], c);
                };
            }
            else
            {
                ROMFuncs.ApplyHack(ModsDirectory + "rainbow-tatl");
            };
        }

        private void WriteQuickText()
        {
            if (cQText.Checked)
            {
                ROMFuncs.ApplyHack(ModsDirectory + "quick-text");
            };
        }

        private void WriteCutscenes()
        {
            if (cCutsc.Checked)
            {
                ROMFuncs.ApplyHack(ModsDirectory + "short-cutscenes");
            };
        }

        private void WriteDungeons()
        {
            if ((cMode.SelectedIndex == 2) || (!cDEnt.Checked))
            {
                return;
            };
            ROMFuncs.WriteEntrances(Values.OldEntrances, _newEntrances);
            ROMFuncs.WriteEntrances(Values.OldExits, _newExits);
            byte[] li = new byte[] { 0x24, 0x02, 0x00, 0x00 };
            List<int[]> addr = new List<int[]>();
            addr = ROMFuncs.GetAddresses(AddrsDirectory + "d-check");
            for (int i = 0; i < addr.Count; i++)
            {
                li[3] = (byte)_newExts[i];
                ROMFuncs.WriteROMAddr(addr[i], li);
            };
            ROMFuncs.ApplyHack(ModsDirectory + "fix-dungeons");
            addr = ROMFuncs.GetAddresses(AddrsDirectory + "d-exit");
            for (int i = 0; i < addr.Count; i++)
            {
                if (i == 2)
                {
                    ROMFuncs.WriteROMAddr(addr[i], new byte[] { (byte)((Values.OldExits[_newEnts[i + 1]] & 0xFF00) >> 8), (byte)(Values.OldExits[_newEnts[i + 1]] & 0xFF) });
                }
                else
                {
                    ROMFuncs.WriteROMAddr(addr[i], new byte[] { (byte)((Values.OldExits[_newEnts[i]] & 0xFF00) >> 8), (byte)(Values.OldExits[_newEnts[i]] & 0xFF) });
                };
            };
            addr = ROMFuncs.GetAddresses(AddrsDirectory + "dc-flagload");
            for (int i = 0; i < addr.Count; i++)
            {
                ROMFuncs.WriteROMAddr(addr[i], new byte[] { (byte)((_newDCFlags[i] & 0xFF00) >> 8), (byte)(_newDCFlags[i] & 0xFF) });
            };
            addr = ROMFuncs.GetAddresses(AddrsDirectory + "dc-flagmask");
            for (int i = 0; i < addr.Count; i++)
            {
                ROMFuncs.WriteROMAddr(addr[i], new byte[] { (byte)((_newDCMasks[i] & 0xFF00) >> 8), (byte)(_newDCMasks[i] & 0xFF) });
            };
        }

        private void WriteGimmicks()
        {
            int i = cDMult.SelectedIndex;
            if (i > 0)
            {
                ROMFuncs.ApplyHack(ModsDirectory + "dm-" + i.ToString());
            };
            i = cDType.SelectedIndex;
            if (i > 0)
            {
                ROMFuncs.ApplyHack(ModsDirectory + "de-" + i.ToString());
            };
            i = cGravity.SelectedIndex;
            if (i > 0)
            {
                ROMFuncs.ApplyHack(ModsDirectory + "movement-" + i.ToString());
            };
            i = cFloors.SelectedIndex;
            if (i > 0)
            {
                ROMFuncs.ApplyHack(ModsDirectory + "floor-" + i.ToString());
            };
        }

        private void WriteEnemies()
        {
            if (cEnemy.Checked)
            {
                SeedRNG();
                ROMFuncs.ShuffleEnemies(RNG);
            };
        }

        private void WriteFreeItem(int Item)
        {
            ROMFuncs.WriteToROM(ITEM_ADDRS[Item], ITEM_VALUES[Item]);
            switch (Item)
            {
                case 1: //bow
                    ROMFuncs.WriteToROM(0xC5CE6F, (byte)0x01);
                    break;
                case 5: //bomb bag
                    ROMFuncs.WriteToROM(0xC5CE6F, (byte)0x08);
                    break;
                case 19: //sword upgrade
                    ROMFuncs.WriteToROM(0xC5CE00, (byte)0x4E);
                    break;
                case 20:
                    ROMFuncs.WriteToROM(0xC5CE00, (byte)0x4F);
                    break;
                case 22: //quiver upgrade
                    ROMFuncs.WriteToROM(0xC5CE6F, (byte)0x02);
                    break;
                case 23:
                    ROMFuncs.WriteToROM(0xC5CE6F, (byte)0x03);
                    break;
                case 24://bomb bag upgrade
                    ROMFuncs.WriteToROM(0xC5CE6F, (byte)0x10);
                    break;
                case 25:
                    ROMFuncs.WriteToROM(0xC5CE6F, (byte)0x18);
                    break;
                default:
                    break;
            };
        }

        private void WriteItems()
        {
            if (cMode.SelectedIndex == 2)
            {
                WriteFreeItem(MaskDeku);

                if (cCutsc.Checked)
                {
                    //giants cs were removed
                    WriteFreeItem(SongOath);
                };

                return;
            };

            //write free item
            int itemId = ItemList.FindIndex(u => u.ReplacesItemId == 0);
            WriteFreeItem(ItemList[itemId].ID);

            //write everything else
            ROMFuncs.ReplaceGetItemTable(ModsDirectory);
            ROMFuncs.InitItems();

            for (int itemIndex = 0; itemIndex < ItemList.Count; itemIndex++)
            {
                itemId = ItemList[itemIndex].ID;

                // Unused item
                if (ItemList[itemIndex].ReplacesItemId == -1)
                {
                    continue;
                };

                bool isRepeatable = REPEATABLE.Contains(itemId);
                bool isCycleRepeatable = CYCLE_REPEATABLE.Contains(itemId);
                int replacesItemId = ItemList[itemIndex].ReplacesItemId;

                if (itemId > AreaInvertedStoneTowerNew) {
                    // Subtract amount of entries describing areas and other
                    itemId -= Values.NumberOfAreasAndOther;
                };

                if (replacesItemId > AreaInvertedStoneTowerNew) {
                    // Subtract amount of entries describing areas and other
                    replacesItemId -= Values.NumberOfAreasAndOther;
                };

                if ((itemIndex >= BottleCatchFairy) 
                    && (itemIndex <= BottleCatchMushroom))
                {
                    ROMFuncs.WriteNewBottle(replacesItemId, itemId);
                }
                else
                {
                    ROMFuncs.WriteNewItem(replacesItemId, itemId, isRepeatable, isCycleRepeatable);
                };
            };

            if (Shops)
            {
                ROMFuncs.ApplyHack(ModsDirectory + "fix-shop-checks");
            };
        }

        private void WriteGossipQuotes()
        {
            if (cMode.SelectedIndex == 2)
            {
                return;
            };

            if (cGossip.Checked)
            {
                SeedRNG();
                ROMFuncs.WriteGossipMessage(GossipQuotes, RNG);
            };
        }

        private void WriteSpoilerLog()
        {
            if (cMode.SelectedIndex == 2)
            {
                return;
            };

            if (cSpoiler.Checked)
            {
                MakeSpoilerLog();
            };
        }

        private void WriteFileSelect()
        {
            if (cMode.SelectedIndex == 2)
            {
                return;
            };

            ROMFuncs.ApplyHack(ModsDirectory + "file-select");
            byte[] SkyboxDefault = new byte[] { 0x91, 0x78, 0x9B, 0x28, 0x00, 0x28 };
            List<int[]> Addrs = ROMFuncs.GetAddresses(AddrsDirectory + "skybox-init");
            Random R = new Random();
            int rot = R.Next(360);
            for (int i = 0; i < 2; i++)
            {
                Color c = Color.FromArgb(SkyboxDefault[i * 3], SkyboxDefault[i * 3 + 1], SkyboxDefault[i * 3 + 2]);
                float h = c.GetHue();
                h += rot;
                h %= 360f;
                c = ROMFuncs.FromAHSB(c.A, h, c.GetSaturation(), c.GetBrightness());
                SkyboxDefault[i * 3] = c.R;
                SkyboxDefault[i * 3 + 1] = c.G;
                SkyboxDefault[i * 3 + 2] = c.B;
            };
            for (int i = 0; i < 3; i++)
            {
                ROMFuncs.WriteROMAddr(Addrs[i], new byte[] { SkyboxDefault[i * 2], SkyboxDefault[i * 2 + 1] });
            };
            rot = R.Next(360);
            byte[] FSDefault = new byte[] { 0x64, 0x96, 0xFF, 0x96, 0xFF, 0xFF, 0x64, 0xFF, 0xFF };
            Addrs = ROMFuncs.GetAddresses(AddrsDirectory + "fs-colour");
            for (int i = 0; i < 3; i++)
            {
                Color c = Color.FromArgb(FSDefault[i * 3], FSDefault[i * 3 + 1], FSDefault[i * 3 + 2]);
                float h = c.GetHue();
                h += rot;
                h %= 360f;
                c = ROMFuncs.FromAHSB(c.A, h, c.GetSaturation(), c.GetBrightness());
                FSDefault[i * 3] = c.R;
                FSDefault[i * 3 + 1] = c.G;
                FSDefault[i * 3 + 2] = c.B;
            };
            for (int i = 0; i < 9; i++)
            {
                if (i < 6)
                {
                    ROMFuncs.WriteROMAddr(Addrs[i], new byte[] { 0x00, FSDefault[i]});
                }
                else
                {
                    ROMFuncs.WriteROMAddr(Addrs[i], new byte[] { FSDefault[i] });
                };
            };
        }

        private void WriteStartupStrings()
        {
            if (cMode.SelectedIndex == 2)
            {
                //ROMFuncs.ApplyHack(ModsDir + "postman-testing");
                return;
            };
            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            string ver = String.Format("v{0}.{1}", v.Major, v.Minor);
            string setting = tSString.Text;
            ROMFuncs.SetStrings(ModsDirectory + "logo-text", ver, setting);
        }

        private bool ValidateROM(string FileName)
        {
            bool res = false;
            BinaryReader ROM = new BinaryReader(File.Open(FileName, FileMode.Open));
            if (ROM.BaseStream.Length == 0x2000000)
            {
                res = ROMFuncs.CheckOldCRC(ROM);
            };
            ROM.Close();
            return res;
        }

        private void MakeROM(string InFile, string FileName)
        {
            // TODO this blocks the ui for a decent period of time. Run in new thread?
            BinaryReader OldROM = new BinaryReader(File.Open(InFile, FileMode.Open));
            ROMFuncs.ReadFileTable(OldROM);
            OldROM.Close();
            WriteAudioSeq();
            WriteLinkAppearance();
            if (cMode.SelectedIndex != 2)
            {
                ROMFuncs.ApplyHack(ModsDirectory + "title-screen");
                ROMFuncs.ApplyHack(ModsDirectory + "misc-changes");
                ROMFuncs.ApplyHack(ModsDirectory + "cm-cs");
                WriteFileSelect();
            };
            ROMFuncs.ApplyHack(ModsDirectory + "init-file");
            WriteQuickText();
            WriteCutscenes();
            WriteTatlColour();
            WriteDungeons();
            WriteGimmicks();
            WriteEnemies();
            WriteItems();
            WriteGossipQuotes();
            WriteStartupStrings();
            WriteSpoilerLog();
            byte[] ROM = ROMFuncs.BuildROM(FileName);
            if (Output_VC)
            {
                string VCFileName = saveWad.FileName;
                ROMFuncs.BuildVC(ROM, VCDirectory, VCFileName);
            };
        }

    }

}