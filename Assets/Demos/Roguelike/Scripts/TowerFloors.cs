using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Elite, rest and shop floors chosen at the branch exits of every fifth floor.
    public sealed partial class TowerGame
    {
        internal bool shopOpen;
        internal const float MerchantReach=2.2f;
        // The stall opens only when the hero walks up to it, and closes for good after one trade.
        internal bool NearMerchant{get{return run!=null&&hero!=null&&run.FloorType==FloorType.Shop&&Mathf.Abs(hero.pos.x-TowerWorld.FeaturePosition.x)<MerchantReach;}}
        internal bool CanOpenShop{get{return run!=null&&run.FloorType==FloorType.Shop&&run.IsFloorCleared&&!run.IsRewardPending&&!run.ShopTraded&&hero.hp>0&&!intro&&NearMerchant;}}
        internal bool InteractMerchant()
        {
            if(run==null||run.FloorType!=FloorType.Shop||!run.IsFloorCleared||run.IsRewardPending||!NearMerchant)return false;
            if(run.ShopTraded){Toast("거래 종료","상인은 더 이상 물건을 내놓지 않습니다.",muted);return false;}
            shopOpen=true;Sound(chimeSound,.3f);return true;
        }
        internal int DifficultyFloor(int floor){return run!=null&&run.IsOathTrial?TowerDifficulty.TrialFloor(floor):floor;}
        internal static string FloorTypeName(FloorType type)
        {return type==FloorType.Elite?"엘리트":type==FloorType.Rest?"휴식":type==FloorType.Shop?"상점":"일반";}
        internal static string FloorTypeSummary(FloorType type)
        {
            return type==FloorType.Elite?"강한 적 · 클리어 시 증강":type==FloorType.Rest?"생명력 "+Mathf.RoundToInt(TowerRun.RestHealFraction*100)+"% 회복":
                type==FloorType.Shop?"포인트로 증강·회복 구매":"다음 층";
        }
        private void MakeElite(Fighter actor)
        {
            actor.isElite=true;
            actor.hp=actor.maxHp*=TowerDifficulty.EliteHealth;
            actor.damageScale*=TowerDifficulty.EliteDamage;
            actor.root.transform.localScale=Vector3.one*TowerDifficulty.EliteScale;
            actor.art.Tint=new Color(1,.84f,.76f);
        }
        // The gate whose doorway the hero is standing in. Fifth floors offer only rest, shop and elite doors.
        internal FloorType? NearestGate()
        {
            FloorType? best=null;float range=2;
            if(!run.IsBranchFloor){if(Mathf.Abs(hero.pos.x-TowerWorld.ExitX)<range)best=FloorType.Normal;return best;}
            foreach(var type in TowerWorld.BranchGates)
            {float distance=Mathf.Abs(hero.pos.x-TowerWorld.GateX(type));if(distance<range){range=distance;best=type;}}
            return best;
        }
        private void OnSpecialFloorCleared()
        {
            if(run.FloorType==FloorType.Rest)
            {
                float healed=Mathf.Min(PlayerMaxHealth-hero.hp,PlayerMaxHealth*TowerRun.RestHealFraction);hero.hp+=healed;
                if(healed>0)FloatText(hero.pos+Vector2.up*2,"+"+Mathf.CeilToInt(healed),TowerWorld.Teal);
                if(vfx!=null)vfx.Cast(SkillEffect.HealPulse,hero.pos+Vector2.up,2.4f,TowerWorld.Teal);
                Toast("모닥불 곁의 휴식",healed>0?"생명력 "+Mathf.CeilToInt(healed)+" 회복":"생명력이 이미 가득합니다",TowerWorld.Teal,3.2f);
            }
            else if(run.FloorType==FloorType.Shop)Toast("상점 층","상인에게 다가가 [E]로 거래하세요. 거래는 한 번뿐입니다.",brass,3.6f);
        }
        internal bool BuyShopItem(ShopItem item)
        {
            var offer=run.GetShopOffer(item);
            if(run.ShopTraded){Toast("거래 종료","이 상점에서는 이미 거래했습니다.",muted);return false;}
            if(item==ShopItem.Heal&&hero.hp>=PlayerMaxHealth-.5f){Toast("구매할 수 없음","생명력이 이미 가득합니다.",muted);return false;}
            if(offer!=null&&progress.Points<offer.Price){Toast("포인트 부족",offer.Price+" P가 필요합니다. 보유 "+progress.Points+" P",crimson);return false;}
            if(!run.Buy(item,progress))return false;
            SaveProgress();Sound(chimeSound,.6f);shopOpen=false;
            if(item==ShopItem.Heal)
            {
                float healed=Mathf.Min(PlayerMaxHealth-hero.hp,PlayerMaxHealth*TowerRun.ShopHealFraction);hero.hp+=healed;
                FloatText(hero.pos+Vector2.up*2,"+"+Mathf.CeilToInt(healed),TowerWorld.Teal);
                if(vfx!=null)vfx.Cast(SkillEffect.HealPulse,hero.pos+Vector2.up,1.8f,TowerWorld.Teal);
                Toast("구매 완료  ·  "+offer.Name,"생명력 "+Mathf.CeilToInt(healed)+" 회복  ·  남은 "+progress.Points+" P",TowerWorld.Teal);
            }
            else if(item==ShopItem.Vitality)
            {
                ReconcileLoadout();if(vfx!=null)vfx.Cast(SkillEffect.ShieldPulse,hero.pos+Vector2.up,1.8f,TowerWorld.Gold);
                Toast("구매 완료  ·  "+offer.Name,"최대 생명력 +"+Mathf.RoundToInt(TowerRun.VitalityHealth)+"  ·  남은 "+progress.Points+" P",gold);
            }
            else Toast("구매 완료  ·  "+offer.Name,"증강 카드를 고르세요  ·  남은 "+progress.Points+" P",silver);
            return true;
        }
    }
}
