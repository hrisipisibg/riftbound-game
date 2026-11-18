using System;
using UnityEngine;

namespace Riftbound.Editor
{
    internal static class WorldExpansionLayout
    {
        internal const float MinX = -320, MinZ = -160, TileSize = 384, BaseY = -8, HeightRange = 136;
        internal const int Resolution = 513;

        internal sealed class Region
        {
            internal readonly string group;
            internal readonly Vector2 oldCenter, center, halfSize;
            internal readonly float elevation;
            internal readonly bool flat;
            internal readonly bool levelPlateau;
            internal float deltaY;
            internal Region(string group, Vector2 oldCenter, Vector2 center, Vector2 halfSize, float elevation, bool flat = false, bool levelPlateau = false)
            { this.group = group; this.oldCenter = oldCenter; this.center = center; this.halfSize = halfSize; this.elevation = elevation; this.flat = flat; this.levelPlateau = levelPlateau; }
            internal Vector3 Delta => new Vector3(center.x - oldCenter.x, deltaY, center.y - oldCenter.y);
        }

        internal static readonly Region[] Regions = {
            new Region("08 - Highspire Keep Alliance Capital", new Vector2(-45,-20), new Vector2(-200,-60), new Vector2(40,40),12,false,true),
            new Region("16 - Dwarf Starter Frostpeak Foothills", new Vector2(20,-15), new Vector2(-95,-90),new Vector2(26,26),16,false,true),
            new Region("17 - High Elf Starter Silverglen Sanctum", new Vector2(-25,15), new Vector2(-150,75),new Vector2(26,26),15,false,true),
            new Region("06 - Scarlet Crypts Landmark", new Vector2(-55,215), new Vector2(-220,260),new Vector2(26,26),18,false,true),
            new Region("07 - Redridge Frontier Landmark", new Vector2(55,215), new Vector2(280,210),new Vector2(28,28),18,false,true),
            new Region("09 - Gor'Kresh Stronghold Horde Capital", new Vector2(85,235), new Vector2(340,290),new Vector2(40,40),25,false,true),
            new Region("18 - Undead Starter Deathknell Hollow", new Vector2(-28,160), new Vector2(-240,180),new Vector2(26,26),12,false,true),
            new Region("19 - Goblin Starter Rustwater Scrapworks", new Vector2(38,175), new Vector2(240,150),new Vector2(26,26),14,false,true),
            new Region("20 - Redridge Volcanic Crater World Boss", new Vector2(10,185), new Vector2(180,300),new Vector2(28,28),22),
            new Region("13 - Wyrmcrest Summit Roost", new Vector2(0,225), new Vector2(250,510),new Vector2(17,17),75,true),
            new Region("08 - Greenhollow Frontier", new Vector2(-23,156), new Vector2(-40,400),new Vector2(77,66),13)
        };

        internal sealed class Route
        {
            internal readonly string name;
            internal readonly Vector3[] points;
            internal Route(string name, params Vector3[] points) { this.name = name; this.points = points; }
        }
        internal static readonly Route[] Routes = {
            new Route("Highspire highway", new Vector3(-18,0,0),new Vector3(-70,7,-35),new Vector3(-135,12,-55),new Vector3(-200,12,-60)),
            new Route("Frostpeak spur",new Vector3(-135,12,-55),new Vector3(-125,15,-105),new Vector3(-95,16,-90)),
            new Route("Silverglen trail",new Vector3(-30,2,44),new Vector3(-95,12,70),new Vector3(-150,15,75)),
            new Route("Duskwood road",new Vector3(0,3,100),new Vector3(-85,10,145),new Vector3(-150,15,225),new Vector3(-220,18,260)),
            new Route("Deathknell branch",new Vector3(-150,15,225),new Vector3(-205,13,200),new Vector3(-240,12,180)),
            new Route("Greenhollow road",new Vector3(0,3,100),new Vector3(-15,9,235),new Vector3(-17,11,349),new Vector3(-17,11,368),new Vector3(-27,11,386),new Vector3(-36,13,395)),
            new Route("Redridge pass",new Vector3(35,5.85f,91),new Vector3(105,10,150),new Vector3(185,13,170),new Vector3(240,14,150),new Vector3(280,18,210)),
            new Route("Gor'Kresh road",new Vector3(280,18,210),new Vector3(310,22,255),new Vector3(340,25,290)),
            new Route("Volcanic trail",new Vector3(280,18,210),new Vector3(230,20,260),new Vector3(180,22,300)),
            new Route("North valley crossing",new Vector3(-40,13,400),new Vector3(70,18,385),new Vector3(180,22,300)),
            new Route("Wyrmcrest switchbacks",new Vector3(340,25,290),new Vector3(365,34,370),new Vector3(330,48,425),new Vector3(245,70,463),new Vector3(250,75,485),new Vector3(250,75,510)),
            new Route("Westbank approach",new Vector3(-200,12,-60),new Vector3(-202.5f,12,-57.5f),new Vector3(-204,12.48f,-15),new Vector3(-225,9,0),new Vector3(-240,6,50),new Vector3(-260,6,50),new Vector3(-285,6,50))
        };

