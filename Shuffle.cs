using MMRando.Models;
using MMRando.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace MMRando
{

    public partial class MainRandomizerForm
    {

        Random RNG;

        private int[] _newEntrances = new int[] { -1, -1, -1, -1 };
        private int[] _newExits = new int[] { -1, -1, -1, -1 };
        private int[] _newDCFlags = new int[] { -1, -1, -1, -1 };
        private int[] _newDCMasks = new int[] { -1, -1, -1, -1 };
        private int[] _newEnts = new int[] { -1, -1, -1, -1 };
        private int[] _newExts = new int[] { -1, -1, -1, -1 };


        public class ItemObject
        {
            public int ID { get; set; }
            public List<int> DependsOnItems { get; set; } = new List<int>();
            public List<List<int>> Conditionals { get; set; } = new List<List<int>>();
            public List<int> CannotRequireItems { get; set; } = new List<int>();
            public int TimeNeeded { get; set; }
            public int TimeAvailable { get; set; }
            public int ReplacesItemId { get; set; } = -1;

            public bool ReplacesAnotherItem => ReplacesItemId != -1;
            public bool HasConditionals => Conditionals != null && Conditionals.Count > 0;
            public bool HasDependencies => DependsOnItems != null
                && DependsOnItems.Count > 0;
            public bool HasCannotRequireItems => CannotRequireItems != null
                && CannotRequireItems.Count > 0;
        }

        public class SequenceInfo
        {
            public string Name { get; set; }
            public int Replaces { get; set; } = -1;
            public int MM_seq { get; set; } = -1;
            public List<int> Type { get; set; } = new List<int>();
            public int Instrument { get; set; }
        }

        public class Gossip
        {
            public string[] SourceMessage { get; set; }
            public string[] DestinationMessage { get; set; }
        }

        List<ItemObject> ItemList { get; set; }
        List<SequenceInfo> SequenceList { get; set; }
        List<SequenceInfo> TargetSequences { get; set; }
        List<Gossip> GossipList { get; set; }

        List<int> ConditionsChecked { get; set; }
        Dictionary<int, Dependence> DependenceChecked { get; set; }
        List<int[]> ConditionRemoves { get; set; }
        List<string> GossipQuotes { get; set; }

        private class Dependence
        {
            public int[] ItemIds { get; set; }
            public DependenceType Type { get; set; }

            public static Dependence Dependent => new Dependence { Type = DependenceType.Dependent };
            public static Dependence NotDependent => new Dependence { Type = DependenceType.NotDependent };
            public static Dependence Circular(params int[] itemIds) => new Dependence { ItemIds = itemIds, Type = DependenceType.Circular };
        }

        private enum DependenceType
        {
            Dependent,
            NotDependent,
            Circular
        }

        Dictionary<int, List<int>> ForbiddenReplacedBy = new Dictionary<int, List<int>>
        {
            // Deku_Mask should not be replaced by trade items, or items that can be downgraded.
            {
                Items.MaskDeku, new List<int>
                {
                    Items.UpgradeGildedSword,
                    Items.UpgradeMirrorShield,
                    Items.UpgradeBiggestQuiver,
                    Items.UpgradeBigBombBag,
                    Items.UpgradeBiggestBombBag,
                    Items.UpgradeGiantWallet
                }
                .Concat(Enumerable.Range(Items.TradeItemMoonTear, Items.TradeItemMamaLetter - Items.TradeItemMoonTear + 1))
                .Concat(Enumerable.Range(Items.ItemBottleWitch, Items.ItemBottleMadameAroma - Items.ItemBottleWitch + 1))
                .ToList()
            },

            // Keaton_Mask and Mama_Letter are obtained one directly after another
            // Keaton_Mask cannot be replaced by items that may be overwritten by item obtained at Mama_Letter
            {
                Items.MaskKeaton,
                new List<int> {
                    Items.UpgradeGiantWallet,
                    Items.UpgradeGildedSword,
                    Items.UpgradeMirrorShield,
                    Items.UpgradeBiggestQuiver,
                    Items.UpgradeBigBombBag,
                    Items.UpgradeBiggestBombBag,
                    Items.TradeItemMoonTear,
                    Items.TradeItemLandDeed,
                    Items.TradeItemSwampDeed,
                    Items.TradeItemMountainDeed,
                    Items.TradeItemOceanDeed,
                    Items.TradeItemRoomKey,
                    Items.TradeItemMamaLetter,
                    Items.TradeItemKafeiLetter,
                    Items.TradeItemPendant
                }
            },
        };

        Dictionary<int, List<int>> ForbiddenPlacedAt = new Dictionary<int, List<int>>
        {
        };

        //rando functions

        #region Gossip quotes

        private void MakeGossipQuotes()
        {
            GossipQuotes = new List<string>();
            ReadAndPopulateGossipList();

            for (int itemIndex = 0; itemIndex < ItemList.Count; itemIndex++)
            {
                if (ItemList[itemIndex].ReplacesItemId == -1)
                {
                    continue;
                };

                // Skip hints for vanilla bottle content
                if ((!Settings.RandomizeBottleCatchContents)
                    && ItemUtils.IsBottleCatchContent(itemIndex))
                {
                    continue;
                };

                // Skip hints for vanilla shop items
                if ((!Settings.AddShopItems)
                    && ItemUtils.IsShopItem(itemIndex))
                {
                    continue;
                };

                // Skip hints for vanilla dungeon items
                if (!Settings.AddDungeonItems
                    && ItemUtils.IsDungeonItem(itemIndex))
                {
                    continue;
                };

                int sourceItemId = ItemList[itemIndex].ReplacesItemId;
                if (ItemUtils.IsItemDefinedPastAreas(sourceItemId))
                {
                    sourceItemId -= Values.NumberOfAreasAndOther;
                };

                int toItemId = itemIndex;
                if (ItemUtils.IsItemDefinedPastAreas(toItemId))
                {
                    toItemId -= Values.NumberOfAreasAndOther;
                };

                // 5% chance of being fake
                bool isFake = (RNG.Next(100) < 5);
                if (isFake)
                {
                    sourceItemId = RNG.Next(GossipList.Count);
                };

                int sourceMessageLength = GossipList[sourceItemId]
                    .SourceMessage
                    .Length;

                int destinationMessageLength = GossipList[toItemId]
                    .DestinationMessage
                    .Length;

                // Randomize messages
                string sourceMessage = GossipList[sourceItemId]
                    .SourceMessage[RNG.Next(sourceMessageLength)];

                string destinationMessage = GossipList[toItemId]
                    .DestinationMessage[RNG.Next(destinationMessageLength)];

                // Sound differs if hint is fake
                string soundAddress;
                if (isFake)
                {
                    soundAddress = "\x1E\x69\x0A";
                }
                else
                {
                    soundAddress = "\x1E\x69\x0C";
                };

                var quote = BuildGossipQuote(soundAddress, sourceMessage, destinationMessage);

                GossipQuotes.Add(quote);
            };

            for (int i = 0; i < Values.JunkGossipMessages.Count; i++)
            {
                GossipQuotes.Add(Values.JunkGossipMessages[i]);
            };
        }

        private void ReadAndPopulateGossipList()
        {
            GossipList = new List<Gossip>();

            string[] gossipLines = Properties.Resources.GOSSIP
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            for (int i = 0; i < gossipLines.Length; i += 2)
            {
                var sourceMessage = gossipLines[i].Split(';');
                var destinationMessage = gossipLines[i + 1].Split(';');
                var nextGossip = new Gossip
                {
                    SourceMessage = sourceMessage,
                    DestinationMessage = destinationMessage
                };

                GossipList.Add(nextGossip);
            };
        }

        public string BuildGossipQuote(string soundAddress, string sourceMessage, string destinationMessage)
        {
            int randomMessageStartIndex = RNG.Next(Values.GossipMessageStartSentences.Count);
            int randomMessageMidIndex = RNG.Next(Values.GossipMessageMidSentences.Count);

            var quote = new StringBuilder();

            quote.Append(soundAddress);
            quote.Append(Values.GossipMessageStartSentences[randomMessageStartIndex]);
            quote.Append("\x01" + sourceMessage + "\x00\x11");
            quote.Append(Values.GossipMessageMidSentences[randomMessageMidIndex]);
            quote.Append("\x06" + destinationMessage + "\x00" + "...\xBF");

            return quote.ToString();
        }

        #endregion

        private void EntranceShuffle()
        {
            _newEntrances = new int[] { -1, -1, -1, -1 };
            _newExits = new int[] { -1, -1, -1, -1 };
            _newDCFlags = new int[] { -1, -1, -1, -1 };
            _newDCMasks = new int[] { -1, -1, -1, -1 };
            _newEnts = new int[] { -1, -1, -1, -1 };
            _newExts = new int[] { -1, -1, -1, -1 };

            for (int i = 0; i < 4; i++)
            {
                int n;
                do
                {
                    n = RNG.Next(4);
                } while (_newEnts.Contains(n));

                _newEnts[i] = n;
                _newExts[n] = i;
            };

            ItemObject[] DE = new ItemObject[] {
                ItemList[Items.AreaWoodFallTempleAccess],
                ItemList[Items.AreaSnowheadTempleAccess],
                ItemList[Items.AreaInvertedStoneTowerTempleAccess],
                ItemList[Items.AreaGreatBayTempleAccess]
            };

            int[] DI = new int[] {
                Items.AreaWoodFallTempleAccess,
                Items.AreaSnowheadTempleAccess,
                Items.AreaInvertedStoneTowerTempleAccess,
                Items.AreaGreatBayTempleAccess
            };

            for (int i = 0; i < 4; i++)
            {
                Debug.WriteLine($"Entrance {DI[_newEnts[i]]} placed at {DE[i].ID}.");
                ItemList[DI[_newEnts[i]]] = DE[i];
            };

            DE = new ItemObject[] {
                ItemList[Items.AreaWoodFallTempleClear],
                ItemList[Items.AreaSnowheadTempleClear],
                ItemList[Items.AreaStoneTowerClear],
                ItemList[Items.AreaGreatBayTempleClear]
            };

            DI = new int[] {
                Items.AreaWoodFallTempleClear,
                Items.AreaSnowheadTempleClear,
                Items.AreaStoneTowerClear,
                Items.AreaGreatBayTempleClear
            };

            for (int i = 0; i < 4; i++)
            {
                ItemList[DI[i]] = DE[_newEnts[i]];
            };

            for (int i = 0; i < 4; i++)
            {
                _newEntrances[i] = Values.OldEntrances[_newEnts[i]];
                _newExits[i] = Values.OldExits[_newExts[i]];
                _newDCFlags[i] = Values.OldDCFlags[_newExts[i]];
                _newDCMasks[i] = Values.OldMaskFlags[_newExts[i]];
            };
        }

        #region Sequences and BGM

        private void ReadSeqInfo()
        {
            SequenceList = new List<SequenceInfo>();
            TargetSequences = new List<SequenceInfo>();

            string[] lines = Properties.Resources.SEQS
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            int i = 0;
            while (i < lines.Length)
            {
                var sourceName = lines[i];
                var sourceType = Array.ConvertAll(lines[i + 1].Split(','), int.Parse).ToList();
                var sourceInstrument = Convert.ToInt32(lines[i + 2], 16);

                var targetName = lines[i];
                var targetType = Array.ConvertAll(lines[i + 1].Split(','), int.Parse).ToList();
                var targetInstrument = Convert.ToInt32(lines[i + 2], 16);

                SequenceInfo sourceSequence = new SequenceInfo
                {
                    Name = sourceName,
                    Type = sourceType,
                    Instrument = sourceInstrument
                };

                SequenceInfo targetSequence = new SequenceInfo
                {
                    Name = targetName,
                    Type = targetType,
                    Instrument = targetInstrument
                };

                if (sourceSequence.Name.StartsWith("mm-"))
                {
                    targetSequence.Replaces = Convert.ToInt32(lines[i + 3], 16);
                    sourceSequence.MM_seq = Convert.ToInt32(lines[i + 3], 16);
                    TargetSequences.Add(targetSequence);
                    i += 4;
                }
                else
                {
                    if (sourceSequence.Name == "mmr-f-sot")
                    {
                        sourceSequence.Replaces = 0x33;
                    };

                    i += 3;
                };

                if (sourceSequence.MM_seq != 0x18)
                {
                    SequenceList.Add(sourceSequence);
                };
            };
        }

        private void BGMShuffle()
        {
            while (TargetSequences.Count > 0)
            {
                List<SequenceInfo> Unassigned = SequenceList.FindAll(u => u.Replaces == -1);

                int targetIndex = RNG.Next(TargetSequences.Count);
                while (true)
                {
                    int unassignedIndex = RNG.Next(Unassigned.Count);

                    if (Unassigned[unassignedIndex].Name.StartsWith("mm")
                        & (RNG.Next(100) < 50))
                    {
                        continue;
                    };

                    for (int i = 0; i < Unassigned[unassignedIndex].Type.Count; i++)
                    {
                        if (TargetSequences[targetIndex].Type.Contains(Unassigned[unassignedIndex].Type[i]))
                        {
                            Unassigned[unassignedIndex].Replaces = TargetSequences[targetIndex].Replaces;
                            Debug.WriteLine(Unassigned[unassignedIndex].Name + " -> " + TargetSequences[targetIndex].Name);
                            TargetSequences.RemoveAt(targetIndex);
                            break;
                        }
                        else if (i + 1 == Unassigned[unassignedIndex].Type.Count)
                        {
                            if ((RNG.Next(30) == 0)
                                && ((Unassigned[unassignedIndex].Type[0] & 8) == (TargetSequences[targetIndex].Type[0] & 8))
                                && (Unassigned[unassignedIndex].Type.Contains(10) == TargetSequences[targetIndex].Type.Contains(10))
                                && (!Unassigned[unassignedIndex].Type.Contains(16)))
                            {
                                Unassigned[unassignedIndex].Replaces = TargetSequences[targetIndex].Replaces;
                                Debug.WriteLine(Unassigned[unassignedIndex].Name + " -> " + TargetSequences[targetIndex].Name);
                                TargetSequences.RemoveAt(targetIndex);
                                break;
                            };
                        };
                    };

                    if (Unassigned[unassignedIndex].Replaces != -1)
                    {
                        break;
                    };
                };
            };

            SequenceList.RemoveAll(u => u.Replaces == -1);
        }

        private void SortBGM()
        {
            if (!Settings.RandomizeBGM)
            {
                return;
            };
            ReadSeqInfo();
            BGMShuffle();
        }

        #endregion

        private void SetTatlColour()
        {
            if (Settings.TatlColorSchema == TatlColorSchema.Rainbow)
            {
                for (int i = 0; i < 10; i++)
                {
                    byte[] c = new byte[4];
                    RNG.NextBytes(c);

                    if ((i % 2) == 0)
                    {
                        c[0] = 0xFF;
                    }
                    else
                    {
                        c[0] = 0;
                    };

                    Values.TatlColours[4, i] = BitConverter.ToUInt32(c, 0);
                };
            };
        }

        private void CreateTextSpoilerLog(Spoiler spoiler, string path)
        {
            StringBuilder log = new StringBuilder();
            log.AppendLine($"{"Version:",-17} {spoiler.Version}");
            log.AppendLine($"{"Settings String:",-17} {spoiler.SettingsString}");
            log.AppendLine($"{"Seed:",-17} {spoiler.Seed}");
            log.AppendLine();

            if (spoiler.RandomizeDungeonEntrances)
            {
                log.AppendLine($" {"Entrance",-21}    {"Destination"}");
                log.AppendLine();
                string[] destinations = new string[] { "Woodfall", "Snowhead", "Inverted Stone Tower", "Great Bay" };
                for (int i = 0; i < 4; i++)
                {
                    log.AppendLine($"{destinations[i],-21} >> {destinations[_newEnts[i]]}");
                }
                log.AppendLine("");
            }

            log.AppendLine($" {"Item",-40}    {"Location"}");
            foreach (var item in spoiler.ItemList)
            {
                string name = Items.ITEM_NAMES[item.ID];
                string replaces = Items.ITEM_NAMES[item.ReplacesItemId];
                log.AppendLine($"{name,-40} >> {replaces}");
            }

            log.AppendLine();
            log.AppendLine();

            log.AppendLine($" {"Item",-40}    {"Location"}");
            foreach (var item in spoiler.ItemList)
            {
                string replaces = Items.ITEM_NAMES[item.ReplacesItemId];
                string name = Items.ITEM_NAMES[item.ID];
                log.AppendLine($"{replaces,-40} >> {name}");
            }

            using (StreamWriter sw = new StreamWriter(path))
            {
                sw.Write(log.ToString());
            }
        }


        private void PrepareRulesetItemData()
        {
            ItemList = new List<ItemObject>();

            if (Settings.LogicMode == LogicMode.Casual
                || Settings.LogicMode == LogicMode.Glitched
                || Settings.LogicMode == LogicMode.UserLogic)
            {
                string[] data = ReadRulesetFromResources();
                PopulateItemListFromLogicData(data);
            }
            else
            {
                PopulateItemListWithoutLogic();
            }

            AddRequirementsForSongOath();
        }

        private void AddRequirementsForSongOath()
        {
            int[] OathReq = new int[] { 100, 103, 108, 113 };
            ItemList[Items.SongOath].DependsOnItems = new List<int>();
            ItemList[Items.SongOath].DependsOnItems.Add(OathReq[RNG.Next(4)]);
        }

        /// <summary>
        /// Populates item list without logic. Default TimeAvailable = 63
        /// </summary>
        private void PopulateItemListWithoutLogic()
        {
            for (var i = 0; i < Items.TotalNumberOfItems; i++)
            {
                var currentItem = new ItemObject
                {
                    ID = i,
                    TimeAvailable = 63
                };

                ItemList.Add(currentItem);
            }
        }

        /// <summary>
        /// Populates the item list using the lines from a logic file, processes them 4 lines per item. 
        /// </summary>
        /// <param name="data">The lines from a logic file</param>
        private void PopulateItemListFromLogicData(string[] data)
        {
            int itemId = 0;
            int lineNumber = 0;

            var currentItem = new ItemObject();

            // Process lines in groups of 4
            foreach (string line in data)
            {
                if (line.Contains("-"))
                {
                    continue;
                }

                switch (lineNumber)
                {
                    case 0:
                        //dependence
                        ProcessDependenciesForItem(currentItem, line);
                        break;
                    case 1:
                        //conditionals
                        ProcessConditionalsForItem(currentItem, line);
                        break;
                    case 2:
                        //time needed
                        currentItem.TimeNeeded = Convert.ToInt32(line);
                        break;
                    case 3:
                        //time available
                        currentItem.TimeAvailable = Convert.ToInt32(line);
                        if (currentItem.TimeAvailable == 0)
                        {
                            currentItem.TimeAvailable = 63;
                        };
                        break;
                };

                lineNumber++;

                if (lineNumber == 4)
                {
                    currentItem.ID = itemId;
                    ItemList.Add(currentItem);

                    currentItem = new ItemObject();

                    itemId++;
                    lineNumber = 0;
                }
            }
        }

        private void ProcessConditionalsForItem(ItemObject currentItem, string line)
        {
            List<List<int>> conditional = new List<List<int>>();

            if (line == "")
            {
                currentItem.Conditionals = null;
            }
            else
            {
                foreach (string conditions in line.Split(';'))
                {
                    int[] conditionaloption = Array.ConvertAll(conditions.Split(','), int.Parse);
                    conditional.Add(conditionaloption.ToList());
                };
                currentItem.Conditionals = conditional;
            };
        }

        private void ProcessDependenciesForItem(ItemObject currentItem, string line)
        {
            List<int> dependencies = new List<int>();

            if (line == "")
            {
                currentItem.DependsOnItems = null;
            }
            else
            {
                foreach (string dependency in line.Split(','))
                {
                    dependencies.Add(Convert.ToInt32(dependency));
                };
                currentItem.DependsOnItems = dependencies;
            };
        }

        private string[] ReadRulesetFromResources()
        {
            string[] lines = null;
            var mode = Settings.LogicMode;

            if (mode == LogicMode.Casual)
            {
                lines = Properties.Resources.REQ_CASUAL.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            }
            else if (mode == LogicMode.Glitched)
            {
                lines = Properties.Resources.REQ_GLITCH.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            }
            else if (mode == LogicMode.UserLogic)
            {
                using (StreamReader Req = new StreamReader(File.Open(openLogic.FileName, FileMode.Open)))
                {
                    lines = Req.ReadToEnd().Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                }
            }

            return lines;
        }

        private Dependence CheckDependence(int CurrentItem, int Target, List<int> dependencyPath)
        {
            Debug.WriteLine($"CheckDependence({CurrentItem}, {Target})");
            if (ItemList[CurrentItem].TimeNeeded == 0
                && !ItemList.Any(io => (io.Conditionals?.Any(c => c.Contains(CurrentItem)) ?? false) || (io.DependsOnItems?.Contains(CurrentItem) ?? false)))
            {
                return Dependence.NotDependent;
            }

            // permanent items ignore dependencies of Blast Mask check
            if (Target == Items.MaskBlast && !ItemUtils.IsTemporaryItem(CurrentItem))
            {
                return Dependence.NotDependent;
            }

            //check timing
            if (ItemList[CurrentItem].TimeNeeded != 0 && dependencyPath.Skip(1).All(p => ItemUtils.IsFakeItem(p) || ItemUtils.IsTemporaryItem(ItemList.Single(i => i.ReplacesItemId == p).ID)))
            {
                if ((ItemList[CurrentItem].TimeNeeded & ItemList[Target].TimeAvailable) == 0)
                {
                    Debug.WriteLine($"{CurrentItem} is needed at {ItemList[CurrentItem].TimeNeeded} but {Target} is only available at {ItemList[Target].TimeAvailable}");
                    return Dependence.Dependent;
                }
            }

            if (ItemList[Target].HasConditionals)
            {
                if (ItemList[Target].Conditionals
                    .FindAll(u => u.Contains(CurrentItem)).Count == ItemList[Target].Conditionals.Count)
                {
                    Debug.WriteLine($"All conditionals of {Target} contains {CurrentItem}");
                    return Dependence.Dependent;
                }

                if (ItemList[CurrentItem].HasCannotRequireItems)
                {
                    for (int i = 0; i < ItemList[CurrentItem].CannotRequireItems.Count; i++)
                    {
                        if (ItemList[Target].Conditionals
                            .FindAll(u => u.Contains(ItemList[CurrentItem].CannotRequireItems[i])
                            || u.Contains(CurrentItem)).Count == ItemList[Target].Conditionals.Count)
                        {
                            Debug.WriteLine($"All conditionals of {Target} cannot be required by {CurrentItem}");
                            return Dependence.Dependent;
                        }
                    }
                }

                int k = 0;
                var circularDependencies = new List<int>();
                for (int i = 0; i < ItemList[Target].Conditionals.Count; i++)
                {
                    bool match = false;
                    for (int j = 0; j < ItemList[Target].Conditionals[i].Count; j++)
                    {
                        int d = ItemList[Target].Conditionals[i][j];
                        if (!ItemUtils.IsFakeItem(d) && !ItemList[d].ReplacesAnotherItem && d != CurrentItem)
                        {
                            continue;
                        }

                        int[] check = new int[] { Target, i, j };

                        if (ItemList[d].ReplacesAnotherItem)
                        {
                            d = ItemList[d].ReplacesItemId;
                        }
                        if (d == CurrentItem)
                        {
                            DependenceChecked[d] = Dependence.Dependent;
                        }
                        else
                        {
                            if (dependencyPath.Contains(d))
                            {
                                DependenceChecked[d] = Dependence.Circular(d);
                            }
                            if (!DependenceChecked.ContainsKey(d) || (DependenceChecked[d].Type == DependenceType.Circular && !DependenceChecked[d].ItemIds.All(id => dependencyPath.Contains(id))))
                            {
                                var childPath = dependencyPath.ToList();
                                childPath.Add(d);
                                DependenceChecked[d] = CheckDependence(CurrentItem, d, childPath);
                            }
                        }

                        if (DependenceChecked[d].Type != DependenceType.NotDependent)
                        {
                            if (!dependencyPath.Contains(d) && DependenceChecked[d].Type == DependenceType.Circular && DependenceChecked[d].ItemIds.All(id => id == d))
                            {
                                DependenceChecked[d] = Dependence.Dependent;
                            }
                            if (DependenceChecked[d].Type == DependenceType.Dependent)
                            {
                                if (!ConditionRemoves.Any(c => c.SequenceEqual(check)))
                                {
                                    ConditionRemoves.Add(check);
                                }
                            }
                            else
                            {
                                circularDependencies = circularDependencies.Union(DependenceChecked[d].ItemIds).ToList();
                            }
                            if (!match)
                            {
                                k++;
                                match = true;
                            }
                        }
                    }
                }

                if (k == ItemList[Target].Conditionals.Count)
                {
                    if (circularDependencies.Any())
                    {
                        return Dependence.Circular(circularDependencies.ToArray());
                    }
                    Debug.WriteLine($"All conditionals of {Target} failed dependency check for {CurrentItem}.");
                    return Dependence.Dependent;
                }
            }

            if (ItemList[Target].DependsOnItems == null)
            {
                return Dependence.NotDependent;
            }

            //cycle through all things
            for (int i = 0; i < ItemList[Target].DependsOnItems.Count; i++)
            {
                int dependency = ItemList[Target].DependsOnItems[i];
                if (dependency == CurrentItem)
                {
                    Debug.WriteLine($"{Target} has direct dependence on {CurrentItem}");
                    return Dependence.Dependent;
                }

                if (ItemList[CurrentItem].HasCannotRequireItems)
                {
                    for (int j = 0; j < ItemList[CurrentItem].CannotRequireItems.Count; j++)
                    {
                        if (ItemList[Target].DependsOnItems.Contains(ItemList[CurrentItem].CannotRequireItems[j]))
                        {
                            Debug.WriteLine($"Dependence {ItemList[CurrentItem].CannotRequireItems[j]} of {Target} cannot be required by {CurrentItem}");
                            return Dependence.Dependent;
                        }
                    }
                }

                if (ItemUtils.IsFakeItem(dependency)
                    || ItemList[dependency].ReplacesAnotherItem)
                {
                    if (ItemList[dependency].ReplacesAnotherItem)
                    {
                        dependency = ItemList[dependency].ReplacesItemId;
                    }

                    if (dependencyPath.Contains(dependency))
                    {
                        DependenceChecked[dependency] = Dependence.Circular(dependency);
                        return DependenceChecked[dependency];
                    }
                    if (!DependenceChecked.ContainsKey(dependency) || (DependenceChecked[dependency].Type == DependenceType.Circular && !DependenceChecked[dependency].ItemIds.All(id => dependencyPath.Contains(id))))
                    {
                        var childPath = dependencyPath.ToList();
                        childPath.Add(dependency);
                        DependenceChecked[dependency] = CheckDependence(CurrentItem, dependency, childPath);
                    }
                    if (DependenceChecked[dependency].Type != DependenceType.NotDependent)
                    {
                        if (DependenceChecked[dependency].Type == DependenceType.Circular && DependenceChecked[dependency].ItemIds.All(id => id == dependency))
                        {
                            DependenceChecked[dependency] = Dependence.Dependent;
                        }
                        Debug.WriteLine($"{CurrentItem} is dependent on {dependency}");
                        return DependenceChecked[dependency];
                    }
                }
            }

            return Dependence.NotDependent;
        }

        private void RemoveConditionals(int CurrentItem)
        {
            for (int i = 0; i < ConditionRemoves.Count; i++)
            {
                int x = ConditionRemoves[i][0];
                int y = ConditionRemoves[i][1];
                int z = ConditionRemoves[i][2];
                ItemList[x].Conditionals[y] = null;
            }

            for (int i = 0; i < ConditionRemoves.Count; i++)
            {
                int x = ConditionRemoves[i][0];
                int y = ConditionRemoves[i][1];
                int z = ConditionRemoves[i][2];

                for (int j = 0; j < ItemList[x].Conditionals.Count; j++)
                {
                    if (ItemList[x].Conditionals[j] != null)
                    {
                        for (int k = 0; k < ItemList[x].Conditionals[j].Count; k++)
                        {
                            int d = ItemList[x].Conditionals[j][k];

                            if (!ItemList[x].HasCannotRequireItems)
                            {
                                ItemList[x].CannotRequireItems = new List<int>();
                            }
                            if (!ItemList[d].CannotRequireItems.Contains(CurrentItem))
                            {
                                ItemList[d].CannotRequireItems.Add(CurrentItem);
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < ItemList.Count; i++)
            {
                if (ItemList[i].Conditionals != null)
                {
                    ItemList[i].Conditionals.RemoveAll(u => u == null);
                }
            }

            /*
            for (int i = 0; i < ConditionRemoves.Count; i++)
            {
                for (int j = 0; j < ItemList[ConditionRemoves[i][0]].Conditional[ConditionRemoves[i][1]].Count; j++)
                {
                    int d = ItemList[ConditionRemoves[i][0]].Conditional[ConditionRemoves[i][1]][j];
                    if (ItemList[d].Cannot_Require == null)
                    {
                        ItemList[d].Cannot_Require = new List<int>();
                    };
                    ItemList[d].Cannot_Require.Add(CurrentItem);
                    if (ItemList[ConditionRemoves[i][0]].Dependence == null)
                    {
                        ItemList[ConditionRemoves[i][0]].Dependence = new List<int>();
                    };
                    ItemList[ConditionRemoves[i][0]].Dependence.Add(d);
                };
                ItemList[ConditionRemoves[i][0]].Conditional[ConditionRemoves[i][1]] = null;
            };
            for (int i = 0; i < ItemList.Count; i++)
            {
                if (ItemList[i].Conditional != null)
                {
                    if (ItemList[i].Conditional.Contains(null))
                    {
                        ItemList[i].Conditional = null;
                    };
                };
            };
            */
        }

        private void UpdateConditionals(int CurrentItem, int Target)
        {
            if (!ItemList[Target].HasConditionals)
            {
                return;
            }

            //if ((Target == 114) || (Target == 115))
            //{
            //    return;
            //};
            /*
            if (ItemList[Target].Cannot_Require != null)
            {
                for (int i = 0; i < ItemList[CurrentItem].Cannot_Require.Count; i++)
                {
                    ItemList[Target].Conditional.RemoveAll(u => u.Contains(ItemList[CurrentItem].Cannot_Require[i]));
                };
            };
            ItemList[Target].Conditional.RemoveAll(u => u.Contains(CurrentItem));
            if (ItemList[Target].Conditional.Count == 0)
            {
                return;
            };
            */
            if (ItemList[Target].Conditionals.Count == 1)
            {
                for (int i = 0; i < ItemList[Target].Conditionals[0].Count; i++)
                {
                    if (!ItemList[Target].HasDependencies)
                    {
                        ItemList[Target].DependsOnItems = new List<int>();
                    }

                    int j = ItemList[Target].Conditionals[0][i];
                    if (!ItemList[Target].DependsOnItems.Contains(j))
                    {
                        ItemList[Target].DependsOnItems.Add(j);
                    }
                    if (!ItemList[j].HasCannotRequireItems)
                    {
                        ItemList[j].CannotRequireItems = new List<int>();
                    }
                    if (!ItemList[j].CannotRequireItems.Contains(CurrentItem))
                    {
                        ItemList[j].CannotRequireItems.Add(CurrentItem);
                    }
                }
                ItemList[Target].Conditionals.RemoveAt(0);
            }
            else
            {
                //check if all conditions have a common item
                for (int i = 0; i < ItemList[Target].Conditionals[0].Count; i++)
                {
                    int testitem = ItemList[Target].Conditionals[0][i];
                    if (ItemList[Target].Conditionals.FindAll(u => u.Contains(testitem)).Count == ItemList[Target].Conditionals.Count)
                    {
                        // require this item and remove from conditions
                        if (!ItemList[Target].HasDependencies)
                        {
                            ItemList[Target].DependsOnItems = new List<int>();
                        }
                        if (!ItemList[Target].DependsOnItems.Contains(testitem))
                        {
                            ItemList[Target].DependsOnItems.Add(testitem);
                        }
                        for (int j = 0; j < ItemList[Target].Conditionals.Count; j++)
                        {
                            ItemList[Target].Conditionals[j].Remove(testitem);
                        }

                        break;
                    }
                }
                //for (int i = 0; i < ItemList[Target].Conditional.Count; i++)
                //{
                //    for (int j = 0; j < ItemList[Target].Conditional[i].Count; j++)
                //    {
                //        int k = ItemList[Target].Conditional[i][j];
                //        if (ItemList[k].Cannot_Require == null)
                //        {
                //            ItemList[k].Cannot_Require = new List<int>();
                //        };
                //        ItemList[k].Cannot_Require.Add(CurrentItem);
                //    };
                //};
            };
        }

        private void AddConditionals(int target, int currentItem, int d)
        {
            List<List<int>> baseConditionals = ItemList[target].Conditionals;

            if (baseConditionals == null)
            {
                baseConditionals = new List<List<int>>();
            }

            ItemList[target].Conditionals = new List<List<int>>();
            foreach (List<int> conditions in ItemList[d].Conditionals)
            {
                if (!conditions.Contains(currentItem))
                {
                    List<List<int>> newConditional = new List<List<int>>();
                    if (baseConditionals.Count == 0)
                    {
                        newConditional.Add(conditions);
                    }
                    else
                    {
                        foreach (List<int> baseConditions in baseConditionals)
                        {
                            newConditional.Add(baseConditions.Concat(conditions).ToList());
                        }
                    }

                    ItemList[target].Conditionals.AddRange(newConditional);
                }
            }
        }

        private void CheckConditionals(int currentItem, int target, List<int> dependencyPath)
        {
            if (target == Items.MaskBlast)
            {
                if (!ItemUtils.IsTemporaryItem(currentItem))
                {
                    ItemList[target].DependsOnItems = null;
                }
            }

            ConditionsChecked.Add(target);
            UpdateConditionals(currentItem, target);

            if (!ItemList[target].HasDependencies)
            {
                return;
            }

            for (int i = 0; i < ItemList[target].DependsOnItems.Count; i++)
            {
                int dependency = ItemList[target].DependsOnItems[i];
                if (!ItemList[dependency].HasCannotRequireItems)
                {
                    ItemList[dependency].CannotRequireItems = new List<int>();
                }
                if (!ItemList[dependency].CannotRequireItems.Contains(currentItem))
                {
                    ItemList[dependency].CannotRequireItems.Add(currentItem);
                }
                if (ItemUtils.IsFakeItem(dependency) || ItemList[dependency].ReplacesAnotherItem)
                {
                    if (ItemList[dependency].ReplacesAnotherItem)
                    {
                        dependency = ItemList[dependency].ReplacesItemId;
                    }

                    if (!ConditionsChecked.Contains(dependency))
                    {
                        var childPath = dependencyPath.ToList();
                        childPath.Add(dependency);
                        CheckConditionals(currentItem, dependency, childPath);
                    }
                }
                else if (ItemList[currentItem].TimeNeeded != 0 && ItemUtils.IsTemporaryItem(dependency) && dependencyPath.Skip(1).All(p => ItemUtils.IsFakeItem(p) || ItemUtils.IsTemporaryItem(ItemList.Single(j => j.ReplacesItemId == p).ID)))
                {
                    ItemList[dependency].TimeNeeded &= ItemList[currentItem].TimeNeeded;
                }
            }

            ItemList[target].DependsOnItems.RemoveAll(u => u == -1);
        }

        private bool CheckMatch(int currentItem, int target)
        {
            if (ForbiddenPlacedAt.ContainsKey(currentItem)
                && ForbiddenPlacedAt[currentItem].Contains(target))
            {
                Debug.WriteLine($"{currentItem} forbidden from being placed at {target}");
                return false;
            }

            if (ForbiddenReplacedBy.ContainsKey(target) && ForbiddenReplacedBy[target].Contains(currentItem))
            {
                Debug.WriteLine($"{target} forbids being replaced by {currentItem}");
                return false;
            }

            //check direct dependence
            ConditionRemoves = new List<int[]>();
            DependenceChecked = new Dictionary<int, Dependence> { { target, new Dependence { Type = DependenceType.Dependent } } };
            var dependencyPath = new List<int> { target };

            if (CheckDependence(currentItem, target, dependencyPath).Type != DependenceType.NotDependent)
            {
                return false;
            }

            //check conditional dependence
            RemoveConditionals(currentItem);
            ConditionsChecked = new List<int>();
            CheckConditionals(currentItem, target, dependencyPath);
            return true;
        }

        private void PlaceItem(int currentItem, List<int> targets)
        {
            if (ItemList[currentItem].ReplacesAnotherItem)
            {
                return;
            }

            var availableItems = targets.ToList();

            while (true)
            {
                if (availableItems.Count == 0)
                {
                    throw new Exception($"Unable to place {Items.ITEM_NAMES[currentItem]} anywhere.");
                }

                int targetItem = 0;
                if (currentItem > Items.SongOath && availableItems.Contains(0))
                {
                    targetItem = RNG.Next(1, availableItems.Count);
                }
                else
                {
                    targetItem = RNG.Next(availableItems.Count);
                }

                Debug.WriteLine($"----Attempting to place {Items.ITEM_NAMES[currentItem]} at {Items.ITEM_NAMES[availableItems[targetItem]]}.---");

                if (CheckMatch(currentItem, availableItems[targetItem]))
                {
                    ItemList[currentItem].ReplacesItemId = availableItems[targetItem];

                    Debug.WriteLine($"----Placed {Items.ITEM_NAMES[currentItem]} at {Items.ITEM_NAMES[ItemList[currentItem].ReplacesItemId]}----");

                    targets.Remove(availableItems[targetItem]);
                    return;
                }
                else
                {
                    Debug.WriteLine($"----Failed to place {Items.ITEM_NAMES[currentItem]} at {Items.ITEM_NAMES[availableItems[targetItem]]}----");
                    availableItems.RemoveAt(targetItem);
                }
            }
        }

        private void ItemShuffle()
        {
            if (Settings.UseCustomItemList)
            {
                SetupCustomItems();
            }
            else
            {
                Setup();
            }

            var itemPool = new List<int>();
            AddAllItems(itemPool);

            // Hard-code list
            ItemList[Items.HeartPieceNotebookHand].ReplacesItemId = Items.MaskCircusLeader;
            ItemList[Items.ItemBottleAliens].ReplacesItemId = Items.MaskGibdo;
            ItemList[Items.MaskAllNight].ReplacesItemId = Items.ShopItemTradingPostFairy;
            ItemList[Items.HeartPieceBank].ReplacesItemId = Items.MaskGreatFairy;
            ItemList[Items.ChestBeanGrottoRedRupee].ReplacesItemId = Items.ChestTerminaGrottoBombchu;
            ItemList[Items.ItemBottleBeavers].ReplacesItemId = Items.ShopItemBombsBombchu10;
            ItemList[Items.HeartPieceBeaverRace].ReplacesItemId = Items.ChestGraveyardGrotto;
            ItemList[Items.HeartPieceZoraGrotto].ReplacesItemId = Items.ChestBeanGrottoRedRupee;
            ItemList[Items.MaskBlast].ReplacesItemId = Items.HeartPieceBank;
            ItemList[Items.HeartPieceBoatArchery].ReplacesItemId = Items.ItemBottleWitch;
            ItemList[Items.ItemBombBag].ReplacesItemId = Items.HeartPieceGreatBayCapeLikeLike;
            ItemList[Items.ShopItemBombsBomb10].ReplacesItemId = Items.GyorgTrialHP;
            ItemList[Items.ShopItemBombsBombchu10].ReplacesItemId = Items.HeartPieceNotebookHand;
            ItemList[Items.ChestBomberHideoutSilverRupee].ReplacesItemId = Items.ItemNotebook;
            ItemList[Items.ItemNotebook].ReplacesItemId = Items.MaskCouple;
            ItemList[Items.BottleCatchBigPoe].ReplacesItemId = Items.BottleCatchPoe;
            ItemList[Items.BottleCatchBug].ReplacesItemId = Items.BottleCatchPrincess;
            ItemList[Items.BottleCatchPrincess].ReplacesItemId = Items.BottleCatchEgg;
            ItemList[Items.BottleCatchFairy].ReplacesItemId = Items.BottleCatchSpringWater;
            ItemList[Items.BottleCatchFish].ReplacesItemId = Items.BottleCatchBug;
            ItemList[Items.BottleCatchHotSpringWater].ReplacesItemId = Items.BottleCatchFairy;
            ItemList[Items.BottleCatchMushroom].ReplacesItemId = Items.BottleCatchHotSpringWater;
            ItemList[Items.BottleCatchPoe].ReplacesItemId = Items.BottleCatchBigPoe;
            ItemList[Items.BottleCatchSpringWater].ReplacesItemId = Items.BottleCatchFish;
            ItemList[Items.BottleCatchEgg].ReplacesItemId = Items.BottleCatchMushroom;
            ItemList[Items.MaskBremen].ReplacesItemId = Items.HeartPieceBeaverRace;
            ItemList[Items.MaskBunnyHood].ReplacesItemId = Items.HeartContainerGreatBay;
            ItemList[Items.MaskCaptainHat].ReplacesItemId = Items.TradeItemMamaLetter;
            ItemList[Items.ItemBottleMadameAroma].ReplacesItemId = Items.ItemMagicBean;
            ItemList[Items.MaskCircusLeader].ReplacesItemId = Items.ShopItemGoronRedPotion;
            ItemList[Items.MaskCouple].ReplacesItemId = Items.ItemFireArrow;
            ItemList[Items.ItemBottleDampe].ReplacesItemId = Items.HeartPieceZoraGrotto;
            ItemList[Items.MaskDeku].ReplacesItemId = Items.MaskKamaro;
            ItemList[Items.HeartPieceDekuPalace].ReplacesItemId = Items.MaskBremen;
            ItemList[Items.HeartPieceDekuPlayground].ReplacesItemId = Items.ChestGreatBayCapeGrotto;
            ItemList[Items.HeartPieceDodong].ReplacesItemId = Items.MaskCaptainHat;
            ItemList[Items.HeartPieceDogRace].ReplacesItemId = Items.HeartPieceBoatArchery;
            ItemList[Items.ChestDogRacePurpleRupee].ReplacesItemId = Items.MaskBunnyHood;
            ItemList[Items.MaskDonGero].ReplacesItemId = Items.UpgradeBiggestBombBag;
            ItemList[Items.ChestEastClockTownSilverRupee].ReplacesItemId = Items.HeartPieceDekuPlayground;
            ItemList[Items.SongElegy].ReplacesItemId = Items.SongNewWaveBossaNova;
            ItemList[Items.SongEpona].ReplacesItemId = Items.ItemStoneTowerKey3;
            ItemList[Items.HeartPieceEvan].ReplacesItemId = Items.ChestIkanaGrottoRecoveryHeart;
            ItemList[Items.MaskFierceDiety].ReplacesItemId = Items.ShopItemTradingPostGreenPotion;
            ItemList[Items.ItemFireArrow].ReplacesItemId = Items.ItemBottleBeavers;
            ItemList[Items.HeartPieceFishermanGame].ReplacesItemId = Items.ChestInvertedStoneTowerBombchu10;
            ItemList[Items.HeartPieceChoir].ReplacesItemId = Items.ItemBottleAliens;
            ItemList[Items.MaskGaro].ReplacesItemId = Items.HeartPieceDekuPalace;
            ItemList[Items.MaskGibdo].ReplacesItemId = Items.ItemGoldDust;
            ItemList[Items.UpgradeGildedSword].ReplacesItemId = Items.HeartPieceDogRace;
            ItemList[Items.HeartContainerSnowhead].ReplacesItemId = Items.MaskBlast;
            ItemList[Items.GohtTrialHP].ReplacesItemId = Items.UpgradeBigBombBag;
            ItemList[Items.SongLullaby].ReplacesItemId = Items.MaskDonGero;
            ItemList[Items.MaskGoron].ReplacesItemId = Items.ItemHookshot;
            ItemList[Items.ItemGoldDust].ReplacesItemId = Items.SongSoaring;
            ItemList[Items.ChestToGoronRaceGrotto].ReplacesItemId = Items.ChestEastClockTownSilverRupee;
            ItemList[Items.ShopItemGoronArrow10].ReplacesItemId = Items.HeartPieceDodong;
            ItemList[Items.ShopItemGoronBomb10].ReplacesItemId = Items.ChestDogRacePurpleRupee;
            ItemList[Items.ShopItemGoronRedPotion].ReplacesItemId = Items.MaskGaro;
            ItemList[Items.HeartPieceGoronVillageScrub].ReplacesItemId = Items.UpgradeGildedSword;
            ItemList[Items.HeartPieceNotebookGran2].ReplacesItemId = Items.TradeItemMountainDeed;
            ItemList[Items.HeartPieceNotebookGran1].ReplacesItemId = Items.TradeItemRoomKey;
            ItemList[Items.ChestBadBatsGrottoPurpleRupee].ReplacesItemId = Items.HeartPieceEvan;
            ItemList[Items.ChestGraveyardGrotto].ReplacesItemId = Items.HeartContainerSnowhead;
            ItemList[Items.HeartPieceKnuckle].ReplacesItemId = Items.ChestGreatBayCapeLedge2;
            ItemList[Items.ItemGreatBayBossKey].ReplacesItemId = Items.TradeItemSwampDeed;
            ItemList[Items.ChestGreatBayCapeGrotto].ReplacesItemId = Items.ChestBadBatsGrottoPurpleRupee;
            ItemList[Items.ChestGreatBayCapeLedge1].ReplacesItemId = Items.HeartPieceFishermanGame;
            ItemList[Items.ChestGreatBayCapeLedge2].ReplacesItemId = Items.HeartPieceGoronVillageScrub;
            ItemList[Items.ChestGreatBayCapeUnderwater].ReplacesItemId = Items.ChestGreatBayCoastGrotto;
            ItemList[Items.ChestGreatBayCoastGrotto].ReplacesItemId = Items.HeartPieceChoir;
            ItemList[Items.HeartPieceGreatBayCoast].ReplacesItemId = Items.ChestGreatBayCapeUnderwater;
            ItemList[Items.ItemGreatBayCompass].ReplacesItemId = Items.ShopItemBombsBomb10;
            ItemList[Items.ItemGreatBayKey1].ReplacesItemId = Items.ItemBottleMadameAroma;
            ItemList[Items.HeartPieceGreatBayCapeLikeLike].ReplacesItemId = Items.ChestInnGuestRoom;
            ItemList[Items.ItemGreatBayMap].ReplacesItemId = Items.ShopItemWitchRedPotion;
            ItemList[Items.MaskGreatFairy].ReplacesItemId = Items.GohtTrialHP;
            ItemList[Items.ItemFairySword].ReplacesItemId = Items.SongLullaby;
            ItemList[Items.HeartContainerGreatBay].ReplacesItemId = Items.ItemBow;
            ItemList[Items.GyorgTrialHP].ReplacesItemId = Items.ShopItemZoraRedPotion;
            ItemList[Items.ItemBow].ReplacesItemId = Items.ItemLens;
            ItemList[Items.HeartPieceHoneyAndDarling].ReplacesItemId = Items.SongOath;
            ItemList[Items.ItemHookshot].ReplacesItemId = Items.ChestToGoronRaceGrotto;
            ItemList[Items.ChestHotSpringGrottoRedRupee].ReplacesItemId = Items.ShopItemGoronArrow10;
            ItemList[Items.ItemIceArrow].ReplacesItemId = Items.SongElegy;
            ItemList[Items.HeartPieceCastle].ReplacesItemId = Items.ItemGreatBayBossKey;
            ItemList[Items.ChestIkanaGrottoRecoveryHeart].ReplacesItemId = Items.HeartPieceNotebookGran2;
            ItemList[Items.HeartPieceIkana].ReplacesItemId = Items.ChestPiratesFortressEntranceRedRupee1;
            ItemList[Items.ChestInnGuestRoom].ReplacesItemId = Items.ShopItemWitchBluePotion;
            ItemList[Items.ChestInnStaffRoom].ReplacesItemId = Items.HeartPieceNotebookGran1;
            ItemList[Items.MaskKafei].ReplacesItemId = Items.HeartPieceKnuckle;
            ItemList[Items.MaskKamaro].ReplacesItemId = Items.ItemTingleMapRanch;
            ItemList[Items.MaskKeaton].ReplacesItemId = Items.HeartPieceNorthClockTown;
            ItemList[Items.HeartPieceKeatonQuiz].ReplacesItemId = Items.ChestGreatBayCapeLedge1;
            ItemList[Items.HeartPieceLabFish].ReplacesItemId = Items.ChestMountainVillageGrottoBottle;
            ItemList[Items.TradeItemLandDeed].ReplacesItemId = Items.MaskKeaton;
            ItemList[Items.ChestLensCaveRedRupee].ReplacesItemId = Items.HeartPieceGreatBayCoast;
            ItemList[Items.ChestLensCavePurpleRupee].ReplacesItemId = Items.ItemGreatBayCompass;
            ItemList[Items.ItemLens].ReplacesItemId = Items.MaskZora;
            ItemList[Items.TradeItemKafeiLetter].ReplacesItemId = Items.ChestPiratesFortressEntranceRedRupee2;
            ItemList[Items.TradeItemMamaLetter].ReplacesItemId = Items.HeartPiecePiratesFortress;
            ItemList[Items.ItemLightArrow].ReplacesItemId = Items.ItemFairySword;
            ItemList[Items.ItemMagicBean].ReplacesItemId = Items.ChestInsidePiratesFortressHeartPieceRoomRedRupee;
            ItemList[Items.ItemTingleMapTown].ReplacesItemId = Items.ItemGreatBayKey1;
            ItemList[Items.ItemTingleMapGreatBay].ReplacesItemId = Items.ChestMountainVillage;
            ItemList[Items.ItemTingleMapRanch].ReplacesItemId = Items.ChestPiratesFortressRedRupee1;
            ItemList[Items.ItemTingleMapSnowhead].ReplacesItemId = Items.HeartPieceIkana;
            ItemList[Items.ItemTingleMapStoneTower].ReplacesItemId = Items.ItemTingleMapTown;
            ItemList[Items.ItemTingleMapWoodfall].ReplacesItemId = Items.HeartPieceToSwamp;
            ItemList[Items.MaskScents].ReplacesItemId = Items.MaskDeku;
            ItemList[Items.MaskTruth].ReplacesItemId = Items.HeartPieceToSnowhead;
            ItemList[Items.HeartPieceNotebookMayor].ReplacesItemId = Items.ChestPiratesFortressRedRupee2;
            ItemList[Items.UpgradeMirrorShield].ReplacesItemId = Items.MaskAllNight;
            ItemList[Items.TradeItemMoonTear].ReplacesItemId = Items.UpgradeAdultWallet;
            ItemList[Items.UpgradeBiggestBombBag].ReplacesItemId = Items.MaskFierceDiety;
            ItemList[Items.TradeItemMountainDeed].ReplacesItemId = Items.ShopItemGoronBomb10;
            ItemList[Items.ChestMountainVillage].ReplacesItemId = Items.MaskScents;
            ItemList[Items.ChestMountainVillageGrottoBottle].ReplacesItemId = Items.ItemPowderKeg;
            ItemList[Items.ChestWoodsGrotto].ReplacesItemId = Items.HeartPiecePostBox;
            ItemList[Items.SongNewWaveBossaNova].ReplacesItemId = Items.SongSonata;
            ItemList[Items.HeartPieceNorthClockTown].ReplacesItemId = Items.MaskPostmanHat;
            ItemList[Items.SongOath].ReplacesItemId = Items.ChestSecretShrineWartGrotto;
            ItemList[Items.HeartPieceOceanSpiderHouse].ReplacesItemId = Items.PreClockTowerDekuNuts;
            ItemList[Items.TradeItemOceanDeed].ReplacesItemId = Items.UpgradeMirrorShield;
            ItemList[Items.UpgradeGiantWallet].ReplacesItemId = Items.HeartPiecePictobox;
            ItemList[Items.HeartContainerWoodfall].ReplacesItemId = Items.ChestPinacleRockRedRupee1;
            ItemList[Items.OdolwaTrialHP].ReplacesItemId = Items.ItemBombBag;
            ItemList[Items.ChestToIkanaRedRupee].ReplacesItemId = Items.MaskRomani;
            ItemList[Items.ChestToIkanaGrotto].ReplacesItemId = Items.ChestSecretShrineHeartPiece;
            ItemList[Items.ChestToSnowheadGrotto].ReplacesItemId = Items.ChestInsidePiratesFortressTankRedRupee;
            ItemList[Items.HeartPieceToSnowhead].ReplacesItemId = Items.ChestSecretShrineDinoGrotto;
            ItemList[Items.ChestToSwampGrotto].ReplacesItemId = Items.ChestSecretShrineWizzGrotto;
            ItemList[Items.HeartPieceToSwamp].ReplacesItemId = Items.ItemSnowheadBossKey;
            ItemList[Items.HeartPiecePeahat].ReplacesItemId = Items.ItemSnowheadKey1;
            ItemList[Items.TradeItemPendant].ReplacesItemId = Items.TradeItemMoonTear;
            ItemList[Items.ChestPiratesFortressRedRupee1].ReplacesItemId = Items.SongStorms;
            ItemList[Items.ChestPiratesFortressRedRupee2].ReplacesItemId = Items.HeartPieceSouthClockTown;
            ItemList[Items.ChestPiratesFortressEntranceRedRupee1].ReplacesItemId = Items.ChestSouthClockTownRedRupee;
            ItemList[Items.ChestPiratesFortressEntranceRedRupee2].ReplacesItemId = Items.ItemSnowheadMap;
            ItemList[Items.ChestPiratesFortressEntranceRedRupee3].ReplacesItemId = Items.HeartPieceCastle;
            ItemList[Items.ChestInsidePiratesFortressGuardSilverRupee].ReplacesItemId = Items.ChestSouthClockTownPurpleRupee;
            ItemList[Items.ChestInsidePiratesFortressHeartPieceRoomRedRupee].ReplacesItemId = Items.TradeItemLandDeed;
            ItemList[Items.ChestInsidePiratesFortressHeartPieceRoomBlueRupee].ReplacesItemId = Items.MaskStone;
            ItemList[Items.ChestInsidePiratesFortressTankRedRupee].ReplacesItemId = Items.ChestLensCaveRedRupee;
            ItemList[Items.ItemPictobox].ReplacesItemId = Items.TradeItemKafeiLetter;
            ItemList[Items.HeartPiecePictobox].ReplacesItemId = Items.ChestHotSpringGrottoRedRupee;
            ItemList[Items.HeartPiecePiratesFortress].ReplacesItemId = Items.HeartPieceSwampArchery;
            ItemList[Items.HeartPiecePoeHut].ReplacesItemId = Items.ChestInnStaffRoom;
            ItemList[Items.HeartPiecePostBox].ReplacesItemId = Items.ItemStoneTowerBossKey;
            ItemList[Items.HeartPieceNotebookPostman].ReplacesItemId = Items.HeartPieceHoneyAndDarling;
            ItemList[Items.MaskPostmanHat].ReplacesItemId = Items.ShopItemTradingPostRedPotion;
            ItemList[Items.ItemPowderKeg].ReplacesItemId = Items.TradeItemPendant;
            ItemList[Items.ChestPinacleRockRedRupee1].ReplacesItemId = Items.ItemStoneTowerMap;
            ItemList[Items.ChestPinacleRockRedRupee2].ReplacesItemId = Items.ItemGreatBayMap;
            ItemList[Items.PreClockTowerDekuNuts].ReplacesItemId = Items.MaskGoron;
            ItemList[Items.UpgradeRazorSword].ReplacesItemId = Items.ChestSwampGrotto;
            ItemList[Items.MaskRomani].ReplacesItemId = Items.UpgradeGiantWallet;
            ItemList[Items.TradeItemRoomKey].ReplacesItemId = Items.SongEpona;
            ItemList[Items.HeartPieceNotebookRosa].ReplacesItemId = Items.ChestLensCavePurpleRupee;
            ItemList[Items.HeartPieceSeaHorse].ReplacesItemId = Items.ItemTingleMapGreatBay;
            ItemList[Items.ChestSecretShrineDinoGrotto].ReplacesItemId = Items.HeartPieceSwordsmanSchool;
            ItemList[Items.ChestSecretShrineGaroGrotto].ReplacesItemId = Items.HeartPieceLabFish;
            ItemList[Items.ChestSecretShrineHeartPiece].ReplacesItemId = Items.ChestInvertedStoneTowerBean;
            ItemList[Items.ChestSecretShrineWartGrotto].ReplacesItemId = Items.MaskTruth;
            ItemList[Items.ChestSecretShrineWizzGrotto].ReplacesItemId = Items.HeartPieceKeatonQuiz;
            ItemList[Items.ItemSnowheadBossKey].ReplacesItemId = Items.HeartPieceOceanSpiderHouse;
            ItemList[Items.ItemSnowheadCompass].ReplacesItemId = Items.ItemTingleMapStoneTower;
            ItemList[Items.ItemSnowheadKey1].ReplacesItemId = Items.HeartPieceNotebookMayor;
            ItemList[Items.ItemSnowheadKey2].ReplacesItemId = Items.ChestWoodsGrotto;
            ItemList[Items.ItemSnowheadKey3].ReplacesItemId = Items.ItemTingleMapWoodfall;
            ItemList[Items.ItemSnowheadMap].ReplacesItemId = Items.ItemTingleMapSnowhead;
            ItemList[Items.SongSonata].ReplacesItemId = Items.OdolwaTrialHP;
            ItemList[Items.SongSoaring].ReplacesItemId = Items.TradeItemOceanDeed;
            ItemList[Items.SongStorms].ReplacesItemId = Items.HeartContainerWoodfall;
            ItemList[Items.ChestSouthClockTownRedRupee].ReplacesItemId = Items.ChestToIkanaGrotto;
            ItemList[Items.ChestSouthClockTownPurpleRupee].ReplacesItemId = Items.ChestToIkanaRedRupee;
            ItemList[Items.HeartPieceSouthClockTown].ReplacesItemId = Items.ItemIceArrow;
            ItemList[Items.MaskStone].ReplacesItemId = Items.ShopItemWitchGreenPotion;
            ItemList[Items.ChestInvertedStoneTowerSilverRupee].ReplacesItemId = Items.ChestToSnowheadGrotto;
            ItemList[Items.ChestInvertedStoneTowerBombchu10].ReplacesItemId = Items.ItemStoneTowerKey2;
            ItemList[Items.ItemStoneTowerBossKey].ReplacesItemId = Items.ChestToSwampGrotto;
            ItemList[Items.ItemStoneTowerCompass].ReplacesItemId = Items.ItemLightArrow;
            ItemList[Items.ItemStoneTowerKey1].ReplacesItemId = Items.MaskKafei;
            ItemList[Items.ItemStoneTowerKey2].ReplacesItemId = Items.HeartPiecePeahat;
            ItemList[Items.ItemStoneTowerKey3].ReplacesItemId = Items.ChestPiratesFortressEntranceRedRupee3;
            ItemList[Items.ItemStoneTowerKey4].ReplacesItemId = Items.ChestInsidePiratesFortressGuardSilverRupee;
            ItemList[Items.ChestInvertedStoneTowerBean].ReplacesItemId = Items.ChestInsidePiratesFortressHeartPieceRoomBlueRupee;
            ItemList[Items.ItemStoneTowerMap].ReplacesItemId = Items.ItemStoneTowerKey1;
            ItemList[Items.HeartPieceSwampArchery].ReplacesItemId = Items.ItemStoneTowerKey4;
            ItemList[Items.UpgradeBiggestQuiver].ReplacesItemId = Items.UpgradeBiggestQuiver;
            ItemList[Items.ChestSwampGrotto].ReplacesItemId = Items.ItemPictobox;
            ItemList[Items.HeartPieceSwampScrub].ReplacesItemId = Items.HeartPiecePoeHut;
            ItemList[Items.TradeItemSwampDeed].ReplacesItemId = Items.HeartPieceNotebookPostman;
            ItemList[Items.HeartPieceSwordsmanSchool].ReplacesItemId = Items.UpgradeRazorSword;
            ItemList[Items.ChestTerminaGrottoRedRupee].ReplacesItemId = Items.HeartPieceNotebookRosa;
            ItemList[Items.ChestTerminaGrottoBombchu].ReplacesItemId = Items.ChestSecretShrineGaroGrotto;
            ItemList[Items.HeartPieceTerminaBusinessScrub].ReplacesItemId = Items.ItemSnowheadCompass;
            ItemList[Items.HeartPieceTerminaGossipStones].ReplacesItemId = Items.ItemSnowheadKey2;
            ItemList[Items.ChestTerminaGrassRedRupee].ReplacesItemId = Items.HeartPieceSwampScrub;
            ItemList[Items.ChestTerminaStumpRedRupee].ReplacesItemId = Items.HeartPieceTerminaBusinessScrub;
            ItemList[Items.ChestTerminaUnderwaterRedRupee].ReplacesItemId = Items.HeartPieceSeaHorse;
            ItemList[Items.HeartPieceTownArchery].ReplacesItemId = Items.UpgradeBigQuiver;
            ItemList[Items.UpgradeBigQuiver].ReplacesItemId = Items.ItemBottleDampe;
            ItemList[Items.UpgradeBigBombBag].ReplacesItemId = Items.ChestBomberHideoutSilverRupee;
            ItemList[Items.UpgradeAdultWallet].ReplacesItemId = Items.ChestTerminaGrottoRedRupee;
            ItemList[Items.ShopItemTradingPostArrow30].ReplacesItemId = Items.HeartPieceTerminaGossipStones;
            ItemList[Items.ShopItemTradingPostArrow50].ReplacesItemId = Items.ShopItemZoraShield;
            ItemList[Items.ShopItemTradingPostFairy].ReplacesItemId = Items.ChestTerminaGrassRedRupee;
            ItemList[Items.ShopItemTradingPostGreenPotion].ReplacesItemId = Items.ChestTerminaUnderwaterRedRupee;
            ItemList[Items.ShopItemTradingPostNut10].ReplacesItemId = Items.ShopItemTradingPostArrow30;
            ItemList[Items.ShopItemTradingPostRedPotion].ReplacesItemId = Items.ShopItemTradingPostArrow50;
            ItemList[Items.ShopItemTradingPostShield].ReplacesItemId = Items.HeartContainerStoneTower;
            ItemList[Items.ShopItemTradingPostStick].ReplacesItemId = Items.ChestPinacleRockRedRupee2;
            ItemList[Items.HeartPieceTreasureChestGame].ReplacesItemId = Items.ItemSnowheadKey3;
            ItemList[Items.ChestToGoronVillageRedRupee].ReplacesItemId = Items.HeartPieceTwinIslandsChest;
            ItemList[Items.HeartPieceTwinIslandsChest].ReplacesItemId = Items.ChestInvertedStoneTowerSilverRupee;
            ItemList[Items.HeartContainerStoneTower].ReplacesItemId = Items.ChestTerminaStumpRedRupee;
            ItemList[Items.TwinmoldTrial10Chus].ReplacesItemId = Items.TwinmoldTrial10Chus;
            ItemList[Items.TwinmoldTrial30Arrows].ReplacesItemId = Items.TwinmoldTrial30Arrows;
            ItemList[Items.TwinmoldTrialHP].ReplacesItemId = Items.ChestWellRightPurpleRupee;
            ItemList[Items.ChestWellLeftPurpleRupee].ReplacesItemId = Items.ItemStoneTowerCompass;
            ItemList[Items.ChestWellRightPurpleRupee].ReplacesItemId = Items.HeartPieceTreasureChestGame;
            ItemList[Items.ItemBottleWitch].ReplacesItemId = Items.ShopItemTradingPostShield;
            ItemList[Items.ShopItemWitchBluePotion].ReplacesItemId = Items.ShopItemTradingPostStick;
            ItemList[Items.ShopItemWitchGreenPotion].ReplacesItemId = Items.ChestWellLeftPurpleRupee;
            ItemList[Items.ShopItemWitchRedPotion].ReplacesItemId = Items.HeartPieceTownArchery;
            ItemList[Items.ChestWoodfallRedRupee].ReplacesItemId = Items.ShopItemTradingPostNut10;
            ItemList[Items.ChestWoodfallBlueRupee].ReplacesItemId = Items.ChestToGoronVillageRedRupee;
            ItemList[Items.ItemWoodfallBossKey].ReplacesItemId = Items.ChestWoodfallRedRupee;
            ItemList[Items.HeartPieceWoodFallChest].ReplacesItemId = Items.TwinmoldTrialHP;
            ItemList[Items.ItemWoodfallCompass].ReplacesItemId = Items.ItemWoodfallBossKey;
            ItemList[Items.ItemWoodfallKey1].ReplacesItemId = Items.HeartPieceWoodFallChest;
            ItemList[Items.ItemWoodfallMap].ReplacesItemId = Items.ItemWoodfallCompass;
            ItemList[Items.HeartPieceZoraHallScrub].ReplacesItemId = Items.ChestWoodfallBlueRupee;
            ItemList[Items.MaskZora].ReplacesItemId = Items.ItemWoodfallMap;
            ItemList[Items.ShopItemZoraArrow10].ReplacesItemId = Items.ItemWoodfallKey1;
            ItemList[Items.ShopItemZoraRedPotion].ReplacesItemId = Items.HeartPieceZoraHallScrub;
            ItemList[Items.ShopItemZoraShield].ReplacesItemId = Items.ShopItemZoraArrow10;
            ItemList[Items.MaskGiant].ReplacesItemId = Items.ChestInsidePiratesFortressMazeRedRupee;
            ItemList[Items.ChestInsidePiratesFortressMazeRedRupee].ReplacesItemId = Items.MaskGiant;
        }

        /// <summary>
        /// Places tingle maps in the randomization pool.
        /// </summary>
        private void PlaceTingleMaps(List<int> itemPool)
        {
            for (int i = Items.ItemTingleMapTown; i <= Items.ItemTingleMapStoneTower; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Places other chests and grottos in the randomization pool.
        /// </summary>
        /// <param name="itemPool"></param>
        private void PlaceOther(List<int> itemPool)
        {
            for (int i = Items.ChestLensCaveRedRupee; i <= Items.ChestSouthClockTownPurpleRupee; i++)
            {
                PlaceItem(i, itemPool);
            }

            PlaceItem(Items.ChestToGoronRaceGrotto, itemPool);
        }

        /// <summary>
        /// Places heart pieces in the randomization pool. Includes rewards/chests, as well as standing heart pieces.
        /// </summary>
        private void PlaceHeartpieces(List<int> itemPool)
        {
            // Rewards/chests
            for (int i = Items.HeartPieceNotebookMayor; i <= Items.HeartPieceKnuckle; i++)
            {
                PlaceItem(i, itemPool);
            }

            // Bank reward
            PlaceItem(Items.HeartPieceBank, itemPool);

            // Standing heart pieces
            for (int i = Items.HeartPieceSouthClockTown; i <= Items.HeartContainerStoneTower; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Places shop items in the randomization pool
        /// </summary>
        private void PlaceShopItems(List<int> itemPool)
        {
            for (int i = Items.ShopItemTradingPostRedPotion; i <= Items.ShopItemZoraRedPotion; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Places dungeon items in the randomization pool
        /// </summary>
        private void PlaceDungeonItems(List<int> itemPool)
        {
            for (int i = Items.ItemWoodfallMap; i <= Items.ItemStoneTowerKey4; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Places songs in the randomization pool
        /// </summary>
        private void PlaceSongs(List<int> itemPool)
        {
            for (int i = Items.SongSoaring; i <= Items.SongOath; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Places masks in the randomization pool
        /// </summary>
        private void PlaceMasks(List<int> itemPool)
        {
            for (int i = Items.MaskPostmanHat; i <= Items.MaskZora; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Places upgrade items in the randomization pool
        /// </summary>
        private void PlaceUpgrades(List<int> itemPool)
        {
            for (int i = Items.UpgradeRazorSword; i <= Items.UpgradeGiantWallet; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Places regular items in the randomization pool
        /// </summary>
        private void PlaceRegularItems(List<int> itemPool)
        {
            for (int i = Items.MaskDeku; i <= Items.ItemNotebook; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Replace starting deku mask with free item if not already replaced.
        /// </summary>
        private void PlaceFreeItem(List<int> itemPool)
        {
            if (ItemList.FindIndex(item => item.ReplacesItemId == Items.MaskDeku) != -1)
            {
                return;
            }

            int freeItem = RNG.Next(Items.SongOath + 1);
            if (ForbiddenReplacedBy.ContainsKey(Items.MaskDeku))
            {
                while (ItemList[freeItem].ReplacesItemId != -1
                    || ForbiddenReplacedBy[Items.MaskDeku].Contains(freeItem))
                {
                    freeItem = RNG.Next(Items.SongOath + 1);
                }
            }
            ItemList[freeItem].ReplacesItemId = Items.MaskDeku;
            itemPool.Remove(Items.MaskDeku);
        }

        /// <summary>
        /// Adds all items into the randomization pool (excludes area/other and items that already have placement)
        /// </summary>
        private void AddAllItems(List<int> itemPool)
        {
            for (int i = 0; i < ItemList.Count; i++)
            {
                // Skip item if its in area and other, is out of range or has placement
                if ((ItemUtils.IsAreaOrOther(i)
                    || ItemUtils.IsOutOfRange(i))
                    || (ItemList[i].ReplacesAnotherItem))
                {
                    continue;
                }

                itemPool.Add(i);
            }
        }

        /// <summary>
        /// Places quest items in the randomization pool
        /// </summary>
        private void PlaceQuestItems(List<int> itemPool)
        {
            for (int i = Items.TradeItemRoomKey; i <= Items.TradeItemMamaLetter; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Places trade items in the randomization pool
        /// </summary>
        private void PlaceTradeItems(List<int> itemPool)
        {
            for (int i = Items.TradeItemMoonTear; i <= Items.TradeItemOceanDeed; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Adds items to randomization pool based on settings.
        /// </summary>
        private void Setup()
        {
            if (Settings.ExcludeSongOfSoaring)
            {
                ItemList[Items.SongSoaring].ReplacesItemId = Items.SongSoaring;
            }

            if (!Settings.AddSongs)
            {
                ShuffleSongs();
            }

            if (!Settings.AddDungeonItems)
            {
                PreserveDungeonItems();
            }

            if (!Settings.AddShopItems)
            {
                PreserveShopItems();
            }

            if (!Settings.AddOther)
            {
                PreserveOther();
            }

            if (Settings.RandomizeBottleCatchContents)
            {
                AddBottleCatchContents();
            }
            else
            {
                PreserveBottleCatchContents();
            }
        }

        /// <summary>
        /// Keeps bottle catch contents vanilla
        /// </summary>
        private void PreserveBottleCatchContents()
        {
            for (int i = Items.BottleCatchFairy; i <= Items.BottleCatchMushroom; i++)
            {
                ItemList[i].ReplacesItemId = i;
            }
        }

        /// <summary>
        /// Randomizes bottle catch contents
        /// </summary>
        private void AddBottleCatchContents()
        {
            var itemPool = new List<int>();
            for (int i = Items.BottleCatchFairy; i <= Items.BottleCatchMushroom; i++)
            {
                itemPool.Add(i);
            };

            for (int i = Items.BottleCatchFairy; i <= Items.BottleCatchMushroom; i++)
            {
                PlaceItem(i, itemPool);
            };
        }

        /// <summary>
        /// Keeps other vanilla
        /// </summary>
        private void PreserveOther()
        {
            for (int i = Items.ChestLensCaveRedRupee; i <= Items.ChestToGoronRaceGrotto; i++)
            {
                ItemList[i].ReplacesItemId = i;
            };
        }

        /// <summary>
        /// Keeps shop items vanilla
        /// </summary>
        private void PreserveShopItems()
        {
            for (int i = Items.ShopItemTradingPostRedPotion; i <= Items.ShopItemZoraRedPotion; i++)
            {
                ItemList[i].ReplacesItemId = i;
            };

            ItemList[Items.ItemBombBag].ReplacesItemId = Items.ItemBombBag;
            ItemList[Items.UpgradeBigBombBag].ReplacesItemId = Items.UpgradeBigBombBag;
            ItemList[Items.MaskAllNight].ReplacesItemId = Items.MaskAllNight;
        }

        /// <summary>
        /// Keeps dungeon items vanilla
        /// </summary>
        private void PreserveDungeonItems()
        {
            for (int i = Items.ItemWoodfallMap; i <= Items.ItemStoneTowerKey4; i++)
            {
                ItemList[i].ReplacesItemId = i;
            };
        }

        /// <summary>
        /// Randomizes songs with other songs
        /// </summary>
        private void ShuffleSongs()
        {
            var itemPool = new List<int>();
            for (int i = Items.SongSoaring; i <= Items.SongOath; i++)
            {
                if (ItemList[i].ReplacesAnotherItem)
                {
                    continue;
                }
                itemPool.Add(i);
            }

            for (int i = Items.SongSoaring; i <= Items.SongOath; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Adds custom item list to randomization. NOTE: keeps area and other vanilla, randomizes bottle catch contents
        /// </summary>
        private void SetupCustomItems()
        {
            // Keep shop items vanilla, unless custom item list contains a shop item
            Settings.AddShopItems = false;

            // Make all items vanilla, and override using custom item list
            MakeAllItemsVanilla();
            PreserveAreasAndOther();

            // Should these be vanilla by default? Why not check settings.
            ApplyCustomItemList();

            // Should these be randomized by default? Why not check settings.
            AddBottleCatchContents();

            if (!Settings.AddSongs)
            {
                ShuffleSongs();
            }
        }

        /// <summary>
        /// Mark all items as replacing themselves (i.e. vanilla)
        /// </summary>
        private void MakeAllItemsVanilla()
        {
            for (int item = 0; item < ItemList.Count; item++)
            {
                if (ItemUtils.IsAreaOrOther(item) 
                    || ItemUtils.IsOutOfRange(item))
                {
                    continue;
                }

                ItemList[item].ReplacesItemId = item;
            }
        }

        /// <summary>
        /// Adds items specified from the Custom Item List to the randomizer pool, while keeping the rest vanilla
        /// </summary>
        private void ApplyCustomItemList()
        {
            for (int i = 0; i < fItemEdit.selected_items.Count; i++)
            {
                int selectedItem = fItemEdit.selected_items[i];

                if (selectedItem > Items.SongOath)
                {
                    // Skip entries describing areas and other
                    selectedItem += Values.NumberOfAreasAndOther;
                }
                int selectedItemIndex = ItemList.FindIndex(u => u.ID == selectedItem);

                if (selectedItemIndex != -1)
                {
                    ItemList[selectedItemIndex].ReplacesItemId = -1;
                }

                if (ItemUtils.IsShopItem(selectedItem))
                {
                    Settings.AddShopItems = true;
                }
            }
        }

        /// <summary>
        /// Keeps area and other vanilla
        /// </summary>
        private void PreserveAreasAndOther()
        {
            for (int i = 0; i < ItemList.Count; i++)
            {
                if (ItemUtils.IsAreaOrOther(i)
                    || ItemUtils.IsOutOfRange(i))
                {
                    continue;
                }

                ItemList[i].ReplacesItemId = i;
            };
        }
    }

}
