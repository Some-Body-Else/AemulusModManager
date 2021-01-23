﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Diagnostics;
using AemulusModManager.Utilities.TblPatching;
using Newtonsoft.Json;

namespace AemulusModManager
{
    public static class tblPatch
    {
        private static string tblDir;

        private static byte[] SliceArray(byte[] source, int start, int end)
        {
            int length = end - start;
            byte[] dest = new byte[length];
            Array.Copy(source, start, dest, 0, length);
            return dest;
        }

        private static int Search(byte[] src, byte[] pattern)
        {
            int c = src.Length - pattern.Length + 1;
            int j;
            for (int i = 0; i < c; i++)
            {
                if (src[i] != pattern[0]) continue;
                for (j = pattern.Length - 1; j >= 1 && src[i + j] == pattern[j]; j--) ;
                if (j == 0) return i;
            }
            return -1;
        }

        
        private static void unpackTbls(string archive, string game)
        {
            if (game == "Persona 3 FES")
                return;
            PAKPackCMD($@"unpack ""{archive}"" ""{tblDir}""");
        }

        private static string exePath = @"Dependencies\PAKPack\PAKPack.exe";

        // Use PAKPack command
        private static void PAKPackCMD(string args)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.FileName = $"\"{exePath}\"";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.Arguments = args;
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();

