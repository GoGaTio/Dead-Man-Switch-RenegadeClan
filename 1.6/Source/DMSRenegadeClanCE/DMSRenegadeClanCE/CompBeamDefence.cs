using CombatExtended;
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

namespace DMSRC.CE
{

    public class CompBeamDefence : DMSRC.CompBeamDefence
	{
		public override bool IsBulletAffected(Thing target)
		{
			ProjectileCE proj = target as ProjectileCE;
			if (proj == null)
			{
				return false;
			}
			if (proj.DamageDef == DamageDefOf.Vaporize || proj.DamageDef.ignoreShields)
			{
				return false;
			}
			if (!Props.interceptSameFaction && !parent.Faction.HostileTo(proj.launcher.Faction))
			{
				return false;
			}
			bool flag = false;
			foreach (InterceptProperties item in Props.intercepts)
			{
				if (item.CanIntercept(proj, proj.DamageAmount))
				{
					flag = true;
					break;
				}
			}
			if (flag && GenSight.LineOfSightToThing(parent.Position, target, parent.Map, true))
			{
				return true;
			}
			return false;
		}
	}
}