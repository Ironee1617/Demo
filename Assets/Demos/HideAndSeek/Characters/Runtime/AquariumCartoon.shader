Shader "Aquarium/Cartoon Character" {
 Properties {
  _MainTex("Original painted colors",2D)="white"{}
  _Tint("Species tint",Color)=(1,1,1,1)
  [Header(Painted eyelids R38)]
  _LidOn("Painted lids (uv3.xy = eye coordinates)",Float)=0
  _LidUpper("Upper lid edge at rest: y = a + b x + c x^2 (m)",Vector)=(.27,0,-.9,0)
  _LidLower("Lower lid edge at rest (a, b, c), w = 1: drawn here",Vector)=(-.3,0,.45,0)
  _LidShut("Line the lids shut onto (a, b, c)",Vector)=(-.0525,0,.434,0)
  _LidStyle("Lash width, shade width, eye rim (m)",Vector)=(.011,.03,.256,0)
  _LidFade("Lash fades out from, to (m from the eye's centre)",Vector)=(.19,.215,0,0)
  _LidSkin("Lid skin",Color)=(1,1,1,1)
  _LidLash("Lash line",Color)=(0,0,0,1)
  [HideInInspector] _Blink("Blink (0 open, 1 shut)",Range(0,1))=0
 }
 SubShader {
  Tags {"RenderType"="Opaque"} LOD 250
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow vertex:vert finalcolor:ClampGlint
  #pragma target 3.0
  #include "AquariumIngestion.cginc"
  sampler2D _MainTex; fixed4 _Tint;
  float _LidOn,_Blink; float4 _LidUpper,_LidLower,_LidShut,_LidStyle,_LidFade; fixed4 _LidSkin,_LidLash;
  // surface.xy = metallic, smoothness (uv2); surface.zw = the eye coordinates for painted lids (uv3), packed into the
  // same interpolator (shader model 3.0 has no room for another)
  struct Input {float2 uv_MainTex; float4 surface;};
  void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.surface=float4(v.texcoord1.xy,v.texcoord3.xy);if(dot(v.normal,v.normal)<1e-12)v.normal=float3(0,1,0);if(dot(v.tangent.xyz,v.tangent.xyz)<1e-12)v.tangent=float4(abs(v.normal.y)<.9?normalize(cross(float3(0,1,0),v.normal)):float3(1,0,0),1);IngestVertex(v);}
  // R38: aliens whose eyelids are paint have no lid mesh. The lids are drawn over the eye in its rest-pose
  // coordinates (uv3: x, y from the eye's centre seen from the front; points away from the eye carry (10, 10), far
  // outside its rim), so they bend with the head like paint does. The upper lid rests on its own edge (above the
  // eye: none; blue's sleepy lid; red's slanted one) and a blink (_Blink, from AquariumPaintedLids) slides it down
  // onto the shut line; a lower lid drawn here rises from below the eye to meet it, the lashes overlapping into one
  // line. Green and yellow keep a lower lid painted in their atlas: their shut line is its edge and no lower lid is
  // drawn. Colours and widths match that paint (paint_lids.py / lid_specs.py).
  float Quad(float4 k,float x){return k.x+k.y*x+k.z*x*x;}
  // One lid, s metres in from its edge: the lash band along the edge, then skin with a soft shade under it.
  fixed4 Lid(float s,float fs,float lash,float lashGone)
  {
   float skinSide=max(smoothstep(lash-fs,lash+fs,s),lashGone);
   fixed3 skin=_LidSkin.rgb*(.88+.12*smoothstep(lash,lash+_LidStyle.y,s));
   return fixed4(lerp(_LidLash.rgb,skin,skinSide),smoothstep(-fs,fs,s));
  }
  fixed3 PaintedLids(fixed3 c,float2 p)
  {
   float x=p.x,r=length(p);
   float fr=max(fwidth(r),1e-5);
   float onEye=1-smoothstep(_LidStyle.z-fr,_LidStyle.z+fr,r);
   float corner=saturate((r-.17)/(_LidStyle.z-.17));
   float lash=_LidStyle.x*(1-.5*corner*corner);                 // the lash thins toward the eye's corners
   float lashGone=smoothstep(_LidFade.x,_LidFade.y,r);          // and stops short of the rim
   float shut=Quad(_LidShut,x);
   float up=p.y-lerp(Quad(_LidUpper,x),shut-.5*_LidStyle.x,_Blink);   // > 0 under the upper lid
   float low=lerp(Quad(_LidLower,x),shut+.5*_LidStyle.x,_Blink)-p.y;  // > 0 under the lower lid
   float fu=max(fwidth(up),1e-5),fl=max(fwidth(low),1e-5);
   fixed4 u=Lid(up,fu,lash,lashGone);c=lerp(c,u.rgb,u.a*onEye);
   if(_LidLower.w>.5){fixed4 l=Lid(low,fl,lash,lashGone);c=lerp(c,l.rgb,l.a*onEye);}
   return c;
  }
  // R67b: the divers' glossy mask frame and glass meet the nose, face and earpieces in thin seams whose normals
  // catch the sun as a mirror: single pixels reach 500x normal brightness for one frame, then vanish as the head
  // moves. The bloom spread each one into a white dot flashing on the face. Ordinary highlights stay under 2.5,
  // so capping the lit colour at 3 removes only those sparks.
  void ClampGlint(Input IN,SurfaceOutputStandard o,inout fixed4 color){color.rgb=min(color.rgb,3);}
  void surf(Input IN,inout SurfaceOutputStandard o)
  {
   fixed3 c=tex2D(_MainTex,IN.uv_MainTex).rgb;
   if(_LidOn>.5) c=PaintedLids(c,IN.surface.zw);
   o.Albedo=c*_Tint.rgb;o.Metallic=IN.surface.x;o.Smoothness=IN.surface.y;o.Alpha=1;
  }
  ENDCG
 } FallBack "Standard"
}
