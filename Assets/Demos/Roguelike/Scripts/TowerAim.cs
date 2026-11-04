using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Mouse aim. Over the world a pixel reticle replaces the pointer; the hero faces it, swings the sword toward it
    // (down from the raised guard, then back up from the low one) and the right button dashes toward it, in the air in
    // any direction. Fixtures keep the old aim straight along the facing unless they set AimOverride.
    public sealed partial class TowerGame
    {
        internal const float ChestHeight=.9f,DashSpeed=19,AimedDashCarry=5.5f;
        // A dash heading up by more than this rises: from the floor a lower one runs flat, and in the air only the first
        // rising dash of an airtime lifts the hero.
        internal const float AimedDashRiseY=.35f;
        /// <summary>A world point the hero aims at while fixtures drive the game; null aims straight along the facing.</summary>
        internal Vector2? AimOverride;
        internal Vector2 AimPoint{get;private set;}
        /// <summary>Unit direction from the hero's chest to the aim point.</summary>
        internal Vector2 AimDirection{get;private set;}=Vector2.right;
        /// <summary>The direction of the swing in progress, fixed when it starts.</summary>
        internal Vector2 AttackAim{get{return attackAim;}}
        internal bool SwingRising{get{return swingRising;}}
        /// <summary>Real play aims with the pointer; fixtures aim only through AimOverride.</summary>
        internal bool MouseAims{get{return !SmokeControlled||AimOverride.HasValue;}}
        private Vector2 attackAim=Vector2.right,dashDirection=Vector2.right;
        private bool swingRising,cursorHidden;
        // Whether the dash in progress sets the vertical speed along its heading; a rising air dash after the airtime's first holds its height instead.
        private bool dashVertical=true;
        private GameObject reticle;private float reticleKick;
        private static Sprite reticleSprite;
        private Vector2 Chest{get{return hero.pos+Vector2.up*ChestHeight;}}

        private void TickAim()
        {
            Vector2 point;
            if(SmokeControlled)point=AimOverride??Chest+new Vector2(hero.facing*4,0);
            else{Vector3 world=view.ScreenToWorldPoint(Input.mousePosition);point=new Vector2(world.x,world.y);}
            AimPoint=point;Vector2 delta=point-Chest;
            if(delta.sqrMagnitude>.04f)AimDirection=delta.normalized;
            hero.art.SetAim(AimDirection,MouseAims);hero.art.SetAimPoint(point);
        }
        // The hero turns to the reticle between swings; a pointer right above or below keeps the current side.
        private void FaceAim()
        {
            float dx=AimPoint.x-hero.pos.x;
            if(Mathf.Abs(dx)>.12f)hero.facing=dx>0?1:-1;
        }
        /// <summary>A swing aimed and started now: its direction, and whether it rises from the low guard.</summary>
        private void AimSwing()
        {
            attackAim=MouseAims?AimDirection:new Vector2(hero.facing,0);
            swingRising=MouseAims&&hero.art.AimGuardLow;
            if(MouseAims&&Mathf.Abs(attackAim.x)>.1f)hero.facing=attackAim.x>0?1:-1;
            reticleKick=1;
        }
        /// <summary>
        /// Whether a swing reaches <paramref name="point"/>: no more than <paramref name="back"/> behind the chest along the aim,
        /// less than <paramref name="reach"/> ahead and less than <paramref name="halfWidth"/> to either side of the aim line.
        /// Aimed straight along the facing this is the old horizontal band.
        /// </summary>
        private bool SwingReaches(Vector2 point,float reach,float back,float halfWidth)
        {
            Vector2 rel=point-Chest;float along=rel.x*attackAim.x+rel.y*attackAim.y,across=Mathf.Abs(rel.y*attackAim.x-rel.x*attackAim.y);
            return along>-back&&along<reach&&across<halfWidth;
        }

        // Shift dodges along the keys (or the facing); the right button dashes toward the reticle. On the ground a dash
        // aimed at the floor or only just above it runs flat. In the air only the first rising dash of an airtime lifts the
        // hero; later ones hold their height like a flat air dash until a landing or a down-cut bounce gives the lift back
        // (ResetAirStrikes, Pogo). It spends a charge (TowerDash.cs); Update asks DashReady first.
        private void StartDash(float move,bool aimed)
        {
            Vector2 heading=aimed?AimDirection:new Vector2(move!=0?Mathf.Sign(move):hero.facing,0);
            if(aimed&&hero.grounded&&heading.y<AimedDashRiseY)heading=new Vector2(heading.x>.05f?1:heading.x<-.05f?-1:hero.facing,0);
            dashDirection=heading.normalized;bool fromGround=hero.grounded;
            bool airRise=!fromGround&&dashDirection.y>AimedDashRiseY;
            dashVertical=!airRise||!airDashLiftSpent;if(airRise)airDashLiftSpent=true;
            if(Mathf.Abs(dashDirection.x)>.1f)hero.facing=dashDirection.x>0?1:-1;
            attackTimer=0;pendingSlash=false;lungeTime=0;CancelPlayerCast();hero.art.InterruptAction();dashTime=.18f;BeginDodgeCounter();SpendDashCharge();invulnerable=Mathf.Max(invulnerable,.25f);
            WeaponsOnDash();
            if(dashDirection.y>0){hero.grounded=false;hero.dropTime=0;}
            if(vfx!=null)
            {
                // The dash-strike burst streaks sideways, so a dash up or down draws only its own angled speed lines.
                bool flat=dashDirection.y==0;
                vfx.Afterimage(hero.root.transform);if(flat)vfx.Cast(SkillEffect.DashStrike,hero.pos+Vector2.up,1,TowerWorld.Teal);if(fromGround)vfx.Dust(hero.pos,hero.facing,.9f);
                if(flat)vfx.SpeedLines(hero.pos+Vector2.up*.9f,hero.facing,TowerWorld.Teal);else vfx.SpeedLines(hero.pos+Vector2.up*.9f,dashDirection,TowerWorld.Teal);
            }
            Sound(dashCue,.5f);
        }
        private void TickDash(float dt)
        {
            dashTime-=dt;hero.pos.x+=dashDirection.x*DashSpeed*dt;
            if(dashDirection.y!=0&&dashVertical)hero.velocityY=dashDirection.y*DashSpeed;
            // Air dashes hold their height instead of dropping mid-dodge, a rising one past the airtime's first included.
            else if(!hero.grounded)hero.velocityY=Mathf.Max(0,hero.velocityY);
            if(vfx!=null&&elapsed>=nextAfterimage){nextAfterimage=elapsed+.05f;vfx.Afterimage(hero.root.transform);}
            if(dashTime<=0){dashEndedAt=elapsed;SettleDash();}
        }
        // A dash that ends rising keeps only a little lift, so it never flings the hero far above where it stopped.
        private void SettleDash(){if(dashDirection.y>0&&dashVertical)hero.velocityY=Mathf.Min(hero.velocityY,dashDirection.y*AimedDashCarry);}

        private void LateUpdate(){UpdateReticle();}
        private bool ReticleShown
        {
            get
            {
                if(SmokeControlled||hero==null||Paused||!Application.isFocused)return false;
                Vector3 mouse=Input.mousePosition;
                return mouse.x>=0&&mouse.y>=0&&mouse.x<Screen.width&&mouse.y<Screen.height&&!MouseInUI();
            }
        }
        // The reticle is a world sprite drawn after the camera has settled, on the screen pixel under the pointer.
        private void UpdateReticle()
        {
            bool shown=ReticleShown;
            if(shown!=cursorHidden){Cursor.visible=!shown;cursorHidden=shown;}
            reticleKick=Mathf.Max(0,reticleKick-Time.unscaledDeltaTime*7);
            if(!shown){if(reticle)reticle.SetActive(false);return;}
            if(!reticle)CreateReticle();
            reticle.SetActive(true);
            Vector3 mouse=Input.mousePosition;
            Vector3 world=view.ScreenToWorldPoint(new Vector3(Mathf.Round(mouse.x),Mathf.Round(mouse.y),-view.transform.position.z));
            float kick=reticleKick*reticleKick;
            reticle.transform.position=new Vector3(world.x,world.y,0);
            reticle.transform.localScale=Vector3.one*(1+.35f*kick);
            reticle.transform.localRotation=Quaternion.Euler(0,0,-45*kick);
        }
        private void CreateReticle()
        {
            if(reticleSprite==null)reticleSprite=BuildReticle();
            reticle=new GameObject("Aim reticle");reticle.transform.SetParent(transform,false);
            var renderer=reticle.AddComponent<SpriteRenderer>();renderer.sprite=reticleSprite;renderer.sortingOrder=900;
        }
        private void OnDisable(){if(cursorHidden){Cursor.visible=true;cursorHidden=false;}if(reticle)reticle.SetActive(false);}
        // A 20 x 20 art-pixel crosshair: a ring broken at the four points, ticks through the gaps and a centre dot, in warm
        // white with a dark rim so it reads on bright marble and dark stone alike.
        internal static Sprite BuildReticle()
        {
            const int Size=20;
            var core=new bool[Size*Size];
            for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)
            {
                float dx=x+.5f-Size*.5f,dy=y+.5f-Size*.5f,ax=Mathf.Abs(dx),ay=Mathf.Abs(dy),r=Mathf.Sqrt(dx*dx+dy*dy);
                bool ring=Mathf.Abs(r-6.6f)<.62f&&ax>1.6f&&ay>1.6f;
                bool tick=(ax<1&&ay>3.5f&&ay<8.6f)||(ay<1&&ax>3.5f&&ax<8.6f);
                core[y*Size+x]=ring||tick||(ax<1&&ay<1);
            }
            var pixels=new Color32[Size*Size];
            for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)
            {
                int i=y*Size+x;if(core[i]){pixels[i]=new Color32(255,246,226,255);continue;}
                bool rim=false;
                for(int oy=-1;oy<=1&&!rim;oy++)for(int ox=-1;ox<=1&&!rim;ox++){int nx=x+ox,ny=y+oy;rim=nx>=0&&ny>=0&&nx<Size&&ny<Size&&core[ny*Size+nx];}
                pixels[i]=rim?new Color32(22,16,30,190):new Color32(0,0,0,0);
            }
            var texture=new Texture2D(Size,Size,TextureFormat.RGBA32,false){name="Aim reticle",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(pixels);texture.Apply(false,true);
            return Sprite.Create(texture,new Rect(0,0,Size,Size),new Vector2(.5f,.5f),32);
        }
    }
}
