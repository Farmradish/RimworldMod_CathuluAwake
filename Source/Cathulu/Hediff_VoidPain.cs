using Verse;

namespace NyaronCathulu
{
    //Pawn의 Hediff(상태이상)를 제어하는 클래스입니다.
    public class Hediff_VoidPain : HediffWithComps
    {
        //Serverity에 비례해 Pawn의 통증(Pain)이 증가합니다.
        public override float PainOffset => this.Severity;
    }
}