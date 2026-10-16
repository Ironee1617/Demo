Shader "Aquarium/Atmosphere"
{
    Properties { _Color ("Tint",Color)=(.4,.9,1,.12) _Mode ("0 glass 1 bubbles 2 rays",Float)=0 }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; float2 data:TEXCOORD1; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; float4 color:COLOR; UNITY_FOG_COORDS(3) float phase:TEXCOORD4; };
            float4 _Color; float _Mode;
            float _AquariumPreviewTime, _AquariumPreviewTimeEnabled;
            float WaterTime() { return lerp(_Time.y,_AquariumPreviewTime,_AquariumPreviewTimeEnabled); }
            v2f vert(appdata v) {
                v2f o;
                float time=WaterTime();
                if (_Mode>.5 && _Mode<1.5) {
                    float rise=frac(time*v.data.y+v.data.x);
                    v.vertex.y+=rise*11.2;
                    v.vertex.x+=sin(time*1.3+v.data.x*37+rise*9)*.11*rise;
                    v.vertex.z+=cos(time+v.data.x*19)*.08*rise;
                } else if(_Mode>1.5) {
                    // Dense ribbon vertices bend with refracted surface waves; the source stays anchored.
                    float depth=1-v.uv.y,phase=v.data.x;
                    v.vertex.x+=depth*(sin(time*.72+phase+depth*3.7)*.52+sin(time*1.21+phase*1.6+depth*8)*.13);
                    v.vertex.z+=depth*(cos(time*.61+phase+depth*4.2)*.34+sin(time*1.09+phase+depth*6)*.10);
                    float3 side=normalize(cross(v.normal,float3(0,1,0))+float3(.0001,0,0));
                    v.vertex.xyz+=side*(v.uv.x*2-1)*depth*.19*sin(time*.83+phase+depth*5);
                }
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal=UnityObjectToWorldNormal(v.normal);
                o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color;o.phase=v.data.x;
                UNITY_TRANSFER_FOG(o,o.vertex); return o;
            }
            fixed4 frag(v2f i):SV_Target {
                float3 v=normalize(_WorldSpaceCameraPos-i.world);
                float rim=pow(1-abs(dot(normalize(i.normal),v)),3);
                fixed4 c=_Color*i.color;
                if (_Mode<.5) {
                    float reflection=pow(saturate(sin(i.world.x*.85+i.world.z*.46+i.world.y*.12)),28);
                    c.a*=.25+rim*.75; c.a+=reflection*_Color.a*.12;
                    c.rgb+=rim*.15+reflection*.16;
                }
                else if (_Mode<1.5) {
                    c.a*=.06+rim*.9;
                    c.rgb+=pow(saturate(dot(normalize(i.normal),normalize(float3(-1,2,-1)))),20)*.8;
                    c.a*=saturate((12-i.world.y)*2);
                } else {
                    float time=WaterTime(),depth=1-i.uv.y;
                    float centre=.5+.10*sin(depth*8-time*.91+i.phase)+.045*sin(depth*17+time*.64+i.phase);
                    float width=.36+.05*sin(time*.77+i.phase+depth*4);
                    float edge=pow(saturate(1-abs(i.uv.x-centre)/width),1.7);
                    float pulse=.70+.19*sin(time*.73+i.phase)+.11*sin(time*1.27+i.phase*1.8);
                    float shimmer=.73+.27*sin(depth*18-time*1.1+i.phase+sin(depth*7+time*.52));
                    c.a*=edge*sin(saturate(i.uv.y)*3.14159)*pulse*shimmer;
                    c.rgb+=.08*shimmer;
                }
                UNITY_APPLY_FOG(i.fogCoord,c); return c;
            }
            ENDCG
        }
    }
}
