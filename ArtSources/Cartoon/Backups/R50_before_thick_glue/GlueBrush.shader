Shader "Aquarium/Brushed Glue"
{
 // R49: grey glue painted round a drain lid's rim with a thick brush (AquariumDrainPresentation.BuildGlue).
 // uv0 = (metres along the stroke, 0..1 across it), uv1 = (paint time 0..1, dry-brush feather 0..1),
 // uv2 = where the vertex sits before the paint reaches it (object space). _Paint runs 0..1 over the application.
 Properties { _Color("Glue",Color)=(.52,.54,.56,1) _Paint("Paint",Float)=1 _Seed("Seed",Float)=0 }
 SubShader {
  Tags {"Queue"="Geometry+20" "RenderType"="Opaque"}
  Pass {
   Tags {"LightMode"="ForwardBase"}
   Cull Back
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   #include "UnityLightingCommon.cginc"
   float4 _Color;float _Paint,_Seed;
   struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;float2 paint:TEXCOORD1;float3 rest:TEXCOORD2;};
   struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 n:TEXCOORD1;float3 world:TEXCOORD2;float3 radial:TEXCOORD3;float4 paint:TEXCOORD4;UNITY_FOG_COORDS(5)};
   v2f vert(appdata v){
    v2f o;float since=_Paint-v.paint.x;float g=saturate(since/.09);
    float3 p=lerp(v.rest,v.vertex.xyz,g);
    // the paint arrives as a wet bead that settles (a little bulge at the brush front)
    p.y+=g*(1-g)*4*.004;
    o.world=mul(unity_ObjectToWorld,float4(p,1)).xyz;o.pos=UnityWorldToClipPos(o.world);
    o.n=UnityObjectToWorldNormal(v.normal);
    float2 xz=v.vertex.xz;o.radial=normalize(mul((float3x3)unity_ObjectToWorld,float3(xz.x,0,xz.y)+float3(1e-5,0,0)));
    o.uv=v.uv;o.paint=float4(v.paint,since,0);
    UNITY_TRANSFER_FOG(o,o.pos);return o;
   }
   fixed4 frag(v2f i):SV_Target{
    clip(i.paint.z);
    float u=i.uv.x,v=i.uv.y;
    // bristle marks along the stroke: fine grooves plus a few coarse ridges, wavering with the hand
    float a1=v*170+sin(u*21+_Seed*7)*2.4+sin(u*57)*.5,a2=v*48+u*5+_Seed*3;
    float b1=.5+.5*sin(a1),b2=.5+.5*sin(a2);
    float bristle=b1*.55+b2*.45;
    // ragged dry-brush edges and stroke tails
    float edge=min(v,1-v);
    clip(edge*12+i.paint.y*1.2-.95+bristle*.9-.55*(1-i.paint.y));
    float3 n=normalize(i.n+i.radial*(cos(a1)*.16+cos(a2)*.12));
    float3 V=normalize(_WorldSpaceCameraPos-i.world);
    float3 L=normalize(_WorldSpaceLightPos0.xyz);float3 H=normalize(L+V);
    float ndl=saturate(dot(n,L)),ndh=saturate(dot(n,H)),ndv=saturate(dot(n,V));
    float wet=1-saturate(i.paint.z*.8);
    float3 base=_Color.rgb*(.78+.22*bristle)*(1-.1*(1-b2));
    float3 col=base*(ndl*_LightColor0.rgb*.8+ShadeSH9(float4(n,1))+.12);
    col+=_LightColor0.rgb*(pow(ndh,90)*1.3+pow(ndh,24)*.18*(1+wet));
    col+=float3(.7,.82,.9)*pow(1-ndv,3.5)*(.35+.25*wet);
    // a soft glossy sheen from above, so it reads wet even without a light hitting it
    col+=pow(saturate(dot(reflect(-V,n),float3(0,1,0))),12)*(.22+.2*wet);
    fixed4 c=fixed4(col,1);UNITY_APPLY_FOG(i.fogCoord,c);return c;
   }
   ENDCG
  }
 }
}
