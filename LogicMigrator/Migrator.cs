﻿using System;
using System.Collections.Generic;
using System.Linq;
using MMRando.Extensions;
using MMRando.Models;

namespace MMRando.LogicMigrator
{
    public static partial class Migrator
    {
        public const int CurrentVersion = 3;

        public static string ApplyMigrations(string logic)
        {
            var lines = logic.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();

            if (GetVersion(lines) < 0)
            {
                AddVersionNumber(lines);
            }

            if (GetVersion(lines) < 1)
            {
                AddItemNames(lines);
            }

            if (GetVersion(lines) < 2)
            {
                AddMoonItems(lines);
            }

            if (GetVersion(lines) < 3)
            {
                AddRequirementsForSongOath(lines);
            }

            return string.Join("\r\n", lines);
        }

        public static int GetVersion(List<string> lines)
        {
            if (!lines[0].StartsWith("-version"))
            {
                return -1;
            }
            return int.Parse(lines[0].Split(' ')[1]);
        }

        private static void AddVersionNumber(List<string> lines)
        {
            lines.Insert(0, "-version 0");
        }

        private static void AddItemNames(List<string> lines)
        {
            if (lines[1] == "- Deku Mask")
            {
                lines[0] = "-version 1";
                return;
            }
            lines.RemoveAll(line => line.StartsWith("-"));
            var itemNames = new string[] {"Deku Mask", "Hero's Bow", "Fire Arrow", "Ice Arrow", "Light Arrow", "Bomb Bag (20)", "Magic Bean",
                "Powder Keg", "Pictobox", "Lens of Truth", "Hookshot", "Great Fairy's Sword", "Witch Bottle", "Aliens Bottle", "Gold Dust",
                "Beaver Race Bottle", "Dampe Bottle", "Chateau Bottle", "Bombers' Notebook", "Razor Sword", "Gilded Sword", "Mirror Shield",
                "Town Archery Quiver (40)", "Swamp Archery Quiver (50)", "Town Bomb Bag (30)", "Mountain Bomb Bag (40)", "Town Wallet (200)", "Ocean Wallet (500)", "Moon's Tear",
                "Land Title Deed", "Swamp Title Deed", "Mountain Title Deed", "Ocean Title Deed", "Room Key", "Letter to Kafei", "Pendant of Memories",
                "Letter to Mama", "Mayor Dotour HP", "Postman HP", "Rosa Sisters HP", "??? HP", "Grandma Short Story HP", "Grandma Long Story HP",
                "Keaton Quiz HP", "Deku Playground HP", "Town Archery HP", "Honey and Darling HP", "Swordsman's School HP", "Postbox HP",
                "Termina Field Gossips HP", "Termina Field Business Scrub HP", "Swamp Archery HP", "Pictograph Contest HP", "Boat Archery HP",
                "Frog Choir HP", "Beaver Race HP", "Seahorse HP", "Fisherman Game HP", "Evan HP", "Dog Race HP", "Poe Hut HP",
                "Treasure Chest Game HP", "Peahat Grotto HP", "Dodongo Grotto HP", "Woodfall Chest HP", "Twin Islands Chest HP",
                "Ocean Spider House HP", "Graveyard Iron Knuckle HP", "Postman's Hat", "All Night Mask", "Blast Mask", "Stone Mask", "Great Fairy's Mask",
                "Keaton Mask", "Bremen Mask", "Bunny Hood", "Don Gero's Mask", "Mask of Scents", "Romani Mask", "Circus Leader's Mask", "Kafei's Mask",
                "Couple's Mask", "Mask of Truth", "Kamaro's Mask", "Gibdo Mask", "Garo Mask", "Captain's Hat", "Giant's Mask", "Goron Mask", "Zora Mask",
                "Song of Soaring", "Epona's Song", "Song of Storms", "Sonata of Awakening", "Goron Lullaby", "New Wave Bossa Nova",
                "Elegy of Emptiness", "Oath to Order", "Poison swamp access", "Woodfall Temple access", "Woodfall clear", "North access", "Snowhead Temple access",
                "Snowhead clear", "Epona access", "West access", "Pirates' Fortress access", "Great Bay Temple access", "Great Bay clear", "East access",
                "Ikana Canyon access", "Stone Tower Temple access", "Inverted Stone Tower Temple access", "Ikana clear", "Explosives", "Arrows", "(Unused)", "(Unused)",
                "(Unused)", "(Unused)", "(Unused)",
                "Woodfall Map", "Woodfall Compass", "Woodfall Boss Key", "Woodfall Key 1", "Snowhead Map", "Snowhead Compass", "Snowhead Boss Key",
                "Snowhead Key 1 - block room", "Snowhead Key 2 - icicle room", "Snowhead Key 3 - bridge room", "Great Bay Map", "Great Bay Compass", "Great Bay Boss Key", "Great Bay Key 1",
                "Stone Tower Map", "Stone Tower Compass", "Stone Tower Boss Key", "Stone Tower Key 1 - armos room", "Stone Tower Key 2 - eyegore room", "Stone Tower Key 3 - updraft room",
                "Stone Tower Key 4 - death armos maze", "Trading Post Red Potion", "Trading Post Green Potion", "Trading Post Shield", "Trading Post Fairy",
                "Trading Post Stick", "Trading Post Arrow 30", "Trading Post Nut 10", "Trading Post Arrow 50", "Witch Shop Blue Potion",
                "Witch Shop Red Potion", "Witch Shop Green Potion", "Bomb Shop Bomb 10", "Bomb Shop Chu 10", "Goron Shop Bomb 10", "Goron Shop Arrow 10",
                "Goron Shop Red Potion", "Zora Shop Shield", "Zora Shop Arrow 10", "Zora Shop Red Potion", "Bottle: Fairy", "Bottle: Deku Princess",
                "Bottle: Fish", "Bottle: Bug", "Bottle: Poe", "Bottle: Big Poe", "Bottle: Spring Water", "Bottle: Hot Spring Water", "Bottle: Zora Egg",
                "Bottle: Mushroom", "Lens Cave 20r", "Lens Cave 50r", "Bean Grotto 20r", "HSW Grotto 20r", "Graveyard Bad Bats", "Ikana Grotto",
                "PF 20r Lower", "PF 20r Upper", "PF Tank 20r", "PF Guard Room 100r", "PF HP Room 20r", "PF HP Room 5r", "PF Maze 20r", "PR 20r (1)", "PR 20r (2)",
                "Bombers' Hideout 100r", "Termina Bombchu Grotto", "Termina 20r Grotto", "Termina Underwater 20r", "Termina Grass 20r", "Termina Stump 20r",
                "Great Bay Coast Grotto", "Great Bay Cape Ledge (1)", "Great Bay Cape Ledge (2)", "Great Bay Cape Grotto", "Great Bay Cape Underwater",
                "PF Exterior 20r (1)", "PF Exterior 20r (2)", "PF Exterior 20r (3)", "Path to Swamp Grotto", "Doggy Racetrack 50r", "Graveyard Grotto",
                "Swamp Grotto", "Woodfall 5r", "Woodfall 20r", "Well Right Path 50r", "Well Left Path 50r", "Mountain Village Chest (Spring)",
                "Mountain Village Grotto Bottle (Spring)", "Path to Ikana 20r", "Path to Ikana Grotto", "Stone Tower 100r", "Stone Tower Bombchu 10",
                "Stone Tower Magic Bean", "Path to Snowhead Grotto", "Twin Islands 20r", "Secret Shrine HP", "Secret Shrine Dinolfos",
                "Secret Shrine Wizzrobe", "Secret Shrine Wart", "Secret Shrine Garo Master", "Inn Staff Room", "Inn Guest Room", "Mystery Woods Grotto",
                "East Clock Town 100r", "South Clock Town 20r", "South Clock Town 50r", "Bank HP", "South Clock Town HP", "North Clock Town HP",
                "Path to Swamp HP", "Swamp Scrub HP", "Deku Palace HP", "Goron Village Scrub HP", "Bio Baba Grotto HP", "Lab Fish HP", "Great Bay Like-Like HP",
                "Pirates' Fortress HP", "Zora Hall Scrub HP", "Path to Snowhead HP", "Great Bay Coast HP", "Ikana Scrub HP", "Ikana Castle HP",
                "Odolwa Heart Container", "Goht Heart Container", "Gyorg Heart Container", "Twinmold Heart Container", "Map: Clock Town", "Map: Woodfall",
                "Map: Snowhead", "Map: Romani Ranch", "Map: Great Bay", "Map: Stone Tower", "Goron Racetrack Grotto" };
            for (var i = 0; i < itemNames.Length; i++)
            {
                lines.Insert(i * 5, $"- {itemNames[i]}");
            }
            lines.Insert(0, "-version 1");
        }

