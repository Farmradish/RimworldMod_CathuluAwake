using RimWorld;
using Verse;


//CompAbility를 xml에서 연결하기 위해 사용되는 CompProperties 클래스입니다.
namespace NyaronCathulu
{
    public class CompProperties_AbilityCallCathulu : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityCallCathulu()
        {
            this.compClass = typeof(CompAbilityEffect_CallCathulu);
        }

    }
}
