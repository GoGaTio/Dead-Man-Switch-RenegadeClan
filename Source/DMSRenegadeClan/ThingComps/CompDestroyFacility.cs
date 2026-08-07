using DelaunatorSharp;
using Gilzoide.ManagedJobs;
using HarmonyLib;
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
using static System.Net.Mime.MediaTypeNames;

namespace DMSRC
{
    public class CompProperties_DestroyFacility : CompProperties
    {
		public bool onlyWhenAllDestroyed = false;

        public int ticksToDestroy = 1;

        public CompProperties_DestroyFacility()
        {
            compClass = typeof(CompDestroyFacility);
        }
    }

    public class CompDestroyFacility : ThingComp
    {
        public CompProperties_DestroyFacility Props => (CompProperties_DestroyFacility)props;

		public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
		{
			base.Notify_Killed(prevMap, dinfo);
            if(prevMap != null && parent.Position.IsValid)
            {
                if (Props.onlyWhenAllDestroyed)
                {
					List<Thing> list = prevMap.listerThings.AllThings;
					for (int i = 0; i < list.Count; i++)
					{
                        if(list[i] is ThingWithComps twc && twc.def == parent.def && !twc.Destroyed && twc.HasComp<CompDestroyFacility>())
                        {
                            return;
                        }
					}
				}
				FacilityDestroyer item = (FacilityDestroyer)ThingMaker.MakeThing(RCDefOf.DMSRC_FacilityDestroyer);
                item.ticksLeft = Props.ticksToDestroy;
                GenSpawn.Spawn(item, parent.Position, prevMap);
			}
		}
    }
}