using Verse;

namespace NyaronCathulu
{
    // GameComponent는 게임 전체에서 지속적으로 존재하는 컴포넌트로, 세이브/로드 시 상태를 유지할 수 있습니다. 이를 통해 콘텐츠 해금 여부와 같은 정보를 저장할 수 있습니다.
    public class GameComponent_CathuluAwakening : GameComponent
    {
        public bool isContentUnlocked = false;

        public GameComponent_CathuluAwakening(Game game) { }

        // 세이브/로드 시 변수 값을 유지하는 메소드
        public override void ExposeData()
        {
            base.ExposeData(); // 기존 매서드를 호출(기본적인 저장기능 유지)
            Scribe_Values.Look(ref isContentUnlocked, "isCathulhuContentUnlocked", false);// 기존 메소드에서 관리되지 않는 custom 변수를 save파일에 저장/로드 할 수 있도록 추가
        }
    }
}