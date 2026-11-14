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
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using RimWorld.Utility;
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
    /*public class CompRealityRiftOpener : ThingComp
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo item in base.CompGetGizmosExtra())
            {
                yield return item;
            }
            Command_Action command_Action = new Command_Action
            {
                defaultLabel = "CommandLaunchGroup".Translate(),
                defaultDesc = "CommandLaunchGroupDesc".Translate(),
                icon = LaunchCommandTex,
                alsoClickIfOtherInGroupClicked = false,
                action = delegate
                {
                    if (AnyInGroupHasAnythingLeftToLoad)
                    {
                        TaggedString text = "ConfirmSendNotCompletelyLoadedLaunchable".Translate() + ":\n";
                        text += Transporter.leftToLoad.Select((TransferableOneWay x) => x.AnyThing.LabelCap).ToLineList(" -");
                        text += "\n\n" + "ConfirmSendLaunchAnyway".Translate();
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, delegate
                        {
                            StartChoosingDestination(TryLaunch);
                        }));
                    }
                    else
                    {
                        StartChoosingDestination(TryLaunch);
                    }
                }
            };
            AcceptanceReport acceptanceReport = CanLaunch();
            if (!acceptanceReport.Accepted)
            {
                command_Action.Disable(acceptanceReport.Reason);
            }
            yield return command_Action;
            if (DebugSettings.ShowDevGizmos)
            {
                Command_Action command_Action2 = new Command_Action();
                command_Action2.defaultLabel = "DEV: End cooldown";
                command_Action2.action = delegate
                {
                    lastLaunchTick = Find.TickManager.TicksGame - Props.cooldownTicks;
                };
                yield return command_Action2;
            }
        }

        public void StartChoosingDestination(Action<PlanetTile, TransportersArrivalAction> launchAction, float? overrideFuelLevel = null)
        {
            PlanetTile tile = parent.Tile;
            CameraJumper.TryJump(CameraJumper.GetWorldTarget(new GlobalTargetInfo(tile)));
            Find.WorldSelector.ClearSelection();
            Find.WorldTargeter.BeginTargeting((GlobalTargetInfo t) => ChoseWorldTarget(t, launchAction, overrideFuelLevel), canTargetTiles: true, TargeterMouseAttachment, !CaravanShuttleUtility.IsCaravanShuttle(Transporter), delegate
            {
                PlanetTile planetTile;
                if (cachedLayer != Find.WorldSelector.SelectedLayer || cachedOrigin != tile)
                {
                    cachedLayer = Find.WorldSelector.SelectedLayer;
                    cachedOrigin = tile;
                    planetTile = (cachedClosest = Find.WorldSelector.SelectedLayer.GetClosestTile_NewTemp(tile));
                }
                else
                {
                    planetTile = cachedClosest;
                }
                int num = MaxLaunchDistanceEver(planetTile.Layer);
                GenDraw.DrawWorldRadiusRing(planetTile, num, CompPilotConsole.GetThrusterRadiusMat(planetTile));
                if (Refuelable != null)
                {
                    int num2 = MaxLaunchDistanceAtFuelLevel(FuelLevel, PlanetLayer.Selected);
                    if (num2 < num)
                    {
                        GenDraw.DrawWorldRadiusRing(planetTile, num2, CompPilotConsole.GetFuelRadiusMat(planetTile));
                    }
                }
            }, (GlobalTargetInfo target) => TargetingLabelGetter(target, tile, MaxLaunchDistanceEver(cachedClosest.Layer), TransportersInGroup, launchAction, this, overrideFuelLevel), null, tile, showCancelButton: true);
        }

        private bool ChoseWorldTarget(GlobalTargetInfo target, Action<PlanetTile, TransportersArrivalAction> launchAction, float? overrideFuelLevel = null)
        {
            if (!Transporter.LoadingInProgressOrReadyToLaunch)
            {
                return true;
            }
            cachedClosest = (cachedOrigin = PlanetTile.Invalid);
            cachedLayer = null;
            PlanetTile tile = parent.Tile;
            return ChoseWorldTarget(target, tile, TransportersInGroup, MaxLaunchDistanceEver(target.Tile.Layer), launchAction, this, overrideFuelLevel);
        }

        public static bool ChoseWorldTarget(GlobalTargetInfo target, PlanetTile tile, IEnumerable<IThingHolder> pods, int maxLaunchDistance, Action<PlanetTile, TransportersArrivalAction> launchAction, CompLaunchable launchable, float? overrideFuelLevel = null)
        {
            if (!target.IsValid)
            {
                Messages.Message("MessageTransportPodsDestinationIsInvalid".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            if (target.HasWorldObject && !target.WorldObject.def.validLaunchTarget)
            {
                Messages.Message("MessageWorldObjectIsInvalid".Translate(target.WorldObject.Named("OBJECT")), MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            if (ModsConfig.OdysseyActive && target.HasWorldObject && target.WorldObject.RequiresSignalJammerToReach)
            {
                Messages.Message("TransportPodDestinationRequiresSignalJammer".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            float fuelLevel = overrideFuelLevel ?? launchable?.FuelLevel ?? (-1f);
            int num = Find.WorldGrid.TraversalDistanceBetween(tile, target.Tile, passImpassable: true, int.MaxValue, canTraverseLayers: true);
            float num2 = ((float?)launchable?.MaxLaunchDistanceAtFuelLevel(fuelLevel, target.Tile.Layer)) ?? (-1f);
            float fuelRequired = launchable?.FuelNeededToLaunchAtDist(num, target.Tile.Layer) ?? (-1f);
            if (maxLaunchDistance >= 0 && num > maxLaunchDistance)
            {
                Messages.Message("TransportPodDestinationBeyondMaximumRange".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            if (num2 >= 0f && (float)num > num2)
            {
                Messages.Message("TransportPodNotEnoughFuel".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            bool flag = launchable?.Shuttle != null;
            List<FloatMenuOption> list = ((launchable != null) ? launchable.GetTransportersFloatMenuOptionsAt(target.Tile, launchAction, flag) : GetOptionsForTile(target.Tile, pods, launchAction)).ToList();
            if (!list.Any())
            {
                if (Find.World.Impassable(target.Tile))
                {
                    Messages.Message("MessageTransportPodsDestinationIsInvalid".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                    return false;
                }
                if (flag && fuelLevel < fuelRequired * 2f)
                {
                    WorldObject worldObject = target.WorldObject;
                    if (worldObject == null || worldObject.Faction?.IsPlayer != true)
                    {
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("ConfirmShuttleNotEnoughFuelToReturn".Translate(fuelLevel, fuelRequired * 2f), delegate
                        {
                            launchAction(target.Tile, null);
                        }));
                        return true;
                    }
                }
                launchAction(target.Tile, null);
                return true;
            }
            if (flag && fuelLevel < fuelRequired * 2f)
            {
                WorldObject worldObject2 = target.WorldObject;
                if (worldObject2 == null || worldObject2.Faction?.IsPlayer != true)
                {
                    for (int num3 = 0; num3 < list.Count; num3++)
                    {
                        Action oldAction = list[num3].action;
                        list[num3].action = delegate
                        {
                            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("ConfirmShuttleNotEnoughFuelToReturn".Translate(fuelLevel, fuelRequired * 2f), oldAction));
                        };
                    }
                }
            }
            if (list.Count == 1)
            {
                if (!list.First().Disabled)
                {
                    list.First().action();
                    return true;
                }
                return false;
            }
            Find.WindowStack.Add(new FloatMenu(list));
            return false;
        }
    }*/
}
