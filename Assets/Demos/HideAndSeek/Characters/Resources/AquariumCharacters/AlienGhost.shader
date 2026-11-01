// R47: the blue alien's invisibility. While it fades, its skin wears this see-through copy of its painted
// material: _Alpha 1 looks like the opaque body, 0 is gone. Teammates (and the alien itself) keep a faint
// ghost with a cool shimmer (_Shimmer). A depth pre-pass (only while fairly opaque) keeps the body from
// showing its own inside. No shadow caster: an invisible alien casts no shadow.
Shader "Aquarium/Alien Ghost"
{
 Properties
 {
  _MainTex("Painted colors",2D)="white"{}
  _Tint("Tint",Color)=(1,1,1,1)
  _Alpha("Opacity",Range(0,1))=1
  _Shimmer("Teammate shimmer",Range(0,1))=0
 }
 SubShader
 {
  Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" "ForceNoShadowCasting"="True" }
  CGINCLUDE
  #include "UnityCG.cginc"
  #include "Lighting.cginc"
  sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Tint; float _Alpha,_Shimmer;
  struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
  struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 n:TEXCOORD1; float3 w:TEXCOORD2; UNITY_FOG_COORDS(3) };
  v2f vert(appdata v)
  {
   v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=TRANSFORM_TEX(v.uv,_MainTex);
   o.n=UnityObjectToWorldNormal(v.normal); o.w=mul(unity_ObjectToWorld,v.vertex).xyz; UNITY_TRANSFER_FOG(o,o.pos); return o;
  }
  ENDCG
  Pass
  {
   Tags { "LightMode"="ForwardBase" }
   ZWrite On ColorMask 0
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment depth
   fixed4 depth(v2f i):SV_Target { clip(_Alpha-.6); return 0; }
   ENDCG
  }
  Pass
  {
   Tags { "LightMode"="ForwardBase" }
   ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   fixed4 frag(v2f i):SV_Target
   {
    float3 n=normalize(i.n),v=normalize(_WorldSpaceCameraPos-i.w);
    fixed3 albedo=tex2D(_MainTex,i.uv).rgb*_Tint.rgb;
    float ndl=saturate(dot(n,normalize(_WorldSpaceLightPos0.xyz)));
    fixed4 c=fixed4(albedo*(ShadeSH9(float4(n,1))+_LightColor0.rgb*ndl),1);
    float fres=pow(1-saturate(abs(dot(n,v))),2);
    float bands=.5+.5*sin(i.w.y*38-_Time.y*7+i.w.x*5+i.w.z*3);
    c.rgb=lerp(c.rgb,c.rgb*.5+fixed3(.45,.8,1)*(fres*1.1+bands*.12),_Shimmer);
    float a=saturate(_Alpha*(1+_Shimmer*(fres*1.6+bands*.25)));
    UNITY_APPLY_FOG(i.fogCoord,c);
    return fixed4(c.rgb,a);
   }
   ENDCG
  }
 }
 Fallback Off
}
