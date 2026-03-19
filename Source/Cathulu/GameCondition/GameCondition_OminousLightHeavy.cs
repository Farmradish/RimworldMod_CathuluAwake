using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Noise;
using static UnityEngine.GraphicsBuffer;

namespace NyaronCathulu
{
    //GameCondition은 특정 맵에 적용되어 지속적으로 효과를 발휘하는 상태입니다. 세이브/로드 시 상태를 유지할 수 있으며, 맵의 환경과 폰들에게 영향을 줄 수 있습니다.
    public class GameCondition_OminousLightHeavy : GameCondition
    {




        public IntVec3 bossSpawnLocation = IntVec3.Invalid;
        private bool bossSpawnSequenceStarted = false;
        private bool bossSpawned = false;
        private Effecter maintainedEffecter;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref bossSpawnLocation, "bossSpawnLocation",IntVec3.Invalid);
            Scribe_Values.Look(ref bossSpawnSequenceStarted, "bossSpawnSequenceStarted", false);
            Scribe_Values.Look(ref bossSpawned, "bossSpawned", false);
        }


        public override void GameConditionTick()
        {
            base.GameConditionTick();
            if(this.TicksPassed > 250 && !bossSpawnSequenceStarted)
            {
                StartBossSpawnSequence();
            }
            else if(this.TicksPassed > 610 && !bossSpawned)
            {
                SpawnBoss();

            }

            if (bossSpawnSequenceStarted && !bossSpawned && this.maintainedEffecter != null)
            {
                TargetInfo target = new TargetInfo(this.bossSpawnLocation, this.SingleMap);

                // 이펙트를 지속적으로 업데이트하여 스폰 위치에 효과과 유지되도록 합니다.
                this.maintainedEffecter.EffectTick(target, target);
            }

        }



        private void StartBossSpawnSequence()
        {
            Map map = this.SingleMap;

            if (!CellFinder.TryFindRandomEdgeCellWith((IntVec3 c) => c.Standable(map), map, CellFinder.EdgeRoadChance_Ignore, out this.bossSpawnLocation))
            {
                this.bossSpawnLocation = DropCellFinder.RandomDropSpot(map);
            }

            // 5x5가 맵 밖으로 잘리지 않도록 강제 보정
            int clampedX = Mathf.Clamp(this.bossSpawnLocation.x, 2, map.Size.x - 3);
            int clampedZ = Mathf.Clamp(this.bossSpawnLocation.z, 2, map.Size.z - 3);
            this.bossSpawnLocation = new IntVec3(clampedX, this.bossSpawnLocation.y, clampedZ);

            TargetInfo target = new TargetInfo(this.bossSpawnLocation, map);
            CameraJumper.TryJump(target, CameraJumper.MovementMode.Cut);

            CellRect spawnRect = CellRect.CenteredOn(this.bossSpawnLocation, 2).ClipInsideMap(map);

            foreach (IntVec3 cell in spawnRect)
            {
                List<Thing> thingList = cell.GetThingList(map);
                for (int i = thingList.Count - 1; i >= 0; i--)
                {
                    Thing t = thingList[i];
                    if (t.def.destroyable && t.def.category != ThingCategory.Pawn)
                    {
                        t.Destroy(DestroyMode.KillFinalize);
                    }
                }
            }


            EffecterDef effecterDef = DefDatabase<EffecterDef>.GetNamedSilentFail("Nr_EffecterMinionIncoming");
            if (effecterDef != null)
            {
                this.maintainedEffecter = effecterDef.Spawn(target, target);
            }

            bossSpawnSequenceStarted = true;
        }

        private void SpawnBoss()
        {
            if (bossSpawnLocation.IsValid)
            {
                // 팩션 설정 
                Faction enemyFaction = Faction.OfEntities;

                PawnKindDef bossDef = DefDatabase<PawnKindDef>.GetNamedSilentFail("Metalhorror"); // 임시로 메탈호러 PawnKind배정 추후, 전용 보스PawnKind로 교체
                if (bossDef == null) return;

                // 보스 생성 및 스폰
                PawnGenerationRequest request = new PawnGenerationRequest(bossDef, enemyFaction, PawnGenerationContext.NonPlayer, -1, true);
                Pawn boss = PawnGenerator.GeneratePawn(request);
                Thing thingBoss = GenSpawn.Spawn(boss, this.bossSpawnLocation, this.SingleMap);

                //보스에게 부여할 Lord(역할) 생성 - 현재 임시로 넣은 Pawnkind에는 적용못하여 주석처리.

                //List<Pawn> bossList = new List<Pawn> { boss };
                //LordMaker.MakeNewLord(enemyFaction, new LordJob_AssaultColony(enemyFaction), map, bossList);
            }


            // 지속형 이펙트 종료 처리
            if (this.maintainedEffecter != null)
            {
                this.maintainedEffecter.Cleanup();
                this.maintainedEffecter = null;
            }
            TargetInfo target = new TargetInfo(this.bossSpawnLocation, this.SingleMap);
            EffecterDef effecterDef = RimWorld.EffecterDefOf.VoidStructureActivated;
            if (effecterDef != null)
            {
                Effecter effecter = effecterDef.Spawn(target, target);
                effecter.Cleanup();
            }

            bossSpawned = true;
        }

        public override void Init()
        {
            base.Init();
            
            this.Permanent = true;
        }
        public override float SkyTargetLerpFactor(Map map)
        {
            return 1f;
        }

        // 하늘(주로 색상 오버레이, 밝기 등)을 표현하기 위한 메소드입니다.
        public override SkyTarget? SkyTarget(Map map)
        {
            // 컨디션이 시작된 후 몇 틱이 지났는지 확인
            int elapsed = this.TicksPassed;
            float transitionDuration = 250f; // 250틱에 걸쳐 변화

            // 0.0(시작)에서 1.0(250틱 도달 시) 사이의 진행도
            float t = Mathf.Clamp01((float)elapsed / transitionDuration);

            Color darkSky = Color.black;
            Color darkShadow = Color.black;
            Color darkOverlay = Color.black;
            float darkGlow = 0f;

            Color redSky = new Color(0.9f, 0.1f, 0.1f);
            Color redShadow = new Color(0.4f, 0.0f, 0.0f);
            Color redOverlay = new Color(1.0f, 0.15f, 0.15f);
            float redGlow = 0.5f;


            Color currentSky = Color.Lerp(darkSky, redSky, t);
            Color currentShadow = Color.Lerp(darkShadow, redShadow, t);
            Color currentOverlay = Color.Lerp(darkOverlay, redOverlay, t);
            float currentGlow = Mathf.Lerp(darkGlow, redGlow, t);


            // 혼합된 색상으로 최종 하늘 타겟 생성
            SkyColorSet currentColorSet = new SkyColorSet(currentSky, currentShadow, currentOverlay, 1f);

            return new SkyTarget?(new SkyTarget(currentGlow, currentColorSet, 1f, 1f));
        }
    }
}
