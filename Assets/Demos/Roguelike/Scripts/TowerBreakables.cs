using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Breakable caches on every tower floor (TowerLoot) and the hearts and coins they scatter. A sword blow breaks
    // them into shards cut from their own painting; coins wait where they fell while monsters remain and fly to the
    // hero once the floor is cleared. Hearts heal only a wounded hero and otherwise stay on the ground.
    public sealed partial class TowerGame
    {
        internal sealed class Cache { public GameObject root; public Transform body; public SpriteRenderer renderer; public TowerCacheDefinition definition; public Vector2 pos; public int support,hits; public float height,wobble,flash; }
        internal sealed class Pickup { public GameObject root; public Transform art; public SpriteRenderer glow; public LootKind kind; public Vector2 pos,velocity; public float age,phase,size; public bool landed,homing; }
        private sealed class Shard { public GameObject root; public SpriteRenderer renderer; public Vector2 pos,velocity; public float angle,spin,life; public bool bounced; }
        internal readonly List<Cache> caches=new List<Cache>();
        internal readonly List<Pickup> pickups=new List<Pickup>();
        private readonly List<Shard> shards=new List<Shard>();
        internal const float PickupReach=.8f,MagnetRadius=2.1f,PickupGravity=22,HomingSpeed=22;
        // The paintings are drawn at 96 texels per unit and shown a little larger so they read beside the heroes.
        internal const float CacheScale=1.2f,CoinScale=1.4f,HeartScale=1.2f;
        internal int GoldCollected{get;private set;}
        internal int CachesBroken{get;private set;}
        internal int HeartsCollected{get;private set;}
        // Scripted fights skip caches so a stray heart never heals them; the loot fixture asks for them explicitly.
        internal bool LootFixture;
        internal bool LootActive{get{return run!=null&&!village&&TowerLoot.HasCaches(run);}}
        private bool goldUnsaved;
        private float goldFlash,lootClock;private int goldFlashAmount;
        private AudioClip coinSound;
        private static readonly Dictionary<string,Sprite> lootSprites=new Dictionary<string,Sprite>();
        private static readonly Dictionary<string,Sprite[]> shardSprites=new Dictionary<string,Sprite[]>();

        /// <summary>Painted by Tools/TowerArt/StoryPainter at 96 texels per unit: caches stand on their base, loot pivots at its centre.</summary>
        internal static Sprite LootSprite(string name,bool centred=false)
        {
            Sprite sprite;if(lootSprites.TryGetValue(name,out sprite)&&sprite!=null)return sprite;
            var texture=Resources.Load<Texture2D>("Tower/Breakables/"+name);if(texture==null)return null;
            sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),centred?new Vector2(.5f,.5f):new Vector2(.5f,0),96,0,SpriteMeshType.FullRect);
            sprite.name=name;lootSprites[name]=sprite;return sprite;
        }
        internal static Sprite PickupSprite(LootKind kind){return LootSprite(kind==LootKind.Heart?"heart":kind==LootKind.GoldCoin?"gold-coin":"coin",true);}
        // A 3x3 cut of the cache's painting; each cell flies off as one shard carrying its colours.
        private static Sprite[] ShardSprites(Sprite whole)
        {
            Sprite[] pieces;if(shardSprites.TryGetValue(whole.name,out pieces)&&pieces[0]!=null)return pieces;
            var texture=whole.texture;pieces=new Sprite[9];
            for(int row=0;row<3;row++)for(int column=0;column<3;column++)
            {
                float x0=Mathf.Round(texture.width*column/3f),x1=Mathf.Round(texture.width*(column+1)/3f),y0=Mathf.Round(texture.height*row/3f),y1=Mathf.Round(texture.height*(row+1)/3f);
                pieces[row*3+column]=Sprite.Create(texture,new Rect(x0,y0,x1-x0,y1-y0),new Vector2(.5f,.5f),96,0,SpriteMeshType.FullRect);
            }
            shardSprites[whole.name]=pieces;return pieces;
        }

        private void SpawnCaches()
        {
            ClearLoot();
            if(!LootActive||SmokeControlled&&!LootFixture)return;
            foreach(var spot in TowerLoot.Place(world.CurrentLayout,run.Theme.Id,seed,run.Floor,run.FloorType,run.IsBranchFloor))
            {
                var sprite=LootSprite(spot.Definition.Id);if(sprite==null)continue;
                var root=new GameObject("Cache · "+spot.Definition.Name);root.transform.SetParent(transform,false);
                // Sunk a hair into the surface so a gentle slope never shows daylight under the base.
                root.transform.position=new Vector3(spot.X,spot.Y-.04f,0);root.transform.localScale=Vector3.one*CacheScale;
                TowerWorld.SpriteObject(root.transform,"Shadow",new Vector2(0,.04f),new Vector2(sprite.bounds.size.x*1.05f,.14f),new Color(0,0,0,.36f),17,TowerWorld.Disc);
                var body=new GameObject("Body");body.transform.SetParent(root.transform,false);
                var renderer=body.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sortingOrder=18;
                caches.Add(new Cache{root=root,body=body.transform,renderer=renderer,definition=spot.Definition,pos=new Vector2(spot.X,spot.Y),support=spot.Support,height=sprite.bounds.size.y*CacheScale});
            }
        }
        private void ClearLoot()
        {
            foreach(var c in caches)if(c.root)Destroy(c.root);caches.Clear();
            foreach(var p in pickups)if(p.root)Destroy(p.root);pickups.Clear();
            foreach(var s in shards)if(s.root)Destroy(s.root);shards.Clear();
        }
        /// <summary>A new run or the village: the floor's loot goes and this run's tallies restart.</summary>
        private void ResetLoot()
        {
            FlushGold();ClearLoot();GoldCollected=CachesBroken=HeartsCollected=0;goldFlash=0;
        }
        /// <summary>Coins still flying to the hero when the floor is left are banked rather than lost.</summary>
        private void BankLooseCoins()
        {
            foreach(var p in pickups)if(p.kind!=LootKind.Heart)AddGold(TowerLoot.GoldOf(p.kind));
        }
        private void FlushGold(){if(goldUnsaved)SaveProgress();}
        private void AddGold(int amount)
        {
            int added=progress.AddGold(TreeGold(amount));if(added<=0)return;
            GoldCollected+=added;goldUnsaved=true;
            goldFlashAmount=goldFlash>0?goldFlashAmount+added:added;goldFlash=1.4f;
        }

        /// <summary>The hero's sword breaks fragile vessels at once; sturdy containers take two blows, or one heavy blow.</summary>
        private bool StrikeCaches(float reach,bool heavy)
        {
            bool hit=false;
            for(int i=caches.Count-1;i>=0;i--)
            {
                var cache=caches[i];float dx=cache.pos.x-hero.pos.x;
                if(dx*hero.facing<-.5f||Mathf.Abs(dx)>reach+.45f||cache.pos.y+cache.height<hero.pos.y-.2f||cache.pos.y>hero.pos.y+1.9f)continue;
                hit=true;cache.hits+=heavy?2:1;
                if(cache.hits>=cache.definition.Hits){BreakCache(cache);continue;}
                cache.wobble=1;cache.flash=.12f;Sound(hitSound,.35f);
                if(vfx!=null){vfx.Hit(cache.pos+new Vector2(-hero.facing*.25f,cache.height*.55f),ChipColor(cache.definition));vfx.Dust(cache.pos,0,.35f);}
            }
            return hit;
        }
        private static Color ChipColor(TowerCacheDefinition definition)
        {Color c;return ColorUtility.TryParseHtmlString("#"+definition.ChipHex,out c)?c:TowerWorld.Gold;}
        private void BreakCache(Cache cache)
        {
            caches.Remove(cache);CachesBroken++;
            var sprite=cache.renderer.sprite;var pieces=ShardSprites(sprite);var random=new System.Random(unchecked(seed*31^run.Floor*977^CachesBroken*7919^Mathf.RoundToInt(cache.pos.x*100)));
            float width=sprite.texture.width/96f*CacheScale,height=sprite.texture.height/96f*CacheScale;
            for(int i=0;i<pieces.Length;i++)
            {
                Vector2 offset=new Vector2(((i%3)-1)*width/3,((i/3)+.5f)*height/3);
                var go=new GameObject("Cache shard");go.transform.SetParent(transform,false);go.transform.localScale=Vector3.one*CacheScale;
                var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=pieces[i];renderer.sortingOrder=22;
                float lift=(float)random.NextDouble();
                shards.Add(new Shard{root=go,renderer=renderer,pos=cache.pos+offset,
                    velocity=new Vector2(offset.x*7+hero.facing*1.2f+((float)random.NextDouble()-.5f)*2.4f,2.6f+lift*3.6f+offset.y*1.5f),
                    spin=((float)random.NextDouble()-.5f)*900,life=.85f+lift*.35f});
            }
            Color chip=ChipColor(cache.definition);
            if(vfx!=null){vfx.Hit(cache.pos+Vector2.up*cache.height*.5f,chip);vfx.Dust(cache.pos,0,1.1f);}
            Sound(hitSound,.6f);Shake(.05f,.1f);
            Vector2 mouth=cache.pos+Vector2.up*Mathf.Min(.6f,cache.height*.6f);
            foreach(var drop in TowerLoot.Roll(random,cache.definition,run.Floor))DropLoot(drop.Kind,mouth,random);
            Destroy(cache.root);
        }
        internal Pickup DropLoot(LootKind kind,Vector2 at,System.Random random)
        {
            var root=new GameObject(kind==LootKind.Heart?"Heart pickup":"Coin pickup");root.transform.SetParent(transform,false);root.transform.position=at;
            bool heart=kind==LootKind.Heart;
            float size=heart?HeartScale:CoinScale;
            var glow=TowerWorld.SpriteObject(root.transform,"Glow",Vector2.zero,Vector2.one*(heart?1.2f:kind==LootKind.GoldCoin?1.15f:.85f),heart?new Color(1,.3f,.36f,.4f):new Color(1,.82f,.36f,.34f),25,TowerWorld.Glow);
            var art=new GameObject("Art");art.transform.SetParent(root.transform,false);art.transform.localScale=Vector3.one*size;
            var renderer=art.AddComponent<SpriteRenderer>();renderer.sprite=PickupSprite(kind);renderer.sortingOrder=26;
            var p=new Pickup{root=root,art=art.transform,glow=glow.GetComponent<SpriteRenderer>(),kind=kind,pos=at,size=size,phase=(float)random.NextDouble()*6.28f,
                velocity=new Vector2(((float)random.NextDouble()-.5f)*5.2f,4.8f+(float)random.NextDouble()*3)};
            pickups.Add(p);return p;
        }
        /// <summary>How far above its surface a piece of loot rests: half its drawn height.</summary>
        internal static float RestHeight(LootKind kind){return kind==LootKind.Heart?.21f*HeartScale:kind==LootKind.GoldCoin?.2f*CoinScale:.15f*CoinScale;}
        /// <summary>Height of the highest surface at x at or below y: a platform the loot is over, else the ground.</summary>
        private float SurfaceBelow(float x,float y)
        {
            float best=world.GroundAt(x);
            foreach(var ledge in world.Ledges)if(ledge.Contains(x)){float h=ledge.HeightAt(x);if(h<=y+.05f&&h>best)best=h;}
            return best;
        }

        private void TickLoot(float dt)
        {
            lootClock+=dt;goldFlash=Mathf.Max(0,goldFlash-dt);
            foreach(var cache in caches)
            {
                cache.wobble=Mathf.MoveTowards(cache.wobble,0,dt*3);cache.flash=Mathf.Max(0,cache.flash-dt);
                cache.body.localRotation=Quaternion.Euler(0,0,Mathf.Sin(lootClock*32)*cache.wobble*6);
                cache.renderer.color=cache.flash>0?new Color(1,.82f,.76f):Color.white;
            }
            for(int i=shards.Count-1;i>=0;i--)
            {
                var s=shards[i];s.life-=dt;
                if(s.life<=0||!s.root){if(s.root)Destroy(s.root);shards.RemoveAt(i);continue;}
                float before=s.pos.y;s.velocity.y-=PickupGravity*dt;s.pos+=s.velocity*dt;s.angle+=s.spin*dt;
                float floor=SurfaceBelow(s.pos.x,before)+.06f;
                if(s.pos.y<floor){s.pos.y=floor;if(!s.bounced){s.velocity=new Vector2(s.velocity.x*.45f,-s.velocity.y*.3f);s.spin*=.4f;s.bounced=true;}else{s.velocity=Vector2.zero;s.spin=0;}}
                s.root.transform.SetPositionAndRotation(s.pos,Quaternion.Euler(0,0,s.angle));
                var c=s.renderer.color;c.a=Mathf.Clamp01(s.life/.35f);s.renderer.color=c;
            }
            Vector2 heroCenter=hero.pos+Vector2.up*.85f;
            for(int i=pickups.Count-1;i>=0;i--)
            {
                var p=pickups[i];p.age+=dt;
                bool heart=p.kind==LootKind.Heart,wanted=!heart||hero.hp<PlayerMaxHealth-.5f;
                float distance=Vector2.Distance(heroCenter,p.pos);
                // Close loot is drawn in at once; after the floor is cleared every landed coin flies to the hero.
                if(wanted&&p.age>.3f&&(distance<MagnetRadius||!heart&&p.landed&&run.IsFloorCleared))p.homing=true;
                if(!wanted)p.homing=false;
                if(p.homing)
                {
                    p.velocity=Vector2.MoveTowards(p.velocity,(heroCenter-p.pos).normalized*HomingSpeed,70*dt);p.pos+=p.velocity*dt;
                }
                else
                {
                    float before=p.pos.y;p.velocity.y-=PickupGravity*dt;p.pos+=p.velocity*dt;
                    p.pos.x=Mathf.Clamp(p.pos.x,TowerWorld.Left+.4f,TowerWorld.Right-.4f);
                    float rest=SurfaceBelow(p.pos.x,before)+RestHeight(p.kind);
                    if(p.pos.y<rest&&p.velocity.y<=0)
                    {
                        p.pos.y=rest;
                        if(p.velocity.y<-3){p.velocity=new Vector2(p.velocity.x*.55f,-p.velocity.y*.36f);}
                        else{p.velocity=Vector2.zero;p.landed=true;}
                    }
                    else if(p.landed&&p.pos.y>rest+.02f)p.landed=false;
                }
                if(wanted&&Vector2.Distance(heroCenter,p.pos)<PickupReach){Collect(p);pickups.RemoveAt(i);continue;}
                // Coins turn on edge, hearts beat; landed loot bobs a little so it reads against busy ground.
                float bob=p.landed?Mathf.Sin(p.age*3+p.phase)*.045f+.03f:0;
                p.root.transform.position=p.pos+Vector2.up*bob;
                if(heart)p.art.localScale=Vector3.one*p.size*(1+Mathf.Max(0,Mathf.Sin(p.age*6+p.phase))*.1f);
                else p.art.localScale=new Vector3(p.size*Mathf.Max(.18f,Mathf.Abs(Mathf.Cos(p.age*5+p.phase))),p.size,1);
                var g=p.glow.color;g.a=(heart?.36f:.3f)+Mathf.Sin(p.age*4+p.phase)*.08f;p.glow.color=g;
            }
        }
        private void Collect(Pickup p)
        {
            if(p.kind==LootKind.Heart)
            {
                float healed=Mathf.Min(PlayerMaxHealth-hero.hp,TreeHeartHeal(TowerLoot.HeartHeal(PlayerMaxHealth)));hero.hp+=healed;HeartsCollected++;
                FloatText(hero.pos+Vector2.up*2,"+"+Mathf.CeilToInt(healed),new Color(1,.45f,.5f));
                if(vfx!=null)vfx.Cast(SkillEffect.HealPulse,hero.pos+Vector2.up,1.1f,new Color(1,.42f,.48f));
                Sound(chimeSound,.3f);
            }
            else
            {
                AddGold(TowerLoot.GoldOf(p.kind));
                if(p.kind==LootKind.GoldCoin)FloatText(hero.pos+Vector2.up*2.2f,"+"+TowerLoot.GoldCoinGold+" G",TowerWorld.Gold);
                if(coinSound==null)coinSound=Tone(1760,.12f);
                Sound(coinSound,p.kind==LootKind.GoldCoin?.45f:.28f);
            }
            if(p.root)Destroy(p.root);
        }

        /// <summary>The wallet: a coin and the gold balance right-aligned in <paramref name="r"/>; fresh pickups float "+N G" to its left.</summary>
        private void DrawGold(Rect r,int size,Color color,string prefix="보유 ")
        {
            string text=prefix+progress.Gold.ToString("N0",CultureInfo.InvariantCulture)+" G";
            textStyle.fontSize=size;textStyle.fontStyle=FontStyle.Bold;float width=textStyle.CalcSize(new GUIContent(text)).x;
            Label(r,text,size,color,TextAnchor.MiddleRight,true);
            var coin=PickupSprite(LootKind.Coin);float icon=size*1.25f,left=r.xMax-width-icon-6;
            if(coin!=null)GUI.DrawTexture(new Rect(left,r.center.y-icon*.5f,icon,icon),coin.texture,ScaleMode.ScaleToFit,true);
            if(goldFlash>0)
            {
                float a=Mathf.Clamp01(goldFlash*2),rise=(1.4f-goldFlash)*8;
                Label(new Rect(left-160,r.y-rise,152,r.height),"+"+goldFlashAmount+" G",size,Alpha(TowerWorld.Gold,a),TextAnchor.MiddleRight,true);
            }
        }
    }
}
