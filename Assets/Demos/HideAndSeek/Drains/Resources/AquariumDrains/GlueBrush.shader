Shader "Aquarium/Brushed Glue"
{
 // R51: one smooth blob of wet grey glue troweled over a drain lid's edge and out onto the sand
 // (AquariumDrainPresentation.BuildGlueMesh). uv2 = the flat base each vertex grows from; _Paint 0..1 pops the
 // whole blob up at once with a little overshoot, then it settles.
 Properties { _Color("Glue",Color)=(.50,.52,.52,1) _Paint("Paint",Float)=1 _Seed("Seed",Float)=0 }
 SubShader {
  Tags {"Queue"="Geometry+20" "RenderType"="Opaque"}
  Pass {
   Tags {"LightMode"="ForwardBase"}
   Cull Off
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   #include "UnityLightingCommon.cginc"
   float4 _Color;float _Paint,_Seed;
   struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;float2 paint:TEXCOORD1;float3 rest:TEXCOORD2;};
   struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 n:TEXCOORD1;float3 world:TEXCOORD2;float wet:TEXCOORD3;UNITY_FOG_COORDS(4)};
   float easeOutBack(float t){float c1=2.2,c3=c1+1;t=t-1;return 1+c3*t*t*t+c1*t*t;}
   v2f vert(appdata v){
    v2f o;float t=saturate(_Paint);float k=t<=0?0:easeOutBack(t);
    // splat: grows out from the middle of the band and up from its base, overshoots and settles
    float3 p=lerp(v.rest,v.vertex.xyz,k);
    o.world=mul(unity_ObjectToWorld,float4(p,1)).xyz;o.pos=UnityWorldToClipPos(o.world);
    o.n=UnityObjectToWorldNormal(v.normal);o.uv=v.uv;o.wet=1-t;
    UNITY_TRANSFER_FOG(o,o.pos);return o;
   }
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float vnoise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+float2(1,1)),f.x),f.y);}
   fixed4 frag(v2f i):SV_Target{
    float3 V=normalize(_WorldSpaceCameraPos-i.world);
    float3 n=normalize(i.n);if(dot(n,V)<0)n=-n;
    // soft mottling and faint trowel swirls, no hard stripes
    float2 q=i.world.xz*9+_Seed;
    float m=vnoise(q)*.55+vnoise(q*2.3)*.3+vnoise(q*5.1)*.15;
    float swirl=.5+.5*sin(i.uv.x*6+i.uv.y*9+vnoise(q*.7)*6);
    float3 L=normalize(_WorldSpaceLightPos0.xyz);float3 H=normalize(L+V);
    float ndl=saturate(dot(n,L)),ndh=saturate(dot(n,H)),ndv=saturate(dot(n,V));
    float3 base=_Color.rgb*(.86+.18*m)*(.96+.06*swirl);
    float3 col=base*(ndl*_LightColor0.rgb*.85+ShadeSH9(float4(n,1))*.9+.06);
    // wet, glossy surface
    col+=_LightColor0.rgb*(pow(ndh,70)*.9+pow(ndh,18)*.12);
    col+=float3(.5,.56,.58)*pow(1-ndv,4)*.22;
    col+=pow(saturate(dot(reflect(-V,n),float3(0,1,0))),20)*.10;
    fixed4 c=fixed4(col,1);UNITY_APPLY_FOG(i.fogCoord,c);return c;
   }
   ENDCG
  }
 }
}
