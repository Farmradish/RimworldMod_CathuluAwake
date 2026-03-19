using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using UnityEngine.Assertions.Must;
using Verse;
using Verse.AI;
using NyaronCathulu;

namespace NyaronCathuluHarmony
{
    // HarmonyPatches 클래스는 게임 내의 특정 메서드에 접근/ 수정하기 위한 패치를 모아둔 클래스입니다. Harmony 라이브러리에 의존합니다.
    public class CathuluHarmony
    {
        [StaticConstructorOnStartup]
        public static class HarmonyPatches
        {
            static HarmonyPatches()
            {
                Harmony harmony = new Harmony("com.farmradish.cathulu");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
        }
        // Dialog_EntityCodex타입(클래스)를 찾아, 해당 클래스의 생성자(Constructor)를 패치합니다. 해당 패치는 인게임 내에서 실체 도감을 열 때, 해금되지 않은 컨텐츠를 숨기는 역할을 합니다.
        [HarmonyPatch(typeof(Dialog_EntityCodex), MethodType.Constructor, new[] { typeof(EntityCodexEntryDef) })]
        public static class Patch_Dialog_EntityCodex_Constructor
        {
            public static void Postfix(Dialog_EntityCodex __instance)
            {
                // 1. 앞서 만든 전역 변수(GameComponent)를 불러옵니다.
                GameComponent_CathuluAwakening gameComponent = Current.Game.GetComponent<GameComponent_CathuluAwakening>();

                // 2. 콘텐츠가 아직 해금되지 않았다면 숨김 처리를 진행합니다.
                if (gameComponent == null || !gameComponent.isContentUnlocked)
                {
                    // XML에 추가했던 "캣후루의 권속" 카테고리 Def를 가져옵니다.
                    EntityCategoryDef category = DefDatabase<EntityCategoryDef>.GetNamedSilentFail("Nr_Cathulu");

                    if (category != null)
                    {
                        // Traverse를 사용하여 Dialog_EntityCodex 인스턴스의 private 필드에 접근합니다.
                        var instanceTraverse = Traverse.Create(__instance);
                        var categoriesInOrder = instanceTraverse.Field<List<EntityCategoryDef>>("categoriesInOrder").Value;
                        var entriesByCategory = instanceTraverse.Field<Dictionary<EntityCategoryDef, List<EntityCodexEntryDef>>>("entriesByCategory").Value;
                        var categoryRectSizes = instanceTraverse.Field<Dictionary<EntityCategoryDef, float>>("categoryRectSizes").Value;

                        // 4. 리스트와 딕셔너리에서 해당 카테고리를 완전히 제거합니다.
                        if (categoriesInOrder != null && categoriesInOrder.Contains(category))
                        {
                            categoriesInOrder.Remove(category);
                            entriesByCategory?.Remove(category);
                            categoryRectSizes?.Remove(category);
                        }
                    }
                }
            }
        }
        // 식사,약물복용 관련 패치입니다. Pawn이 음식을 먹거나 약물을 복용할 때마다 '불길한 빛'의 진행도가 서서히 증가하고, 일정 확률로 무드 디버프와 기절이 발생하도록 합니다.
        [HarmonyPatch(typeof(Thing), "Ingested")]
        public static class Patch_Thing_Ingested
        {
            public static void Postfix(Thing __instance, Pawn ingester)
            {
                if (ingester == null || !ingester.IsColonist || ingester.Map == null) return;

                var gc = ingester.Map.gameConditionManager.GetActiveCondition<GameCondition_OminousLight>();
                if (gc != null)
                {
                    OminousTriggerAction type = __instance.def.IsDrug ? OminousTriggerAction.Drug : OminousTriggerAction.Eat;
                    // 한번 먹을 때 진행도 0.3% 증가, 무드 디버프/기절 확률 3%
                    gc.Notify_ActionPerformed(ingester, type, 0.3f, 0.03f);
                }
            }
        }

        // Pawn이 특정Job을 진행중일 때, 250틱마다 '불길한 빛'의 진행도가 서서히 증가하고, 일정 확률로 무드 디버프와 기절이 발생하도록 하는 패치입니다. (예: 수면, 여가활동 등)

