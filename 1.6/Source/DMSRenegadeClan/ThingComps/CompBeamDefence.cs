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
	public class InterceptProperties
    {
		public InterceptProperties()
        {

        }

		public Type type;

		public float maxExplosionRadius;

		public int maxDamage;

		public bool CanIntercept(Thing proj, float damageAmount)
        {
			if (maxDamage > 0 && damageAmount > maxDamage)
			{
				return false;
			}
			if (maxExplosionRadius > 0 && proj.def.projectile.explosionRadius > maxExplosionRadius)
			{
				return false;
			}
			if (type.IsAssignableFrom(proj.def.thingClass))
            {
				return true;
            }
			return false;
        }
	}
    public class CompProperties_BeamDefence : CompProperties
    {
		public float range;

		public ThingDef mote;

		public bool activeWhileSleep;

		public bool interceptSameFaction;

		public int delay = 10;

		public int delayIntercepted = 3;

		public int maxCharge = 100;

		public int chargeRegenInterval = 120;

		public int inactiveRegenOffset = 0;

		public int chargeLossPerIntercept = 5;

		public Color barColor;

		public Color barColorAlt;

		public SoundDef interceptSound;

		public List<Vector3> offsetSouth = new List<Vector3>();

		public List<Vector3> offsetEast = new List<Vector3>();

		public List<Vector3> offsetNorth = new List<Vector3>();

		public List<Vector3> offsetWest = new List<Vector3>();

		public Vector3 offset = Vector3.zero;

		public bool useDynamicOffsets = false;

		public bool showToggleButton = true;

		public List<InterceptProperties> intercepts = new List<InterceptProperties>();

		public float maxExplosionRadius = 2.9f;

		public int maxDamage = 50;

        public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);
			if(inactiveRegenOffset >= chargeRegenInterval)
            {
				inactiveRegenOffset = chargeRegenInterval - 1;
			}
			string s = parentDef.defName + " CompProperties_BeamDefence report:";
			foreach(var item in intercepts)
			{
				s += "\n " + item.type.ToString() + " maxDamage: " + item.maxDamage + " maxExplosionRadius: " + item.maxExplosionRadius;
			}
			Log.Message(s);
        }

        public Vector3 Offset(Rot4 rot)
        {
            if (useDynamicOffsets)
            {
				if (rot == Rot4.North)
				{
					return offsetNorth.RandomElement();
				}
				if (rot == Rot4.South)
				{
					return offsetSouth.RandomElement();
				}
				if (rot == Rot4.East)
				{
					return offsetEast.RandomElement();
				}
				return offsetWest.RandomElement();
			}
			return offset;
		}

		public CompProperties_BeamDefence()
        {
            compClass = typeof(CompBeamDefence);
        }
    }

    public class CompBeamDefence : ThingComp
    {
        public CompProperties_BeamDefence Props => (CompProperties_BeamDefence)props;

		public bool shouldBeActive = true;

		public bool active = true;

		public int delay;

		public int charge;

		public int range;

		private bool interceptedLastTime;

		private MoteDualAttached mote;

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref active, "active", true);
			Scribe_Values.Look(ref interceptedLastTime, "interceptedLastTime", defaultValue: false);
			Scribe_Values.Look(ref charge, "charge");
			Scribe_Values.Look(ref delay, "delay");
			Scribe_Values.Look(ref shouldBeActive, "shouldBeActive", true);
		}

		public override void PostPostMake()
        {
            base.PostPostMake();
			charge = Props.maxCharge;
		}

		private CompCanBeDormant compDormant;

		private CompPowerTrader compPowerTrader;

		public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
		{
			base.PostDeSpawn(map, mode);
		}

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
			compDormant = parent.GetComp<CompCanBeDormant>();
			compPowerTrader = parent.GetComp<CompPowerTrader>();
			range = Mathf.CeilToInt(Props.range);
			if (compPowerTrader != null && interceptedLastTime)
			{
				compPowerTrader.PowerOutput = 0f - compPowerTrader.Props.PowerConsumption;
			}
		}

        public override void PostDrawExtraSelectionOverlays()
		{
			if (!Find.Selector.IsSelected(parent))
			{
				return;
			}
			GenDraw.DrawRadiusRing(parent.Position, Props.range);
		}

		public override void CompTick()
		{
			if (shouldBeActive && active)
			{
				if (delay > 0)
				{
					delay--;
				}
				else
				{
					delay = Props.delay;
					if ((Props.activeWhileSleep || compDormant?.Awake != false) && parent.Spawned && compPowerTrader?.PowerOn != false)
					{
						IntVec3 pos = parent.Position;
						foreach (IntVec3 cell in new CellRect(pos.x, pos.z, 1, 1).ExpandedBy(range).ClipInsideMap(parent.Map))
						{
							List<Thing> list = parent.Map.thingGrid.ThingsListAt(cell);
							for (int i = 0; i < list.Count; i++)
							{
								Thing thing = list[i];
								if (Vector3.Distance(thing.DrawPos, parent.TrueCenter()) < Props.range && IsBulletAffected(thing))
								{
									Intercept(thing);
									delay = Props.delayIntercepted;
									if (compPowerTrader != null)
									{
										compPowerTrader.PowerOutput = 0f - compPowerTrader.Props.PowerConsumption;
									}
									return;
								}
							}
						}
						if (compPowerTrader != null)
						{
							compPowerTrader.PowerOutput = 0f - compPowerTrader.Props.idlePowerDraw;
						}
					}
				}
			}
			if (charge < Props.maxCharge && parent.IsHashIntervalTick(Props.chargeRegenInterval - (active ? 0 : Props.inactiveRegenOffset)))
			{
				charge++;
				if(charge == Props.maxCharge)
                {
					active = true;
                }
			}
		}

		public void Intercept(Thing proj)
		{
			if (mote == null || mote.Destroyed)
			{
				mote = MoteMaker.MakeInteractionOverlay(Props.mote, parent, new TargetInfo(proj.Position, parent.Map));
			}
			mote.UpdateTargets(parent, new TargetInfo(proj.Position, parent.Map), Props.Offset(parent.Rotation), Vector3.zero);
			mote.Maintain();
			proj.Destroy(DestroyMode.KillFinalize);
			Props.interceptSound?.PlayOneShot(parent);
			charge -= Props.chargeLossPerIntercept;
			if(charge <= 0)
            {
				charge = 0;
				active = false;
			}
		}

		public virtual bool IsBulletAffected(Thing target)
		{
			Projectile proj = target as Projectile;
			if (proj == null)
			{
				return false;
			}
			if(proj.DamageDef == DamageDefOf.Vaporize || proj.DamageDef.ignoreShields)
			{
				return false;
			}
			if (!Props.interceptSameFaction && !parent.Faction.HostileTo(proj.Launcher.Faction))
			{
				return false;
			}
			bool flag = false;
			foreach(InterceptProperties item in Props.intercepts)
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

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			if (Find.Selector.SingleSelectedThing == parent && (parent.Faction == Faction.OfPlayer || DebugSettings.ShowDevGizmos))
			{
				yield return new BeamDefenceGizmo(this, "Beam defence", "n", Props.barColor, Props.barColorAlt);
			}
		}

		
	}
	[StaticConstructorOnStartup]
	public class BeamDefenceGizmo : Gizmo
	{
		private CompBeamDefence comp;

		private Texture2D BarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.34f, 0.42f, 0.43f));

		private Texture2D BarTexAlt = SolidColorMaterials.NewSolidColorTexture(new Color(0.34f, 0.42f, 0.43f));

		private static readonly Texture2D EmptyBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.03f, 0.035f, 0.05f));

		private static List<float> bandPercentages;

		private string title;

		private string tooltip;

		public BeamDefenceGizmo(CompBeamDefence comp, string title, string tooltip, Color barColor, Color barColorAlt)
		{
			this.comp = comp;
			this.tooltip = tooltip;
			this.title = title;
			BarTex = SolidColorMaterials.NewSolidColorTexture(barColor);
			BarTexAlt = SolidColorMaterials.NewSolidColorTexture(barColorAlt);
			if (bandPercentages == null)
			{
				bandPercentages = new List<float>();
				int num = 12;
				for (int i = 0; i <= num; i++)
				{
					float item = 1f / (float)num * (float)i;
					bandPercentages.Add(item);
				}
			}
		}

		public override float GetWidth(float maxWidth)
		{
			return 160f;
		}

		public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
		{
			Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
			Rect rect2 = rect.ContractedBy(10f);
			Widgets.DrawWindowBackground(rect);
			Text.Font = GameFont.Small;
			TaggedString labelCap = title;
			float height = Text.CalcHeight(labelCap, rect2.width);
			Rect rect3 = new Rect(rect2.x, rect2.y, rect2.width, height);
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(rect3, labelCap);
			Text.Anchor = TextAnchor.UpperLeft;
			float num = rect2.height - rect3.height;
			float num2 = num - 4f;
			float num3 = (num - num2) / 2f;
			Rect rect4 = new Rect(rect2.x, rect3.yMax + num3, rect2.width, num2);
			Widgets.FillableBar(rect4, (float)comp.charge / (float)comp.Props.maxCharge, comp.active ? BarTex : BarTexAlt, EmptyBarTex, true);
			Rect rect5 = new Rect(rect2.xMax - height, rect3.y, height, height);
			Text.Anchor = TextAnchor.MiddleCenter;
			rect4.y -= 2f;
			Widgets.Label(rect4, comp.charge + " / " + comp.Props.maxCharge);
			Text.Anchor = TextAnchor.UpperLeft;
			TooltipHandler.TipRegion(rect4, () => tooltip, Gen.HashCombineInt(comp.GetHashCode(), 34242369));
			Widgets.Checkbox(new Vector2(rect.x + (rect.width - 24f), rect.y), ref comp.shouldBeActive);
			return new GizmoResult(GizmoState.Clear);
		}
	}
}