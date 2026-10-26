using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Floor traps laid out by the terrain generator: spike strips, flame vents, rockfall zones, bolt-spitting totems
    // and launch pads. Traps threaten only the hero. Clearing a floor quiets the vents, rockfalls and totems while the
    // spikes stay sharp; rest and shop floors keep only their launch pads.
    public sealed partial class TowerGame
    {
        internal const float VentPeriod=3.4f,VentWarning=.65f,VentBurst=1.05f,VentHeight=2.7f,VentReach=.55f;
        internal const float RockWarning=.75f,RockFall=.55f,RockCooldown=2.2f,RockDrop=10;
        internal const float TotemPeriod=3.3f,TotemWarning=.75f,TotemRange=10.5f,TotemBoltSpeed=5.2f;
        internal const float SpikeDamage=6,VentDamage=9,RockDamage=10,TotemDamage=6,SpikeBounce=8.5f,PadVelocity=16.5f;
        internal const int TotemHits=3;
        internal sealed class Trap
        {
            public TowerHazard spec; public float baseY,clock,cooldown,emit,rockTime=-1,rockX; public int hits;
            public bool broken,rockHit,erupting;
            public Transform root,rock; public SpriteRenderer body,glow,core,shadow; public SpriteRenderer[] marks;
        }
        internal readonly List<Trap> traps=new List<Trap>();
        private GameObject trapRoot;
        /// <summary>Smoke fixtures run without traps unless they ask for them.</summary>
        internal bool TrapOverride;
        internal int TrapHits{get;private set;}
        private bool TrapsArmed{get{return (!SmokeControlled||TrapOverride)&&!village&&hero!=null;}}

        private void ClearTraps(){if(trapRoot)Destroy(trapRoot);trapRoot=null;traps.Clear();}

        private void BuildTraps()
        {
            ClearTraps();
            var layout=world.CurrentLayout;if(layout==null||village||run==null)return;
            bool peaceful=run.FloorType==FloorType.Rest||run.FloorType==FloorType.Shop;
            var theme=run.Theme.Id;var palette=TowerHazardArt.For(theme);
            trapRoot=new GameObject("Floor traps");trapRoot.transform.SetParent(transform,false);
            foreach(var spec in layout.Hazards)
            {
                if(peaceful&&spec.Kind!=TowerHazardKind.JumpPad)continue;
                var trap=new Trap{spec=spec,clock=spec.Phase,baseY=world.SurfaceHeight(spec.Support,spec.X)};
                trap.root=new GameObject(spec.Kind+" trap").transform;trap.root.SetParent(trapRoot.transform,false);trap.root.localPosition=new Vector3(spec.X,trap.baseY,0);
                switch(spec.Kind)
                {
                    case TowerHazardKind.Spikes:
                    {
                        // Tiles follow the ground under each of them, sunk a little so no gap shows on gentle slopes.
                        int tiles=Mathf.Max(1,Mathf.RoundToInt(spec.Width/TowerHazardArt.SpikeTile));float tile=spec.Width/tiles;
                        TowerWorld.SpriteObject(trap.root,"Spike pit shadow",new Vector2(0,.06f),new Vector2(spec.Width+.5f,.4f),new Color(0,0,0,.4f),-13,TowerWorld.Glow);
                        for(int i=0;i<tiles;i++)
                        {
                            float x=-spec.Width*.5f+(i+.5f)*tile,low=Mathf.Min(world.GroundAt(spec.X+x-tile*.5f),world.GroundAt(spec.X+x+tile*.5f));
                            TowerWorld.SpriteObject(trap.root,"Spike tile",new Vector2(x,low-trap.baseY-.05f),new Vector2(tile/TowerHazardArt.SpikeTile,1),Color.white,-12,TowerHazardArt.Spikes(theme));
                        }
                        break;
                    }
                    case TowerHazardKind.FlameVent:
                        trap.body=TowerWorld.SpriteObject(trap.root,"Vent grate",new Vector2(0,.07f-TowerHazardArt.GrateHeight),Vector2.one,Color.white,-12,TowerHazardArt.Grate(theme)).GetComponent<SpriteRenderer>();
                        trap.glow=TowerWorld.SpriteObject(trap.root,"Vent ember glow",new Vector2(0,.12f),new Vector2(1.6f,.75f),new Color(1,.45f,.12f,.08f),-11,TowerWorld.Glow).GetComponent<SpriteRenderer>();
                        trap.core=TowerWorld.SpriteObject(trap.root,"Vent heat column",new Vector2(0,VentHeight*.55f),new Vector2(1.25f,VentHeight*1.25f),new Color(1,.55f,.2f,0),25,TowerWorld.Glow).GetComponent<SpriteRenderer>();
                        break;
                    case TowerHazardKind.Rockfall:
                        TowerWorld.SpriteObject(trap.root,"Rockfall rubble",new Vector2(0,-.06f),Vector2.one,Color.white,-12,TowerHazardArt.Rubble(theme));
                        trap.shadow=TowerWorld.SpriteObject(trapRoot.transform,"Rockfall shadow",new Vector2(spec.X,trap.baseY),Vector2.one,new Color(0,0,0,0),-1,TowerWorld.Disc).GetComponent<SpriteRenderer>();
                        trap.rock=TowerWorld.SpriteObject(trapRoot.transform,"Falling boulder",new Vector2(spec.X,trap.baseY+RockDrop),Vector2.one,Color.white,35,TowerHazardArt.Boulder(theme)).transform;
                        trap.rock.gameObject.SetActive(false);
                        break;
                    case TowerHazardKind.BoltTotem:
                        trap.body=TowerWorld.SpriteObject(trap.root,"Cursed totem",new Vector2(0,-.04f),Vector2.one,Color.white,-12,TowerHazardArt.Totem(theme,false)).GetComponent<SpriteRenderer>();
                        trap.glow=TowerWorld.SpriteObject(trap.root,"Totem gem glow",new Vector2(0,TowerHazardArt.TotemGemY),Vector2.one*.55f,Alpha(palette.rune,.35f),-11,TowerWorld.Glow).GetComponent<SpriteRenderer>();
                        trap.core=TowerWorld.SpriteObject(trap.root,"Totem gem",new Vector2(0,TowerHazardArt.TotemGemY),Vector2.one*.13f,palette.rune,-10,TowerWorld.Disc).GetComponent<SpriteRenderer>();
                        break;
                    case TowerHazardKind.JumpPad:
                        trap.body=TowerWorld.SpriteObject(trap.root,"Launch pad",new Vector2(0,-.06f),Vector2.one,Color.white,-12,TowerHazardArt.Pad(theme)).GetComponent<SpriteRenderer>();
                        trap.glow=TowerWorld.SpriteObject(trap.root,"Launch pad rune glow",new Vector2(0,.3f),new Vector2(1.6f,.6f),Alpha(palette.rune,.45f),-11,TowerWorld.Glow).GetComponent<SpriteRenderer>();
                        // Chevrons drift up off the plate so the pad reads as a way up.
                        trap.marks=new SpriteRenderer[2];
                        for(int i=0;i<trap.marks.Length;i++)trap.marks[i]=TowerWorld.SpriteObject(trap.root,"Launch pad chevron",new Vector2(0,.6f),Vector2.one,Alpha(palette.rune,0),-9,TowerHazardArt.Chevron()).GetComponent<SpriteRenderer>();
                        break;
                }
                traps.Add(trap);
            }
        }

        private void TickTraps(float dt)
        {
            if(traps.Count==0)return;
            bool armed=TrapsArmed,live=armed&&!run.IsFloorCleared;
            float scale=TowerDifficulty.DamageMultiplier(DifficultyFloor(run.Floor));
            var palette=TowerHazardArt.For(run.Theme.Id);
            foreach(var trap in traps)
                switch(trap.spec.Kind)
                {
                    case TowerHazardKind.Spikes:if(armed)TickSpikes(trap,scale);break;
                    case TowerHazardKind.FlameVent:TickVent(trap,dt,live,scale);break;
                    case TowerHazardKind.Rockfall:TickRockfall(trap,dt,live,scale,palette);break;
                    case TowerHazardKind.BoltTotem:TickTotem(trap,dt,live,scale,palette);break;
                    case TowerHazardKind.JumpPad:TickPad(trap,dt,armed,palette);break;
                }
        }

        private void HurtByTrap(float damage){TrapHits++;HurtHero(damage);}
        private bool HeroExposed{get{return hero.hp>0&&invulnerable<=0;}}

        // Standing on the strip hurts and throws the hero up off it.
        private void TickSpikes(Trap trap,float scale)
        {
            var s=trap.spec;
            if(!HeroExposed||!hero.grounded||hero.support>=0||hero.pos.x<s.Left+.08f||hero.pos.x>s.Right-.08f)return;
            HurtByTrap(SpikeDamage*scale);
            hero.velocityY=SpikeBounce;hero.grounded=false;jumpCut=true;
            if(vfx!=null)vfx.Hit(hero.pos+Vector2.up*.25f,new Color(1,.45f,.35f));
        }

        // Idle, then embers and a reddening grate, then a column of flame.
        private void TickVent(Trap trap,float dt,bool live,float scale)
        {
            Vector2 mouth=new Vector2(trap.spec.X,trap.baseY);
            float phase=-1;
            if(live){trap.clock+=dt;phase=Mathf.Repeat(trap.clock,VentPeriod);}
            float warnAt=VentPeriod-VentBurst-VentWarning,burstAt=VentPeriod-VentBurst;
            bool warning=phase>=warnAt&&phase<burstAt,burst=phase>=burstAt;
            float warmth=burst?1:warning?(phase-warnAt)/VentWarning:0,flicker=Mathf.Sin(elapsed*37+trap.spec.X)*.07f;
            trap.glow.color=new Color(1,.45f+warmth*.2f,.12f,Mathf.Clamp01(.08f+warmth*.78f+(warning||burst?flicker:0)));
            trap.core.color=new Color(1,.55f,.2f,burst?.3f+flicker:warning?warmth*.07f:0);
            if(burst&&!trap.erupting&&Mathf.Abs(hero.pos.x-mouth.x)<14)Sound(slashSound,.22f);
            trap.erupting=burst;
            // Vents far off screen keep their rhythm but spend nothing on particles.
            if(vfx!=null&&(warning||burst)&&Mathf.Abs(mouth.x-view.transform.position.x)<16)
            {
                trap.emit-=dt;
                if(trap.emit<=0){trap.emit=burst?.04f:.09f;if(burst)vfx.VentFlame(mouth,VentHeight);else vfx.VentWarning(mouth);}
            }
            if(burst&&HeroExposed&&Mathf.Abs(hero.pos.x-mouth.x)<VentReach&&hero.pos.y<mouth.y+VentHeight&&hero.pos.y+1.5f>mouth.y)HurtByTrap(VentDamage*scale);
        }

        // Stepping into the zone marks a spot under the hero: a growing shadow and trickling grit, then the boulder.
        private void TickRockfall(Trap trap,float dt,bool live,float scale,TowerHazardArt.Palette palette)
        {
            var s=trap.spec;trap.cooldown=Mathf.Max(0,trap.cooldown-dt);
            if(trap.rockTime<0)
            {
                if(!live||trap.cooldown>0||hero.hp<=0)return;
                if(hero.pos.x<s.Left-.3f||hero.pos.x>s.Right+.3f||hero.pos.y>world.GroundAt(hero.pos.x)+3.2f)return;
                trap.rockTime=0;trap.rockHit=false;trap.rockX=Mathf.Clamp(hero.pos.x,s.Left,s.Right);
            }
            if(!live&&trap.rockTime<RockWarning){EndRockfall(trap);return;}
            trap.rockTime+=dt;
            float ground=world.GroundAt(trap.rockX),warn=Mathf.Clamp01(trap.rockTime/RockWarning);
            trap.shadow.transform.position=new Vector3(trap.rockX,ground+.03f,0);
            trap.shadow.transform.localScale=new Vector3(.35f+.75f*warn,.12f+.1f*warn,1);
            trap.shadow.color=new Color(0,0,0,.12f+.38f*warn);
            if(trap.rockTime<RockWarning)
            {
                trap.emit-=dt;
                if(trap.emit<=0&&vfx!=null){trap.emit=.07f;vfx.RockTrickle(new Vector2(trap.rockX,view.transform.position.y+view.orthographicSize+.3f));}
                return;
            }
            float fall=Mathf.Clamp01((trap.rockTime-RockWarning)/RockFall),y=ground+.36f+RockDrop*(1-fall*fall);
            trap.rock.gameObject.SetActive(true);trap.rock.position=new Vector3(trap.rockX,y,0);trap.rock.rotation=Quaternion.Euler(0,0,-trap.rockTime*240);
            if(!trap.rockHit&&HeroExposed&&Mathf.Abs(hero.pos.x-trap.rockX)<.62f&&y-.32f<hero.pos.y+1.7f&&y+.32f>hero.pos.y){trap.rockHit=true;HurtByTrap(RockDamage*scale);}
            if(fall<1)return;
            if(vfx!=null)vfx.RockImpact(new Vector2(trap.rockX,ground),palette.stone);
            if(Mathf.Abs(hero.pos.x-trap.rockX)<9){Shake(.08f,.12f);Sound(hitSound,.5f);}
            if(!trap.rockHit&&HeroExposed&&Mathf.Abs(hero.pos.x-trap.rockX)<.8f&&hero.pos.y<ground+1.1f){trap.rockHit=true;HurtByTrap(RockDamage*scale);}
            EndRockfall(trap);trap.cooldown=RockCooldown;
        }
        private static void EndRockfall(Trap trap)
        {trap.rockTime=-1;trap.rock.gameObject.SetActive(false);trap.shadow.color=new Color(0,0,0,0);}

        // While the hero is in range and in view the gem charges, flares, and spits one aimed bolt.
        private void TickTotem(Trap trap,float dt,bool live,float scale,TowerHazardArt.Palette palette)
        {
            if(trap.broken)return;
            Vector2 gem=new Vector2(trap.spec.X,trap.baseY+TowerHazardArt.TotemGemY);
            bool watching=live&&hero.hp>0&&Mathf.Abs(hero.pos.x-gem.x)<TotemRange&&Mathf.Abs(hero.pos.y-gem.y)<7&&InView(gem);
            if(watching)trap.clock+=dt;else trap.clock=Mathf.Min(trap.clock,TotemPeriod-TotemWarning-.05f);
            float charge=Mathf.Clamp01((trap.clock-(TotemPeriod-TotemWarning))/TotemWarning);
            trap.glow.color=Alpha(palette.rune,(live?.5f:.1f)+charge*.45f+(charge>0?Mathf.Sin(elapsed*30)*.06f:0));
            trap.glow.transform.localScale=Vector3.one*(.7f+charge*.7f);
            trap.core.color=live?Color.Lerp(palette.rune,Color.white,charge*.7f):Color.Lerp(palette.rune,Color.black,.55f);
            if(trap.clock<TotemPeriod)return;
            trap.clock=0;
            Fire(gem,hero.pos+Vector2.up*.9f,TotemBoltSpeed,TotemDamage*scale,true,palette.rune);StyleHostileLastShot(.16f);
            if(vfx!=null)vfx.Hit(gem,palette.rune);
            Sound(hitSound,.2f);
        }

        /// <summary>A slash that reaches a totem chips it; the third hit topples it, and heavy blows count twice.</summary>
        private bool StrikeTraps(float reach,bool heavy)
        {
            bool struck=false;
            foreach(var trap in traps)
            {
                if(trap.spec.Kind!=TowerHazardKind.BoltTotem||trap.broken)continue;
                float dx=trap.spec.X-hero.pos.x;
                if(dx*hero.facing<-.4f||Mathf.Abs(dx)>reach+.3f||Mathf.Abs(trap.baseY+.8f-(hero.pos.y+.9f))>1.9f)continue;
                struck=true;trap.hits+=heavy?2:1;
                if(vfx!=null)vfx.Hit(new Vector2(trap.spec.X,trap.baseY+.9f),TowerHazardArt.For(run.Theme.Id).stone);
                if(trap.hits>=TotemHits)BreakTotem(trap);
            }
            return struck;
        }
        private void BreakTotem(Trap trap)
        {
            trap.broken=true;var palette=TowerHazardArt.For(run.Theme.Id);
            trap.body.sprite=TowerHazardArt.Totem(run.Theme.Id,true);trap.glow.enabled=false;trap.core.enabled=false;
            if(vfx!=null)vfx.TotemBreak(new Vector2(trap.spec.X,trap.baseY),palette.stone,palette.rune);
            FloatText(new Vector2(trap.spec.X,trap.baseY+2.2f),"석상 파괴",TowerWorld.Gold);Sound(hitSound,.6f);
        }

        // Landing on the plate fires the hero upward and restores the air jump.
        private void TickPad(Trap trap,float dt,bool armed,TowerHazardArt.Palette palette)
        {
            trap.cooldown=Mathf.Max(0,trap.cooldown-dt);
            trap.glow.color=Alpha(palette.rune,Mathf.Clamp01(.42f+Mathf.Sin(elapsed*3+trap.spec.X)*.1f+trap.cooldown*1.6f));
            trap.body.transform.localScale=new Vector3(1,1-trap.cooldown*.7f,1);
            for(int i=0;i<trap.marks.Length;i++)
            {
                float rise=Mathf.Repeat(elapsed*.7f+i*.5f,1);
                trap.marks[i].transform.localPosition=new Vector3(0,.5f+rise*.75f,0);
                trap.marks[i].color=Color.Lerp(palette.rune,Color.white,.35f)*new Color(1,1,1,Mathf.Sin(rise*Mathf.PI)*.85f);
            }
            if(!armed||hero.hp<=0||!hero.grounded||hero.support>=0||hero.velocityY>0||Mathf.Abs(hero.pos.x-trap.spec.X)>trap.spec.Width*.5f-.1f)return;
            hero.velocityY=PadVelocity;hero.grounded=false;jumpCut=true;coyote=0;jumpBuffer=0;airJumps=MaxAirJumps;
            trap.cooldown=.35f;
            if(vfx!=null)vfx.PadLaunch(new Vector2(trap.spec.X,trap.baseY),palette.rune);
            Sound(chimeSound,.35f);
        }

        private bool InView(Vector2 point)
        {
            if(!view)return false;
            Vector3 visible=view.WorldToViewportPoint(point);
            return visible.z>0&&visible.x>.02f&&visible.x<.98f&&visible.y>.02f&&visible.y<.98f;
        }
    }
}
