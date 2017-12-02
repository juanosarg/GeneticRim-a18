﻿using Harmony;
using RimWorld;
using System.Reflection;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Verse.AI;


// So, let's comment this code, since it uses Harmony and has moderate complexity

namespace DraftingPatcher
{
    //Setting the Harmony instance
    [StaticConstructorOnStartup]
    public class Main
    {
        static Main()
        {
            var harmony = HarmonyInstance.Create("com.geneticrim");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

     
    }
    /*This first Harmony postfix deals with adding a Pawn_DraftController if it detects the creature
     * belongs to the player and to the custom class CompDraftable. It also adds a Pawn_EquipmentTracker
     * because some ugly errors are produced otherwise, though it is basically unused
     * 
     */
    [HarmonyPatch(typeof(PawnComponentsUtility))]
    [HarmonyPatch("AddAndRemoveDynamicComponents")]
    public static class PawnComponentsUtility_AddAndRemoveDynamicComponents_Patch
    {
        [HarmonyPostfix]
        static void AddDraftability(Pawn pawn)   
        {           
            //These two flags detect if the creature is part of the colony and if it has the custom class
            bool flagIsCreatureMine = pawn.Faction != null && pawn.Faction.IsPlayer;
            bool flagIsCreatureDraftable = (pawn.TryGetComp<CompDraftable>() != null);

                 
            if (flagIsCreatureMine && flagIsCreatureDraftable)
            {
                //Log.Message("Patching "+ pawn.kindDef.ToString() + " with a draft controller and equipment tracker");
                //If everything goes well, add drafter and equipment to the pawn 
                pawn.drafter = new Pawn_DraftController(pawn);
                pawn.equipment = new Pawn_EquipmentTracker(pawn);
            }
        }
    }

    /*This second Harmony postfix adds or removes gizmos from the pawn's gizmo list (which is actually IEnumerable)
     * 
     */
    [HarmonyPatch(typeof(Pawn))]
    [HarmonyPatch("GetGizmos")]

    static class Pawn_GetGizmos_Patch
    {
        [HarmonyPostfix]
      
        public static void AddGizmo(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            //I want access to the pawn object, and want to modify the original method's result
            var pawn = __instance;
            var gizmos = __result.ToList();
            // First two flags detect if the pawn is mine, and if it is draftable
            bool flagIsCreatureMine = pawn.Faction != null && pawn.Faction.IsPlayer;
            bool flagIsCreatureDraftable = (pawn.TryGetComp<CompDraftable>()!=null);
            /* I do it this way to avoid errors due to pawn.TryGetComp<CompDraftable>() being null. The code inside
             * the conditional only executes if it isn't*/
            bool flagIsCreatureRageable = false;
            bool flagIsCreatureExplodable = false;
            if (flagIsCreatureDraftable){ 
                flagIsCreatureRageable = pawn.TryGetComp<CompDraftable>().GetRage;
                flagIsCreatureExplodable = pawn.TryGetComp<CompDraftable>().GetExplodable;
            }
            /*Is the creature mine, and draftable (the custom comp class)? Then display the drafting gizmo, called
             * Mind Control. It's action is just calling on toggle the Drafted method in the pawn's drafter, which
             * we initialized in the first Harmony Postfix
            */
            if ((pawn.drafter != null) && flagIsCreatureMine && flagIsCreatureDraftable)
            {
                Command_Action GR_Gizmo_MindControl = new Command_Action();
                GR_Gizmo_MindControl.action = delegate
                {
                    pawn.drafter.Drafted= !pawn.drafter.Drafted;
                };
                GR_Gizmo_MindControl.defaultLabel = "GR_CreatureMindControl".Translate();
                GR_Gizmo_MindControl.defaultDesc = "GR_CreatureMindControlDesc".Translate();
                GR_Gizmo_MindControl.icon = ContentFinder<Texture2D>.Get("ui/commands/ControlAnimal", true);
                gizmos.Insert(0, GR_Gizmo_MindControl);
            }

            if ((pawn.drafter != null) && flagIsCreatureDraftable && flagIsCreatureRageable && flagIsCreatureMine && pawn.drafter.Drafted)
            {
                Command_Target GR_Gizmo_AttackRage = new Command_Target();
                GR_Gizmo_AttackRage.defaultLabel = "GR_CreatureRageAttack".Translate();
                GR_Gizmo_AttackRage.defaultDesc = "GR_CreatureRageAttackDesc".Translate();
                GR_Gizmo_AttackRage.targetingParams = TargetingParameters.ForAttackAny();
                GR_Gizmo_AttackRage.icon = ContentFinder<Texture2D>.Get("Things/Item/AnimalPaws", true);

                GR_Gizmo_AttackRage.action = delegate (Thing target)
                {
                    IEnumerable<Pawn> enumerable = Find.Selector.SelectedObjects.Where(delegate (object x)
                    {
                        Pawn pawn2 = x as Pawn;
                        return pawn2 != null &&  pawn2.Drafted;
                    }).Cast<Pawn>();
                    foreach (Pawn current in enumerable)
                    {
                        Job job = new Job(JobDefOf.AttackMelee, target);
                        Pawn pawn2 = target as Pawn;
                        if (pawn2 != null)
                        {
                            job.killIncappedTarget = pawn2.Downed;
                        }
                        pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    }
                };
                gizmos.Insert(1, GR_Gizmo_AttackRage);

            }

            if ((pawn.drafter != null) && flagIsCreatureExplodable && flagIsCreatureMine)
            {
                Command_Action GR_Gizmo_Detonate = new Command_Action();
                GR_Gizmo_Detonate.action = delegate
                {
                    pawn.health.AddHediff(HediffDef.Named("GR_Kamikaze"));
                    HealthUtility.AdjustSeverity(pawn, HediffDef.Named("GR_Kamikaze"),1);
                };
                GR_Gizmo_Detonate.defaultLabel = "GR_DetonateChemfuel".Translate();
                GR_Gizmo_Detonate.defaultDesc = "GR_DetonateChemfuelDesc".Translate();
                GR_Gizmo_Detonate.icon = ContentFinder<Texture2D>.Get("UI/Commands/Detonate", true);
                gizmos.Insert(1, GR_Gizmo_Detonate);
            }

            __result = gizmos;
        }
    }

    [HarmonyPatch(typeof(FloatMenuMakerMap))]
    [HarmonyPatch("CanTakeOrder")]
    public static class FloatMenuMakerMap_CanTakeOrder_Patch
    {
        [HarmonyPostfix]
        public static void MakePawnControllable(Pawn pawn, ref bool __result)

        {
            bool flagIsCreatureMine = pawn.Faction != null && pawn.Faction.IsPlayer;
            bool flagIsCreatureDraftable = (pawn.TryGetComp<CompDraftable>() != null);

            if (flagIsCreatureDraftable && flagIsCreatureMine)
            {
                //Log.Message("You should be controllable now");
                __result = true;
            }
            
        }
    }









}