        internal static Func<float,float,float> OriginalHeight;
        internal static float Smooth(float from,float to,float value) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(from,to,value));
        static float Hill(float x,float z,float cx,float cz,float sx,float sz) => Mathf.Exp(-Mathf.Pow((x-cx)/sx,2)-Mathf.Pow((z-cz)/sz,2));
        internal static float BoxWeight(float x,float z,Vector2 center,Vector2 size,float fade)
        {
            float outside = Mathf.Max(0,Mathf.Max(Mathf.Abs(x-center.x)-size.x,Mathf.Abs(z-center.y)-size.y));
            return 1-Mathf.SmoothStep(0,1,Mathf.Clamp01(outside/fade));
        }
        internal static float SegmentDistance(Vector2 p,Vector3 a,Vector3 b,out float t)
        {
            Vector2 from=new Vector2(a.x,a.z), delta=new Vector2(b.x-a.x,b.z-a.z);
            t=Mathf.Clamp01(Vector2.Dot(p-from,delta)/Mathf.Max(.001f,delta.sqrMagnitude));
            return Vector2.Distance(p,from+delta*t);
        }
        internal static float RoadDistance(float x,float z)
        {
            float nearest=float.MaxValue;
            foreach(var route in Routes) for(int i=1;i<route.points.Length;i++)
                nearest=Mathf.Min(nearest,SegmentDistance(new Vector2(x,z),route.points[i-1],route.points[i],out _));
            return nearest;
        }
        internal static float RiverX(float z) => -264+9*Mathf.Sin(z*.025f);
        internal static float Height(float x,float z)
        {
            float h=5+2*Mathf.PerlinNoise((x+370)*.009f,(z+210)*.009f);
            h+=12*Hill(x,z,-180,95,120,160)+13*Hill(x,z,310,215,145,125);
            h+=65*Hill(x,z,250,510,125,105)+20*Hill(x,z,290,430,70,80)+12*Hill(x,z,-270,340,100,150);
            h+=8*Hill(x,z,30,525,165,85)+3*Mathf.PerlinNoise((x+210)*.031f,(z+85)*.031f);

            // Natural Zone Boundary Barriers (WoW-Scale Mountain Divides)
            // 1. Redridge Mountain Wall: dividing central plains from eastern red canyons
            float redridgeWall=26f*Mathf.Exp(-Mathf.Pow((x-112f)/28f,2))*Smooth(50,95,z)*(1f-Smooth(280,340,z));
            // 2. Highspire Southern Divide: enclosing the southern alpine valley
            float southWall=22f*Smooth(-20,-75,z)*(1f-Mathf.Exp(-Mathf.Pow((x+150f)/55f,2)));
            // 3. Willowmere Duskwood Bluff: elevated forested ridge west of the river
            float westRiverBluff=14f*Hill(x,z,-210,160,60,100);

            h+=redridgeWall+southWall+westRiverBluff;

            float edge=Mathf.Min(Mathf.Min(x-MinX,MinX+TileSize*2-x),Mathf.Min(z-MinZ,MinZ+TileSize*2-z));
            h+=24*(1-Smooth(8,30,edge));
            if(z>-130&&z<140)
            {
                float river=1-Smooth(4,14,Mathf.Abs(x-RiverX(z)));
                h=Mathf.Lerp(h,1.3f,river);
            }
            foreach(var route in Routes) for(int i=1;i<route.points.Length;i++)
            {
                float distance=SegmentDistance(new Vector2(x,z),route.points[i-1],route.points[i],out float t);
                if(route.name=="Westbank approach"&&Mathf.Abs(x-RiverX(z))<9&&Mathf.Abs(z-50)<9) continue;
                h=Mathf.Lerp(h,Mathf.Lerp(route.points[i-1].y,route.points[i].y,t),1-Smooth(5,17,distance));
            }
            if(OriginalHeight!=null)
                h=Mathf.Lerp(h,OriginalHeight(x,z),BoxWeight(x,z,new Vector2(0,18),new Vector2(83,90),14));
            foreach(var region in Regions)
            {
                Vector2 center=region.center;
                if(region.group=="08 - Greenhollow Frontier") center+=new Vector2(23,9);
                float weight=BoxWeight(x,z,center,region.halfSize,18);
                if(weight<=0) continue;
                float mapped=(region.flat||region.levelPlateau)?region.elevation:OriginalHeight(x-region.Delta.x,z-region.Delta.z)+region.deltaY;
                h=Mathf.Lerp(h,mapped,weight);
            }
            // Keep the pass traversable where the relocated summit pad blends into the mountain.
            var summitRoad=Routes[10];
            for(int i=1;i<summitRoad.points.Length;i++)
            {
                float distance=SegmentDistance(new Vector2(x,z),summitRoad.points[i-1],summitRoad.points[i],out float t);
                h=Mathf.Lerp(h,Mathf.Lerp(summitRoad.points[i-1].y,summitRoad.points[i].y,t),1-Smooth(6,18,distance));
            }
            return Mathf.Clamp(h,-4,118);
        }
        internal static float Dirt(float x,float z)
        {
            float dirt=1-Smooth(3,6,RoadDistance(x,z));
            float core=BoxWeight(x,z,new Vector2(0,18),new Vector2(83,90),14);
            dirt=Mathf.Max(dirt,TerrainLayout.Dirt(x,z)*core);
            foreach(var region in Regions)
            {
                if(region.group!="08 - Greenhollow Frontier") continue;
                float weight=BoxWeight(x,z,region.center+new Vector2(23,9),region.halfSize,18);
                dirt=Mathf.Max(dirt,TerrainLayout.Dirt(x-region.Delta.x,z-region.Delta.z)*weight);
            }
            if(z>-130&&z<140) dirt=Mathf.Max(dirt,.75f*(1-Smooth(8,18,Mathf.Abs(x-RiverX(z)))));
            return Mathf.Clamp01(dirt);
        }
    }
}
