﻿
using System.Drawing;

namespace MMRando.Models
{
    public enum LogicMode
    {
        Casual,
        Glitched,
        Vanilla,
        UserLogic,
        NoLogic
    }

    public enum DamageMode
    {
        Default, Double, Quadruple, OHKO, Doom
    }

    public enum DamageEffect
    {
        Default,
        Fire,
        Ice,
        Shock,
        Knockdown,
        Random
    }

    public enum MovementMode
    {
        Default,
        HighSpeed,
        SuperLowGravity,
        LowGravity,
        HighGravity
    }

    public enum Character
    {
        LinkMM,
        LinkOOT,
        AdultLink,
        Kafei
    }

    public enum TatlColorSchema
    {
        Default,
        Dark,
        Hot,
        Cool,
        Rainbow,
        Random,
    }

    public class Settings
    {
        // TODO checkboxes should not be checked for settings, but should rather
        // update a settings model representing each option
        // TODO make base36-string from settings
        // TODO make settings from base36-string

        // General

        /// <summary>
        /// Indicates a N64 Rom to be randomized. Default true.
        /// </summary>
        public bool N64Rom { get; private set; } = true;

        /// <summary>
        /// 
        /// </summary>
        public bool WiiVirtualConsoleChannel { get; set; }

        /// <summary>
        /// Use Custom Item list for the logic.
        /// </summary>
        public bool UseCustomItemList { get; set; }


        // Random Elements

        /// <summary>
        /// Add songs to the randomization pool
        /// </summary>
        public bool AddSongs { get; set; }

        /// <summary>
        /// (KeySanity) Add dungeon items (maps, compasses, keys) to the randomization pool
        /// </summary>
        public bool AddDungeonItems { get; set; }

        /// <summary>
        /// Add shop items to the randomization pool
        /// </summary>
        public bool AddShopItems { get; set; }

        /// <summary>
        /// Add everything else to the randomization pool
        /// </summary>
        public bool AddOther { get; set; }

        /// <summary>
        /// Randomize the content of a bottle when catching (e.g. catching a fairy puts poe in bottle)
        /// </summary>
        public bool RandomizeBottleCatchContents { get; set; }

        /// <summary>
        /// Exclude song of soaring from randomization (it will be found in vanilla location)
        /// </summary>
        public bool ExcludeSongOfSoaring { get; set; }

        /// <summary>
        /// Gossip stones give hints on where to find items, and sometimes junk
        /// </summary>
        public bool EnableGossipHints { get; set; }

        /// <summary>
        /// Randomize which dungeon you appear in when entering one
        /// </summary>
        public bool RandomizeDungeonEntrances { get; set; }

        /// <summary>
        /// (Beta) Randomize enemies
        /// </summary>
        public bool RandomizeEnemies { get; set; }

        /// <summary>
        /// Randomize background music (includes bgm from other video games)
        /// </summary>
        public bool RandomizeBGM { get; set; }


        // Gimmicks

        /// <summary>
        /// Modifies the damage value when Link is damaged
        /// </summary>
        public DamageMode DamageMode { get; set; }

        /// <summary>
        /// Adds an additional effect when Link is damaged
        /// </summary>
        public DamageEffect DamageEffect { get; set; }

        /// <summary>
        /// Modifies Link's movement
        /// </summary>
        public MovementMode MovementMode { get; set; }

        // Comfort / Cosmetics

        /// <summary>
        /// Certain cutscenes will play shorter, or will be skipped
        /// </summary>
        public bool ShortenCutscenes { get; set; }

        /// <summary>
        /// Text is fast-forwarded
        /// </summary>
        public bool QuickText { get; set; }

        /// <summary>
        /// The color of Link's tunic
        /// </summary>
        public Color TunicColor { get; set; }

        /// <summary>
        /// Replaces Link's default model
        /// </summary>
        public Character Character { get; set; }

        /// <summary>
        /// Replaces Tatl's colors
        /// </summary>
        public TatlColorSchema TatlColorSchema { get; set; }

    }
}
