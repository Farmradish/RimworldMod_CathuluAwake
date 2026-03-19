using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
// Defof는 특정 def를 다른 클래스에서 바로 참조할 수 있게 해주는 특수한 클래스입니다. DefOf 클래스는 게임이 로드될 때 자동으로 초기화되며, 해당 def들을 정적 필드로 만들어줍니다 
namespace NyaronCathulu
{
    [DefOf]
    public static class EffecterDefOf
    {
        public static EffecterDef Nr_EffectDistortionPestilence;
    }


    [DefOf]
    public static class  ThingDefOf
    {
        public static ThingDef Nr_MoteVoidExplosion;
    }
}
