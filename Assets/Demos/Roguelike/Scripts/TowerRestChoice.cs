using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // A cleared rest floor in real play: a small band offers the campfire's heal ([1], the default) or a tempering ([2]) that
    // raises one owned level-one skill to level two. The floor keeps running; leaving undecided takes the heal.
    public sealed partial class TowerGame
    {
        // The climb whose rest floor still waits for a choice; a new climb or the village leaves it behind.
        private TowerRun restChoiceRun;
        internal bool RestChoiceOpen{get{return restChoiceRun!=null&&restChoiceRun==run&&run.FloorType==FloorType.Rest;}}
        private static readonly Rect RestChoiceRect=new Rect(530,772,640,96);

        private void OpenRestChoice()
        {
            restChoiceRun=run;
            Toast("모닥불 곁의 휴식","[1] 생명력 회복  ·  [2] 모닥불 단련  ·  그냥 떠나면 회복",TowerWorld.Teal,4);
        }
        /// <summary>Called as the next floor is built: a choice still open takes the heal on the way out.</summary>
        private void SettleRestChoice()
        {
            if(restChoiceRun!=null&&restChoiceRun==run)RestHeal();
            restChoiceRun=null;
        }
        internal bool ChooseRestHeal()
        {
            if(!RestChoiceOpen)return false;
            RestHeal();return true;
        }
        /// <summary>Opens the tempering cards: one owned level-one skill to level two, or a gold stat when none is left.</summary>
        internal bool ChooseRestTemper()
        {
            if(!RestChoiceOpen||!run.BeginTempering())return false;
            restChoiceRun=null;
            // The cards ignore input for a moment, so the key or click that chose tempering cannot also take a card.
            rewardRevealAt=Time.unscaledTime;
            Sound(chimeSound,.5f);
            if(vfx!=null)vfx.Cast(SkillEffect.ShieldPulse,hero.pos+Vector2.up,2.4f,TowerWorld.Gold);
            Toast("모닥불 단련",run.Draft==RewardDraft.Skills?"Lv.1 스킬 하나를 Lv."+TowerRun.TemperedLevel+"로 단련합니다":"단련할 Lv.1 스킬이 없어 금 등급 능력치를 받습니다",brass,3.2f);
            return true;
        }

        private void DrawRestChoice()
        {
            if(!RestChoiceOpen||run.IsRewardPending)return;
            // 1 and 2 (on the row or the keypad) take the matching option while the floor is live.
            var key=Event.current;
            if(GUI.enabled&&key.type==EventType.KeyDown)
            {
                int pick=key.keyCode==KeyCode.Alpha1||key.keyCode==KeyCode.Keypad1?1:key.keyCode==KeyCode.Alpha2||key.keyCode==KeyCode.Keypad2?2:0;
                if(pick>0){key.Use();if(pick==1)ChooseRestHeal();else ChooseRestTemper();return;}
            }
            Rect r=RestChoiceRect;Panel(r,TowerWorld.Teal);
            Label(new Rect(r.x+22,r.y+8,r.width-44,20),"모닥불 곁에서 하나를 고르세요  ·  그냥 떠나면 생명력 회복",13,muted,TextAnchor.MiddleLeft);
            float healed=Mathf.Min(PlayerMaxHealth-hero.hp,PlayerMaxHealth*TowerRun.RestHealFraction);
            string heal=healed>0?"+"+Mathf.CeilToInt(healed)+"  ·  최대 생명력의 "+Mathf.RoundToInt(TowerRun.RestHealFraction*100)+"%":"생명력이 이미 가득합니다";
            if(RestOption(new Rect(r.x+18,r.y+34,296,52),"1","생명력 회복",heal,TowerWorld.Teal)){ChooseRestHeal();return;}
            string temper=run.TemperableSkills>0?"Lv.1 스킬 하나를 Lv."+TowerRun.TemperedLevel+"로":"금 등급 능력치 카드 1장";
            if(RestOption(new Rect(r.x+326,r.y+34,296,52),"2","모닥불 단련",temper,brass))ChooseRestTemper();
        }
        private bool RestOption(Rect r,string keyName,string title,string detail,Color accent)
        {
            bool clicked=Button(r,"",accent);
            Keycap(new Rect(r.x+14,r.y+14,28,24),keyName);
            Title(new Rect(r.x+54,r.y+4,r.width-64,24),title,18,paper,TextAnchor.MiddleLeft);
            Label(new Rect(r.x+54,r.y+28,r.width-64,18),detail,13,muted,TextAnchor.MiddleLeft);
            return clicked;
        }
    }
}