        [HarmonyPatch(typeof(Pawn_JobTracker), "JobTrackerTick")]
        public static class Patch_Pawn_JobTracker_Tick
        {
            public static void Postfix(Pawn_JobTracker __instance, Pawn ___pawn)
            {
                if (___pawn == null || !___pawn.IsColonist || ___pawn.Map == null) return;

                // 250틱(약 4초)에 한 번만 체크하여 성능 최적화
                if (___pawn.IsHashIntervalTick(250))
                {
                    var gc = ___pawn.Map.gameConditionManager.GetActiveCondition<GameCondition_OminousLight>();
                    if (gc != null && ___pawn.CurJob != null)
                    {
                        
                        OminousTriggerAction? currentAction = null;
                        float defaultProgress = 0.1f;
                        int joyMultiplier = 2; // 행동에 따른 진행도 증가량과 확률을 조절하기 위한 변수
                        // 현재 Pawn의 직업(Job)을 검사하여 어떤 행동 중인지 판별
                        if (___pawn.jobs.curDriver.asleep)
                        {
                            currentAction = OminousTriggerAction.Sleep;

                        }
                        else if (___pawn.CurJob.def.joyKind != null || ___pawn.CurJob.def == JobDefOf.Reading || ___pawn.CurJob.def == JobDefOf.Meditate)
                        {
                            currentAction = OminousTriggerAction.Joy;
                            defaultProgress = defaultProgress * joyMultiplier;

                        }
                        // 행동이 감지되었다면
                        if (currentAction.HasValue)
                        {
                            // 250틱마다 서서히 진행도 증가 (0.1%), 무드 디버프/기절 확률 2%
                            gc.Notify_ActionPerformed(___pawn, currentAction.Value, 0.1f, 0.02f);
                        }
                    }
                }
            }
        }

        //Building_Door를 패치하여, Pawn이 접근하여 문을 열 때마다, 일정 확률로 무드 디버프와 기절이 발생하도록 하는 패치입니다.

        [HarmonyPatch(typeof(Building_Door), "Notify_PawnApproaching")]
        public static class Patch_Building_Door_Notify_PawnApproaching
        {
            public static void Postfix(Building_Door __instance, Pawn p)
            {
                // Pawn이 유효한 정착민인지 확인
                if (p == null || !p.IsColonist || __instance.Map == null) return;

                var gc = __instance.Map.gameConditionManager.GetActiveCondition<GameCondition_OminousLight>();
                if (gc != null)
                {
                    gc.Notify_ActionPerformed(p, OminousTriggerAction.OpenDoor, 0.1f, 0.02f);
                }
            }
        }
        // 특정 Hediff로 습득하는 어빌리티가, Pawn을 복사하는 행동에 의해 복사될 수 없도록 패치합니다.
        [HarmonyPatch(typeof(AnomalyUtility), nameof(AnomalyUtility.TryDuplicatePawn))]
        public static class Patch_AnomalyUtility_TryDuplicatePawn
        {
            // 원본 메서드가 bool을 반환하므로 __result로 복제 성공 여부를 확인하고,
            // out 매개변수였던 duplicatePawn을 ref로 받아와서 조작합니다.
            public static void Postfix(bool __result, ref Pawn duplicatePawn)
            {
                // 1. 메서드가 성공적으로 실행되었고(__result == true), 복제된 Pawn과 능력 트래커가 존재하는지 확인
                if (__result && duplicatePawn != null && duplicatePawn.abilities != null)
                {
                    // 2. 우리가 만든 커스텀 능력 정의를 가져옵니다.
                    AbilityDef customVoidAbility = DefDatabase<AbilityDef>.GetNamedSilentFail("Nr_CallCathulu");

                    if (customVoidAbility != null)
                    {
                        // 3. 복제본이 해당 능력을 가지고 있는지 확인
                        Ability abilityToRemove = duplicatePawn.abilities.GetAbility(customVoidAbility);

                        if (abilityToRemove != null)
                        {
                            // 💡 4. VoidTouched Hediff는 그대로 둔 채 능력만 조용히 삭제!
                            duplicatePawn.abilities.RemoveAbility(customVoidAbility);

                            // (디버그용/연출용 메시지 - 필요시 주석 해제)
                            // Messages.Message($"[OminousLight] 오벨리스크가 {duplicatePawn.LabelShort}을(를) 복제했지만, 공허의 권한은 원본에게만 허락됩니다.", duplicatePawn, MessageTypeDefOf.NeutralEvent);
                        }
                    }
                }
            }
        }


    }
}