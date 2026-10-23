using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerGame
    {
        private void DrawMythPortrait(Rect rect,int artIndex)
        {
            var sprite=TowerMythArt.PortraitSprite(artIndex);var texture=sprite.texture;var cell=sprite.rect;
            float scale=Mathf.Min(rect.width,rect.height)/Mathf.Max(cell.width,cell.height);
            float width=cell.width*scale,height=cell.height*scale;
            var destination=new Rect(rect.center.x-width*.5f,rect.center.y-height*.5f,width,height);
            GUI.DrawTextureWithTexCoords(destination,texture,new Rect(cell.x/texture.width,cell.y/texture.height,cell.width/texture.width,cell.height/texture.height),true);
        }
        // Title screen: the story so far on the left, the tower's nine chapters and their state on the right.
        private void DrawTitle()
        {
            MenuBackdrop("서약의 탑","TOWERBOUND  ·  신화를 잃은 신들의 탑  ·  90 FLOORS");
            Label(new Rect(1150,72,180,47),progress.Points+" P",28,brass,TextAnchor.MiddleRight,true);
            if(Button(new Rect(1364,76,172,44),"도움말",muted))help=true;
            Panel(new Rect(64,176,560,516),brass);
            Label(new Rect(96,200,500,36),"이야기",24,paper,TextAnchor.MiddleLeft,true);
            string[] heads={"행성 "+TowerCampaign.Planet,"악신 "+TowerCampaign.EvilGod,"신화를 잃은 신들","유일신의 서약"};
            string[] lines={"당신은 행성 루멘의 단 하나뿐인 신입니다.",
                "지구를 삼킨 악신이 루멘의 대지에 탑을 꽂고, 지구의 신화들을 층마다 가두었습니다.",
                "부서진 지구를 건너온 신들이 자신의 신화를 되찾으려 당신과 함께 탑에 오릅니다.",
                "열 층마다 신화 하나를 되찾고 마을로 돌아와, 마지막 장에서 악신을 쓰러뜨리십시오."};
            for(int i=0;i<heads.Length;i++)
            {
                float y=256+i*104;Diamond(new Vector2(104,y+13),7,brass);
                Label(new Rect(124,y,460,26),heads[i],18,brass,TextAnchor.MiddleLeft,true);
                Label(new Rect(124,y+30,470,66),lines[i],15,muted);
            }
            Panel(new Rect(656,176,880,516),brass);
            Label(new Rect(690,196,520,36),"탑의 아홉 장",24,paper,TextAnchor.MiddleLeft,true);
            Label(new Rect(1130,200,380,30),RestoredLine(),15,brass,TextAnchor.MiddleRight);
            for(int i=0;i<TowerCampaign.Chapters.Count;i++)
            {
                var ch=TowerCampaign.Chapters[i];bool restored=campaign.IsRestored(ch),next=ch==campaign.NextChapter;float y=246+i*48;
                if(i%2==0)Round(new Rect(672,y-4,848,46),new Color(1,1,1,.025f),4);
                Label(new Rect(690,y,120,38),ch.Floors,14,muted,TextAnchor.MiddleLeft);
                Label(new Rect(806,y,250,38),ch.Numeral+"  "+ch.Title,17,restored||next?paper:muted,TextAnchor.MiddleLeft,true);
                Label(new Rect(1030,y,330,38),ch.Subtitle,14,muted,TextAnchor.MiddleLeft);
                Label(new Rect(1360,y,140,38),restored?"되찾음":next?"다음 원정":"봉인됨",14,restored?brass:next?TowerWorld.Teal:Alpha(muted,.7f),TextAnchor.MiddleRight,true);
            }
            Fill(new Rect(64,708,1472,1),new Color(.5f,.56f,.63f,.15f));
            bool started=campaign.PrologueSeen||campaign.ClearedChapters>0;
            if(Button(new Rect(1046,722,490,57),started?"이어하기":"모험 시작",brass,true,true))StartClimb();
            var upcoming=campaign.NextChapter;
            Label(new Rect(64,733,960,32),!started?"Enter로도 시작할 수 있습니다  ·  등불 마을의 수련장에서 조작을 익히고 탑으로 향합니다":
                upcoming==null?"악신을 쓰러뜨렸습니다  ·  마을에서 되찾은 신들을 만날 수 있습니다":"다음 원정  ·  "+upcoming.Numeral+" "+upcoming.Title+"  "+upcoming.Floors,16,muted);
            Fill(new Rect(64,804,1472,1),new Color(.5f,.56f,.63f,.15f));
            Label(new Rect(64,829,480,29),"서약 연계  ·  세 천사와 함께하는 10층",18,paper,TextAnchor.MiddleLeft);
            if(Button(new Rect(566,820,318,50),"서약 시험장",brass,true,true)){oathLobby=true;confirmReset=false;}
            if(started&&Button(new Rect(1218,820,318,50),confirmReset?"정말 처음부터 시작할까요?":"새로운 여정",confirmReset?crimson:muted))
            {if(confirmReset){confirmReset=false;ResetCampaign();}else confirmReset=true;}
        }
    }
}