        private static void AddMoonItems(List<string> lines)
        {
            lines[0] = "-version 2";
            var newItems = new ItemObject[]
            {
                new ItemObject
                {
                    ID = Items.OtherOneMask,
                    Conditionals = Enumerable.Range(Items.MaskPostmanHat, 20).Select(i => new List<int> { i }).ToList()
                },
                new ItemObject
                {
                    ID = Items.OtherTwoMasks,
                    Conditionals = Enumerable.Range(Items.MaskPostmanHat, 20).Combinations(2).Select(a => a.ToList()).ToList()
                },
                new ItemObject
                {
                    ID = Items.OtherThreeMasks,
                    Conditionals = Enumerable.Range(Items.MaskPostmanHat, 20).Combinations(3).Select(a => a.ToList()).ToList()
                },
                new ItemObject
                {
                    ID = Items.OtherFourMasks,
                    Conditionals = Enumerable.Range(Items.MaskPostmanHat, 20).Combinations(4).Select(a => a.ToList()).ToList()
                },
                new ItemObject
                {
                    ID = Items.AreaMoonAccess,
                    DependsOnItems = new List<int>
                    {
                        Items.SongOath, Items.AreaWoodFallTempleClear, Items.AreaSnowheadTempleClear, Items.AreaGreatBayTempleClear, Items.AreaStoneTowerClear
                    }
                },
                new ItemObject
                {
                    ID = Items.HeartPieceDekuTrial,
                    DependsOnItems = new List<int>
                    {
                        Items.AreaMoonAccess, Items.MaskDeku, Items.OtherOneMask
                    }
                },
                new ItemObject
                {
                    ID = Items.HeartPieceGoronTrial,
                    DependsOnItems = new List<int>
                    {
                        Items.AreaMoonAccess, Items.MaskGoron, Items.OtherTwoMasks
                    }
                },
                new ItemObject
                {
                    ID = Items.HeartPieceZoraTrial,
                    DependsOnItems = new List<int>
                    {
                        Items.AreaMoonAccess, Items.MaskZora, Items.OtherThreeMasks
                    }
                },
                new ItemObject
                {
                    ID = Items.HeartPieceLinkTrial,
                    DependsOnItems = new List<int>
                    {
                        Items.AreaMoonAccess, Items.OtherFourMasks, Items.OtherExplosive, Items.OtherArrow, Items.ItemFireArrow, Items.ItemHookshot
                    }
                },
                new ItemObject
                {
                    ID = Items.MaskFierceDeity,
                    DependsOnItems = new List<int>
                    {
                        Items.AreaMoonAccess, Items.MaskDeku, Items.MaskGoron, Items.MaskZora, Items.OtherExplosive, Items.OtherArrow, Items.ItemFireArrow, Items.ItemHookshot
                    }
                    .Concat(Enumerable.Range(Items.MaskPostmanHat, 20))
                    .ToList()
                }
            };
            var itemNames = new string[]
            {
                "One Mask", "Two Masks", "Three Masks", "Four Masks", "Moon Access", "Deku Trial HP", "Goron Trial HP", "Zora Trial HP", "Link Trial HP", "Fierce Deity's Mask"
            };
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.StartsWith("-") || string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                var updatedItemSections = line
                    .Split(';')
                    .Select(section => section.Split(',').Select(id => 
                        {
                            var itemId = int.Parse(id);
                            if (itemId >= Items.OtherOneMask)
                            {
                                itemId += newItems.Length;
                            }
                            return itemId;
                        }).ToList()).ToList();
                lines[i] = string.Join(";", updatedItemSections.Select(section => string.Join(",", section)));
            }
            foreach (var item in newItems)
            {
                lines.Insert(item.ID * 5 + 1, $"- {itemNames[item.ID - Items.OtherOneMask]}");
                lines.Insert(item.ID * 5 + 2, string.Join(",", item.DependsOnItems));
                lines.Insert(item.ID * 5 + 3, string.Join(";", item.Conditionals.Select(c => string.Join(",", c))));
                lines.Insert(item.ID * 5 + 4, "0");
                lines.Insert(item.ID * 5 + 5, "0");
            }
        }

        private static void AddRequirementsForSongOath(List<string> lines)
        {
            lines[0] = "-version 3";
            var oathIndex = lines.FindIndex(s => s == "- Oath to Order");
            lines[oathIndex + 1] = "";
            lines[oathIndex + 2] = $"{Items.AreaWoodFallTempleClear};{Items.AreaSnowheadTempleClear};{Items.AreaGreatBayTempleClear};{Items.AreaStoneTowerClear}";
            lines[oathIndex + 3] = "0";
            lines[oathIndex + 4] = "0";
        }
    }
}
