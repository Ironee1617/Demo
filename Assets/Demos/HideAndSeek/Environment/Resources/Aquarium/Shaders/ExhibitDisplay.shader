Shader "Aquarium/Exhibit Backlit Print"
{
 Properties {_MainTex("Marine interpretation atlas",2D)="black"{} _Brightness("Backlight",Float)=.58}
 SubShader {
  Tags {"RenderType"="Opaque"} Cull Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   sampler2D _MainTex;float _Brightness;
   struct v2f{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;UNITY_FOG_COORDS(1)};
   v2f vert(appdata_base v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;UNITY_TRANSFER_FOG(o,o.vertex);return o;}
   fixed4 frag(v2f i):SV_Target{fixed4 c=tex2D(_MainTex,i.uv);c.rgb*=_Brightness;UNITY_APPLY_FOG(i.fogCoord,c);return c;}
   ENDCG
  }
 }
}
