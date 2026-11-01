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
   struct appdata {float4 vertex:POSITION;float3 tangent:NORMAL;float2 uv:TEXCOORD0;float2 width:TEXCOORD1;float4 color:COLOR;};
   struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 screen:TEXCOORD1;float depth:TEXCOORD2;float4 color:COLOR;UNITY_FOG_COORDS(3)};
   v2f vert(appdata v) {
    v2f o;float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
    float3 tangent=normalize(mul((float3x3)unity_ObjectToWorld,v.tangent));
    float3 side=cross(tangent,normalize(_WorldSpaceCameraPos-world));
    side=length(side)>.001?normalize(side):float3(1,0,0);
    world+=side*((v.uv.y*2-1)*v.width.x);
    o.pos=UnityWorldToClipPos(world);o.uv=v.uv;o.screen=ComputeScreenPos(o.pos);
    o.depth=-mul(UNITY_MATRIX_V,float4(world,1)).z;o.color=v.color;UNITY_TRANSFER_FOG(o,o.pos);return o;
   }
   fixed4 frag(v2f i):SV_Target {
    float across=i.uv.y*2-1;
    float curl=sin(i.uv.x*14+_Age*1.7+_Seed*6.28)*.12;
    float core=exp(-pow((across-curl)*2.4,2));
    float wisps=.78+.22*sin(i.uv.x*19+sin(i.uv.x*7-_Age*.8)*2+_Seed*9);
    float edge=1-smoothstep(.66,1,abs(across));
    float soft=saturate((LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)))-i.depth)*8);
    fixed4 c=fixed4(lerp(_EdgeTint.rgb,_Pigment.rgb,core),core*edge*wisps*i.color.a*soft);
    UNITY_APPLY_FOG(i.fogCoord,c);return c;
   }
   ENDCG
  }
 }
}
