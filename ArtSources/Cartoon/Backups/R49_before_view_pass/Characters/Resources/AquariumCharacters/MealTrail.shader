Shader "Aquarium/Soft Fish Blood Trail"
{
 Properties { _Pigment("Original fish blood",Color)=(.10,.026,.125,1) _EdgeTint("Diluted violet",Color)=(.28,.12,.31,1) }
 SubShader {
  Tags {"Queue"="Transparent+12" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
   float4 _Pigment,_EdgeTint;float _Age,_Seed;
   // R39: one soft, round puff of dye per quad (AquariumMealTrail lays a chain of them along the path).
   // vertex = the puff's centre (all four corners), normal = the path's direction there, uv = its corner (-1..1),
   // uv2 = (half-width, emission time), uv3 = (how much of it is kept as the dye thins out, spacing to the next
   // kept puff), colour alpha = how dense the dye is there (the trail's opacity, before sharing it out).
   struct appdata {float4 vertex:POSITION;float3 along:NORMAL;float2 corner:TEXCOORD0;float2 width:TEXCOORD1;float2 share:TEXCOORD2;float4 color:COLOR;};
   struct v2f {float4 pos:SV_POSITION;float3 puff:TEXCOORD0;float4 screen:TEXCOORD1;float depth:TEXCOORD2;float4 color:COLOR;UNITY_FOG_COORDS(3)};
   v2f vert(appdata v) {
    v2f o;float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
    // facing the camera
    float3 right=UNITY_MATRIX_V[0].xyz,up=UNITY_MATRIX_V[1].xyz;
    world+=(right*v.corner.x+up*v.corner.y)*v.width.x;
    o.pos=UnityWorldToClipPos(world);o.puff=float3(v.corner,v.width.y);o.screen=ComputeScreenPos(o.pos);
    o.depth=-mul(UNITY_MATRIX_V,float4(world,1)).z;o.color=v.color;
    // Share the density out among the puffs that overlap this one on screen: about 1.1 half-widths over their
    // spacing seen from the side, many more looking down the path (where they stack up), so the dye is as dense
    // from any side and a trail running away from the camera does not pile into a dark blob.
    float3 centre=mul(unity_ObjectToWorld,v.vertex).xyz;
    float3 path=normalize(mul((float3x3)unity_ObjectToWorld,v.along));
    float sideways=max(length(cross(path,normalize(_WorldSpaceCameraPos-centre))),.06);
    float overlap=max(1,1.1*v.width.x/(v.share.y*sideways));
    o.color.a=v.share.x*(1-pow(1-min(v.color.a,.995),1/overlap));
    UNITY_TRANSFER_FOG(o,o.pos);return o;
   }
   fixed4 frag(v2f i):SV_Target {
    float r=length(i.puff.xy);
    // dense in the middle, soft to nothing at the rim (the old ribbon's profile across its width)
    float core=exp(-r*r*2.56);
    float edge=1-smoothstep(.8,1,r);
    // the wisps are pinned to the dye's emission time, so they do not slide along the trail
    float u=i.puff.z*.35;
    float wisps=.78+.22*sin(u*19+sin(u*7-_Age*.8)*2+_Seed*9);
    float soft=saturate((LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)))-i.depth)*8);
    fixed4 c=fixed4(lerp(_EdgeTint.rgb,_Pigment.rgb,core),core*edge*wisps*i.color.a*soft);
    UNITY_APPLY_FOG(i.fogCoord,c);return c;
   }
   ENDCG
  }
 }
}
