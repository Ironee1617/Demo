// R60: the divers' changing room (AquariumEnvironment.LockerRoom). Lit by its own ceiling lamps in room space so the
// closed room always reads warm and clean, wherever the tank's sun points: wrap diffuse and a soft highlight from three
// ceiling panels and a cool lamp over the door, a ceiling/floor bounce ambient, vertex-alpha occlusion. Posters blend
// their picture over the paper colour (_Tex). The Diffuse fallback gives the room a shadow caster (the ceiling keeps
// the tank's sun off the divers inside).
Shader "Aquarium/Locker Room"
{
    Properties
    {
        _MainTex ("Picture", 2D) = "white" {}
        _Tex ("Use picture", Float) = 0
        _Gloss ("Highlight", Float) = .25
        _Emission ("Glow", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex; float _Tex, _Gloss, _Emission;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float4 color:COLOR; float3 obj:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; float3 world:TEXCOORD3; };
            v2f vert(appdata v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color; o.obj = v.vertex.xyz;
                o.normal = v.normal; o.uv = v.uv; o.world = mul(unity_ObjectToWorld, v.vertex).xyz; return o;
            }
            float3 Lamp(float3 p, float3 n, float3 v, float3 lamp, float3 colour, float reach)
            {
                float3 l = lamp - p; float d2 = dot(l, l); l *= rsqrt(d2);
                float att = reach / (reach + d2);
                float diffuse = saturate(dot(n, l) * .8 + .2);
                float spec = pow(saturate(dot(n, normalize(l + v))), 42) * _Gloss;
                return colour * att * (diffuse + spec);
            }
            fixed4 frag(v2f i, float face : VFACE) : SV_Target
            {
                float3 n = normalize(i.normal) * (face >= 0 ? 1 : -1);
                float3 camObj = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                float3 v = normalize(camObj - i.obj);
                float3 albedo = i.color.rgb;
                if (_Tex > .5) { float4 t = tex2D(_MainTex, i.uv); albedo = lerp(albedo, t.rgb, t.a); }
                float3 warm = float3(1.0, .93, .80), cool = float3(.62, .86, 1.0);
                float3 light = lerp(float3(.30, .28, .26), float3(.40, .43, .46), n.y * .5 + .5);
                light += Lamp(i.obj, n, v, float3(-3.2, 3.05, .3), warm, 4.5);
                light += Lamp(i.obj, n, v, float3(0, 3.05, .3), warm, 4.5);
                light += Lamp(i.obj, n, v, float3(3.2, 3.05, .3), warm, 4.5);
                light += Lamp(i.obj, n, v, float3(4.3, 2.55, -.1), cool, 1.6);
                light += Lamp(i.obj, n, v, float3(0, 2.3, -2.6), float3(1, .97, .9), 1.4);   // picture lights on the guide wall
                float occlusion = lerp(.55, 1, i.color.a);
                float3 c = albedo * light * occlusion + albedo * _Emission;
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
