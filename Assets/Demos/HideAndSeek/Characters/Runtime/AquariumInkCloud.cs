using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // One ray-marched density volume per meal. No sprites, shell spheres or
    // frame-rate-dependent particle simulation; snapshot age drives the flow.
    public sealed class AquariumInkCloud : MonoBehaviour
    {
        private static Texture3D noise;
        private static Material material;
        private Renderer volume;
        private MaterialPropertyBlock properties;
        private Vector3 origin, direction;
        private bool anchored, yellow, concealTransformation, oralJet, capture;
        private float seed,previousAge=-100;
        public static bool IsInk(string kind)=>kind=="eat" || kind=="allyEat" || kind=="burp";

        public void Initialize(int eventId,bool isYellow,bool wholeBody=false,bool jet=false,bool tentacleCapture=false)
        {
            yellow=isYellow;concealTransformation=wholeBody;oralJet=jet;capture=tentacleCapture;seed=Mathf.Repeat(eventId*0.6180339f,1);
            Prewarm();
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name="Continuous ink volume";cube.transform.SetParent(transform,false);
            var collider=cube.GetComponent<Collider>();if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);
            volume=cube.GetComponent<Renderer>();volume.sharedMaterial=material;volume.shadowCastingMode=ShadowCastingMode.Off;volume.receiveShadows=false;volume.enabled=false;
            properties=new MaterialPropertyBlock();
        }
        public static void Prewarm()
        {
            if(material==null)
            {
                var shader=Resources.Load<Shader>("AquariumCharacters/UnderwaterInk");
                material=new Material(shader){name="Underwater pigment density",hideFlags=HideFlags.HideAndDontSave};
                noise=MakeNoise();material.SetTexture("_VolumeNoise",noise);
            }
        }

        public void Present(float age,float duration,Vector3 mouth,Vector3 forward)
        {
            if(age<previousAge-.1f)anchored=false;previousAge=age;
            volume.enabled=age>=0 && age<duration;if(!volume.enabled)return;
            if(!anchored){origin=mouth;direction=forward.sqrMagnitude>.01f?forward.normalized:Vector3.forward;anchored=true;}
            // A brief injection follows the mouth; diluted pigment then drifts independently.
            float hold=concealTransformation?.80f:.65f;
            if(age<hold)origin=mouth;
            // Preserve the initial concentration, then slow the underwater dispersion.
            float flowAge=age<=1?age:1+(age-1)*.50f;
            float expand=1-Mathf.Exp(-flowAge*3.5f),drift=Mathf.Max(0,flowAge-hold);
            Vector3 side=Vector3.Cross(Mathf.Abs(direction.y)>.95f?Vector3.forward:Vector3.up,direction).normalized;
            transform.position=origin+direction*(.025f+drift*.12f)+Vector3.up*(drift*.22f)+side*(Mathf.Sin(seed*6.28f+flowAge*.8f)*drift*.10f);
            transform.rotation=Quaternion.LookRotation(direction,Mathf.Abs(direction.y)>.95f?Vector3.right:Vector3.up);
            transform.localScale=new Vector3(1.10f+expand*1.40f+drift*.36f,1.05f+expand*1.30f+drift*.40f,1.10f+expand*1.35f+drift*.28f)*(concealTransformation?1.12f:1);
            if(oralJet)
            {
                // Start with a narrow directed puff at the actual lower lip,
                // then let the plume spread after the expelled body clears it.
                float jet=1-Mathf.Exp(-age*4),bloom=Mathf.Max(0,age-.45f);
                if(age<.35f)direction=forward.normalized;
                transform.rotation=Quaternion.LookRotation(direction,Mathf.Abs(direction.y)>.95f?Vector3.right:Vector3.up);
                transform.localScale=new Vector3(.18f+jet*.55f+bloom*.45f,.18f+jet*.55f+bloom*.45f,.35f+jet*1.65f+bloom*.2f);
                transform.position=origin+direction*(transform.localScale.z*.42f)+Vector3.up*bloom*.12f;
            }
            properties.Clear();properties.SetFloat("_Age",flowAge);properties.SetFloat("_Seed",seed);
            float fade=Mathf.SmoothStep(0,1,Mathf.InverseLerp(duration*.36f,duration,age));
            // Keep the held body and the leading end leaving the lip readable.
            float visibility=capture?.30f:oralJet?.75f:1;
            properties.SetFloat("_Density",Mathf.SmoothStep(0,1,age/.14f)*1.65f*Mathf.Exp(-drift*.33f)*(1-fade)*visibility);
            properties.SetColor("_Pigment",yellow?new Color(.68f,.31f,.008f):new Color(.13f,.0015f,.008f));
            properties.SetColor("_EdgeTint",yellow?new Color(.98f,.73f,.045f):new Color(.48f,.010f,.027f));
            volume.SetPropertyBlock(properties);
        }

        private static Texture3D MakeNoise()
        {
            const int size=48;var pixels=new Color32[size*size*size];
            for(int z=0;z<size;z++)for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float n=Value(x*8f/size,y*8f/size,z*8f/size,8)*.7f+Value(x*16f/size,y*16f/size,z*16f/size,16)*.3f;
                byte v=(byte)Mathf.Clamp(n*255,0,255);pixels[x+size*(y+size*z)]=new Color32(v,v,v,255);
            }
            var texture=new Texture3D(size,size,size,TextureFormat.RGBA32,false){name="Periodic 3D pigment turbulence",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,hideFlags=HideFlags.HideAndDontSave};
            texture.SetPixels32(pixels);texture.Apply(false,true);return texture;
        }
        private static float Value(float x,float y,float z,int period)
        {
            int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y),iz=Mathf.FloorToInt(z);x-=ix;y-=iy;z-=iz;x=x*x*(3-2*x);y=y*y*(3-2*y);z=z*z*(3-2*z);
            return Mathf.Lerp(Mathf.Lerp(Mathf.Lerp(Hash(ix,iy,iz,period),Hash(ix+1,iy,iz,period),x),Mathf.Lerp(Hash(ix,iy+1,iz,period),Hash(ix+1,iy+1,iz,period),x),y),Mathf.Lerp(Mathf.Lerp(Hash(ix,iy,iz+1,period),Hash(ix+1,iy,iz+1,period),x),Mathf.Lerp(Hash(ix,iy+1,iz+1,period),Hash(ix+1,iy+1,iz+1,period),x),y),z);
        }
        private static float Hash(int x,int y,int z,int period)
        {
            unchecked{uint n=(uint)((x%period)*73856093^(y%period)*19349663^(z%period)*83492791);n=(n^(n>>13))*1274126177;return (n&65535)/65535f;}
        }
    }
}
