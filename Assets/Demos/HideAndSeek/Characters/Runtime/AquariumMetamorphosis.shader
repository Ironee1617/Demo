Shader "Aquarium/Opaque Metamorphosis" {
 Properties {_SourceTint("Source tint",Color)=(1,1,1,1) _TargetTint("Target tint",Color)=(1,1,1,1) _Blend("Geometric blend",Range(0,1))=0}
 SubShader {Tags {"RenderType"="Opaque"} LOD 250
 CGPROGRAM
 #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
 #pragma target 3.0
 fixed4 _SourceTint,_TargetTint;float _Blend;
 #include "AquariumIngestion.cginc"
 struct Input {float4 surfaceColor;};
 void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);IngestVertex(v);o.surfaceColor=lerp(v.color*_SourceTint,v.texcoord2*_TargetTint,_Blend);}
 void surf(Input IN,inout SurfaceOutputStandard o){o.Albedo=IN.surfaceColor.rgb;o.Metallic=0;o.Smoothness=.12;o.Alpha=1;}
 ENDCG
 } FallBack "Standard"
}
