using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerGame
    {
        internal bool oathLobby;
        private string oathSelection="michael";
        private void DrawOathSelection()
        {
            MenuBackdrop("서약을 선택하세요","OATH TRIAL");
            Label(new Rect(1230,83,300,35),"10층의 여정",20,brass,TextAnchor.MiddleRight);
            string[] ids={"michael","gabriel","raphael"};string[] roles={"공세","관통","수호"};
            for(int i=0;i<ids.Length;i++)
            {
                var d=TowerRun.GetDefinition(ids[i]);var oath=new OathState(ids[i]);float x=124+i*456;
                Color c=i==0?brass:i==1?silver:TowerWorld.Teal;bool selected=oathSelection==ids[i];
                Rect card=new Rect(x,190,440,478);
                if(Button(card,"",c))oathSelection=ids[i];SelectionFrame(card,c,selected);
                Light(new Rect(x+3,193,434,150),Alpha(c,.06f));
                Label(new Rect(x+30,215,310,43),d.Name,32,paper,TextAnchor.MiddleLeft,true);
                Label(new Rect(x+31,265,310,25),roles[i],16,c);
                Halo(new Rect(x+35,275,370,270),Alpha(c,selected?.45f:.23f));
                PortraitArt(new Rect(x+109,294,222,222),d.ArtIndex);
                Fill(new Rect(x+32,535,376,1),Alpha(c,.22f));
                Label(new Rect(x+30,553,380,32),oath.Name,23,c,TextAnchor.MiddleLeft,true);
                string summary=i==0?"검격과 함께 넓은 참격":i==1?"적을 꿰뚫는 빛의 광선":"검격에 응답하는 회복과 보호막";
                Label(new Rect(x+30,599,380,32),summary,18,muted);
            }
            Label(new Rect(124,700,1352,32),"검격과 순간 회피로 충전 · 다음 검격에 연계 발동",19,muted,TextAnchor.MiddleCenter);
            Fill(new Rect(64,760,1472,1),new Color(.5f,.56f,.63f,.15f));
            if(Button(new Rect(64,791,236,53),"돌아가기",muted))oathLobby=false;
            if(Button(new Rect(1240,21,296,34),"조작과 규칙",muted))help=true;
            Label(new Rect(342,796,650,42),"5·10층 보스  ·  신화 포인트 미적립",16,muted,TextAnchor.MiddleLeft);
            if(Button(new Rect(1080,786,456,64),TowerRun.GetDefinition(oathSelection).Name+"과 출발",brass,true,true))StartOathTrial(oathSelection);
        }
        private void DrawOathGauge()
        {
            var oath=run.Oath;if(oath==null)return;
            Color c=oath.CompanionId=="michael"?brass:oath.CompanionId=="gabriel"?silver:TowerWorld.Teal;
            Rect r=new Rect(566,786,500,80);Panel(r,c);
            if(oath.Ready)Halo(new Rect(550,741,530,165),Alpha(c,.22f+.08f*Mathf.Sin(Time.unscaledTime*3)));
            Diamond(new Vector2(590,813),9,c);
            Label(new Rect(610,795,285,30),oath.Name,20,paper,TextAnchor.MiddleLeft,true);
            Label(new Rect(889,796,155,29),oath.Ready?"연계 준비":oath.Charge+" / "+oath.RequiredCharge,16,oath.Ready?c:muted,TextAnchor.MiddleRight);
            float gap=5,width=(458-(oath.RequiredCharge-1)*gap)/oath.RequiredCharge;
            for(int i=0;i<oath.RequiredCharge;i++)
            {
                var segment=new Rect(587+i*(width+gap),839,width,10);
                Round(segment,i<oath.Charge?c:new Color(.16f,.20f,.25f),2);
                if(i<oath.Charge)Light(segment,new Color(1,1,1,.22f));
            }
        }
        private float OathDetailHeight(HeroState owner)
        {return run.Oath!=null&&(owner.Id=="player"||owner.Id==run.Oath.CompanionId)?151+run.Oath.Acquired.Count*112:0;}
        private float DrawOathDetails(HeroState owner,float y)
        {
            if(OathDetailHeight(owner)<=0)return y;
            var oath=run.Oath;
            Label(new Rect(12,y,840,31),oath.Name+"  ·  서약",22,brass,TextAnchor.UpperLeft,true);
            Label(new Rect(12,y+42,840,93),oath.Description.Replace("\n"," ")+"\n검격 +1 · 순간 회피 "+(oath.Has(OathState.Dodge)?"완충":"+2")+" · 필요 충전 "+oath.RequiredCharge+
                "\n완충 후 다음 검격에 발동",18,muted);y+=151;
            foreach(var a in oath.Acquired)
            {
                Round(new Rect(3,y,872,101),new Color(.072f,.095f,.123f),5);
                Label(new Rect(18,y+10,836,27),a.Name,19,brass,TextAnchor.UpperLeft,true);
                Label(new Rect(18,y+44,836,54),a.Description,17,muted);y+=112;
            }
            return y;
        }
    }
}