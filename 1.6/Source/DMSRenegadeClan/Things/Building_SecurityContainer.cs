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
	public class ITab_ContentsSecurityContainer : ITab_ContentsBase
	{
		private List<Thing> listInt = new List<Thing>();

		private static readonly CachedTexture DropTex = new CachedTexture("UI/Buttons/Drop");

		public Building_SecurityContainer Container => SelThing as Building_SecurityContainer;

		public override bool IsVisible => Container.opened;

        public override IList<Thing> container
		{
			get
			{
				Building_SecurityContainer building_SecurityContainer = Container;
				listInt.Clear();
				if (building_SecurityContainer != null && building_SecurityContainer.innerContainer != null)
				{
					listInt = building_SecurityContainer.innerContainer.ToList();
				}
				return listInt;
			}
		}

		public ITab_ContentsSecurityContainer()
		{
			labelKey = "TabCasketContents";
			containedItemsKey = "ContainedItems";
			canRemoveThings = true;
		}

		protected override void DoItemsLists(Rect inRect, ref float curY)
		{
			ListContainedBooks(inRect, container, ref curY);
		}

		private void ListContainedBooks(Rect inRect, IList<Thing> things, ref float curY)
		{
			GUI.BeginGroup(inRect);
			float num = curY;
			Widgets.ListSeparator(ref curY, inRect.width, containedItemsKey.Translate());
			Rect rect = new Rect(0f, num, inRect.width, curY - num - 3f);
			bool flag = false;
			for (int i = 0; i < things.Count; i++)
			{
				Thing thing = things[i];
				DoRow(thing, inRect.width, i, ref curY);
				flag = true;
			}
			if (!flag)
			{
				Widgets.NoneLabel(ref curY, inRect.width);
			}
			GUI.EndGroup();
		}

		private void DoRow(Thing thing, float width, int i, ref float curY)
		{
			Rect rect = new Rect(0f, curY, width, 28f);
			Widgets.InfoCardButton(0f, curY, thing);
			if (Mouse.IsOver(rect))
			{
				Widgets.DrawHighlightSelected(rect);
			}
			else if (i % 2 == 1)
			{
				Widgets.DrawLightHighlight(rect);
			}
			Rect rect2 = new Rect(rect.width - 24f, curY, 24f, 24f);
			if (Widgets.ButtonImage(rect2, DropTex.Texture))
			{
				if (!Container.OccupiedRect().AdjacentCells.Where((IntVec3 x) => x.Walkable(Container.Map)).TryRandomElement(out var result))
				{
					result = Container.Position;
				}
				Container.GetDirectlyHeldThings().TryDrop(thing, result, Container.Map, ThingPlaceMode.Near, thing.stackCount, out var resultingThing);
				if (resultingThing.TryGetComp(out CompForbiddable comp))
				{
					comp.Forbidden = false;
				}
			}
			TooltipHandler.TipRegionByKey(rect2, "DMSRC_EjectThingTooltip");
			Widgets.ThingIcon(new Rect(24f, curY, 28f, 28f), thing);
			Rect rect3 = new Rect(60f, curY, rect.width - 36f, rect.height);
			rect3.xMax = rect2.xMin;
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(rect3, thing.LabelCap.Truncate(rect3.width));
			Text.Anchor = TextAnchor.UpperLeft;
			if (Mouse.IsOver(rect))
			{
				TargetHighlighter.Highlight(thing, arrow: true, colonistBar: false);
				TooltipHandler.TipRegion(rect, thing.DescriptionDetailed);
			}
			curY += 28f;
		}
	}

	public class Building_SecurityContainer : Building, IThingHolder, IHackable
	{
		public ThingOwner innerContainer;

		public bool opened;

		public string openedSignal;

		private CompSecurityContainer comp;

		public CompSecurityContainer Comp
        {
            get
            {
				if(comp == null)
                {
					comp = this.GetComp<CompSecurityContainer>();
                }
				return comp;
            }
        }

		public void OnLockedOut(Pawn pawn = null)
		{
		}

		public void OnHacked(Pawn pawn = null)
		{
			if (!opened)
			{
				Open(pawn);
			}
		}

		public bool HasAnyContents => innerContainer.Count > 0;

		public Building_SecurityContainer()
		{
			innerContainer = new ThingOwner<Thing>(this, oneStackOnly: false);
		}

		public ThingOwner GetDirectlyHeldThings()
		{
			return innerContainer;
		}

		public void GetChildHolders(List<IThingHolder> outChildren)
		{
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo gizmo in base.GetGizmos())
			{
				yield return gizmo;
			}
			if (DebugSettings.ShowDevGizmos && !opened)
			{
				yield return new Command_Action
				{
					defaultLabel = "DEV: Open",
					action = Open
				};
			}
		}

		public void Open() => Open(null);

		public void Open(Pawn pawn)
        {
			opened = true;
			Comp.Props.completedSound?.PlayOneShot(this);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Deep.Look(ref innerContainer, "DMSRC_innerContainer", this);
			Scribe_Values.Look(ref opened, "DMSRC_opened", defaultValue: false);
			Scribe_Values.Look(ref openedSignal, "DMSRC_openedSignal");
		}

		public override AcceptanceReport ClaimableBy(Faction by)
		{
			if (!opened)
			{
				return false;
			}
			return base.ClaimableBy(by);
		}

		public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
		{
			GenExplosion.DoExplosion(this.Position, Map, 1.9f, DamageDefOf.Vaporize, this, ignoredThings: new List<Thing>() { this });
			base.Destroy(mode);
			innerContainer.ClearAndDestroyContents();
		}
	}
}