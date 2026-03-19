using RimWorld;
using Verse;

namespace NyaronCathulu
{
    public class CompAbilityEffect_CallCathulu : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            // 1. 전역 상태를 '해금됨'으로 변경
            GameComponent_CathuluAwakening gameComponent = Current.Game.GetComponent<GameComponent_CathuluAwakening>();
            if (gameComponent != null)
            {
                gameComponent.isContentUnlocked = true;
            }

            this.parent.pawn.abilities.RemoveAbility(this.parent.def);
        }
    }
}