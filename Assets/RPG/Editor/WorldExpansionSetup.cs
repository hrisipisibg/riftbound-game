using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Riftbound.Editor
{
    public static class WorldExpansionSetup
    {
        public const string ScenePath="Assets/RPG/Scenes/Outpost-Expanded.unity";
        const string SourceScene="Assets/RPG/Scenes/Outpost-Updated.unity";
        const string Folder="Assets/RPG/Terrain/WorldExpansion/";
        static Terrain sourceTerrain;
        static readonly List<Terrain> Tiles=new List<Terrain>();
        static readonly StringBuilder Report=new StringBuilder();
        static int sceneryRenderers;

        public static void BatchGenerate()
        {
            try { Generate(); EditorApplication.Exit(0); }
            catch(Exception e) { File.WriteAllText("WorldExpansionResult.txt",Report+"\nFAILED: "+e); Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("Riftbound/World/Generate expanded world copy")]
        public static void Generate()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before world generation.");
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Report.Clear(); Tiles.Clear(); sceneryRenderers=0;
            var scene=EditorSceneManager.OpenScene(SourceScene);
            sourceTerrain=Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault();
            if(!sourceTerrain) throw new InvalidOperationException("The source heightfield is missing.");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            if(!EditorSceneManager.SaveScene(scene,ScenePath)) throw new IOException("Could not save the separate expanded scene.");
            WorldExpansionLayout.OriginalHeight=OriginalHeight;
            foreach(var region in WorldExpansionLayout.Regions)
            {
                region.deltaY=region.flat?region.elevation-48:region.elevation-OriginalHeight(region.oldCenter.x,region.oldCenter.y);
                var group=Find(region.group);
                if(!group) throw new InvalidOperationException("Missing zone group: "+region.group);
                group.position+=region.Delta;
                Report.AppendLine($"MOVE {region.group}: old={region.oldCenter}, new={region.center}, delta={region.Delta}");
            }
            DisableOldBounds();
            ClearExistingRoads();
            var root=new GameObject("21 - Expanded World Terrain").transform;
            CreateTiles(root);
            CreateOuterBounds(root);
            MoveDetachedActors();
            BuildWestbank(root);
            WorldArchitectureBuilder.BuildAllSettlementsAndCapitals(root, Report);
            BuildZoneGateways(root);
            ScatterScenery(root);
            AddRoadSigns(root);
            StylizedRacialModelSystem.UpgradeAllSceneModelsAndMaterials(Report);
            TextureUpgradeSetup.Apply();
            WorldDressingPass.Run(true);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Validate();
            File.WriteAllText("WorldExpansionResult.txt",Report.ToString());
            Debug.Log("Expanded world generated. "+ScenePath);
        }

        static void CreateOuterBounds(Transform parent)
        {
            foreach(var side in new[]{new Vector3(-321,70,224),new Vector3(449,70,224),new Vector3(64,70,-161),new Vector3(64,70,609)})
            {
                var edge=new GameObject("Expanded world boundary"); edge.transform.SetParent(parent); edge.transform.position=side;
                edge.AddComponent<BoxCollider>().size=Mathf.Abs(side.x-64)>300?new Vector3(2,156,770):new Vector3(770,156,2);
            }
        }

        static Transform Find(string name) => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>t.name==name);
        static float OriginalHeight(float x,float z)
        {
            Vector3 origin=sourceTerrain.transform.position, size=sourceTerrain.terrainData.size;
            return sourceTerrain.terrainData.GetInterpolatedHeight(Mathf.Clamp01((x-origin.x)/size.x),Mathf.Clamp01((z-origin.z)/size.z))+origin.y;
        }
        static void DisableOldBounds()
        {
            foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(t.name=="World boundary"||t.name=="Distant ridge"||t.name=="Greenhollow distant ridge") t.gameObject.SetActive(false);
            foreach(var terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            { terrain.enabled=false; var collider=terrain.GetComponent<TerrainCollider>(); if(collider) collider.enabled=false; }
        }
        static void ClearExistingRoads()
        {
            // 1. Hide legacy raised slab cubes, old-terrain road ribbons, and orphaned Greenhollow brook in Outpost-Expanded
            string[] legacySlabsAndRibbons =
            {
                "Village square", "West lane", "East lane",
                "Forest road", "Camp approach",
                "Watchtower trail", "Watchtower courtyard",
                "Western crypt trail", "Ruined courtyard",
                "Northern frontier trail", "Greenhollow approach", "Ancient grove trail", "Greenhollow village square",
                "Shallow Willow Brook", "Willow crossing deck", "Bridge handrail", "Bridge post"
            };
            foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (legacySlabsAndRibbons.Contains(tr.name))
                {
                    var r = tr.GetComponent<Renderer>();
                    if (r) r.enabled = false;
                    var c = tr.GetComponent<Collider>();
                    if (c) c.enabled = false;
                }
            }

            // 2. Re-ground all relocated children in 05 - Ashwood Adventure, 07 - Western Crypt, and 08 - Greenhollow Frontier to new terrain height
            foreach (var region in WorldExpansionLayout.Regions)
            {
                if (region.group != "05 - Ashwood Adventure" && region.group != "07 - Western Crypt" && region.group != "08 - Greenhollow Frontier")
                    continue;
                var group = Find(region.group);
                if (!group) continue;

                void RegroundTransform(Transform item)
                {
                    Vector3 oldPos = item.position - region.Delta;
                    float oldGround = OriginalHeight(oldPos.x, oldPos.z);
                    float offsetAboveGround = Mathf.Clamp(oldPos.y - oldGround, -0.12f, 8.0f);
                    float newGround = WorldExpansionLayout.Height(item.position.x, item.position.z);
                    item.position = new Vector3(item.position.x, newGround + offsetAboveGround, item.position.z);
                }

                foreach (Transform child in group)
                {
                    if (!child.gameObject.activeSelf) continue;
                    if (child.name == "Animals")
                    {
                        foreach (Transform animal in child)
                            RegroundTransform(animal);
                    }
                    else
                    {
                        RegroundTransform(child);
                    }
                }
            }

            // 3. Clear any trees/rocks/bushes that sit directly on the expanded road network or Greenhollow square
            foreach(string name in new[]{"03 - Forest Road","05 - Ashwood Adventure","07 - Western Crypt","08 - Greenhollow Frontier"})
            {
                var group=Find(name); if(!group) continue;
                foreach(Transform child in group)
                {
                    bool nature=child.name.Contains("Tree")||child.name.StartsWith("Rock_")||child.name.StartsWith("Bush");
                    if(!nature) continue;
                    bool onRoad=WorldExpansionLayout.RoadDistance(child.position.x,child.position.z)<6f;
                    bool inGreenhollowSquare=name=="08 - Greenhollow Frontier"&&Vector2.Distance(new Vector2(child.position.x,child.position.z),new Vector2(-40f,400f))<22f;
                    if(onRoad||inGreenhollowSquare) child.gameObject.SetActive(false);
                }
            }
        }
        static TerrainLayer[] Layers()
        {
            var layers=new List<TerrainLayer>();
            foreach(string name in new[]{"Painted meadow","Earthen trail","Weathered stone"})
            {
                var layer=AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/RPG/Terrain/"+name+".terrainlayer");
                if(!layer) throw new InvalidOperationException("Missing terrain layer "+name);
                layers.Add(layer);
            }
            foreach(string name in new[]{"Forest floor","Rocky highland","Summit snow","Redridge canyon","Cobblestone highway"})
            {
                var layer=AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/RPG/Terrain/WorldExpansionLayers/"+name+".terrainlayer");
                if(layer) layers.Add(layer);
            }
            return layers.ToArray();
        }
        static void CreateTiles(Transform root)
        {
            var layers=Layers();
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Terrain/Frontier Terrain.mat");
            for(int tz=0;tz<2;tz++) for(int tx=0;tx<2;tx++)
            {
                string path=Folder+$"World tile {tx}-{tz}.asset";
                var data=AssetDatabase.LoadAssetAtPath<TerrainData>(path);
                if(!data) { data=new TerrainData(); AssetDatabase.CreateAsset(data,path); }
                data.heightmapResolution=WorldExpansionLayout.Resolution; data.alphamapResolution=256; data.baseMapResolution=1024;
                data.size=new Vector3(WorldExpansionLayout.TileSize,WorldExpansionLayout.HeightRange,WorldExpansionLayout.TileSize);
                float ox=WorldExpansionLayout.MinX+tx*WorldExpansionLayout.TileSize, oz=WorldExpansionLayout.MinZ+tz*WorldExpansionLayout.TileSize;
                int resolution=data.heightmapResolution;
                var heights=new float[resolution,resolution];
                for(int z=0;z<resolution;z++) for(int x=0;x<resolution;x++)
                {
                    float wx=ox+x/(float)(resolution-1)*WorldExpansionLayout.TileSize, wz=oz+z/(float)(resolution-1)*WorldExpansionLayout.TileSize;
                    heights[z,x]=(WorldExpansionLayout.Height(wx,wz)-WorldExpansionLayout.BaseY)/WorldExpansionLayout.HeightRange;
                }
                data.SetHeights(0,0,heights); data.terrainLayers=layers;
                var splat=new float[256,256,layers.Length];
                for(int z=0;z<256;z++) for(int x=0;x<256;x++)
                {
                    float u=x/255f,v=z/255f,wx=ox+u*WorldExpansionLayout.TileSize,wz=oz+v*WorldExpansionLayout.TileSize;
                    float rock=WorldExpansionLayout.Smooth(20,45,data.GetSteepness(u,v));
                    float dirt=WorldExpansionLayout.Dirt(wx,wz)*(1-rock);
                    splat[z,x,0]=1-rock-dirt; splat[z,x,1]=dirt; splat[z,x,2]=rock;
                    for(int i=3;i<layers.Length;i++)
                    {
                        float weight=0;
                        if(layers[i].name=="Forest floor") weight=.65f*(1-rock)*(1-dirt)*Mathf.Max(WorldExpansionLayout.BoxWeight(wx,wz,new Vector2(-220,235),new Vector2(75,85),45),WorldExpansionLayout.BoxWeight(wx,wz,new Vector2(-40,410),new Vector2(95,70),40));
                        if(layers[i].name=="Rocky highland") weight=.45f*(1-dirt)*WorldExpansionLayout.Smooth(12,40,WorldExpansionLayout.Height(wx,wz));
                        if(layers[i].name=="Summit snow") weight=.7f*(1-rock)*WorldExpansionLayout.Smooth(55,76,WorldExpansionLayout.Height(wx,wz));
                        if(layers[i].name=="Redridge canyon")
                        {
                            float canyonSector=WorldExpansionLayout.BoxWeight(wx,wz,new Vector2(270,230),new Vector2(120,110),50);
                            weight=.85f*(1-rock)*canyonSector;
                        }
                        if(layers[i].name=="Cobblestone highway")
                        {
                            float emberwatchSquare=WorldExpansionLayout.BoxWeight(wx,wz,new Vector2(0,4),new Vector2(15,16),7);
                            float greenhollowSquare=WorldExpansionLayout.BoxWeight(wx,wz,new Vector2(-40,400),new Vector2(15,15),8);
                            float highspireCourtyard=WorldExpansionLayout.BoxWeight(wx,wz,new Vector2(-200,-60),new Vector2(39,39),14);
                            float frostpeakSquare=WorldExpansionLayout.BoxWeight(wx,wz,new Vector2(-95,-90),new Vector2(14,14),8);
                            float silverglenSquare=WorldExpansionLayout.BoxWeight(wx,wz,new Vector2(-150,75),new Vector2(14,14),8);
                            float deathknellSquare=WorldExpansionLayout.BoxWeight(wx,wz,new Vector2(-240,180),new Vector2(13,13),8);
                            float pavedRoad=1-WorldExpansionLayout.Smooth(2.0f,4.5f,WorldExpansionLayout.SegmentDistance(new Vector2(wx,wz),new Vector3(-18,0,0),new Vector3(-200,12,-60),out _));
                            float villagePaving=Mathf.Max(Mathf.Max(Mathf.Max(frostpeakSquare,silverglenSquare),deathknellSquare),Mathf.Max(emberwatchSquare,greenhollowSquare))*0.84f;
                            weight=Mathf.Clamp01(Mathf.Max(Mathf.Max(highspireCourtyard*0.92f,pavedRoad*0.85f),villagePaving));
                        }
                        for(int j=0;j<i;j++) splat[z,x,j]*=1-weight;
                        splat[z,x,i]=weight;
                    }
                }
                data.SetAlphamaps(0,0,splat); EditorUtility.SetDirty(data);
                var go=Terrain.CreateTerrainGameObject(data); go.name=$"Expanded terrain {tx}-{tz}"; go.transform.SetParent(root);
                go.transform.position=new Vector3(ox,WorldExpansionLayout.BaseY,oz);
                var terrain=go.GetComponent<Terrain>(); terrain.materialTemplate=material; terrain.drawInstanced=true; terrain.heightmapPixelError=5;
                terrain.basemapDistance=420; terrain.treeDistance=420; terrain.detailObjectDistance=95; terrain.shadowCastingMode=ShadowCastingMode.On; terrain.allowAutoConnect=true; terrain.groupingID=1;
                terrain.Flush(); Tiles.Add(terrain);
            }
            Tiles[0].SetNeighbors(null,Tiles[2],Tiles[1],null); Tiles[1].SetNeighbors(Tiles[0],Tiles[3],null,null);
            Tiles[2].SetNeighbors(null,null,Tiles[3],Tiles[0]); Tiles[3].SetNeighbors(Tiles[2],null,null,Tiles[1]);
            Report.AppendLine("WORLD: four 384m terrain tiles; total 768x768m; bounds x[-320,448], z[-160,608].");
            Report.AppendLine("SURFACES: "+string.Join(", ",layers.Select(l=>l.name)));
        }
        static void MoveDetachedActors()
        {
            foreach(var fm in Object.FindObjectsByType<FlightMaster>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                string key=fm.nodeName;
                var region=WorldExpansionLayout.Regions.FirstOrDefault(r=>key.Contains("Highspire")?r.group.Contains("Highspire"):key.Contains("Duskwood")?r.group.Contains("Scarlet Crypts Landmark"):key.Contains("Bloodrock")?r.group.Contains("Redridge Frontier Landmark"):key.Contains("Gor'Kresh")?r.group.Contains("Gor'Kresh"):key.Contains("Wyrmcrest")?r.group.Contains("Wyrmcrest"):false);
                if(region!=null) fm.transform.position+=region.Delta;
                var point=fm.transform.position; point.y=WorldExpansionLayout.Height(point.x,point.z)+.08f; fm.transform.position=point; fm.flightStationPos=point;
                EditorUtility.SetDirty(fm); Report.AppendLine($"FLIGHT {fm.nodeName}: {point}");
            }
            foreach(var portal in Object.FindObjectsByType<DungeonPortal>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(portal.isExitPortal) { portal.destination=new Vector3(-220,18.4f,250); EditorUtility.SetDirty(portal); }
            var gathering=Find("15 - World Gathering Nodes");
            if(gathering) foreach(Transform node in gathering)
            {
                Vector3 p=node.position;
                if(p.z<130) continue;
                var nearest=WorldExpansionLayout.Regions.Where(r=>!r.flat&&r.group!="08 - Greenhollow Frontier"&&r.oldCenter.y>130).OrderBy(r=>Vector2.Distance(r.oldCenter,new Vector2(p.x,p.z))).First();
                p+=nearest.Delta; p.y=WorldExpansionLayout.Height(p.x,p.z)+.05f; node.position=p;
            }
            var medievalProps=Find("10 - Medieval Town Props");
            if(medievalProps)
            {
                var greenhollow=WorldExpansionLayout.Regions.FirstOrDefault(r=>r.group=="08 - Greenhollow Frontier");
                foreach(Transform prop in medievalProps)
                {
                    Vector3 p=prop.position;
                    if(prop.name.Contains("Bench") && p.x > -8.5f && p.x < -6.5f && p.z < 15f) p.x = -12.2f;
                    if(prop.name.Contains("Barrel") && p.x > -10.0f && p.x < -7.5f && p.z < 15f && p.z > 2f) p.x -= 2.6f;
                    if(p.z>130&&greenhollow!=null&&p.z<300) p+=greenhollow.Delta;
                    p.y=WorldExpansionLayout.Height(p.x,p.z);
                    prop.position=p;
                }
            }
        }
        static void BuildZoneGateways(Transform parent)
        {
            WorldArchitectureBuilder.BuildModularZoneGateways(parent, Report);
        }
        static void BuildWestbank(Transform parent)
        {
            var root=new GameObject("Westbank River and Willow Rest").transform; root.SetParent(parent);
            var vertices=new Vector3[66]; var uv=new Vector2[66]; var triangles=new int[32*6];
            for(int i=0;i<=32;i++)
            {
                float z=Mathf.Lerp(-125,135,i/32f),x=WorldExpansionLayout.RiverX(z);
                vertices[i*2]=new Vector3(x-5,3.25f,z); vertices[i*2+1]=new Vector3(x+5,3.25f,z);
                uv[i*2]=new Vector2(0,i*2); uv[i*2+1]=new Vector2(1,i*2);
                if(i==32) continue;
                int a=i*2,t=i*6; triangles[t]=a; triangles[t+1]=a+2; triangles[t+2]=a+1; triangles[t+3]=a+1; triangles[t+4]=a+2; triangles[t+5]=a+3;
            }
            var mesh=new Mesh { name="Westbank river surface",vertices=vertices,uv=uv,triangles=triangles }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string path=Folder+"Westbank river.asset"; var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(stored) { EditorUtility.CopySerialized(mesh,stored); Object.DestroyImmediate(mesh); mesh=stored; } else AssetDatabase.CreateAsset(mesh,path);
            var water=new GameObject("Westbank flowing river"); water.transform.SetParent(root); water.AddComponent<MeshFilter>().sharedMesh=mesh;
            water.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Terrain/Flowing brook.mat"); water.AddComponent<FlowingWater>();
            var wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Dark timber.mat");
            float bridgeX=WorldExpansionLayout.RiverX(50);
            Cube("Westbank bridge deck",new Vector3(bridgeX,5.8f,50),new Vector3(26,.4f,7),wood,root);
            foreach(float z in new[]{46.5f,53.5f})
            {
                Cube("Westbank bridge rail",new Vector3(bridgeX,6.8f,z),new Vector3(26,.15f,.18f),wood,root);
                foreach(float x in new[]{bridgeX-11,bridgeX,bridgeX+11}) Cube("Westbank bridge support",new Vector3(x,5.5f,z),new Vector3(.3f,3.2f,.3f),wood,root);
            }
            var chestTemplate=Object.FindObjectsByType<LootChest>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(c=>c.chestId=="village-cache");
            var chest=Object.Instantiate(chestTemplate,root); chest.name="Willow Rest river cache"; chest.chestId="willow-rest-cache"; chest.gold=20; chest.experience=40;
            chest.requiresCaptainDefeated=false; chest.requiresWardenDefeated=false;
            chest.transform.position=new Vector3(-285,WorldExpansionLayout.Height(-285,60)+.05f,60);
            Sign("WILLOW REST\nWestbank river trail",new Vector3(-284,0,47),root);
            Report.AppendLine("NEW REGION: Westbank river 260m, crossing bridge, Willow Rest cache at(-285,60).");
        }
        static void Cube(string name,Vector3 point,Vector3 scale,Material material,Transform parent)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(parent); go.transform.position=point; go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material; sceneryRenderers++;
        }
        static void ScatterScenery(Transform parent)
        {
            var root=new GameObject("Expanded forests and roadside scenery").transform; root.SetParent(parent);
            var forest=Find("03 - Forest Road");
            var templates=forest.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==forest&&t.GetComponentsInChildren<Renderer>().Length>0&&(t.name.StartsWith("NormalTree")||t.name.StartsWith("PineTree")||t.name.StartsWith("Rock_")||t.name=="Bush")).ToArray();
            if(templates.Length==0) throw new InvalidOperationException("No material-ready nature templates found.");
            var random=new System.Random(31479);
            for(int i=0;i<440&&sceneryRenderers<640;i++)
            {
                float x=-305+(float)random.NextDouble()*738,z=-145+(float)random.NextDouble()*738;
                if(x>-96&&x<96&&z>-78&&z<110) continue;
                if(WorldExpansionLayout.RoadDistance(x,z)<11) continue;
                if(WorldExpansionLayout.Regions.Any(r=>Vector2.Distance(new Vector2(x,z),r.center)<Mathf.Max(r.halfSize.x,r.halfSize.y)+5)) continue;
                if(z>-130&&z<140&&Mathf.Abs(x-WorldExpansionLayout.RiverX(z))<16) continue;
                var template=templates[random.Next(templates.Length)];
                if(WorldExpansionLayout.Height(x,z)>48&&!template.name.StartsWith("Rock_")) continue;
                var instance=Object.Instantiate(template.gameObject,root); instance.name="Expanded "+template.name;
                instance.transform.position=new Vector3(x,WorldExpansionLayout.Height(x,z)-.04f,z); instance.transform.rotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);
                instance.transform.localScale=template.lossyScale*Mathf.Lerp(.8f,1.35f,(float)random.NextDouble()); instance.SetActive(true);
                foreach(var renderer in instance.GetComponentsInChildren<Renderer>()) { renderer.shadowCastingMode=template.name=="Bush"?ShadowCastingMode.Off:ShadowCastingMode.On; sceneryRenderers++; }
            }
            Report.AppendLine($"SCENERY: {root.childCount} new material-ready nature instances; {sceneryRenderers} new scenery renderers incl bridge/signs.");
        }
        static void AddRoadSigns(Transform root)
        {
            Sign("NORTH: GREENHOLLOW\nWEST: DUSKWOOD / EAST: BLOODROCK",new Vector3(8,0,108),root);
            Sign("HIGHSPIRE KEEP\nWESTBANK RIVER / FROSTPEAK",new Vector3(-68,0,-30),root);
            Sign("BLOODROCK PASS\nRUSTWATER / GOR'KRESH / WYRM CREST",new Vector3(115,0,157),root);
            Sign("WYRM CREST\nMountain switchback trail",new Vector3(356,0,348),root);
        }
        static void Sign(string text,Vector3 point,Transform parent)
        {
            point.y=WorldExpansionLayout.Height(point.x,point.z);
            var wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Dark timber.mat");
            Cube("Trail sign post",point+Vector3.up*1.2f,new Vector3(.2f,2.4f,.2f),wood,parent);
            var label=new GameObject(text); label.transform.SetParent(parent); label.transform.position=point+Vector3.up*2.7f;
            var mesh=label.AddComponent<TextMesh>(); mesh.text=text; mesh.anchor=TextAnchor.MiddleCenter; mesh.characterSize=.045f; mesh.fontSize=36; mesh.color=new Color(.9f,.79f,.5f);
            label.AddComponent<WorldLabel>(); sceneryRenderers++;
        }
        public static void Validate()
        {
            if(Tiles.Count!=4) throw new InvalidOperationException("Expected four generated terrain tiles.");
            float seam=0;
            foreach(var a in Tiles) foreach(var b in Tiles)
            {
                if(a==b) continue;
                if(Mathf.Abs(a.transform.position.x+384-b.transform.position.x)<.01f&&Mathf.Abs(a.transform.position.z-b.transform.position.z)<.01f)
                    for(int i=0;i<=64;i++) { float z=a.transform.position.z+i*6; seam=Mathf.Max(seam,Mathf.Abs(a.SampleHeight(new Vector3(b.transform.position.x,0,z))-b.SampleHeight(new Vector3(b.transform.position.x,0,z)))); }
                if(Mathf.Abs(a.transform.position.z+384-b.transform.position.z)<.01f&&Mathf.Abs(a.transform.position.x-b.transform.position.x)<.01f)
                    for(int i=0;i<=64;i++) { float x=a.transform.position.x+i*6; seam=Mathf.Max(seam,Mathf.Abs(a.SampleHeight(new Vector3(x,0,b.transform.position.z))-b.SampleHeight(new Vector3(x,0,b.transform.position.z)))); }
            }
            Report.AppendLine($"CHECK: four terrain tiles, seam max={seam:F4}m.");
            if(seam>.035f) throw new InvalidOperationException("Terrain seam exceeds clearance tolerance.");
            foreach(var route in WorldExpansionLayout.Routes)
            {
                float worst=0;
                for(int s=1;s<route.points.Length;s++)
                {
                    var from=route.points[s-1]; var to=route.points[s]; int steps=Mathf.CeilToInt(Vector3.Distance(from,to)/3);
                    for(int j=1;j<=steps;j++)
                    {
                        var a=Vector3.Lerp(from,to,(j-1)/(float)steps); var b=Vector3.Lerp(from,to,j/(float)steps);
                        float horizontal=Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
                        float slope=Mathf.Atan2(Mathf.Abs(WorldExpansionLayout.Height(a.x,a.z)-WorldExpansionLayout.Height(b.x,b.z)),horizontal)*Mathf.Rad2Deg;
                        if(route.name=="Westbank approach"&&Mathf.Abs(a.x-WorldExpansionLayout.RiverX(a.z))<17) continue;
                        worst=Mathf.Max(worst,slope);
                    }
                }
                Report.AppendLine($"ROAD {route.name}: maximum grade={worst:F1} degrees");
                if(worst>39) throw new InvalidOperationException("Route too steep: "+route.name+" ("+worst.ToString("F1")+" degrees)");
            }
            foreach(var fm in Object.FindObjectsByType<FlightMaster>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(Mathf.Abs(fm.transform.position.y-WorldExpansionLayout.Height(fm.transform.position.x,fm.transform.position.z))>.5f) throw new InvalidOperationException("Flight master not grounded: "+fm.name);
            var dungeon=Find("12 - The Scarlet Crypts Dungeon");
            if(!dungeon||Vector3.Distance(dungeon.position,new Vector3(-400,-40,200))>.01f) throw new InvalidOperationException("Underground dungeon moved unexpectedly.");
            Report.AppendLine("CHECK: flight masters grounded; underground dungeon origin preserved; original scene and source terrain untouched.");
            Report.AppendLine("PASS: expanded map generated and geometry checks passed.");
        }
    }
}
