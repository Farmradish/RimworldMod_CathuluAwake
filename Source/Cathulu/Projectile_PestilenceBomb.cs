using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NyaronCathulu
{
    // Projectile_PestilenceBomb 클래스는 특정 폭발 효과와 시각적 표현을 폭탄에 적용하기 위한 클래스입니다.
    public class Projectile_PestilenceBomb : Projectile_Explosive
    {
        public const string DefName = "Nr_PestilenceBomb";
        private Vector2 baseDrawSize = Vector2.one;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            this.spawnedTick = Find.TickManager.TicksGame;

            if (this.def.graphicData != null)
            {
                this.baseDrawSize = this.def.graphicData.drawSize;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref baseDrawSize, "baseDrawSize");
            Scribe_Values.Look(ref this.spawnedTick, "spawnedTick");
        }
        protected override void Explode()
        {
            Map map = base.Map;
            IntVec3 pos = this.Position;

            if (map == null)
                {
                }

            ThingDef moteExplosion = NyaronCathulu.ThingDefOf.Nr_MoteVoidExplosion;
            if (moteExplosion != null)
                {
                    MoteMaker.MakeStaticMote(pos, map, moteExplosion, 1f);
            }
            base.Explode();
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            drawLoc = this.Position.ToVector3Shifted();
            if (!this.Destroyed && this.landed && this.def.projectile.explosionDelay >= 0)
            {
                float totalDelay = (float)this.def.projectile.explosionDelay;
                float elapsed = (float)(Find.TickManager.TicksGame - this.spawnedTick);
                float timePercent = Mathf.Clamp01(elapsed / totalDelay);

                float currentScale = Mathf.Lerp(0.5f, 1.5f, timePercent);
                Vector3 finalDrawSize = new Vector3(this.baseDrawSize.x * currentScale, 1f, this.baseDrawSize.y * currentScale);

                float currentAngle = 720f * timePercent;
                // 림월드의 2D 평면(바닥)은 XZ축이므로, Y축(Vector3.up)을 기준으로 팽이처럼 회전시킵니다
                Quaternion rotation = Quaternion.AngleAxis(currentAngle, Vector3.up);

                drawLoc.y = AltitudeLayer.MetaOverlays.AltitudeFor();

                // 매트릭스 생성 (위치, 회전, 최종 크기)
                Matrix4x4 matrix = default(Matrix4x4);
                matrix.SetTRS(drawLoc, rotation, finalDrawSize);

                // 마테리얼을 생성하고 텍스처 경로와 색상을 적용합니다.
                Material mat = MaterialPool.MatFrom(this.def.graphicData.texPath, ShaderDatabase.MetaOverlay, this.Graphic.color);
                //  Graphics.DrawMesh() 호출
                UnityEngine.Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0);
            }
        }
    }
}