using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerGame
    {
        private string previewMyth="eden",mythMessage="";
        private int previewCompanion;
        private void DrawMythPortrait(Rect rect,int artIndex)
        {
            var sprite=TowerMythArt.PortraitSprite(artIndex);var texture=sprite.texture;var cell=sprite.rect;
            float scale=Mathf.Min(rect.width,rect.height)/Mathf.Max(cell.width,cell.height);
            float width=cell.width*scale,height=cell.height*scale;
            var destination=new Rect(rect.center.x-width*.5f,rect.center.y-height*.5f,width,height);
            GUI.DrawTextureWithTexCoords(destination,texture,new Rect(cell.x/texture.width,cell.y/texture.height,cell.width/texture.width,cell.height/texture.height),true);
        }
        private void DrawMythSelection()
        {
            MenuBackdrop("서약의 탑","TOWERBOUND");
            Label(new Rect(1150,72,180,47),progress.Points+" P",28,brass,TextAnchor.MiddleRight,true);
            if(Button(new Rect(1364,76,172,44),"도움말",muted))help=true;
            Label(new Rect(65,176,450,27),"신화",17,muted);
            for(int i=0;i<TowerMythCatalog.Definitions.Count;i++)
            {
                var myth=TowerMythCatalog.Definitions[i];float x=64+i%2*272,y=220+i/2*98;
                bool owned=progress.IsUnlocked(myth.Id),viewing=previewMyth==myth.Id;
                Rect r=new Rect(x,y,256,82);Color c=owned?brass:muted;
                if(Button(r,"",c)){previewMyth=myth.Id;previewCompanion=0;mythMessage="";}
                SelectionFrame(r,brass,viewing);
                Label(new Rect(x+19,y+13,218,28),myth.Name,20,owned?paper:muted,TextAnchor.MiddleLeft,true);
                Label(new Rect(x+19,y+47,215,22),progress.SelectedMythId==myth.Id?"선택됨":owned?"보유":myth.Price+" P  ·  잠김",13,viewing?brass:muted);
            }
            var current=TowerMythCatalog.Get(previewMyth)??TowerMythCatalog.Definitions[0];
            previewCompanion=Mathf.Clamp(previewCompanion,0,current.CompanionIds.Count-1);
            Panel(new Rect(656,176,880,516),brass);
            Label(new Rect(690,196,785,51),current.Name,31,paper,TextAnchor.MiddleLeft,true);
            Label(new Rect(692,260,787,50),current.Description,17,muted);
            var dfn=TowerRun.GetDefinition(current.CompanionIds[previewCompanion]);
            Halo(new Rect(668,304,320,320),Alpha(brass,.25f));PortraitArt(new Rect(697,337,252,252),dfn.ArtIndex);
            Label(new Rect(980,329,510,41),dfn.Name,29,paper,TextAnchor.MiddleLeft,true);
            Label(new Rect(982,377,510,27),RoleName(dfn.Role)+"  ·  "+dfn.Title,16,brass);
            Label(new Rect(982,427,510,28),dfn.InnateTrait,20,paper,TextAnchor.MiddleLeft,true);
            Label(new Rect(982,469,510,94),dfn.InnateDescription,18,muted);
            for(int i=0;i<current.CompanionIds.Count;i++)
            {
                var d=TowerRun.GetDefinition(current.CompanionIds[i]);float x=981+i*102;Rect r=new Rect(x,586,86,80);
                if(Button(r,"",silver))previewCompanion=i;SelectionFrame(r,brass,previewCompanion==i);
                PortraitArt(new Rect(x+7,592,72,67),d.ArtIndex);
            }
            bool unlocked=progress.IsUnlocked(current.Id),selected=progress.SelectedMythId==current.Id;
            if(!unlocked)
            {
                if(Button(new Rect(1046,718,490,57),"신화 해금  ·  "+current.Price+" P",brass,progress.Points>=current.Price,true))
                {if(progress.TryPurchase(current.Id)){SaveProgress();mythMessage="신화 해금 완료";Sound(chimeSound,.5f);}}
                if(progress.Points<current.Price)Label(new Rect(656,731,366,30),"층 클리어마다 1 P 획득",16,muted);
            }
            else
            {
                if(Button(new Rect(656,718,355,57),selected?"선택한 신화":"이 신화 선택",silver,!selected))
                {if(progress.TrySelect(current.Id)){SaveProgress();NewRun();intro=true;mythMessage="";}}
                if(Button(new Rect(1046,718,490,57),"등반 시작",brass,selected,true))StartClimb();
            }
            if(!string.IsNullOrEmpty(mythMessage))Label(new Rect(64,727,544,39),mythMessage,16,brass);
            Fill(new Rect(64,804,1472,1),new Color(.5f,.56f,.63f,.15f));
            Label(new Rect(64,829,480,29),"서약 연계  ·  세 천사와 함께하는 10층",18,paper,TextAnchor.MiddleLeft);
            if(Button(new Rect(566,820,318,50),"서약 시험장",brass,true,true))oathLobby=true;
            Label(new Rect(1000,831,536,28),"일반 등반 25층",16,muted,TextAnchor.MiddleRight);
        }
    }
}