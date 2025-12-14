using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;
using System.Xml.Xsl;
using DelaunatorSharp;
using Gilzoide.ManagedJobs;
using Ionic.Crc;
using Ionic.Zlib;
using JetBrains.Annotations;
using KTrie;
using LudeonTK;
using NVorbis.NAudioSupport;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using RimWorld.Utility;
using RuntimeAudioClipLoader;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;
using HarmonyLib;

namespace DMSRC
{
    public class CompProperties_Module : CompProperties
    {
        public int charges;

        public AbilityDef ability;

        public List<StatModifier> statFactors = new List<StatModifier>();

        public List<StatModifier> statOffsets = new List<StatModifier>();

        public List<string> tags = new List<string>();

        [NoTranslate]
        public string texPath;

        public bool graphicFromCharges = false;

        [NoTranslate]
        public List<string> texPaths = new List<string>();

        public Vector2 drawSize;

        public CompProperties_Module()
        {
            compClass = typeof(CompModule);
        }
    }

    public class CompModule : ThingComp
    {
        public CompProperties_Module Props => (CompProperties_Module)props;

        public int abilityCharges;

        public string nodeName;

        public string TexPath
        {
            get
            {
                if(!Props.texPaths.NullOrEmpty() && Props.graphicFromCharges)
                {
                    return Props.texPaths[abilityCharges];
				}
                return Props.texPath;
            }
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            abilityCharges = Props.charges;
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref abilityCharges, "DMSRC_abilityCharges");
        }
    }
}