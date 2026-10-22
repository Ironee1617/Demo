using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Branch exits on every fifth floor, plus the campfire and merchant stall of special floors.
    public sealed partial class TowerWorld
    {
        private readonly List<SpriteRenderer> branchOpenings=new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> flickers=new List<SpriteRenderer>();
        private readonly List<Vector3> flickerScales=new List<Vector3>();
        // Fifth floors replace the plain exit with three doors; the elite door takes the exit's place.
        public static readonly FloorType[] BranchGates={FloorType.Rest,FloorType.Shop,FloorType.Elite};
        public static float GateX(FloorType type){return type==FloorType.Rest?17f:type==FloorType.Shop?21.5f:ExitX;}
        public static Color GateColor(FloorType type)
        {return type==FloorType.Elite?new Color(.93f,.36f,.33f):type==FloorType.Shop?new Color(.96f,.78f,.36f):type==FloorType.Rest?new Color(.45f,.9f,.55f):Teal;}
        public static readonly Vector2 FeaturePosition=new Vector2(5,Ground);
        public int BranchGateCount{get{return branchOpenings.Count;}}
        public void SetFloorFeature(FloorType type,bool branch)
        {
            branchOpenings.Clear();flickers.Clear();flickerScales.Clear();
            if(portal)portal.gameObject.SetActive(!branch);
            if(branch)foreach(var gate in BranchGates)BuildBranchGate(gate);
            if(type==FloorType.Rest)BuildCampfire(FeaturePosition);
            else if(type==FloorType.Shop)BuildMerchant(FeaturePosition);
        }
        private void TickFeatures(bool open)
        {
            foreach(var opening in branchOpenings)if(opening){var c=opening.color;c.a=open?.78f+Mathf.Sin(clock*2.4f)*.1f:.1f;opening.color=c;}
            for(int i=0;i<flickers.Count;i++)
            {
                var flame=flickers[i];if(!flame)continue;
                float wave=Mathf.Sin(clock*11+i*1.7f)*.5f+Mathf.Sin(clock*17.3f+i)*.5f;
                flame.transform.localScale=new Vector3(flickerScales[i].x*(1+wave*.05f),flickerScales[i].y*(1+wave*.1f),1);
                var c=flame.color;c.a=Mathf.Clamp01(.72f+wave*.18f)*(i==0?.55f:1);flame.color=c;
            }
        }
        private Transform FeatureRoot(string name,Vector2 at)
        {var root=new GameObject(name).transform;root.SetParent(environment,false);root.localPosition=at;return root;}
        private void Flicker(GameObject go){var r=go.GetComponent<SpriteRenderer>();flickers.Add(r);flickerScales.Add(go.transform.localScale);}
        private void BuildBranchGate(FloorType type)
        {
            var gate=FeatureRoot(type+" branch gate",new Vector2(GateX(type),Ground));Color tint=GateColor(type);
            // The carved arch is shared with the ascent gate and multiplied by the destination colour.
            SpriteObject(gate,"Carved branch arch",new Vector2(0,1.625f),Vector2.one,Color.Lerp(Color.white,tint,.45f),-10,GateSprite(activeTheme,Hex("a2a59a")));
            branchOpenings.Add(SpriteObject(gate,"Branch gate opening",new Vector2(0,1.4f),new Vector2(1.5f,2.8f),new Color(tint.r,tint.g,tint.b,.1f),-9,Glow).GetComponent<SpriteRenderer>());
            var emblem=SpriteObject(gate,"Branch gate emblem",new Vector2(0,3.62f),new Vector2(.36f,.36f),tint,-8);emblem.transform.localRotation=Quaternion.Euler(0,0,45);
            SpriteObject(gate,"Branch gate emblem glow",new Vector2(0,3.62f),Vector2.one*1.1f,new Color(tint.r,tint.g,tint.b,.35f),-9,Glow);
        }
        private void BuildCampfire(Vector2 at)
        {
            var root=FeatureRoot("Rest floor campfire",at);
            Flicker(SpriteObject(root,"Campfire warm light",new Vector2(0,.9f),new Vector2(5,4),new Color(1,.55f,.2f,.4f),-12,Glow));
            for(int i=0;i<7;i++)SpriteObject(root,"Campfire stone",new Vector2(-.75f+i*.25f,.09f+(i%2)*.03f),new Vector2(.22f,.16f),Hex(i%2==0?"6b6660":"585550"),-6);
            var log=SpriteObject(root,"Campfire log",new Vector2(-.12f,.22f),new Vector2(1.15f,.17f),Hex("6b4426"),-7);log.transform.localRotation=Quaternion.Euler(0,0,16);
            log=SpriteObject(root,"Campfire log",new Vector2(.12f,.22f),new Vector2(1.15f,.17f),Hex("5a3820"),-7);log.transform.localRotation=Quaternion.Euler(0,0,-16);
            Flicker(SpriteObject(root,"Campfire flame",new Vector2(0,.62f),new Vector2(.62f,.95f),new Color(1,.52f,.16f,.9f),-5,Glow));
            Flicker(SpriteObject(root,"Campfire flame core",new Vector2(0,.52f),new Vector2(.32f,.55f),new Color(1,.93f,.56f,1),-4,Glow));
            SpriteObject(root,"Bedroll",new Vector2(-2.3f,.11f),new Vector2(1.7f,.22f),Hex("7c3f3a"),-6);
            SpriteObject(root,"Bedroll pillow",new Vector2(-3.05f,.2f),new Vector2(.36f,.18f),Hex("d8cbb0"),-5);
            SpriteObject(root,"Travel pack",new Vector2(2.2f,.3f),new Vector2(.62f,.6f),Hex("5d6a45"),-6);
            SpriteObject(root,"Travel pack strap",new Vector2(2.2f,.36f),new Vector2(.66f,.08f),Hex("3c4430"),-5);
        }
        private void BuildMerchant(Vector2 at)
        {
            var root=FeatureRoot("Shop floor merchant stall",at);
            SpriteObject(root,"Stall post",new Vector2(-1.55f,1.75f),new Vector2(.14f,2.6f),Hex("5a3b25"),-8);
            SpriteObject(root,"Stall post",new Vector2(1.55f,1.75f),new Vector2(.14f,2.6f),Hex("5a3b25"),-8);
            SpriteObject(root,"Stall counter",new Vector2(0,.55f),new Vector2(3.2f,1.1f),Hex("6d4a2f"),-6);
            SpriteObject(root,"Stall counter panel",new Vector2(0,.5f),new Vector2(2.8f,.7f),Hex("5b3c26"),-5);
            SpriteObject(root,"Stall counter top",new Vector2(0,1.13f),new Vector2(3.4f,.12f),Hex("a9784b"),-5);
            for(int i=0;i<6;i++)SpriteObject(root,"Awning stripe",new Vector2(-1.5f+i*.6f,3.1f),new Vector2(.6f,.5f),Hex(i%2==0?"c94f45":"f2e2c4"),-6);
            for(int i=0;i<6;i++)SpriteObject(root,"Awning scallop",new Vector2(-1.5f+i*.6f,2.8f),new Vector2(.42f,.16f),Hex(i%2==0?"a83d36":"d9c7a6"),-6);
            string[] goods={"d8584c","5fd4c4","f0c25a","9a7ae0"};
            for(int i=0;i<goods.Length;i++)
            {
                SpriteObject(root,"Potion",new Vector2(-1.05f+i*.7f,1.36f),new Vector2(.2f,.3f),Hex(goods[i]),-4);
                SpriteObject(root,"Potion neck",new Vector2(-1.05f+i*.7f,1.56f),new Vector2(.08f,.1f),Hex("e9e2cf"),-4);
            }
            SpriteObject(root,"Shop sign",new Vector2(0,3.62f),new Vector2(1.3f,.42f),Hex("3b2a1c"),-6);
            SpriteObject(root,"Shop sign coin",new Vector2(0,3.62f),new Vector2(.22f,.22f),Hex("f0c25a"),-5,Disc);
            Flicker(SpriteObject(root,"Lantern glow",new Vector2(1.25f,2.45f),Vector2.one*1.7f,new Color(1,.8f,.4f,.5f),-7,Glow));
            SpriteObject(root,"Lantern",new Vector2(1.25f,2.45f),new Vector2(.18f,.26f),Hex("ffd27a"),-5);
        }
    }
}