                // Add this: wait until process does its work
                process.WaitForExit();
            }
        }

        private static void repackTbls(string tbl, string archive, string game)
        {
            string parent = null;
            if (game == "Persona 4 Golden")
                parent = "battle";
            else if (game == "Persona 5")
                parent = "table";
            else if (game == "Persona 3 FES")
                return;
            PAKPackCMD($@"replace ""{archive}"" {parent}/{Path.GetFileName(tbl)} ""{tbl}""");
        }

        private static string[] p4gTables = { "SKILL", "UNIT", "MSG", "PERSONA", "ENCOUNT", "EFFECT", "MODEL", "AICALC" };
        private static string[] p3fTables = { "SKILL", "SKILL_F", "UNIT", "UNIT_F", "MSG", "PERSONA", "PERSONA_F", "ENCOUNT", "ENCOUNT_F", "EFFECT", "MODEL", "AICALC", "AICALC_F" };
        private static string[] p5Tables = { "AICALC", "ELSAI", "ENCOUNT", "EXIST", "ITEM", "NAME", "PERSONA", "PLAYER", "SKILL", "TALKINFO", "UNIT", "VISUAL" };
        public static void Patch(List<string> ModList, string modDir, bool useCpk, string cpkLang, string game)
        {
            if (!File.Exists(exePath))
            {
                Console.WriteLine($"[ERROR] Couldn't find {exePath}. Please check if it was blocked by your anti-virus.");
                return;
            }
            Console.WriteLine("[INFO] Patching .tbl's...");
            // Check if init_free exists and return if not
            string archive = null;
            if (game == "Persona 4 Golden")
            {
                if (useCpk)
                    archive = $@"{Path.GetFileNameWithoutExtension(cpkLang)}\init_free.bin";
                else
                {
                    switch (cpkLang)
                    {
                        case "data_e.cpk":
                            archive = $@"data00004\init_free.bin";
                            break;
                        case "data.cpk":
                            archive = $@"data00001\init_free.bin";
                            break;
                        case "data_c.cpk":
                            archive = $@"data00006\init_free.bin";
                            break;
                        case "data_k.cpk":
                            archive = $@"data00005\init_free.bin";
                            break;
                        default:
                            archive = $@"data00004\init_free.bin";
                            break;
                    }
                }
            }
            else if (game == "Persona 5")
                archive = @"battle\table.pac";
            if (game != "Persona 3 FES")
            {
                if (!File.Exists($@"{modDir}\{archive}"))
                {
                    if (File.Exists($@"Original\{game}\{archive}"))
                    {
                        Directory.CreateDirectory($@"{modDir}\{Path.GetDirectoryName(archive)}");
                        File.Copy($@"Original\{game}\{archive}", $@"{modDir}\{archive}", true);
                        Console.WriteLine($"[INFO] Copied over {archive} from Original directory.");
                    }
                    else
                    {
                        Console.WriteLine($"[WARNING] {archive} not found in output directory or Original directory.");
                        return;
                    }
                }
            
                tblDir = $@"{modDir}\{Path.ChangeExtension(archive, null)}_tbls";
                // Unpack archive
                Console.WriteLine($"[INFO] Unpacking tbl's from {archive}...");
                unpackTbls($@"{modDir}\{archive}", game);
            }
            // Keep track of which tables are edited
            List<string> editedTables = new List<string>();
            List<NameSection> sections = null;
            // Load EnabledPatches in order
            foreach (string dir in ModList)
            {
                Console.WriteLine($"[INFO] Searching for/applying tblpatches in {dir}...");
                if (!Directory.Exists($@"{dir}\tblpatches"))
                {
                    Console.WriteLine($"[INFO] No tblpatches folder found in {dir}");
                    continue;
                }
                // Apply original tblpatch files
                foreach (var t in Directory.EnumerateFiles($@"{dir}\tblpatches", "*.tblpatch"))
                {
                    byte[] file = File.ReadAllBytes(t);
                    string fileName = Path.GetFileName(t);
                    Console.WriteLine($"[INFO] Loading {fileName}");
                    if (file.Length < 3)
                    {
                        Console.WriteLine("[ERROR] Improper .tblpatch format.");
                        continue;
                    }

                    // Name of tbl file
                    string tblName = Encoding.ASCII.GetString(SliceArray(file, 0, 3));

                    switch (tblName)
                    {
                        case "SKL":
                            tblName = "SKILL.TBL";
                            break;
                        case "UNT":
                            tblName = "UNIT.TBL";
                            break;
                        case "MSG":
                            tblName = "MSG.TBL";
                            if (game == "Persona 5")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "PSA":
                            tblName = "PERSONA.TBL";
                            break;
                        case "ENC":
                            tblName = "ENCOUNT.TBL";
                            break;
                        case "EFF":
                            tblName = "EFFECT.TBL";
                            if (game == "Persona 5")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "MDL":
                            tblName = "MODEL.TBL";
                            if (game == "Persona 5")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "AIC":
                            tblName = "AICALC.TBL";
                            break;
                        case "AIF":
                            tblName = "AICALC_F.TBL";
                            if (game != "Persona 3 FES")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "ENF":
                            tblName = "ENCOUNT_F.TBL";
                            if (game != "Persona 3 FES")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "PSF":
                            tblName = "PERSONA_F.TBL";
                            if (game != "Persona 3 FES")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "SKF":
                            tblName = "SKILL_F.TBL";
                            if (game != "Persona 3 FES")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "UNF":
                            tblName = "UNIT_F.TBL";
                            if (game != "Persona 3 FES")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "EAI":
                            tblName = "ELSAI.TBL";
                            if (game != "Persona 5")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "EXT":
                            tblName = "EXIST.TBL";
                            if (game != "Persona 5")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "ITM":
                            tblName = "ITEM.TBL";
                            if (game != "Persona 5")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "NME":
                            tblName = "NAME.TBL";
                            if (game != "Persona 5")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "PLY":
                            tblName = "PLAYER.TBL";
                            if (game != "Persona 5")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "TKI":
                            tblName = "TALKINFO.TBL";
                            if (game != "Persona 5")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "VSL":
                            tblName = "VISUAL.TBL";
                            if (game != "Persona 5")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        default:
                            Console.WriteLine($"[ERROR] Unknown tbl name for {t}.");
                            continue;
                    }

                    

                    // Keep track of which TBL's were edited
                    if (!editedTables.Contains(tblName))
                    {
                        editedTables.Add(tblName);
                        if (tblName == "NAME.TBL")
                            sections = GetNameSections($@"{tblDir}\table\{tblName}");
                    }

                    if (tblName != "NAME.TBL")
                    {
                        if (file.Length < 12)
                        {
                            Console.WriteLine("[ERROR] Improper .tblpatch format.");
                            continue;
                        }
                        // Offset to start overwriting at
                        byte[] byteOffset = SliceArray(file, 3, 11);
                        // Reverse endianess
                        Array.Reverse(byteOffset, 0, 8);
                        long offset = BitConverter.ToInt64(byteOffset, 0);
                        // Contents is what to replace
                        byte[] fileContents = SliceArray(file, 11, file.Length);

                        // TBL file to edit
                        if (game != "Persona 3 FES")
                        {
                            string unpackedTblPath = null;
                            if (game == "Persona 4 Golden")
                                unpackedTblPath = $@"{tblDir}\battle\{tblName}";
                            else
                                unpackedTblPath = $@"{tblDir}\table\{tblName}";
                            byte[] tblBytes = File.ReadAllBytes(unpackedTblPath);
                            fileContents.CopyTo(tblBytes, offset);
                            File.WriteAllBytes(unpackedTblPath, tblBytes);
                        }
                        else
                        {
                            if (!File.Exists($@"{modDir}\BTL\BATTLE\{tblName}"))
                            {
                                if (File.Exists($@"Original\{game}\BTL\BATTLE\{tblName}") && !File.Exists($@"{modDir}\BTL\BATTLE\{tblName}"))
                                {
                                    Directory.CreateDirectory($@"{modDir}\BTL\BATTLE");
                                    File.Copy($@"Original\{game}\BTL\BATTLE\{tblName}", $@"{modDir}\BTL\BATTLE\{tblName}", true);
                                    Console.WriteLine($"[INFO] Copied over {tblName} from Original directory.");
                                }
                                else if (!File.Exists($@"Original\{game}\BTL\BATTLE\{tblName}") && !File.Exists($@"{modDir}\BTL\BATTLE\{tblName}"))
                                {
                                    Console.WriteLine($"[WARNING] {tblName} not found in output directory or Original directory.");
                                    continue;
                                }
                            }
                            string tblPath = $@"{modDir}\BTL\BATTLE\{tblName}";
                            byte[] tblBytes = File.ReadAllBytes(tblPath);
                            fileContents.CopyTo(tblBytes, offset);
                            File.WriteAllBytes(tblPath, tblBytes);
                        }
                    }
                    else
                    {
                        if (file.Length < 6)
                        {
                            Console.WriteLine("[ERROR] Improper .tblpatch format.");
                            continue;
                        }
                        var temp = ReplaceName(sections, file, null);
                        if (temp != null)
                            sections = temp;
                    }
                }

                if (editedTables.Contains("NAME.TBL") && File.Exists($@"{tblDir}\table\NAME.TBL"))
                    WriteNameTbl(sections, $@"{tblDir}\table\NAME.TBL");
                
                List<Table> tables = new List<Table>();
                // Apply new tbp json patching
                foreach (var t in Directory.EnumerateFiles($@"{dir}\tblpatches", "*.tbp"))
                {
                    TablePatches tablePatches = JsonConvert.DeserializeObject<TablePatches>(File.ReadAllText(t));
                    if (tablePatches.Version != 1)
                    {
                        Console.WriteLine($"[ERROR] Invalid version for {t}, skipping...");
                        continue;
                    }
                    if (tablePatches.Patches != null)
                    {
                        foreach (var patch in tablePatches.Patches)
                        {
                            if (patch.tbl == "NAME")
                            {
                                Console.WriteLine($"[ERROR] NAME.TBL patches are formatted as NamePatches not Patches, skipping...");
                                continue;
                            }
                            // Keep track of which TBL's were edited and get sections
                            if (!tables.Exists(x => x.tableName == patch.tbl))
                            {
                                if ((game == "Persona 4 Golden" && !p4gTables.Contains(patch.tbl))
                                    || (game == "Persona 3 FES" && !p3fTables.Contains(patch.tbl))
                                    || (game == "Persona 5" && !p5Tables.Contains(patch.tbl)))
                                {
                                    Console.WriteLine($"[ERROR] {patch.tbl} doesn't exist in {game}, skipping...");
                                    continue;
                                }
                                Table table = new Table();
                                string tablePath = null;
                                if (game == "Persona 3 FES")
                                    tablePath = $@"{modDir}\BTL\BATTLE\{patch.tbl}.TBL";
                                else if (game == "Persona 4 Golden")
                                    tablePath = $@"{tblDir}\battle\{patch.tbl}.TBL";
                                else
                                    tablePath = $@"{tblDir}\table\{patch.tbl}.TBL";
                                table.sections = GetSections(tablePath, game);
                                table.tableName = patch.tbl;
                                tables.Add(table);
                            }
                            tables.Find(x => x.tableName == patch.tbl).sections = ReplaceSection(tables.Find(x => x.tableName == patch.tbl).sections, patch);
                        }
                    }
                    if (tablePatches.NamePatches != null && game == "Persona 5")
                    {
                        foreach (var namePatch in tablePatches.NamePatches)
                        {
                            if (!tables.Exists(x => x.tableName == "NAME"))
                            {
                                Table table = new Table();
                                string tablePath = $@"{tblDir}\table\NAME.TBL";
                                table.nameSections = GetNameSections(tablePath);
                                table.tableName = "NAME";
                                tables.Add(table);
                            }
                            
                            tables.Find(x => x.tableName == "NAME").nameSections = ReplaceName(tables.Find(x => x.tableName == "NAME").nameSections, null, namePatch);
                        }
                    }
                }
                foreach (var table in tables)
                {
                    // Keep track of which TBL's were edited
                    if (!editedTables.Contains($"{table.tableName}.TBL"))
                        editedTables.Add($"{table.tableName}.TBL");
                    string path = null;
                    if (game == "Persona 3 FES")
                        path = $@"{modDir}\BTL\BATTLE\{table.tableName}.TBL";
                    else if (game == "Persona 4 Golden")
                        path = $@"{tblDir}\battle\{table.tableName}.TBL";
                    else
                        path = $@"{tblDir}\table\{table.tableName}.TBL";
                    if (table.tableName == "NAME")
                        WriteNameTbl(table.nameSections, path);
                    else
                        WriteTbl(table.sections, path, game);
                }

                Console.WriteLine($"[INFO] Applied patches from {dir}");
                
            }
            
            if (game != "Persona 3 FES")
            {
                // Replace each edited TBL's
                foreach (string u in editedTables)
                {
                    Console.WriteLine($"[INFO] Replacing {u} in {archive}");
                    if (game == "Persona 5")
                        repackTbls($@"{tblDir}\table\{u}", $@"{modDir}\{archive}", game);
                    else
                        repackTbls($@"{tblDir}\battle\{u}", $@"{modDir}\{archive}", game);
                }

                Console.WriteLine($"[INFO] Deleting temp tbl folder...");
                // Delete all unpacked files
                Directory.Delete(tblDir, true);
            }
            Console.WriteLine("[INFO] Finished patching tbl's!");
        }

        private static List<Section> GetSections(string tbl, string game)
        {
            List<Section> sections = new List<Section>();
            bool bigEndian = false;
            if (game == "Persona 5")
                bigEndian = true;
            using (FileStream
            fileStream = new FileStream(tbl, FileMode.Open))
            {
                using (BinaryReader br = new BinaryReader(fileStream))
                {
                    while (br.BaseStream.Position < fileStream.Length)
                    {
                        Section section = new Section();
                        if (bigEndian)
                        {
                            var data = br.ReadBytes(4);
                            Array.Reverse(data);
                            section.size = BitConverter.ToInt32(data, 0);
                        }
                        else
                            section.size = br.ReadInt32();
                        section.data = br.ReadBytes(section.size);
                        if ((br.BaseStream.Position % 16) != 0)
                        {
                            br.BaseStream.Position += 16 - (br.BaseStream.Position % 16);
                        }
                        sections.Add(section);
                    }
                }
            }
            return sections;
        }

        // P5's NAME.TBL Expandable support
        private static List<NameSection> GetNameSections(string tbl)
        {
            List<NameSection> sections = new List<NameSection>();
            byte[] tblBytes = File.ReadAllBytes(tbl);
            int pos = 0;
            NameSection section;
            // 33 sections
            for (int i = 0; i <= 16; i++)
            {
                section = new NameSection();
                // Get big endian section size
                section.pointersSize = BitConverter.ToInt32(SliceArray(tblBytes, pos, pos + 4).Reverse().ToArray(), 0);

                // Get pointers
                byte[] segment = SliceArray(tblBytes, pos + 4, pos + 4 + section.pointersSize);
                section.pointers = new List<UInt16>();
                for (int j = 0; j < segment.Length; j += 2)
                {
                    section.pointers.Add(BitConverter.ToUInt16(SliceArray(segment, j, j + 2).Reverse().ToArray(), 0));
                }

                // Get to name section
                pos += section.pointersSize + 4;
                if ((pos % 16) != 0)
                {
                    pos += 16 - (pos % 16);
                }

                // Get big endian section size
                section.namesSize = BitConverter.ToInt32(SliceArray(tblBytes, pos, pos + 4).Reverse().ToArray(), 0);

                // Get names
                segment = SliceArray(tblBytes, pos + 4, pos + 4 + section.namesSize);
                section.names = new List<byte[]>();
                List<byte> name = new List<byte>();
                foreach (var segmentByte in segment)
                {
                    if (segmentByte == (byte)0)
                    {
                        section.names.Add(name.ToArray());
                        name = new List<byte>();
                    }
                    else
                    {
                        name.Add(segmentByte);
                    }

                }

                // Get to next section
                pos += section.namesSize + 4;
                if ((pos % 16) != 0)
                {
                    pos += 16 - (pos % 16);
                }
                sections.Add(section);
            }
            return sections;
        }

        private static List<NameSection> ReplaceName(List<NameSection> sections, byte[] patch, NamePatch namePatch)
        {
            int section = 0;
            int index = 0;
            byte[] fileContents = null;
            if (patch != null)
            {
                section = Convert.ToInt32(patch[3]);
                if (section >= sections.Count)
                {
                    Console.WriteLine($"[ERROR] Section chosen is out of range.");
                    return null;
                }
                index = BitConverter.ToInt16(SliceArray(patch, 4, 6).Reverse().ToArray(), 0);
                // Contents is what to replace
                fileContents = SliceArray(patch, 6, patch.Length);
            }
            else if (namePatch != null)
            {
                if (namePatch.section == null || namePatch.index == null || namePatch.name == null)
                {
                    Console.WriteLine($"[ERROR] Incomplete patch, skipping...");
                    return sections;
                }
                section = (int)namePatch.section;
                index = (int)namePatch.index;
                string[] stringData = namePatch.name.Split(' ');
                byte[] name = new byte[stringData.Length];
                for (int i = 0; i < name.Length; i++)
                {
                    try
                    {
                        name[i] = Convert.ToByte(stringData[i], 16);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Couldn't parse hex string ({ex.Message}), skipping...");
                        return sections;
                    }
                }
                fileContents = name;
            }
            else
            {
                Console.WriteLine($"[ERROR] No patch passed to replace function, skipping...");
                return sections;
            }

            if (section >= sections.Count)
            {
                Console.WriteLine($"[ERROR] Section chosen is out of bounds, skipping...");
                return sections;
            }

            if (index < 0)
            {
                Console.WriteLine($"[ERROR] Index cannot be negative, skipping...");
                return sections;
            }

            if (index >= sections[section].names.Count)
            {
                byte[] dummy = Encoding.ASCII.GetBytes("RESERVE");
                // Add RESERVE names if index is further down
                while (sections[section].names.Count < index)
                {
                    sections[section].pointers.Add((ushort)(sections[section].pointers.Last() + sections[section].names.Last().Length + 1));
                    sections[section].names.Add(dummy);
                    sections[section].pointersSize += 2;
                    sections[section].namesSize += dummy.Length + 1;
                }
                // Add expanded name
                sections[section].pointers.Add((ushort)(sections[section].pointers.Last() + sections[section].names.Last().Length + 1));
                sections[section].names.Add(fileContents);
                sections[section].pointersSize += 2;
                sections[section].namesSize += fileContents.Length + 1;
            }
            else
            {
                int delta = fileContents.Length - sections[section].names[index].Length;
                sections[section].names[index] = fileContents;
                sections[section].namesSize += delta;
                for (int i = index + 1; i < sections[section].pointers.Count; i++)
                {
                    sections[section].pointers[i] += (UInt16)delta;
                }
            }
            return sections;
        }

        private static List<Section> ReplaceSection(List<Section> sections, TablePatch patch)
        {
            if (patch.offset == null || patch.section == null || patch.data == null)
            {
                Console.WriteLine($"[ERROR] Incomplete patch, skipping...");
                return sections;
            }
            Console.WriteLine(patch.offset);
            // Get info from json patch
            int section = (int)patch.section;
            int offset = (int)patch.offset;
            string[] stringData = patch.data.Split(' ');
            byte[] data = new byte[stringData.Length];
            for (int i = 0; i < data.Length; i++)
            {
                try
                {
                    data[i] = Convert.ToByte(stringData[i], 16);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Couldn't parse hex string ({ex.Message}), skipping...");
                    return sections;
                }
            }
            if (offset < 0)
            {
                Console.WriteLine($"[ERROR] Offset cannot be negative, skipping...");
                return sections;
            }
            if (section >= sections.Count)
            {
                Console.WriteLine($"[ERROR] Section chosen is out of bounds, skipping...");
                return sections;
            }
            if (offset + data.Length >= sections[section].data.Length)
            {
                using (MemoryStream
                memoryStream = new MemoryStream())
                {
                    using (BinaryWriter bw = new BinaryWriter(memoryStream))
                    {
                        bw.Write(sections[section].data);
                        while (offset >= memoryStream.Length)
                            bw.Write((byte)0);
                        bw.BaseStream.Position = offset;
                        bw.Write(data);
                        sections[section].data = memoryStream.ToArray();
                        sections[section].size = sections[section].data.Length;
                    }
                }
            }
            else
            {
                using (MemoryStream
                memoryStream = new MemoryStream(sections[section].data))
                {
                    using (BinaryWriter bw = new BinaryWriter(memoryStream))
                    {
                        if (offset >= memoryStream.Length)
                        {
                            bw.BaseStream.Position = memoryStream.Length - 1;
                            while (offset >= memoryStream.Length)
                                bw.Write((byte)0);
                        }
                        bw.BaseStream.Position = offset;
                        bw.Write(data);
                    }
                }
            }

            return sections;
        }

        private static void WriteNameTbl(List<NameSection> sections, string path)
        {
            using (FileStream
            fileStream = new FileStream(path, FileMode.Create))
            {
                using (BinaryWriter bw = new BinaryWriter(fileStream))
                {
                    foreach (var section in sections)
                    {
                        // Write Pointer size
                        bw.Write(BitConverter.GetBytes(section.pointersSize).Reverse().ToArray());
                        // Write pointer section
                        foreach (var pointer in section.pointers)
                            bw.Write(BitConverter.GetBytes(pointer).Reverse().ToArray());
                        while (bw.BaseStream.Position % 16 != 0)
                            bw.Write((byte)0);
                        // Write names size
                        bw.Write(BitConverter.GetBytes(section.namesSize).Reverse().ToArray());
                        // Write names
                        foreach (var name in section.names)
                        {
                            bw.Write(name);
                            bw.Write((byte)0);
                        }
                        while (bw.BaseStream.Position % 16 != 0)
                            bw.Write((byte)0);
                    }
                }
            }
        }
        private static void WriteTbl(List<Section> sections, string path, string game)
        {
            bool bigEndian = false;
            if (game == "Persona 5")
                bigEndian = true;
            using (FileStream
            fileStream = new FileStream(path, FileMode.Create))
            {
                using (BinaryWriter bw = new BinaryWriter(fileStream))
                {
                    foreach (var section in sections)
                    {
                        // Write names size
                        if (bigEndian)
                            bw.Write(BitConverter.GetBytes(section.size).Reverse().ToArray());
                        else
                            bw.Write(BitConverter.GetBytes(section.size));
                        bw.Write(section.data);
                        while (bw.BaseStream.Position % 16 != 0)
                            bw.Write((byte)0);
                    }
                }
            }
        }
    }


}