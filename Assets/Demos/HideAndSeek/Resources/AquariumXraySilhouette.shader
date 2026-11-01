// R47: the x-ray diver's red silhouettes (Widowmaker style): every living creature drawn over everything
// through a command buffer, once per pixel (stencil bit 16; the screw rims use 32 and 64).
Shader "Aquarium/Xray Silhouette"
{
 Properties { _Color("Silhouette",Color)=(1,.07,.05,.85) }
 SubShader
 {
  Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" "ForceNoShadowCasting"="True" }
  Pass
  {
   ZWrite Off ZTest Always Cull Back Blend SrcAlpha OneMinusSrcAlpha
   Stencil { Ref 16 ReadMask 16 WriteMask 16 Comp NotEqual Pass Replace }
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   float4 _Color;
   struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
   struct v2f { float4 pos:SV_POSITION; float3 n:TEXCOORD0; float3 v:TEXCOORD1; };
   v2f vert(appdata i)
   { v2f o; o.pos=UnityObjectToClipPos(i.vertex); o.n=UnityObjectToWorldNormal(i.normal); o.v=_WorldSpaceCameraPos-mul(unity_ObjectToWorld,i.vertex).xyz; return o; }
   fixed4 frag(v2f i):SV_Target
   {
    float f=1-saturate(abs(dot(normalize(i.n),normalize(i.v))));
    float rim=pow(f,1.5);
    return fixed4(_Color.rgb*(.6+1.2*rim),_Color.a*(.32+.68*rim));
   }
   ENDCG
  }
 }
 Fallback Off
}
