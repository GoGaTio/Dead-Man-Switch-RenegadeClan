using HarmonyLib;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using RimWorld.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
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
using static HarmonyLib.Code;

namespace DMSRC
{
	public class Firecracker : Bullet
	{
		private Vector3 lastDrawPos = new Vector3(-999,-999,-999);

		protected override void Tick()
		{
			if (Spawned)
			{
				ThrowSparks(DrawPos);
			}
			base.Tick();
		}

		public void ThrowSparks(Vector3 drawPos)
		{
			if (lastDrawPos.x > 0 &&Position.ShouldSpawnMotesAt(MapHeld))
			{
				Vector3 offset = drawPos - lastDrawPos;
				for (int i = 0; i < 3; i++)
				{
					FleckCreationData dataStatic = FleckMaker.GetDataStatic(lastDrawPos + (offset * Rand.Value), MapHeld, RCDefOf.DMSRC_Fleck_SparksFast);
					dataStatic.scale = new FloatRange(0.3f, 0.6f).RandomInRange;
					dataStatic.rotationRate = 0;
					dataStatic.velocityAngle = new FloatRange(0, 360).RandomInRange;
					dataStatic.velocitySpeed = new FloatRange(2, 3).RandomInRange;
					MapHeld.flecks.CreateFleck(dataStatic);
				}
			}
			lastDrawPos = drawPos;
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			//lastDrawPos = DrawPos;
		}

		protected override void Impact(Thing hitThing, bool blockedByShield = false)
		{
			Map map = base.Map;
			IntVec3 position = hitThing?.Position ?? base.Position;
			bool instigatorGuilty = !(launcher is Pawn pawn) || !pawn.Drafted;
			DamageInfo dinfo = new DamageInfo(RCDefOf.DMSRC_Firecracker, RCDefOf.DMSRC_Firecracker.defaultDamage, RCDefOf.DMSRC_Firecracker.defaultArmorPenetration, instigator: launcher, weapon: equipmentDef, intendedTarget: intendedTarget.Thing, instigatorGuilty: instigatorGuilty);
			base.Impact(hitThing);
			float angle = (origin - position.ToVector3Shifted()).AngleFlat();
			dinfo.SetAngle(Vector3Utility.FromAngleFlat(angle + 180f));
			if (hitThing?.FireBulwark == true || position.GetEdifice(map)?.FireBulwark == true)
			{
				AffectCell(position);
				IntVec3 firstCell = FromAngleFlat(angle);
				angle = firstCell.AngleFlat;
				firstCell += position;
				AffectCell(firstCell);
				if (firstCell.GetEdifice(map)?.FireBulwark != true)
				{
					for (int i = -1; i < 2; i += 2)
					{
						float workingAngle = angle;
						for (int j = 1; j < 4; j++)
						{
							workingAngle += i * 45;
							IntVec3 c = FromAngleFlat(workingAngle) + position;
							AffectCell(c);
							if (c.GetEdifice(map)?.FireBulwark == true)
							{
								break;
							}
						}
					}
				}
			}
			else
			{
				foreach (IntVec3 c in CellRect.FromCell(position).ExpandedBy(1).ClipInsideMap(map).Cells.ToList())
				{
					AffectCell(c);
				}
			}
			void AffectCell(IntVec3 c)
			{
				foreach (Thing t in c.GetThingList(map).ToList())
				{
					t.TakeDamage(dinfo);
				}
				if (Rand.Chance(FireUtility.ChanceToStartFireIn(c, map) * 2f))
				{
					FireUtility.TryStartFireIn(c, map, Rand.Range(0.2f, 0.6f), launcher);
				}
			}
		}


		private IntVec3 FromAngleFlat(float angle)
		{
			angle = GenMath.PositiveMod(angle, 360f);
			if (angle < 22.5f)
			{
				return IntVec3.North;
			}
			if (angle < 67.5f)
			{
				return IntVec3.NorthEast;
			}
			if (angle < 112.5f)
			{
				return IntVec3.East;
			}
			if (angle < 157.5f)
			{
				return IntVec3.SouthEast;
			}
			if (angle < 202.5f)
			{
				return IntVec3.South;
			}
			if (angle < 247.5f)
			{
				return IntVec3.SouthWest;
			}
			if (angle < 292.5f)
			{
				return IntVec3.West;
			}
			if (angle < 337.5f)
			{
				return IntVec3.NorthWest;
			}
			return IntVec3.North;
		}
	}
}
