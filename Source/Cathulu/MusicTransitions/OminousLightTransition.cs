using RimWorld;
using Verse;

namespace NyaronCathulu // 작업 중인 네임스페이스로 변경
{
    // MusicTransitionWorker를 상속받아 커스텀 워커 생성
    public class OminousLightTransition : MusicTransition
    {
        public override bool IsTransitionSatisfied()
        {
            // 1. Anomaly DLC가 활성화되어 있지 않다면 재생하지 않음
            if (!ModsConfig.AnomalyActive)
            {
                return false;
            }

            // 2. 기본 MusicTransition의 조건(예: 이미 재생 중인지 등) 검사
            if (!base.IsTransitionSatisfied())
            {
                return false;
            }

            // 커스텀 GameConditionDef를 불러옵니다.
            GameConditionDef gameConditionDef = DefDatabase<GameConditionDef>.GetNamedSilentFail("Nr_ConditionOminousLight");
            if (gameConditionDef == null)
            {
                return false;
            }

            // 3. 현재 로드된 모든 맵을 순회하며 '불길한 빛' 상태가 켜져 있는지 확인
            foreach (Map map in Find.Maps)
            {
                if (map.gameConditionManager.ConditionIsActive(gameConditionDef))
                {
                    return true;
                }
            }

            return false;
        }
        
    }
}