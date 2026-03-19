using RimWorld;
using Verse;

namespace NyaronCathulu
{
    // GameCondition을 발생시키는 바닐라의 기본 워커를 상속받습니다.
    public class IncidentWorker_OminousLight : IncidentWorker_MakeGameCondition
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
            {
                return false;
            }

            //컴포넌트 로드 상태
            var comp = Current.Game.GetComponent<GameComponent_CathuluAwakening>();
            if (comp == null)
            {
                return false;
            }

            //해금 변수 상태
            if (!comp.isContentUnlocked)
            {
                return false;
            }

            //모든 관문 통과 성공!
            return true;
        }
    }
}