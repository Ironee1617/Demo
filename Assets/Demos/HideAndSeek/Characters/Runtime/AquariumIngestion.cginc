// Head-first, opaque surface deformation, shared by skin and morph surfaces.
float _Ingest;
float4 _IngestOrigin, _IngestAxis, _IngestMouth;
float _IngestHalfLength, _IngestDepth;
float3 IngestWorld(float3 p)
{
    float3 delta=p-_IngestOrigin.xyz;
    float axial=dot(delta,_IngestAxis.xyz);
    // Translate the whole prey together: the leading end cannot stretch away
    // from the body. Only sections that enter the oral cavity lose thickness.
    float distance=dot(_IngestOrigin.xyz-_IngestMouth.xyz,_IngestAxis.xyz);
    float travel=max(.08,distance+_IngestHalfLength+.08)*smoothstep(0,1,saturate(_Ingest));
    float section=distance+axial-travel;
    float3 centre=_IngestMouth.xyz+_IngestAxis.xyz*max(-.06,section);
    float3 radial=delta-_IngestAxis.xyz*axial;
    float depth=max(.06,_IngestDepth);
    float limit=_IngestMouth.w+max(0,section-depth)*1.15;
    float ratio=length(radial)/max(.001,limit);
    float fit=pow(1+pow(min(ratio,100),8),-1.0/8.0);
    float inside=smoothstep(0,1,saturate((depth-section)/(depth+.06)));
    radial*=lerp(1,fit*(1-inside),smoothstep(0,.06,_Ingest));
    return centre+radial;
}
void IngestVertex(inout appdata_full v)
{
    if(_Ingest<=0)return;
    float3 p=mul(unity_ObjectToWorld,v.vertex).xyz;
    float3 n=UnityObjectToWorldNormal(v.normal);
    float3 t=mul((float3x3)unity_ObjectToWorld,v.tangent.xyz);
    if(dot(t,t)<1e-8)t=cross(n,abs(n.y)<.9?float3(0,1,0):float3(1,0,0));
    t=normalize(t);
    float3 b=normalize(cross(n,t));
    float3 dt=IngestWorld(p+t*.002)-IngestWorld(p-t*.002);
    float3 db=IngestWorld(p+b*.002)-IngestWorld(p-b*.002);
    float3 warpedNormal=cross(dt,db);
    if(dot(warpedNormal,warpedNormal)>1e-16)
    {
        v.normal=normalize(mul(normalize(warpedNormal),(float3x3)unity_ObjectToWorld));
        v.tangent.xyz=normalize(mul((float3x3)unity_WorldToObject,dt));
    }
    v.vertex=mul(unity_WorldToObject,float4(IngestWorld(p),1));
}
