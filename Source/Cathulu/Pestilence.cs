using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace NyaronCathulu
{
    public class Pestilence : ThingWithComps
    {
        private static ThingDef CachedProjectileDef;
        private static List<Pawn> tmpTargets = new List<Pawn>();
        private int spawnTickCount = 0;
        public bool isActive = false;


        public int activeStartTick = -1;
        private Material pillarMaterial;
        private MaterialPropertyBlock propBlock;
        protected override void Tick()
        {
            base.Tick();

            if (!this.Spawned || this.Map == null || isActive) return;
            // 120틱(2초)마다 이펙트 생성 및 주변 Pawn에게 효과 적용
            if (this.IsHashIntervalTick(120))
            {
                SpawnRandomVectorEffect();
                spawnTickCount++;

            }

            if(this.IsHashIntervalTick(60))
            {
                ApplyEffectToPawnsInRadius();
            }


            // spawnTickCount가 70 쌓이면 자신을 제거합니다 (자폭장치)
            if (spawnTickCount >= 150)
            {
                if (!this.Destroyed)
                {
                    this.Destroy(DestroyMode.Vanish);
                }
            }


        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref spawnTickCount, "spawnTickCount", 0);
            Scribe_Values.Look(ref isActive, "isActive", false);
            Scribe_Values.Look(ref activeStartTick, "activeStartTick", -1);
        }


        private void SpawnRandomVectorEffect()
        {
            float radius = 2.9f;

            Vector3 centerPos = this.DrawPos;

            // 이펙트가 그려질 고도(Altitude) 설정
            centerPos.y = AltitudeLayer.MoteOverhead.AltitudeFor();

            // 반경 내의 완전 무작위 2D 벡터 생성 (원형 분포)
            Vector2 randomCircle = Rand.InsideUnitCircle * radius;

            // 2D 오프셋을 3D 공간(X, Z 축)에 더하여 최종 스Pawn 위치 계산
            Vector3 spawnPos = centerPos + new Vector3(randomCircle.x, 0f, randomCircle.y);

            // 해당 소수점 위치가 맵 밖을 벗어나지 않았는지 타일로 변환하여 안전 검사
            if (spawnPos.ToIntVec3().InBounds(this.Map))
            {
                ThingDef randomMote = DefDatabase<ThingDef>.GetNamedSilentFail("Mote_SparkSimple");
                if (randomMote != null)
                {
                    // 크기도 약간씩 다르게 주어 자연스럽게 보이도록 합니다
                    float randomScale = Rand.Range(0.6f, 1.2f);
                    MoteMaker.MakeStaticMote(spawnPos, this.Map, randomMote, randomScale);
                }
            }
        }

        public void endPhaseActivate()
        {
            if (!this.Spawned || this.Map == null) return;
            if (!isActive)
            {
                isActive = true;
                activeStartTick = Find.TickManager.TicksGame;
            }
        }

        // 1틱마다 유니티 엔진의 DrawMesh()를 호출/제어 하기 위한 메소드 입니다. 특정 지점에서 위로 솟아오르는 빛의 기둥을 시각적으로 구현하기 위해 사용합니다.
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {

            if (this.isActive && this.activeStartTick > 0)
            {
                if (this.pillarMaterial == null)
                {
                    this.pillarMaterial = MaterialPool.MatFrom("Things/Mote/Nr_LightPillar", ShaderDatabase.MetaOverlay, Color.white);
                    this.propBlock = new MaterialPropertyBlock();
                }

                Color currentSkyColor = this.Map.skyManager.CurSky.colors.sky;

                Color tintedColor = Color.Lerp(Color.white, currentSkyColor, 1f);

                // 블록에 계산된 색상을 주입합니다.
                this.propBlock.SetColor("_Color", tintedColor);

                // 틱 경과에 따른 크기(Scale) 계산
                int elapsed = Find.TickManager.TicksGame - this.activeStartTick;
                float duration = 60f; // 60틱(1초)에 걸쳐 최대 크기까지 도달하게 세팅

                // timePercent는 0.0에서 시작해 1초 뒤 1.0에 고정됩니다.
                float timePercent = Mathf.Clamp01((float)elapsed / duration);

                // 현재 길이 (0에서 시작해 최대 15까지 서서히 증가)
                float currentLength = Mathf.Lerp(0f, 15f, timePercent);

                Vector3 pillarLoc = drawLoc;
                pillarLoc.y = AltitudeLayer.MetaOverlays.AltitudeFor();

                // 현재 길이의 딱 절반만큼 위(+Z)로 중심점을 옮겨줍니다!
                pillarLoc.z += (currentLength / 2f);

                // X: 너비 , Z: 길이 (실제론 Z축이 Y축처럼 작동합니다)
                Vector3 pillarSize = new Vector3(2f, 1f, currentLength);
                // TRS 행렬을 사용하여 위치, 회전, 크기를 한 번에 설정합니다.
                Matrix4x4 matrix = default(Matrix4x4);
                matrix.SetTRS(pillarLoc, Quaternion.identity, pillarSize);
                // 유니티 엔진의 DrawMesh()를 직접 호출하여, 커스텀 머티리얼과 프로퍼티 블록을 적용한 빛의 기둥을 그립니다.
                UnityEngine.Graphics.DrawMesh(MeshPool.plane10, matrix, this.pillarMaterial, 0, null, 0, this.propBlock);
            }
        }

        // 일정 반경내의 Pawn들을 탐색하여, 조건에 맞는 Pawn을 찾아 위치에 공격을 하는 메소드입니다.
        private void ApplyEffectToPawnsInRadius()
        {
            float radius = 3.9f;
            Map map = this.Map;

            // 해당 반경 내에 포함되는 타일의 총 개수를 가져옵니다. (반경 4.9면 약 75개)
            int numCells = GenRadial.NumCellsInRadius(radius);
            if (CachedProjectileDef == null)
            {
                CachedProjectileDef = ThingDef.Named("Nr_PestilenceBomb"); // 커스텀 폭탄 이름
            }

            // 탐색을 시작하기 전에 임시 리스트를 깨끗하게 비웁니다.
            tmpTargets.Clear();
            // 반경 내 타일 순회
            for (int i = 0; i < numCells; i++)
            {
                IntVec3 cell = this.Position + GenRadial.RadialPattern[i];
                if (!cell.InBounds(map)) continue;

                List<Thing> thingList = cell.GetThingList(map);
                for (int j = 0; j < thingList.Count; j++)
                {
                    // 조건에 맞는 Pawn 탐색 (Pawn이면서, Dead,Downed Stance가 아니고,raceprops의 race가 Humanlike로 지정된 개체)
                    if (thingList[j] is Pawn pawn && !pawn.Dead && !pawn.Downed && pawn.def.race.Humanlike)
                    {
                        // 즉시 효과를 주지 않고 후보 리스트에 추가
                        tmpTargets.Add(pawn);
                    }
                }
            }
            if (tmpTargets.TryRandomElement(out Pawn selectedPawn)) // 후보 리스트에서 무작위로 한 개를 지정
            {

                    ShootEnergyFlecks(this.Position, selectedPawn.Position, map);
                    SoundDef soundDef = DefDatabase<SoundDef>.GetNamedSilentFail("Pawn_Revenant_Wounded");
                    soundDef.PlayOneShot(new TargetInfo(this.Position, map));

                if (CachedProjectileDef != null)
                {
                    Vector3 targetPos = selectedPawn.DrawPos;

                    // 선택된 Pawn의 위치에 폭탄 소환
                    Projectile_Explosive grenade = (Projectile_Explosive)GenSpawn.Spawn(CachedProjectileDef, selectedPawn.Position, map, WipeMode.Vanish);
                    grenade.Launch(this, selectedPawn.Position, selectedPawn.Position, ProjectileHitFlags.IntendedTarget,false, null);
                    if (!this.Destroyed)
                    {
                        Log.Message("destroyed.");
                        this.Destroy(DestroyMode.Vanish);
                    }
                }
            }

            tmpTargets.Clear();
        }

        // 💡 Pestilence 위치에서 타겟(Pawn) 위치로 빛의 입자들을 쏘아보냅니다.
        public static void ShootEnergyFlecks(IntVec3 start, IntVec3 target, Map map)
        {
            if (!start.InBounds(map) || !target.InBounds(map)) return;

            // IntVec3(타일 좌표)를 Vector3(소수점 픽셀 좌표)로 변환합니다.
            Vector3 startCenter = start.ToVector3Shifted();
            Vector3 targetVec = target.ToVector3Shifted();

            // FleckDef(이펙트 파티클)을 준비합니다.
            FleckDef fleckMarkDef  = DefDatabase<FleckDef>.GetNamed("Nr_PestilenceMark");
            FleckMaker.Static(startCenter, map, fleckMarkDef, 1f);
            FleckDef fleckDef = DefDatabase<FleckDef>.GetNamed("Nr_PestilenceSpark");
            int fleckCount = 15;

            for (int i = 0; i < fleckCount; i++)
            {
                // 시작 위치를 랜덤으로 약간씩 흩뿌려줍니다
                Vector3 randomOffset = new Vector3(Rand.Range(-1.5f, 1.5f), 0, Rand.Range(-1.5f, 1.5f));
                Vector3 actualStartVec = startCenter + randomOffset;

                // 흩뿌려진 각자의 위치에서 타겟까지의 '진짜' 거리와 각도를 구합니다.
                float distance = Vector3.Distance(actualStartVec, targetVec);
                float angle = (targetVec - actualStartVec).AngleFlat();

                FleckCreationData data = FleckMaker.GetDataStatic(actualStartVec, map, fleckDef);

                // solidTimeOverride 적용
                float solidTime = Rand.Range(0.2f, 0.5f);
                data.solidTimeOverride = solidTime;

                // 총 지속시간을 이용해 도착하는 지점 제어
                float totalDuration = fleckDef.fadeInTime + solidTime + fleckDef.fadeOutTime;
                float speed = distance / totalDuration;

                // 각도 오차 없이 타겟의 정중앙을 향해 쏩니다.
                data.velocityAngle = angle;
                data.velocitySpeed = speed;

                data.exactScale = new Vector3(Rand.Range(1.0f, 1.8f),0, Rand.Range(1.0f, 1.8f));

                map.flecks.CreateFleck(data);
            }
        }





    }
}