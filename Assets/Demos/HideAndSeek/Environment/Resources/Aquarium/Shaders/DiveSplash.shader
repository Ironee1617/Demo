Shader "Aquarium/Dive Splash"
{
 // R49: the diver's entry splash, one static mesh animated here from _Age (AquariumDiveSplash).
 // Object space: y up, the water surface at y = 0, +z the way the diver jumps.
 // uv0 = quad corner (-1..1), uv1 = (type, seed, a, b), uv2 = (c, d, e, f) per type (see AquariumDiveSplash).
 Properties { _Water("Water",Color)=(.80,.92,1,1) _Foam("Foam",Color)=(.95,.98,1,1) }
 SubShader {
  Tags {"Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   float4 _Water,_Foam;float _Age;float4 _Trail[32];
   struct appdata {float4 vertex:POSITION;float2 corner:TEXCOORD0;float4 p:TEXCOORD1;float4 q:TEXCOORD2;};
   struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 info:TEXCOORD1;UNITY_FOG_COORDS(2)};
   float3 ToWorld(float3 p){return mul(unity_ObjectToWorld,float4(p,1)).xyz;}
   v2f vert(appdata v){
    v2f o;float type=v.p.x,seed=v.p.y,t=_Age;float2 c=v.corner;
    float3 world=float3(0,0,0);float alpha=0;float kind=type;float extra=0;
    float3 up=float3(0,1,0);
    float3 camW=_WorldSpaceCameraPos;
    if(type<.5){
     // droplet: thrown up on an arc, stretched along its flight, falls back in
     float ang=v.p.z,vu=v.p.w,sp=v.q.x,size=v.q.y,tt=t-v.q.z;
     float3 outw=float3(cos(ang),0,sin(ang));
     float3 lp=outw*(.3+sp*tt)+up*(vu*tt-4.9*tt*tt)+float3(0,0,.25);
     float3 vel=outw*sp+up*(vu-9.8*tt);
     float alive=step(0,tt)*step(-.03,lp.y);
     size*=(1-.45*saturate(tt/.9))*alive;
     float stretch=1+min(1.8,length(vel)*.32);
     float3 cw=ToWorld(lp);float3 axis=normalize(mul((float3x3)unity_ObjectToWorld,vel)+float3(0,1e-4,0));
     float3 side=normalize(cross(axis,normalize(camW-cw))+float3(1e-4,0,0));
     world=cw+side*c.x*size+axis*c.y*size*stretch;alpha=alive;
    } else if(type<1.5){
     // foam patch: lacy white water spreading on the surface, then breaking up
     float ang=v.p.z,dist=v.p.w,size=v.q.x,tt=t-v.q.y;
     float grow=1-exp(-3.2*max(tt,0));
     float3 centre=float3(cos(ang),0,sin(ang))*dist*(.4+grow)+float3(0,.012+.002*seed,.2);
     float s=size*(.45+1.1*grow);
     world=ToWorld(centre+float3(c.x,0,c.y)*s);
     alpha=step(0,tt)*(1-smoothstep(.5,2.3,tt));extra=tt;
    } else if(type<2.5){
     // burst bubble: dragged down with the diver, wobbles back up
     float ang=v.p.z,reach=v.p.w,depth=v.q.x,born=v.q.y,life=v.q.z,size=v.q.w,tt=t-born;
     float3 lp=float3(cos(ang)*reach,-depth,sin(ang)*reach+.3)+up*(.32*tt+.22*tt*tt)+float3(sin(tt*7+seed*20),0,cos(tt*6+seed*31))*.03;
     float alive=step(0,tt)*step(tt,life)*step(lp.y,-.04);
     size*=smoothstep(0,.08,tt)*(1-smoothstep(.6,1,tt/life))*alive;
     float3 cw=ToWorld(lp);float3 r=UNITY_MATRIX_V[0].xyz,u=UNITY_MATRIX_V[1].xyz;
     world=cw+(r*c.x+u*c.y*.88)*size;alpha=alive;
    } else if(type<3.5){
     // ripple: soft, irregular ring spreading out, slower and fainter as it goes
     float tt=t-v.p.z,speed=v.p.w;
     float R=.25+speed*(1-exp(-1.2*max(tt,0)))*2.2;
     world=ToWorld(float3(v.q.x,.006,v.q.y+.2)+float3(c.x,0,c.y)*R);
     alpha=step(0,tt)*(1-smoothstep(.4,2.4,tt));extra=tt;
    } else if(type<4.5){
     // crown sheet: a thin wall of water thrown up round the hole, flaring out, its lip tearing into drops
     float a0=v.p.z,a1=v.p.w,flare=v.q.x,hmax=v.q.y;
     float ang=lerp(a0,a1,c.x*.5+.5),vv=c.y*.5+.5;
     float h=hmax*(1-exp(-t*10))*(1-smoothstep(.3,.75,t));
     float R=.32+flare*t+vv*(.12+.8*t)*hmax;
     world=ToWorld(float3(cos(ang)*R,vv*h-.02,sin(ang)*R+.2));
     alpha=1-smoothstep(.45,.8,t);extra=ang;
    } else {
     // trail bubble: streams off the diver as it drives down under the surface
     float4 slot=_Trail[(int)v.p.z];float life=v.q.w,size=v.q.z;
     float tt=t-slot.w-v.p.w*.1;
     float3 lp=slot.xyz+float3(v.q.x,v.p.w-.5,v.q.y)*.22+up*(.36*tt+.24*tt*tt)+float3(sin(tt*8+seed*40),0,cos(tt*7+seed*23))*.025;
     float alive=step(0,slot.w)*step(0,tt)*step(tt,life)*step(lp.y,-.05);
     size*=smoothstep(0,.06,tt)*(1-smoothstep(.7,1,tt/life))*alive;
     float3 cw=ToWorld(lp);float3 r=UNITY_MATRIX_V[0].xyz,u=UNITY_MATRIX_V[1].xyz;
     world=cw+(r*c.x+u*c.y*.88)*size;alpha=alive;kind=2;
    }
    o.pos=UnityWorldToClipPos(world);o.uv=c;o.info=float4(kind,seed,alpha,extra);
    UNITY_TRANSFER_FOG(o,o.pos);return o;
   }
   float N(float2 p){return sin(p.x*1.7+sin(p.y*2.3))*.5+sin(p.y*2.9+sin(p.x*1.3))*.5;}
   fixed4 frag(v2f i):SV_Target{
    float kind=i.info.x,seed=i.info.y,alpha=i.info.z;float2 uv=i.uv;float r=length(uv);
    fixed4 col=0;
    if(kind<.5){
     float body=smoothstep(1,.55,r);
     float hl=smoothstep(.35,0,length(uv-float2(-.3,.45)));
     col=fixed4(lerp(_Water.rgb*.78,float3(1,1,1),.35*body+.6*hl),body*.9);
    } else if(kind<1.5){
     float tt=i.info.w;float ang=atan2(uv.y,uv.x);
     float edge=r*(1+.18*sin(ang*3+seed*20)+.1*sin(ang*7+seed*9));
     float lace=N(uv*4.5+seed*11)*.5+.5;lace=lace*.6+(N(uv*9+seed*5+tt*.3)*.5+.5)*.4;
     float holes=smoothstep(.15+.45*saturate(tt/2),.5+.4*saturate(tt/2),lace);
     float a=smoothstep(1,.45,edge)*lerp(1,holes,.85)*.85;
     col=fixed4(lerp(_Water.rgb,_Foam.rgb,.7+.3*holes),a);
    } else if(kind<2.5){
     float shell=smoothstep(1,.86,r)*(.18+.82*smoothstep(.5,.95,r));
     float hl=smoothstep(.28,0,length(uv-float2(-.35,.4)));
     col=fixed4(lerp(_Water.rgb,float3(1,1,1),.5+.5*hl),saturate(shell*.75+hl*.8));
    } else if(kind<3.5){
     float tt=i.info.w;float ang=atan2(uv.y,uv.x);
     float rr=r*(1+.06*sin(ang*3+seed*20+tt*.7)+.035*sin(ang*7+seed*7));
     float w=.05+.06*saturate(tt/2);
     float ring=exp(-pow((rr-.8)/w,2));float trough=exp(-pow((rr-.8+w*1.6)/(w*1.5),2));
     float broken=.55+.45*sin(ang*4+seed*13+tt*.6);
     col=fixed4(lerp(_Water.rgb*.7,float3(1,1,1),ring),(ring*.45+trough*.12)*broken);
    } else {
     float vv=uv.y*.5+.5;float ang=i.info.w;
     float lip=1-.22*(sin(ang*23+seed*30)*.5+.5)-.18*(sin(ang*57+seed*13)*.5+.5)-.35*smoothstep(.3,.7,_Age);
     float a=smoothstep(lip,lip-.06,vv);
     float streak=.65+.35*sin(ang*41+vv*3+seed*9);
     float rim=smoothstep(lip-.25,lip-.02,vv);
     col=fixed4(lerp(_Water.rgb,float3(1,1,1),.25+.55*rim),a*(.32+.38*rim)*streak*smoothstep(0,.12,vv));
    }
    col.a*=alpha;
    UNITY_APPLY_FOG(i.fogCoord,col);return col;
   }
   ENDCG
  }
 }
}
