Shader "Aquarium/Underwater Ink Volume"
{
 Properties { _VolumeNoise("3D turbulence",3D)="white"{} _Pigment("Dense pigment",Color)=(.53,.009,.033,1) _EdgeTint("Dilute pigment",Color)=(.86,.035,.095,1) _Age("Flow time",Float)=0 _Seed("Seed",Float)=0 _Density("Concentration",Float)=1 }
 SubShader
 {
  Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
  Cull Front ZWrite Off ZTest Always Blend One OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   sampler3D _VolumeNoise;
   UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
   float4 _Pigment,_EdgeTint;
   float _Age,_Seed,_Density;
   struct v2f { float4 position:SV_POSITION; float3 world:TEXCOORD0; float4 screen:TEXCOORD1; };
   v2f vert(appdata_base v){v2f o;o.position=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.screen=ComputeScreenPos(o.position);return o;}
   float lobe(float3 p,float3 c,float r){float3 q=(p-c)/r;return exp(-dot(q,q)*2.4);}
   float density(float3 p)
   {
    float time=_Age*.58;float spread=1-exp(-max(0,_Age-.20)*.65);
    float swirl=.7*sin(p.y*3.8+time+_Seed*6.28);
    float sn=sin(swirl),cs=cos(swirl);float3 q=p;q.xz=float2(cs*p.x-sn*p.z,sn*p.x+cs*p.z);
    float n=tex3Dlod(_VolumeNoise,float4(q*.37+float3(_Seed,time*.055,-time*.07),0)).r;
    float f=tex3Dlod(_VolumeNoise,float4(q*.86+float3(n*.12,-time*.08,_Seed),0)).r;
    float fine=tex3Dlod(_VolumeNoise,float4(q*1.7+float3(0,time*.10,_Seed*.7),0)).r;
    float warp=(n-.5)*.40;float3 w=p+float3(warp,(f-.5)*.24,-warp);
    float body=lobe(w,float3(0,-.12,0),.48)*(1-spread*.80);
    body+=lobe(w,float3(-.27-spread*.18,.13+spread*.16,.12),.36)*.85;
    body+=lobe(w,float3(.26+spread*.19,.28+spread*.13,-.08-spread*.12),.34)*.83;
    body+=lobe(w,float3(-.11-spread*.06,.48+spread*.13,-.20),.29)*.68;
    body+=lobe(w,float3(.36+spread*.13,-.18,.22+spread*.12),.29)*.63;
    body+=lobe(w,float3(-.35-spread*.14,-.30+spread*.10,-.24-spread*.13),.29)*.59;
    // Turbulence erodes the volume into rolling curls, with fine wisps at its edge.
    float billow=smoothstep(.36+spread*.06,.64+spread*.05,n*.26+f*.49+fine*.25);
    float border=saturate((.97-max(abs(p.x),max(abs(p.y),abs(p.z))))*12);
    float core=(1-smoothstep(.12,.56,length(w)))*.13*(1-smoothstep(.20,1.20,_Age));
    return max(core,max(0,body*billow-(1-billow)*.055))*border*_Density;
   }
   fixed4 frag(v2f i):SV_Target
   {
    float3 ro=mul(unity_WorldToObject,float4(_WorldSpaceCameraPos,1)).xyz*2;
    float3 ray=normalize(i.world-_WorldSpaceCameraPos);
    float3 rd=mul((float3x3)unity_WorldToObject,ray)*2;
    float3 inv=(step(0,rd)*2-1)/max(abs(rd),.00001);
    float3 t0=(-1-ro)*inv,t1=(1-ro)*inv;
    float3 nearT=min(t0,t1),farT=max(t0,t1);
    float entry=max(0,max(nearT.x,max(nearT.y,nearT.z)));
    float finish=min(farT.x,min(farT.y,farT.z));
    float scene=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
    float viewFactor=max(.001,-mul((float3x3)UNITY_MATRIX_V,ray).z);
    finish=min(finish,scene/viewFactor);
    if(finish<=entry)return 0;
    int steps=(int)lerp(16,28,saturate((finish-entry)/3));
    float stepSize=(finish-entry)/steps;
    float jitter=frac(sin(dot(i.position.xy,float2(12.9898,78.233)))*43758.5453);
    float distance=entry+stepSize*jitter;float alpha=0;float3 color=0;
    [loop]for(int step=0;step<steps;step++)
    {
     float3 p=ro+rd*distance;float d=density(p);
     if(d<.001){distance+=stepSize;continue;}
     float soft=saturate((finish-distance)*12);
     float a=1-exp(-d*stepSize*8*soft);
     float3 lightDirection=float3(-.35,.8,-.28);
     float shade=exp(-2.5*(density(p+lightDirection*.22)));
     float3 pigment=lerp(_Pigment.rgb,_EdgeTint.rgb,shade*.72+.16);
     pigment*=.72+.28*saturate(p.y*.5+.5);
     color+=(1-alpha)*a*pigment;alpha+=(1-alpha)*a;
     distance+=stepSize;if(alpha>.985)break;
    }
    // Keep a readable silhouette even through the freshly emitted pigment.
    float translucency=lerp(.80,.64,smoothstep(.35,2.8,_Age));
    return float4(color,alpha)*translucency;
   }
   ENDCG
  }
 }
}
