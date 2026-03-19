using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace NyaronCathulu
{
    //GameCondition은 특정 맵에 적용되어 지속적으로 효과를 발휘하는 상태입니다. 세이브/로드 시 상태를 유지할 수 있으며, 맵의 환경과 폰들에게 영향을 줄 수 있습니다.
    public class GameCondition_OminousLight : GameCondition
    {

        // 실체의 진행도 (0.0f ~ 100.0f)
        private float progress = 50f;

        // 자연 감소를 체크하기 위한 틱 타이머
        private int decayTimer = 0;

        // 타이머 만료 기준 틱 (예: 2500틱 = 인게임 약 1시간)
        // 기획에 맞게 이 수치를 조절하여 감소 주기를 변경할 수 있습니다.
        private const int DecayIntervalTicks = 2500;

        // 만료 시 감소할 진행도 수치 (0.7%)
        private const float DecayAmount = 0.7f;

        private int spawnPestilenceTimer = 0;
        private const int spawnPestilenceIntervalTicks = 5000;

        private int sequenceState = 0;
        private int sequenceStartTick = 0;
        private int sequenceTickForShake = 0;
        private float currentDarkness = 0f;


        private List<Thing> thingPestilences = new List<Thing>();
        private List<NyaronCathulu.Pestilence> pestilences = new List<NyaronCathulu.Pestilence>();
        private int totalPestilenceCount = 0;
        private int curruntIndex = 0;

        public OminousTriggerAction activeTrigger;

        public override void Init()
        {
            base.Init();
            this.Permanent = true;
            // 실체가 등장할 때 0~4 중 하나를 랜덤으로 선택하여 타겟 행동 지정
            activeTrigger = (OminousTriggerAction)Rand.RangeInclusive(0, 4);
            // 개발자 테스트용 로그 (어떤 행동이 뽑혔는지 확인)
            Log.Message($"[OminousLight] 의 타겟: {activeTrigger}");
        }

        // 현재 진행도에 따른 페이즈 반환
        public int CurrentPhase
        {
            get
            {
                if (progress >= 100f) return 3;
                if (progress >= 70f) return 2;
                return 1;
            }
        }

        // 세이브/로드 시 진행도와 타이머 상태 보존
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref progress, "ominousLightProgress", 50f);
            Scribe_Values.Look(ref decayTimer, "ominousLightDecayTimer", 0);
            Scribe_Values.Look(ref activeTrigger, "ominousLightActiveTrigger", OminousTriggerAction.Eat);
            Scribe_Values.Look(ref spawnPestilenceTimer, "ominousLightSpawnPestilenceTimer", 0);
            Scribe_Values.Look(ref sequenceState, "ominousLightSequenceState", 0);
            Scribe_Values.Look(ref sequenceStartTick, "ominousLightSequenceStartTick", 0);
            Scribe_Values.Look(ref sequenceTickForShake, "ominousLightSequenceTickForShake", 0);
            Scribe_Values.Look(ref currentDarkness, "ominousLightCurrentDarkness", 0f);
            Scribe_Collections.Look(ref this.pestilences, "pestilences", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (this.pestilences == null)
                {
                    this.pestilences = new List<NyaronCathulu.Pestilence>();
                }
                // 로드 과정에서 파괴되거나 증발한 객체가 있다면 리스트에서 깔끔하게 청소해 줍니다.
                this.pestilences.RemoveAll(x => x == null);
            }
        }


        public void Notify_ActionPerformed(Pawn pawn, OminousTriggerAction action, float progressAmount, float stunChance)
        {
            // 정해진 랜덤 행동과 현재 폰이 한 행동이 일치할 때만 작동
            if (action != activeTrigger) return;
            // 3페이즈에 돌입했을 때 더이상 진행도를 변경하지 않음
            if (CurrentPhase >= 3) return;
            progress = Mathf.Clamp(progress + progressAmount, 0f, 100f);
            ResetDecayTimer(); // 타이머 초기화
            // pawn의 defName이 "Alien_Nyaron"일 때는 3배의 확률로 스턴과 무드감소 유발
            if (pawn.def.defName == "Alien_Nyaron")
                {
                    stunChance = stunChance * 3f;
                }
            // 스턴과 무드감소 유발
            if (Rand.Chance(stunChance))
            {
                ApplyStun(pawn);
            }
        }

        private void ApplyStun(Pawn pawn)
        {
            if (!pawn.Downed && pawn.Spawned)
            {

                int stunTicks = Rand.RangeInclusive(120, 600);
                pawn.stances.stunner.StunFor(stunTicks, null, false, true);
                MoteMaker.MakeColonistActionOverlay(pawn, RimWorld.ThingDefOf.Mote_ColonistFleeing);
                ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Nr_ThoughtOminousGaze");
                if (thought != null)
                {
                    pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
                }
                    
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);


            }
        }


        public override void GameConditionTick()
        {
            base.GameConditionTick();
            //3페이즈에 도달하지 않았을 때만 진행도가 감소하도록 함.
            if (CurrentPhase < 3)
            {
                // 1. 매 틱마다 타이머 증가
                decayTimer++;

                // 2. 타이머가 만료되었을 때의 처리
                if (decayTimer >= DecayIntervalTicks)
                {

                    progress = progress - DecayAmount;
                    Log.Message("[Ominous Light] Progress decreased by " + DecayAmount + "%. Current Progress: " + progress + "%");
                    // 타이머를 즉시 초기화
                    decayTimer = 0;
                    if (progress <= 0f)
                    {
                        // progress가 0이하일경우 종료

                        this.End();
                        return;
                    }
                }
            }


            // 현재 페이즈 기믹 실행 (시각 효과 등)
            if (this.SingleMap != null)
            {
                ApplyPhaseEffects(this.SingleMap);
            }
        }

        // progress 타이머를 초기화 하는 메소드
        public void ResetDecayTimer() 
        {
            decayTimer = 0;
        }

        private void ApplyPhaseEffects(Map map)
        {
            switch (CurrentPhase)
            {
                case 1:
                    // 페이즈 1 기믹 로직
                    break;
                case 2:
                    DoSpawnPestilence(map);
                    break;
                case 3:

                    int currentTick = Find.TickManager.TicksGame; // 현재의 게임 틱을 기억합니다.
                    // 시간경과에 따른 페이즈 진행을 위한 시퀀스 구분(0~4로 구분되어 있으며 현재 시퀀스 종료시 다음 페이즈로 이동)

                    if (sequenceState == 0)
                    {
                            sequenceState = 1;
                            sequenceStartTick = currentTick;
                            sequenceTickForShake = currentTick;
                            Find.LetterStack.ReceiveLetter(
                            "캣후루의 미니언",
                            "공포의 존재, 발밑을 기어다니는 혼돈 캣후루의 하수인이 강림하였다.",
                            LetterDefOf.ThreatBig,
                            new TargetInfo(map.Center, map)
                            
                        );
                        thingPestilences.Clear(); // 혹시라도 남아있을지 모르는 Pestilence 리스트 초기화
                        thingPestilences = this.SingleMap.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamedSilentFail("Nr_Pestilence"));
                        pestilences.Clear();
                        pestilences = thingPestilences.OfType<NyaronCathulu.Pestilence>().ToList();
                        foreach (NyaronCathulu.Pestilence pestilence in pestilences)
                        {
                            Log.Message(pestilence.ThingID + " added to pestilences list.");
                        }
                        if (pestilences != null)
                        {
                            totalPestilenceCount = pestilences.Count;
                            curruntIndex = 0;
                        }
                    }
                    //시퀀스 1에서는 Pestilence가 하나씩 활성화(하늘로 빛이 솟는 연출)되며, 동시에 30틱마다 카메라 흔들림 효과가 발생합니다. 모든 Pestilence가 활성화되고 나면 다음 시퀀스로 넘어갑니다.
                    else if (sequenceState == 1)
                    {
                        if(currentTick >= sequenceTickForShake + 30)
                        {
                            if (pestilences != null && curruntIndex < pestilences.Count)
                            {
                               
                                NyaronCathulu.Pestilence p = pestilences[curruntIndex];
                                if (p != null) 
                                {
                                    p.endPhaseActivate();
                                }
                                ++curruntIndex;

                            }
                            Find.CameraDriver.shaker.DoShake(4.0f);
                            sequenceTickForShake= currentTick;

                        }

                        if (currentTick >= sequenceStartTick + Mathf.Max(totalPestilenceCount,1200))
                        {
                            sequenceState = 2;
                            sequenceStartTick = currentTick;
                        }
                    }
                    // 시퀀스 2에서는 120틱 동안 암전이 점점 심해지는 연출이 진행됩니다
                    else if (sequenceState == 2)
                    {

                        if (currentTick >= sequenceTickForShake + 15)
                        {
                            Find.CameraDriver.shaker.DoShake(2.0f);
                            sequenceTickForShake = currentTick;

                        }
                        // 120틱 동안 0.0에서 1.0으로 부드럽게 상승
                        currentDarkness = Mathf.Clamp01((float)(currentTick - sequenceStartTick) / 120f);

                        if (currentDarkness >= 1f)
                        {

                            sequenceState = 3;
                            sequenceStartTick = currentTick;
                            foreach (NyaronCathulu.Pestilence pestilence in pestilences)
                            {
                                if (pestilence != null && !pestilence.Destroyed)
                                {

                                    pestilence.Destroy(DestroyMode.Vanish);
                                }
                            }
                        }
                    }
                    // 시퀀스 3에서는 300틱 동안 암전 상태를 유지하며, 이후 다음 게임 컨디션으로 넘어갑니다.
                    else if (sequenceState == 3)
                        {
                            currentDarkness = 1f; // 확실하게 1.0 유지

                            if (currentTick >= sequenceStartTick + 300)
                            {
                                sequenceState = 4;
                                sequenceStartTick = currentTick;
                            // 다음 GameCondition 등록 및 현재 GameCondition 종료
                            GameConditionDef nextConditionDef = DefDatabase<GameConditionDef>.GetNamedSilentFail("Nr_ConditionOminousLightHeavy");
                            GameCondition nextGameCondition = GameConditionMaker.MakeCondition(nextConditionDef, 999999);
                            map.gameConditionManager.RegisterCondition(nextGameCondition);
                            this.End();
                        }
                    }
                        else if (sequenceState == 4)
                        {
                        }
                    break;
            }
        }



        private void DoSpawnPestilence(Map map)
        {
            // 타이머 로직...
            spawnPestilenceTimer++;
            if (spawnPestilenceTimer>= 1200)
            {

                // Pestilence 스폰
                ThingDef pestilenceDef = DefDatabase<ThingDef>.GetNamedSilentFail("Nr_Pestilence");
                if (pestilenceDef != null)
                {
                    int currentCount = map.listerThings.ThingsOfDef(pestilenceDef).Count;

                    // 맵 내에 최대 20개까지 등장하도록 제한
                    if (currentCount < 20)
                    {
                        SpawnPestilenceWithEffect(map, pestilenceDef);
                    }
                }

                spawnPestilenceTimer = 0; // 타이머 초기화
            }
        }

        // Pestilence 스폰 및 시각 효과 생성 메서드
        private void SpawnPestilenceWithEffect(Map map, ThingDef pestilenceDef)
        {
            IntVec3 spawnCell = IntVec3.Invalid;
            
            
            // 3가지 시도 순서: 1) 주거 구역 내부의 빈 공간, 2) 임의의 정착민 근처 빈 공간, 3) 맵 전체의 무작위 빈 공간
            // 주거 구역(Home Area) 내부의 빈 공간
            if (map.areaManager.Home != null)
            {
                var homeCells = map.areaManager.Home.ActiveCells.Where(c => c.Standable(map) && !c.Fogged(map));
                if (homeCells.Any())
                {
                    spawnCell = homeCells.RandomElement();
                }
            }

            // 임의의 정착민 근처 빈 공간
            if (!spawnCell.IsValid && map.mapPawns.FreeColonists.Where(p => p.Spawned).TryRandomElement(out Pawn colonist))
            {
                CellFinder.TryFindRandomReachableNearbyCell(colonist.Position, map, 15f, TraverseParms.For(TraverseMode.NoPassClosedDoors), (IntVec3 c) => c.Standable(map), null, out spawnCell);

            }


            // 맵 전체의 무작위 빈 공간
            if (spawnCell.IsValid)
            {
                // 투명 지뢰(Pestilence) 스폰
                GenSpawn.Spawn(pestilenceDef, spawnCell, map);
                SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail("FlareImpact");
                if (sound != null) 
                { 
                    sound.PlayOneShot(new TargetInfo(spawnCell, map));
                }


                // 중심점의 Vector3 좌표
                Vector3 centerPos = spawnCell.ToVector3ShiftedWithAltitude(AltitudeLayer.MoteOverhead);

                // 왜곡효과
                EffecterDef effectDef = NyaronCathulu.EffecterDefOf.Nr_EffectDistortionPestilence;
                if (effectDef != null) { 
                    TargetInfo target = new TargetInfo(spawnCell, map);

                    Effecter effecter = effectDef.Spawn(target,target);
                    effecter.Cleanup();
                }
                // 중심으로 빨려 들어가는 빛의 입자들 생성
                FleckDef sparkDef = DefDatabase<FleckDef>.GetNamed("Nr_ConvergingSpark");
                if (sparkDef != null)
                {
                    int particleCount = 12; // 모여드는 빛줄기의 개수
                    float radius = 1f; // 빛이 생성되기 시작하는 반경 (3.5칸 밖에서부터)

                    // 파티클의 수명(solidTime + fadeTime = 약 0.5초). 
                    // 0.5초 동안 3.5칸을 이동해야 하므로 속도는 거리/시간 = 7.0f 정도가 적당합니다.
                    float speed = radius / 0.2f;

                    for (int i = 0; i < particleCount; i++)
                    {
                        // 원형을 따라 파티클이 생성될 각도를 균등하게 나눔 (약간의 랜덤성 부여)
                        float angle = (360f / particleCount * i) + Rand.Range(-10f, 10f);

                        // 삼각함수를 이용해 각도와 반경에 따른 시작 위치 계산
                        Vector3 spawnOffset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
                        Vector3 startPos = centerPos + spawnOffset;

                        // FleckCreationData를 사용하여 디테일한 컨트롤 (속도, 각도, 회전)
                        FleckCreationData fcd = FleckMaker.GetDataStatic(startPos, map, sparkDef, Rand.Range(0.6f, 1.2f));

                        // 중심을 향해 날아가도록 설정 (현재 위치에서 중심점까지의 각도)
                        fcd.velocityAngle = (centerPos - startPos).AngleFlat();
                        fcd.velocitySpeed = speed * Rand.Range(0.2f, 0.4f); // 속도에 약간의 오차를 줘서 자연스럽게
                        // 입자(선)의 이미지 회전값도 날아가는 방향과 일치시킴
                        fcd.rotation = fcd.velocityAngle;

                        // 맵에 최종적으로 Fleck 생성
                        map.flecks.CreateFleck(fcd);
                    }
                }

            }
        }













        // 화면이 부드럽게 전환되도록 하는 변수
        public override float SkyTargetLerpFactor(Map map)
        {
            if (currentDarkness > 0f)
            {
                return 1f;
            }

            return GameConditionUtility.LerpInOutValue(this, 2500f);
        }

        // 하늘(주로 색상 오버레이, 밝기 등)을 표현하기 위한 메소드입니다.
        public override SkyTarget? SkyTarget(Map map)
        {
            // 1페이즈 진행 중 하늘 색상 변경을 위한 진행도 비율 계산 (0~50퍼센트 까지) 
            float t = Mathf.Clamp(progress / 50f, 0f, 1f);

            Color baseRed = new Color(0.9f, 0.7f, 0.7f);
            Color baseShadow = new Color(0.9f, 0.4f, 0.4f);
            Color baseOverlay = new Color(1.0f, 0.5f, 0.5f);

            Color maxRed = new Color(0.8f, 0.1f, 0.1f);
            Color maxShadow = new Color(0.3f, 0.0f, 0.0f);
            Color maxOverlay = new Color(0.9f, 0.1f, 0.1f);

            Color currentRed = Color.Lerp(baseRed, maxRed, t);
            Color currentShadow = Color.Lerp(baseShadow, maxShadow, t);
            Color currentOverlay = Color.Lerp(baseOverlay, maxOverlay, t);

            float currentGlow = Mathf.Lerp(0.9f, 0.4f, t);


            if (currentDarkness > 0f)
            {
                // currentDarkness가 0.0 -> 1.0으로 오름에 따라, 붉은빛 하늘이 완전히 암전됩니다.
                currentRed = Color.Lerp(currentRed, Color.black, currentDarkness);
                currentShadow = Color.Lerp(currentShadow, Color.black, currentDarkness);
                currentOverlay = Color.Lerp(currentOverlay, Color.black, currentDarkness);
                currentGlow = Mathf.Lerp(currentGlow, 0f, currentDarkness);
            }

            // 최종 계산된 색상 반환
            SkyColorSet finalColors = new SkyColorSet(currentRed, currentShadow, currentOverlay, 1f);
            return new SkyTarget?(new SkyTarget(currentGlow, finalColors, 1f, 1f));
        }


    }
}