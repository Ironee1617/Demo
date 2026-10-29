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
        // Title screen: the game's name and its actions on the left, the tower standing in the painted night behind,
        // and the nine chapters with their state on the right.
        private void DrawTitle()
        {
            ScreenBackdrop(true);
            // The light on the tower's spire (painted at 1560, 118 of the 2560×1440 backdrop) breathes slowly.
            float breath=.5f+.5f*Mathf.Sin(Time.unscaledTime*1.3f);
            Halo(new Rect(978-70-breath*20,66-70-breath*20,140+breath*40,140+breath*40),new Color(1,.85f,.55f,.18f+.14f*breath));
            Halo(new Rect(-40,40,820,320),new Color(.95f,.75f,.4f,.07f));
            Label(new Rect(100,96,700,22),"TOWERBOUND   ·   "+TowerCampaign.Chapters.Count+" CHAPTERS   ·   90 FLOORS",13,brass,TextAnchor.MiddleLeft,true);
            GildedTitle(new Rect(90,112,700,112),"서약의 탑",90);
            Title(new Rect(100,226,600,30),"신화를 잃은 신들의 탑",22,new Color(.88f,.82f,.68f),TextAnchor.MiddleLeft);
            Divider(new Rect(96,266,470,10),.9f,true);
            Label(new Rect(100,288,520,66),"당신은 행성 "+TowerCampaign.Planet+"의 단 하나뿐인 신입니다.\n악신 "+TowerCampaign.EvilGod+"이 꽂은 탑을 올라, 층마다 갇힌 지구의 신화를 되찾으십시오.",16,new Color(.82f,.84f,.9f));
            bool started=campaign.PrologueSeen||campaign.ClearedChapters>0;var upcoming=campaign.NextChapter;
            float y=384;
            if(Button(new Rect(96,y,420,62),started?"이어하기":"모험 시작",brass,true,true))StartClimb();y+=80;
            if(Button(new Rect(96,y,420,50),"서약 시험장  ·  세 천사와 10층",silver)){oathLobby=true;confirmReset=false;}y+=62;
            if(Button(new Rect(96,y,420,50),"스킬 연습장  ·  모든 스킬 이펙트 시험",TowerWorld.Teal)){confirmReset=false;EnterPractice();return;}y+=62;
            if(Button(new Rect(96,y,420,50),"조작과 규칙",muted))help=true;y+=62;
            if(started&&Button(new Rect(96,y,420,50),confirmReset?"정말 처음부터 시작할까요?":"새로운 여정",confirmReset?crimson:muted))
            {if(confirmReset){confirmReset=false;ResetCampaign();}else confirmReset=true;}
            ShadowLabel(new Rect(100,824,980,26),!started?"Enter로도 시작할 수 있습니다  ·  등불 마을의 수련장에서 조작을 익히고 탑으로 향합니다":
                upcoming==null?"악신을 쓰러뜨렸습니다  ·  마을에서 되찾은 신들을 만날 수 있습니다":"다음 원정  ·  "+upcoming.Numeral+" "+upcoming.Title+"  "+upcoming.Floors,14,muted,TextAnchor.MiddleLeft);
            DrawGold(new Rect(1136,40,400,40),24,brass,"");
            Panel(new Rect(1136,104,400,704),brass);
            Title(new Rect(1166,124,340,32),"탑의 아홉 장",22,paper,TextAnchor.MiddleLeft);
            Label(new Rect(1166,156,340,22),RestoredLine(),13,brass);
            Divider(new Rect(1160,182,352,10),.6f);
            for(int i=0;i<TowerCampaign.Chapters.Count;i++)
            {
                var ch=TowerCampaign.Chapters[i];bool restored=campaign.IsRestored(ch),next=ch==campaign.NextChapter;float ry=200+i*65;
                Rect row=new Rect(1154,ry,364,59);
                if(next)Slot(row,TowerWorld.Teal,true);else if(i%2==0)Slot(row,.5f);
                Color c=restored?brass:next?TowerWorld.Teal:new Color(.42f,.46f,.56f);
                if(next)Halo(new Rect(row.x-2,row.y+6,44,44),Alpha(c,.35f+.15f*Mathf.Sin(Time.unscaledTime*3)));
                Diamond(new Vector2(row.x+20,row.y+29),restored||next?14:10,c);if(!restored)Diamond(new Vector2(row.x+20,row.y+29),restored||next?7:6,night);
                Title(new Rect(row.x+40,row.y+6,220,26),ch.Numeral+"  "+ch.Title,17,restored||next?paper:muted,TextAnchor.MiddleLeft);
                Label(new Rect(row.x+40,row.y+33,230,20),ch.Floors+"  ·  "+ch.Subtitle,12,Alpha(muted,restored||next?1:.75f));
                Label(new Rect(row.xMax-110,row.y,96,row.height),restored?"되찾음":next?"다음 원정":"봉인됨",13,restored?brass:next?TowerWorld.Teal:Alpha(muted,.7f),TextAnchor.MiddleRight,true);
            }
        }
    }
}
