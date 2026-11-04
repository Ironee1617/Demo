using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Combat cues: every verb's own synthesized sound, the telegraph of a monster's or boss's windup (a low tone as it
    // begins, then a tick and a white flash for its last moments, TelegraphFlashOf) and the hero's invulnerability blink.
    // The flash and the blink use their own TowerActorArt channels, so elite colours, the veil and death fades keep Tint.
    // Every sound plays through the one effects source, and a cue sounds at most CueVoices times within CueWindow.
    public sealed partial class TowerGame
    {
        private sealed class SoundCue
        {
            // Pitch variants of one sound (a single clip for a fixed cue), when it last sounded (unscaled time, a ring of
            // CueVoices) and the voice of the frame it last sounded in.
            public AudioClip[] clips;
            public readonly float[] recent=new float[CueVoices];
            public int next,frame=-1,variant,played,dropped;
            public float loudest;
            public SoundCue(){for(int i=0;i<recent.Length;i++)recent[i]=float.NegativeInfinity;}
        }
        /// <summary>
        /// A synthesized sound: a tone gliding from hz to toHz with its octave above, pushed toward a square wave by rough, a
        /// second tone hz2 for a chord, white noise band-passed from noiseFrom to noiseTo (no upper edge at 0) and a click on
        /// the onset, under an attack ramp and a (1-t)^curve fall.
        /// </summary>
        private sealed class SoundShape
        { public float length,hz,toHz,octave,rough,hz2,noise,noiseFrom,noiseTo,click,attack=.002f,curve=2,gain=.4f; }

        internal const int CueVoices=3;
        internal const float CueWindow=.05f,CuePitchSway=.05f;
        // A windup lights up for its last TelegraphFlashTime before a blow (a monster's lunge, a boss's slam and the techniques
        // sharing its ground-pounding motion) and its last RangedFlashTime before a volley of bolts.
        internal const float TelegraphFlashTime=.3f,RangedFlashTime=.2f;
        // The tick sounds TelegraphTickHz times its monster kind's step (light walker, shooter, heavy, flyer), so each
        // threat is told apart by ear; a boss's sits lower at BossTickHz.
        internal const float TelegraphTickHz=1200,BossTickHz=800;
        internal static readonly float[] TelegraphTickSteps={1,1.19f,.84f,1.33f};
        // Five pitches across ±CuePitchSway; a verb picks one by a hash of the frame, so a replayed run sounds alike.
        private static readonly float[] CuePitches={1-CuePitchSway,1-CuePitchSway*.5f,1,1+CuePitchSway*.5f,1+CuePitchSway};
        private const int SoundRate=22050;
        private SoundCue jumpCue,airJumpCue,dashCue,swingCue,strikeCue,heavyCue,hurtCue,dodgeCue,bossWindupCue,bossTickCue;
        // Blows that are not the player's own (companions, damage over time) sound like a hit but keep a voice budget of their
        // own, so they never crowd out the player's.
        private SoundCue struckCue;
        // A windup's low tone and its telegraph tick for each monster kind: the light walker, the shooter, the heavy and the flyer.
        private readonly SoundCue[] windupCues=new SoundCue[4],tickCues=new SoundCue[4];
        private readonly List<SoundCue> builtCues=new List<SoundCue>();
        private readonly Dictionary<AudioClip,SoundCue> clipCues=new Dictionary<AudioClip,SoundCue>();
        /// <summary>Windups begun, and telegraph ticks sounded: one for each windup that reached its flash (TelegraphFlashOf).</summary>
        internal int TelegraphStarts{get;private set;}
        internal int TelegraphTicks{get;private set;}
        /// <summary>The pitch of the last telegraph tick: its monster kind's, or the boss's.</summary>
        internal float LastTelegraphTickHz{get;private set;}
        // Invulnerability blinks the hero's alpha between 1 and InvulnerableBlinkAlpha InvulnerableBlinkHz times a second,
        // outside a dash and the veil (which fades the hero itself). Its last InvulnerableBlinkSettle seconds stay solid, so
        // the short cover a dash leaves behind never flickers.
        internal const float InvulnerableBlinkHz=10,InvulnerableBlinkAlpha=.55f,InvulnerableBlinkSettle=.1f;

        private void BuildCombatCues()
        {
            // The jumps chirp upward; the dash hisses high and the swing whooshes mid-band; a hit clicks over a low knock and
            // a heavy blow thumps lower and longer; the hero's hurt buzzes; a perfect dodge rings a chord.
            jumpCue=Cue("jump",new SoundShape{length=.05f,hz=330,toHz=520,octave=.2f,attack=.003f,curve=1.6f});
            airJumpCue=Cue("air jump",new SoundShape{length=.06f,hz=494,toHz=740,octave=.2f,attack=.003f,curve=1.6f,gain=.38f});
            dashCue=Cue("dash",new SoundShape{length=.09f,noise=1.4f,noiseFrom=2500,attack=.012f,curve=1.4f,gain=.32f});
            swingCue=Cue("swing",new SoundShape{length=.06f,noise=2.2f,noiseFrom=700,noiseTo=2800,attack=.006f,curve=1.5f,gain=.38f});
            strikeCue=Cue("strike",new SoundShape{length=.05f,hz=180,toHz=140,octave=.35f,click=.9f,gain=.42f});
            struckCue=Cue("struck",new SoundShape{length=.05f,hz=180,toHz=140,octave=.35f,click=.9f,gain=.36f});
            heavyCue=Cue("heavy strike",new SoundShape{length=.12f,hz=70,toHz=52,octave=.6f,rough=.25f,noise=.5f,noiseFrom=80,noiseTo=900,click=1.1f,curve=1.8f,gain=.55f});
            hurtCue=Cue("hurt",new SoundShape{length=.14f,hz=110,toHz=88,octave=.35f,rough=.7f,noise=.35f,noiseFrom=300,noiseTo=2000,curve=1.6f});
            dodgeCue=Cue("perfect dodge",new SoundShape{length=.12f,hz=1320,hz2=1760,octave=.08f,attack=.003f,curve=1.6f,gain=.34f});
            // Telegraphs keep one pitch, so each threat is learned by its sound; a boss's sit lower and last longer.
            float[] windupHz={150,196,98,220};
            for(int kind=0;kind<windupCues.Length;kind++)
                windupCues[kind]=Cue("windup "+kind,new SoundShape{length=.1f,hz=windupHz[kind],toHz=windupHz[kind]*1.16f,octave=kind==2?.7f:.5f,rough=kind==2?.3f:.2f,attack=.008f,curve=1.4f,gain=.42f},false);
            for(int kind=0;kind<tickCues.Length;kind++)
                tickCues[kind]=Cue("telegraph tick "+kind,new SoundShape{length=.035f,hz=TelegraphTickHz*TelegraphTickSteps[kind],octave=.1f,click=.25f,attack=.001f,curve=3},false);
            bossWindupCue=Cue("boss windup",new SoundShape{length=.22f,hz=62,toHz=74,octave=.8f,rough=.35f,noise=.3f,noiseFrom=60,noiseTo=500,attack=.015f,curve=1.2f,gain=.55f},false);
            bossTickCue=Cue("boss telegraph tick",new SoundShape{length=.07f,hz=BossTickHz,octave=.25f,click=.3f,attack=.002f,curve=2.4f,gain=.42f},false);
        }
        private void DestroyCombatCues()
        {
            foreach(var cue in builtCues)foreach(var clip in cue.clips)if(clip)Destroy(clip);
            builtCues.Clear();clipCues.Clear();
        }
        private SoundCue Cue(string name,SoundShape shape,bool sway=true)
        {
            var cue=new SoundCue{clips=new AudioClip[sway?CuePitches.Length:1]};
            for(int v=0;v<cue.clips.Length;v++)cue.clips[v]=Synthesize("Tower "+name,shape,sway?CuePitches[v]:1,builtCues.Count*7+v);
            builtCues.Add(cue);return cue;
        }
        private static AudioClip Synthesize(string name,SoundShape s,float pitch,int seed)
        {
            // A higher variant is the same sound played faster: every frequency scales by pitch and the length by 1/pitch.
            int n=Mathf.Max(1,Mathf.RoundToInt(SoundRate*s.length/pitch));var data=new float[n];
            float step=pitch/SoundRate,from=1-Mathf.Exp(-6.2832f*s.noiseFrom*step),to=s.noiseTo>0?1-Mathf.Exp(-6.2832f*s.noiseTo*step):1;
            float phase=0,phase2=0,floor=0,band=0;uint state=unchecked(2463534242u+(uint)seed*2654435761u);if(state==0)state=1;
            for(int i=0;i<n;i++)
            {
                float progress=i/(float)n,time=i*step;
                state^=state<<13;state^=state>>17;state^=state<<5;float white=(state>>8)/8388607.5f-1;
                float voice=0;
                if(s.hz>0)
                {
                    phase+=Mathf.Lerp(s.hz,s.toHz>0?s.toHz:s.hz,progress)*step;phase-=Mathf.Floor(phase);
                    float sine=Mathf.Sin(phase*6.2832f);voice=Mathf.Lerp(sine,Mathf.Sign(sine),s.rough)+Mathf.Sin(phase*12.566f)*s.octave;
                }
                if(s.hz2>0){phase2+=s.hz2*step;phase2-=Mathf.Floor(phase2);voice=(voice+Mathf.Sin(phase2*6.2832f))*.6f;}
                // Two one-pole stages: the first follows the noise below noiseFrom (removed), the second smooths above noiseTo.
                floor+=(white-floor)*from;band+=(white-floor-band)*to;
                float envelope=Mathf.Min(1,time/Mathf.Max(.0001f,s.attack))*Mathf.Pow(1-progress,s.curve);
                data[i]=Mathf.Clamp(((voice+band*s.noise)*envelope+white*s.click*Mathf.Exp(-time*900))*s.gain,-1,1);
            }
            var clip=AudioClip.Create(name,n,1,SoundRate,false);clip.SetData(data,0);return clip;
        }

        private void Sound(AudioClip clip,float volume)
        {
            if(!clip)return;SoundCue cue;
            if(!clipCues.TryGetValue(clip,out cue))clipCues.Add(clip,cue=new SoundCue{clips=new[]{clip}});
            Sound(cue,volume);
        }
        private void Sound(SoundCue cue,float volume){Voice(cue,volume,Time.frameCount,Time.unscaledTime);}
        /// <summary>
        /// Sounds <paramref name="cue"/> unless it already sounded CueVoices times within CueWindow before <paramref name="now"/>.
        /// Asked for again in the frame it sounded, it only tops that voice up to the louder volume, since copies would just
        /// stack. Returns whether a new voice began.
        /// </summary>
        private bool Voice(SoundCue cue,float volume,int frame,float now)
        {
            if(!audioSource||cue==null||cue.clips==null||cue.clips.Length==0||volume<=0)return false;
            if(cue.frame==frame)
            {
                var playing=cue.clips[cue.variant];
                if(volume>cue.loudest&&playing){audioSource.PlayOneShot(playing,volume-cue.loudest);cue.loudest=volume;}
                return false;
            }
            if(now-cue.recent[cue.next]<CueWindow){cue.dropped++;return false;}
            int variant=CueVariant(cue.clips.Length,frame,cue.played);var clip=cue.clips[variant];if(!clip)return false;
            cue.recent[cue.next]=now;cue.next=(cue.next+1)%cue.recent.Length;
            cue.frame=frame;cue.variant=variant;cue.loudest=volume;cue.played++;
            audioSource.PlayOneShot(clip,volume);return true;
        }
        private static int CueVariant(int count,int frame,int played)
        {
            if(count<2)return 0;
            uint h=unchecked((uint)frame*2654435761u^(uint)played*2246822519u);h^=h>>15;h=unchecked(h*2246822519u);h^=h>>13;
            return (int)(h%(uint)count);
        }

        /// <summary>A windup has begun: its kind's low tone, a boss's lower and longer.</summary>
        private void TelegraphBegan(Fighter actor)
        {
            if(actor==null)return;
            TelegraphStarts++;
            if(actor.isBoss)Sound(bossWindupCue,.75f);else Sound(windupCues[Mathf.Clamp(actor.kind,0,windupCues.Length-1)],.5f);
        }
        /// <summary>
        /// How long before its release <paramref name="actor"/>'s windup lights up: <see cref="TelegraphFlashTime"/> before a
        /// blow (a monster's lunge; a boss's slam, rockfall or fire pillars), <see cref="RangedFlashTime"/> before a volley.
        /// </summary>
        internal float TelegraphFlashOf(Fighter actor)
        {
            if(actor==null)return TelegraphFlashTime;
            if(actor.isBoss){BossState boss;return bossCombat.TryGetValue(actor,out boss)&&BossMotion(boss.pattern)!=2?RangedFlashTime:TelegraphFlashTime;}
            EnemyState state;return enemyCombat.TryGetValue(actor,out state)&&state.pattern!=EnemyPattern.MeleeSlam?RangedFlashTime:TelegraphFlashTime;
        }
        /// <summary>
        /// Called every tick just before the actor is posed: the last moments of its windup (TelegraphFlashOf) light it white
        /// (TowerActorArt.TelegraphLit) and its kind's tick sounds once as the light comes on; the release, a stun or a fall
        /// puts it out.
        /// </summary>
        private void TelegraphCue(Fighter actor)
        {
            if(actor==null||actor.art==null)return;
            bool lit=actor.hp>0&&actor.windup>0&&actor.windup<=TelegraphFlashOf(actor);
            if(lit&&!actor.art.TelegraphLit)
            {
                int kind=Mathf.Clamp(actor.kind,0,tickCues.Length-1);TelegraphTicks++;
                LastTelegraphTickHz=actor.isBoss?BossTickHz:TelegraphTickHz*TelegraphTickSteps[kind];
                Sound(actor.isBoss?bossTickCue:tickCues[kind],actor.isBoss?.65f:.55f);
            }
            actor.art.TelegraphLit=lit;
        }
        /// <summary>Sets the hero's blink channel (TowerActorArt.BlinkAlpha) each frame; a pause shows the hero solid.</summary>
        private void TickHeroBlink()
        {
            if(hero==null||hero.art==null)return;
            bool blink=!Paused&&hero.hp>0&&dashTime<=0&&veilRemaining<=0&&invulnerable>InvulnerableBlinkSettle;
            hero.art.BlinkAlpha=blink&&Mathf.Repeat(invulnerable*InvulnerableBlinkHz,1)>=.5f?InvulnerableBlinkAlpha:1;
        }
    }
}
