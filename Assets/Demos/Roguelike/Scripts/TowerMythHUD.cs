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
        // Title screen: the climb's structure on the left, the mythology synergy codex on the right.
        private void DrawTitle()
        {
            MenuBackdrop("서약의 탑","TOWERBOUND  ·  100 FLOORS");
            Label(new Rect(1150,72,180,47),progress.Points+" P",28,brass,TextAnchor.MiddleRight,true);
            if(Button(new Rect(1364,76,172,44),"도움말",muted))help=true;
            Panel(new Rect(64,176,560,516),brass);
            Label(new Rect(96,200,500,36),"등반 안내",24,paper,TextAnchor.MiddleLeft,true);
            string[] heads={"100층","동료 영입","증강","갈림길","포인트"};
            string[] lines={"5층마다 테마와 보스가 바뀝니다. 보스는 테마마다 고유 기술을 씁니다.",
                "출발할 때, 그리고 30층과 60층에서 모든 신화의 동료 중 한 명을 고릅니다.",
                "5층마다 증강 3장 중 1장을 고릅니다. 엘리트 층을 깨도 한 장 더 얻습니다.",
                "5층마다 일반 출구 대신 휴식 · 상점 · 엘리트 문 3개 중 하나를 고릅니다.",
                "층을 클리어할 때마다 1 P를 얻고, 상인에게 가서 한 번 거래할 때 씁니다."};
            for(int i=0;i<heads.Length;i++)
            {
                float y=256+i*84;Diamond(new Vector2(104,y+13),7,brass);
                Label(new Rect(124,y,460,26),heads[i],18,brass,TextAnchor.MiddleLeft,true);
                Label(new Rect(124,y+29,470,48),lines[i],15,muted);
            }
            Panel(new Rect(656,176,880,516),brass);
            Label(new Rect(690,196,520,36),"신화 시너지",24,paper,TextAnchor.MiddleLeft,true);
            Label(new Rect(1130,200,380,30),"같은 신화 동료 2명 · 3명",15,muted,TextAnchor.MiddleRight);
            for(int i=0;i<TowerMythCatalog.Synergies.Count;i++)
            {
                var synergy=TowerMythCatalog.Synergies[i];var myth=TowerMythCatalog.Get(synergy.MythId);float y=244+i*44;
                if(i%2==0)Round(new Rect(672,y-2,848,44),new Color(1,1,1,.025f),4);
                Label(new Rect(690,y,150,20),myth.Name,15,paper,TextAnchor.MiddleLeft,true);
                Label(new Rect(690,y+20,150,20),synergy.Name,13,brass);
                Label(new Rect(850,y+1,660,20),"2명  "+synergy.Describe(2),13,muted);
                Label(new Rect(850,y+20,660,20),"3명  "+synergy.Describe(3),13,muted);
            }
            Fill(new Rect(64,708,1472,1),new Color(.5f,.56f,.63f,.15f));
            if(Button(new Rect(1046,722,490,57),"등반 시작",brass,true,true))StartClimb();
            Label(new Rect(64,733,940,32),"Enter로도 시작할 수 있습니다  ·  출발과 함께 첫 동료를 영입합니다",16,muted);
            Fill(new Rect(64,804,1472,1),new Color(.5f,.56f,.63f,.15f));
            Label(new Rect(64,829,480,29),"서약 연계  ·  세 천사와 함께하는 10층",18,paper,TextAnchor.MiddleLeft);
            if(Button(new Rect(566,820,318,50),"서약 시험장",brass,true,true))oathLobby=true;
            Label(new Rect(1000,831,536,28),"일반 등반 100층",16,muted,TextAnchor.MiddleRight);
        }
    }
}
