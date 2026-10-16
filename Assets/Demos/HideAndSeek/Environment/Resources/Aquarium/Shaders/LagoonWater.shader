Shader "Aquarium/Lagoon Water"
{
    Properties { _Color ("Water tint",Color)=(.03,.64,.72,.12) }
    SubShader {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct v2f { float4 vertex:SV_POSITION; float3 world:TEXCOORD0; UNITY_FOG_COORDS(1) };
            float4 _Color;
            float _AquariumPreviewTime, _AquariumPreviewTimeEnabled;
            float WaterTime() { return lerp(_Time.y,_AquariumPreviewTime,_AquariumPreviewTimeEnabled); }
            v2f vert(appdata v) {
                v2f o;
                float t=WaterTime();
                v.vertex.y += sin(v.vertex.x*1.8+t)*cos(v.vertex.z*1.5+t*.8)*.024;
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.vertex=UnityObjectToClipPos(v.vertex);
                UNITY_TRANSFER_FOG(o,o.vertex); return o;
            }
            fixed4 frag(v2f i):SV_Target {
                float t=WaterTime();
                float x=i.world.x,z=i.world.z;
                float sx=sin(x*1.9+z*.53+t)*.045+sin(x*.72-z*1.27+t*.69)*.025;
                float sz=cos(z*1.65-x*.38+t*.9)*.048+sin(x*1.13+z*.84-t*.58)*.024;
                float3 n=normalize(float3(sx,1,sz));
                float3 v=normalize(_WorldSpaceCameraPos-i.world);
                float fresnel=pow(1-abs(dot(n,v)),4);
                float glint=pow(saturate(dot(reflect(-normalize(float3(-.4,1,.4)),n),v)),150);
                float threads=pow(saturate(sin(i.world.x*3.2+sin(i.world.z*3.5+t*.6)+t*.5)),28);
                fixed4 c=float4(_Color.rgb+float3(.22,.38,.4)*fresnel+glint*.22+threads*.035,_Color.a+fresnel*.3+glint*.09);
                UNITY_APPLY_FOG(i.fogCoord,c); return c;
            }
            ENDCG
        }
    }
}
