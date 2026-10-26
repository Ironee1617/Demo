using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // The story campaign around the tower: the lantern village on Lumen (tutorial and hub), chapter
    // expeditions that each restore one mythology, and the story pages shown between them.
    public sealed partial class TowerGame
    {
        private const string CampaignKey="Tower.Campaign.v1";
        internal const float AwakenedBossHealth=1.3f;
        internal TowerCampaignSave campaign;
        internal bool village;
        internal enum VillagePanel { None, Gate, Shrine }
        internal VillagePanel villagePanel;
        private bool chapterRecorded;
        private IReadOnlyList<TowerStoryPage> storyPages;private int storyPage;private string storyEyebrow;private Action storyDone;
        internal bool StoryOpen{get{return storyPages!=null;}}
        internal bool VillageBlocking{get{return villagePanel!=VillagePanel.None||StoryOpen;}}

        // The village runs left to right: tutorial signs and ledges, the training yard, the shrine,
        // the plaza where restored gods wait, then the tower gate where the next chapter's gods stand.
        internal const float VillageSpawnX=-13,SteleX=-14.6f,ShrineX=11.2f;
        internal static readonly float[] DummyXs={4.6f,7.2f};
        private static readonly float[] CandidateXs={20.1f,22.5f};
        // Five places in the plaza by the shrine, then the clearing under the tutorial ledges; the gods of the
        // most recently restored myths take them first.
        private static readonly float[] RosterXs={13.4f,14.65f,15.9f,17.15f,18.4f,-.9f,.5f,1.9f};
        // Neighbouring signs alternate their label height so the labels never overlap.
        private struct Sign { public float x,y,lift;public string keys,text; }
        private static readonly Sign[] TutorialSigns={
            new Sign{x=-11.8f,y=TowerWorld.Ground,lift=1.85f,keys="A / D",text="이동"},
            new Sign{x=-8.6f,y=TowerWorld.Ground,lift=2.75f,keys="Space",text="점프 · 공중에서 2단 점프"},
            new Sign{x=-3.6f,y=-.4f,lift=1.85f,keys="S",text="발판 아래로"},
            new Sign{x=2.8f,y=TowerWorld.Ground,lift=1.85f,keys="J",text="공격 · 3타째 마무리 일격"},
            new Sign{x=8.4f,y=TowerWorld.Ground,lift=2.75f,keys="Shift → J",text="회피 직후 돌진 베기"}};
        private sealed class Dummy { public Transform root,body;public Vector2 pos;public float wobble,flash;public int hits; }
        private readonly List<Dummy> dummies=new List<Dummy>();
        private readonly List<Fighter> villagers=new List<Fighter>();
        private readonly List<SpriteRenderer> shrineLights=new List<SpriteRenderer>();
        private GameObject villageRoot;
        private float villageClock;

        internal void SaveCampaign()
        {if(campaign==null||SuppressProgressPersistence||SmokeControlled)return;PlayerPrefs.SetString(CampaignKey,campaign.Save());PlayerPrefs.Save();}

        /// <summary>Title → (prologue on a new game) → the village.</summary>
        internal void BeginCampaign()
        {
            intro=false;oathLobby=false;
            if(!village)EnterVillage();
            if(!campaign.PrologueSeen)ShowStory(TowerCampaign.Prologue,"PROLOGUE",()=>{campaign.PrologueSeen=true;SaveCampaign();});
        }
        internal void ResetCampaign()
        {
            campaign=new TowerCampaignSave();SaveCampaign();EnterVillage();intro=true;
            Toast("새로운 여정","되찾은 신화와 성장이 모두 초기화되었습니다",muted,3.2f);
        }

        internal void EnterVillage()
        {
            seed=Environment.TickCount&0x7fffffff;
            ResetActors();
            village=true;chapterRecorded=false;
            run=campaign.Preview(seed,new string[0]);
            SpawnHero(new Vector2(VillageSpawnX,TowerWorld.Ground));
            BeginFloorRecord();
            using(TowerProfiling.SetTheme.Auto()){world.SetTheme(TowerThemeId.Village,1,seed,VillageLayout());world.SetFloorFeature(FloorType.Normal,false);}
            music.PlayTheme(TowerThemeId.Village);
            cameraCeiling=Mathf.Max(1.8f,world.HighestPlatform+4);
            hero.facing=1;hero.grounded=true;hero.support=-1;hero.lastX=hero.pos.x;previousHeroPosition=hero.pos;
            view.transform.position=new Vector3(-7.5f,0,-10);appliedShake=Vector3.zero;
            BuildVillage();
            var next=campaign.NextChapter;
            if(!campaign.TutorialDone)Notice(TowerCampaign.Village+"  ·  표지판을 따라 조작을 익히고 오른쪽 끝 탑의 문으로 가세요",6);
            else if(next==null)Notice("악신이 쓰러진 루멘의 고요한 밤",4);
            else Notice(TowerCampaign.Village+"  ·  "+next.Numeral+" "+next.Title+" 원정을 준비하세요",3.5f);
        }

        /// <summary>Flat ground with three stepped ledges for the jump, double jump and drop lessons.</summary>
        private static TowerFloorLayout VillageLayout()
        {
            var ground=new TowerSurface(new[]{TowerTerrainGenerator.TerrainLeft,TowerTerrainGenerator.TerrainRight},new[]{TowerWorld.Ground,TowerWorld.Ground});
            var ledges=new List<TowerPlatform>{Ledge(-6.6f,-1.8f,3f,1),Ledge(-3.4f,-.4f,2.8f,2),Ledge(-.3f,1f,2.6f,3)};
            return new TowerFloorLayout(9090,TowerCampaign.Village,1,ground,ledges);
        }
        private static TowerPlatform Ledge(float x,float y,float width,int tier)
        {return new TowerPlatform(new TowerSurface(new[]{x-width*.5f,x+width*.5f},new[]{y,y}),tier);}

        private void BuildVillage()
        {
            villageRoot=new GameObject("Lantern village");villageRoot.transform.SetParent(transform,false);
            foreach(var sign in TutorialSigns)StoryProp("signpost",new Vector2(sign.x,sign.y),-8);
            StoryProp("stele",new Vector2(SteleX,TowerWorld.Ground),-8);
            var shrine=StoryProp("shrine",new Vector2(ShrineX,TowerWorld.Ground),-9);
            // One flame on the shrine for every restored mythology.
            shrineLights.Clear();
            for(int i=0;i<TowerCampaign.Chapters.Count;i++)
            {
                float x=ShrineX-1.24f+i*.31f;bool lit=campaign.IsRestored(TowerCampaign.Chapters[i]);
                var glow=TowerWorld.SpriteObject(villageRoot.transform,"Restored myth flame",new Vector2(x,TowerWorld.Ground+1.42f),Vector2.one*(lit?.42f:.18f),
                    lit?new Color(1,.72f,.36f,.85f):new Color(.4f,.45f,.6f,.35f),-7,TowerWorld.Glow).GetComponent<SpriteRenderer>();
                if(lit)shrineLights.Add(glow);
            }
            if(shrine!=null&&campaign.IsComplete)
                TowerWorld.SpriteObject(villageRoot.transform,"Shrine dawn glow",new Vector2(ShrineX,TowerWorld.Ground+2.6f),new Vector2(5,4),new Color(1,.85f,.55f,.35f),-10,TowerWorld.Glow);
            dummies.Clear();
            foreach(float x in DummyXs)
            {
                var root=new GameObject("Training dummy").transform;root.SetParent(villageRoot.transform,false);root.localPosition=new Vector3(x,TowerWorld.Ground,0);
                TowerWorld.SpriteObject(root,"Shadow",new Vector2(0,.025f),new Vector2(1.1f,.15f),new Color(0,0,0,.34f),-1,TowerWorld.Disc);
                var body=StoryProp("dummy",Vector2.zero,20,root);
                dummies.Add(new Dummy{root=root,body=body!=null?body.transform:root,pos=new Vector2(x,TowerWorld.Ground)});
            }
            villagers.Clear();
            // Gods of restored myths wait in the plaza; the next chapter's two gods stand at the tower gate.
            var restored=new List<string>();
            for(int c=campaign.ClearedChapters-1;c>=0;c--)restored.AddRange(TowerCampaign.Chapters[c].Candidates);
            for(int i=0;i<restored.Count&&i<RosterXs.Length;i++)SpawnVillager(restored[i],RosterXs[i],i%2==0?1:-1);
            var next=campaign.NextChapter;
            if(next!=null)for(int i=0;i<next.Candidates.Count;i++)SpawnVillager(next.Candidates[i],CandidateXs[i],-1);
        }
        private void SpawnVillager(string id,float x,int facing)
        {
            var definition=TowerRun.GetDefinition(id);if(definition==null)return;
            var npc=Spawn(definition.Name,definition.ArtIndex,false,new Vector2(x,TowerWorld.Ground));
            npc.id=id;npc.facing=facing;npc.grounded=true;npc.root.transform.SetParent(villageRoot.transform,true);
            npc.art.transform.localScale=Vector3.one*.82f;var shadow=npc.root.transform.Find("Shadow");if(shadow)shadow.localScale=new Vector3(.9f,.12f,1);
            npc.art.SetLocomotion(true,0,false,0,0);villagers.Add(npc);
        }
        private GameObject StoryProp(string name,Vector2 at,int order,Transform parent=null)
        {
            var sprite=StorySprite(name);if(sprite==null)return null;
            var go=new GameObject("Village "+name);go.transform.SetParent(parent??villageRoot.transform,false);go.transform.localPosition=at;
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sortingOrder=order;return go;
        }
        private static readonly Dictionary<string,Sprite> storySprites=new Dictionary<string,Sprite>();
        /// <summary>Village props painted by Tools/TowerArt/StoryPainter at 96 texels per unit, pivoted at their base.</summary>
        private static Sprite StorySprite(string name)
        {
            Sprite sprite;if(storySprites.TryGetValue(name,out sprite)&&sprite!=null)return sprite;
            var texture=Resources.Load<Texture2D>("Tower/Story/"+name);if(texture==null)return null;
            sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,0),96,0,SpriteMeshType.FullRect);
            sprite.name=name;storySprites[name]=sprite;return sprite;
        }
        private void ClearVillage()
        {
            village=false;villagePanel=VillagePanel.None;storyPages=null;storyDone=null;
            if(villageRoot){villageRoot.SetActive(false);Destroy(villageRoot);}
            villageRoot=null;dummies.Clear();villagers.Clear();shrineLights.Clear();
        }

        private void TickVillage(float dt)
        {
            villageClock+=dt;
            foreach(var npc in villagers)
            {
                // Waiting gods turn toward the hero when the hero comes close.
                if(Mathf.Abs(hero.pos.x-npc.pos.x)<3.2f)npc.facing=hero.pos.x>npc.pos.x?1:-1;
                npc.art.SetPose(false,false,false,npc.facing,dt);
            }
            foreach(var dummy in dummies)
            {
                dummy.wobble=Mathf.MoveTowards(dummy.wobble,0,dt*2.2f);dummy.flash=Mathf.Max(0,dummy.flash-dt);
                float swing=Mathf.Sin(villageClock*18)*dummy.wobble*9;
                dummy.body.localRotation=Quaternion.Euler(0,0,swing);
                var renderer=dummy.body.GetComponent<SpriteRenderer>();if(renderer)renderer.color=dummy.flash>0?new Color(1,.78f,.7f):Color.white;
            }
            for(int i=0;i<shrineLights.Count;i++)
            {var c=shrineLights[i].color;c.a=.7f+Mathf.Sin(villageClock*3+i*1.3f)*.15f;shrineLights[i].color=c;}
            if(!campaign.TutorialDone&&hero.pos.x>17.5f)
            {
                campaign.TutorialDone=true;SaveCampaign();
                Toast("수련 완료","탑의 문 앞에서 [E]를 눌러 원정을 준비하세요",TowerWorld.Teal,4);
            }
        }

        /// <summary>The training dummies take every sword hit in the village and sway back.</summary>
        private bool StrikeDummies(float reach,float damage,bool heavy)
        {
            bool hit=false;
            foreach(var dummy in dummies)
            {
                float dx=dummy.pos.x-hero.pos.x;
                if(dx*hero.facing<-.4f||Mathf.Abs(dx)>reach+.3f||Mathf.Abs(hero.pos.y-dummy.pos.y)>1.9f)continue;
                dummy.hits++;dummy.wobble=heavy?1.3f:.8f;dummy.flash=.12f;hit=true;
                FloatText(dummy.pos+Vector2.up*2.1f,Mathf.CeilToInt(damage).ToString(),heavy?brass:TowerWorld.Gold);
                Burst(dummy.pos+Vector2.up*1.1f,TowerWorld.Gold,4,.3f);Sound(hitSound,.3f);
            }
            return hit;
        }

        private enum VillageSpot { None, Stele, Shrine, Gate, Villager }
        private VillageSpot NearestSpot(out Fighter npc)
        {
            npc=null;float best=float.PositiveInfinity;var spot=VillageSpot.None;float x=hero.pos.x;
            Action<VillageSpot,float,float> consider=(kind,at,range)=>{float d=Mathf.Abs(x-at);if(d<range&&d<best){best=d;spot=kind;}};
            consider(VillageSpot.Stele,SteleX,1.3f);consider(VillageSpot.Shrine,ShrineX,1.9f);consider(VillageSpot.Gate,TowerWorld.ExitX,2.1f);
            foreach(var v in villagers){float d=Mathf.Abs(x-v.pos.x);if(d<.9f&&d<best){best=d;spot=VillageSpot.Villager;npc=v;}}
            return spot;
        }
        private string SpotPrompt()
        {
            Fighter npc;var spot=NearestSpot(out npc);
            switch(spot)
            {
                case VillageSpot.Stele: return "이야기 비석  ·  루멘의 이야기를 다시 읽기";
                case VillageSpot.Shrine: return "신화의 제단  ·  되찾은 신화 보기";
                case VillageSpot.Gate: return campaign.IsComplete?"서약의 탑  ·  악신은 쓰러졌습니다":"서약의 탑  ·  원정 준비";
                case VillageSpot.Villager: return TowerRun.GetDefinition(npc.id).Name+"  ·  대화하기";
                default: return null;
            }
        }
        private bool IsCandidate(string id)
        {var next=campaign.NextChapter;if(next==null)return false;foreach(string c in next.Candidates)if(c==id)return true;return false;}
        internal void InteractVillage()
        {
            Fighter npc;var spot=NearestSpot(out npc);
            if(spot==VillageSpot.Stele)ShowStory(TowerCampaign.Prologue,"PROLOGUE",null);
            else if(spot==VillageSpot.Shrine){villagePanel=VillagePanel.Shrine;Sound(chimeSound,.3f);}
            else if(spot==VillageSpot.Gate)OpenGate();
            else if(spot==VillageSpot.Villager)
            {
                var definition=TowerRun.GetDefinition(npc.id);
                foreach(var chapter in TowerCampaign.Chapters)
                    if(chapter.Line(npc.id)!="")Toast(definition.Name+"  ·  "+definition.Title,"「"+chapter.Line(npc.id)+"」",TierColor(definition.Tier),4);
            }
        }
        /// <summary>The gate shows the next chapter, its companion slots and the gods its draft deals from.</summary>
        internal void OpenGate(){villagePanel=VillagePanel.Gate;Sound(chimeSound,.3f);}

        /// <summary>
        /// Leaves the village for the next chapter. After its intro page the expedition opens with companion
        /// cards drawn like augments, one set per slot; <paramref name="kept"/> skips the slots it already fills.
        /// </summary>
        internal bool StartExpedition(IList<string> kept=null)
        {
            var chapter=campaign.NextChapter;if(chapter==null||!campaign.CanDepart(kept))return false;
            seed=Environment.TickCount&0x7fffffff;
            StartRun(campaign.BeginChapter(seed,kept));
            chapterRecorded=false;
            ShowStory(new[]{new TowerStoryPage(chapter.Title+"  ·  "+chapter.Subtitle,chapter.Intro)},chapter.Numeral+"  /  "+chapter.Floors,null);
            return true;
        }
        /// <summary>After a defeat: the same chapter again with the same companions, without a new draft.</summary>
        internal void RetryExpedition()
        {
            var kept=new List<string>();foreach(var c in run.Companions)kept.Add(c.Id);
            if(!StartExpedition(kept))EnterVillage();
        }
        private void CompleteChapter()
        {
            chapterRecorded=true;
            if(campaign.RecordChapterClear(run))SaveCampaign();
            Sound(chimeSound,1);
        }
        /// <summary>Leaves a finished chapter: the epilogue follows the evil god's fall, otherwise straight home.</summary>
        internal void ReturnFromChapter()
        {
            if(run.Chapter!=null&&run.Chapter.IsFinal&&campaign.IsComplete&&!campaign.EndingSeen)
            {ShowStory(TowerCampaign.Epilogue,"EPILOGUE",()=>{campaign.EndingSeen=true;SaveCampaign();EnterVillage();});return;}
            EnterVillage();
        }

        internal void ShowStory(IReadOnlyList<TowerStoryPage> pages,string eyebrow,Action done)
        {storyPages=pages;storyPage=0;storyEyebrow=eyebrow;storyDone=done;Sound(chimeSound,.25f);}
        internal void AdvanceStory()
        {
            if(!StoryOpen)return;
            if(++storyPage<storyPages.Count){Sound(chimeSound,.18f);return;}
            var done=storyDone;storyPages=null;storyDone=null;if(done!=null)done();
        }
        internal void SkipStory(){if(!StoryOpen)return;storyPage=storyPages.Count-1;AdvanceStory();}
    }
}
