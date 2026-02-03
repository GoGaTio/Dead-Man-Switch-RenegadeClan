using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.Sound;
using static RimWorld.MechClusterSketch;

namespace DMSRC
{
	public class FloatMenuOptionProvider_OverseerMech : FloatMenuOptionProvider
	{
		protected override bool Drafted => true;

		protected override bool Undrafted => true;

		protected override bool Multiselect => false;

		protected override bool RequiresManipulation => true;

		protected override bool MechanoidCanDo => true;

		protected override bool CanSelfTarget => true;

		protected override bool AppliesInt(FloatMenuContext context)
		{
			return context.FirstSelectedPawn is OverseerMech;
		}

		public override IEnumerable<FloatMenuOption> GetOptions(FloatMenuContext context)
		{
			return base.GetOptions(context);
		}

		public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn, FloatMenuContext context)
		{
			if (context.FirstSelectedPawn is OverseerMech mech)
			{
				if (!clickedPawn.IsColonyMech)
				{
					yield break;
				}
				if(clickedPawn != mech)
				{
					if (clickedPawn.GetOverseer() != mech.Comp.dummyPawn)
					{
						if (!mech.Comp.Props.instantControl && !mech.CanReach(clickedPawn, PathEndMode.Touch, Danger.Deadly))
						{
							yield return new FloatMenuOption("CannotControlMech".Translate(clickedPawn.LabelShort) + ": " + "NoPath".Translate().CapitalizeFirst(), null);
						}
						else if (!MechanitorUtility.CanControlMech(mech.Comp.dummyPawn, clickedPawn))
						{
							AcceptanceReport acceptanceReport = MechanitorUtility.CanControlMech(mech.Comp.dummyPawn, clickedPawn);
							if (!acceptanceReport.Reason.NullOrEmpty())
							{
								yield return new FloatMenuOption("CannotControlMech".Translate(clickedPawn.LabelShort) + ": " + acceptanceReport.Reason, null);
							}
						}
						else
						{
							yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("ControlMech".Translate(clickedPawn.LabelShort), delegate
							{
								if (mech.Comp.Props.instantControl)
								{
									mech.Comp.Connect(clickedPawn, mech.Comp.dummyPawn);
									SoundDefOf.ControlMech_Complete.PlayOneShot(clickedPawn);
								}
								else
								{
									Job job = JobMaker.MakeJob(RCDefOf.DMSRC_ControlMech, clickedPawn);
									mech.jobs.TryTakeOrderedJob(job, JobTag.Misc);
								}
							}), mech, new LocalTargetInfo(clickedPawn));
						}
						yield return new FloatMenuOption("CannotDisassembleMech".Translate(clickedPawn.LabelCap) + ": " + "MustBeOverseer".Translate().CapitalizeFirst(), null);
					}
					else
					{
						yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("DisconnectMech".Translate(clickedPawn.LabelShort), delegate
						{
							MechanitorUtility.ForceDisconnectMechFromOverseer(clickedPawn);
						}, MenuOptionPriority.Low, null, null, 0f, null, null, playSelectionSound: true, -10), mech, new LocalTargetInfo(clickedPawn));
						if (!clickedPawn.IsFighting())
						{
							yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("DisassembleMech".Translate(clickedPawn.LabelCap), delegate
							{
								Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("ConfirmDisassemblingMech".Translate(clickedPawn.LabelCap) + ":\n" + (from x in MechanitorUtility.IngredientsFromDisassembly(clickedPawn.def)
																																								select x.Summary).ToLineList("  - "), delegate
																																								{
																																									mech.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.DisassembleMech, clickedPawn), JobTag.Misc);
																																								}, destructive: true));
							}, MenuOptionPriority.Low, null, null, 0f, null, null, playSelectionSound: true, -20), mech, new LocalTargetInfo(clickedPawn));
						}
					}
				}
				if (!MechRepairUtility.CanRepair(clickedPawn) || !mech.Comp.Props.canRepair)
				{
					yield break;
				}
				if (clickedPawn == mech)
				{
					yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("RepairThing".Translate(mech.LabelShort), delegate
					{
						Job job = JobMaker.MakeJob(RCDefOf.DMSRC_RepairMech, mech);
						mech.jobs.TryTakeOrderedJob(job, JobTag.Misc);
					}), mech, new LocalTargetInfo(clickedPawn));
					yield break;
				}
				if (!mech.CanReach(clickedPawn, PathEndMode.Touch, Danger.Deadly))
				{
					yield return new FloatMenuOption("CannotRepairMech".Translate(clickedPawn.LabelShort) + ": " + "NoPath".Translate().CapitalizeFirst(), null);
					yield break;
				}
				yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("RepairThing".Translate(clickedPawn.LabelShort), delegate
				{
					Job job = JobMaker.MakeJob(RCDefOf.DMSRC_RepairMech, clickedPawn);
					mech.jobs.TryTakeOrderedJob(job, JobTag.Misc);
				}), mech, new LocalTargetInfo(clickedPawn));
			}
		}
	}
}