using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Riftbound.Editor
{
    public static class WorldPresentationSetup
    {
        public static void BatchPreviewBuild()
        {
            try
            {
                CreateLayers();
                EditorSceneManager.OpenScene(WorldRegions.ScenePath);
                Directory.CreateDirectory(".code-check");
                RenderPreview("Expanded-Overview",new Vector3(64,650,224),new Vector3(64,0,224),true);
                RenderPreview("Expanded-Bloodrock",new Vector3(325,58,145),new Vector3(280,20,215),false);
                PrototypeBuild.BuildWindows();
                EditorApplication.Exit(File.ReadAllText("PrototypeBuildResult.txt").StartsWith("Succeeded")?0:1);
            }
            catch(Exception e){File.WriteAllText("ExpandedPreviewBuildResult.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }

        public static void RenderPreview(string name,Vector3 position,Vector3 target,bool overhead)
        {
            var go=new GameObject("Temporary world preview camera");
            var camera=go.AddComponent<Camera>();camera.transform.position=position;
            camera.transform.LookAt(target,overhead?Vector3.forward:Vector3.up);
            camera.nearClipPlane=.2f;camera.farClipPlane=1600;camera.fieldOfView=55;
            camera.orthographic=overhead;camera.orthographicSize=430;
            var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=!overhead;
            var texture=new RenderTexture(overhead?1024:1280,overhead?1024:720,24);
            texture.Create();camera.targetTexture=texture;
            bool fog=RenderSettings.fog;if(overhead)RenderSettings.fog=false;
            camera.Render();RenderTexture.active=texture;
            var image=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();
            File.WriteAllBytes(".code-check/"+name+".png",image.EncodeToPNG());
            RenderTexture.active=null;camera.targetTexture=null;RenderSettings.fog=fog;
            UnityEngine.Object.DestroyImmediate(image);texture.Release();UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(go);
        }

        public static void RenderShotsOnly()
        {
            try
            {
                EditorSceneManager.OpenScene(WorldRegions.ScenePath);
                Directory.CreateDirectory(".code-check");
                foreach (var text in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
                {
                    if (text.GetComponentInParent<TownNPC>() || text.GetComponentInParent<Merchant>() || text.GetComponentInParent<QuestGiver>() || text.name.Contains("Nameplate"))
                        continue;
                    var renderer = text.GetComponent<MeshRenderer>(); if (renderer) renderer.enabled = false;
                    var billboard = text.GetComponent<WorldLabel>(); if (billboard) billboard.enabled = false;
                    text.gameObject.SetActive(false);
                }
                RenderPreview("Verify-Emberwatch-Player", new Vector3(0f, 3.6f, -5.5f), new Vector3(0f, 1.3f, 8f), false);
                RenderPreview("Verify-Highspire-Keep", new Vector3(-200f, 19f, -38f), new Vector3(-200f, 14f, -75f), false);
                RenderPreview("Verify-Gorkresh-Stronghold", new Vector3(340f, 32f, 268f), new Vector3(336f, 27f, 310f), false);
                RenderPreview("Verify-Greenhollow-Frontier", new Vector3(-40f, 15f, 378f), new Vector3(-40f, 10f, 405f), false);
                RenderPreview("Verify-Silverglen-Sanctum", new Vector3(-150f, 20f, 66f), new Vector3(-150f, 16f, 92f), false);
                RenderPreview("Verify-Expanded-Overview", new Vector3(64f, 650f, 224f), new Vector3(64f, 0f, 224f), true);
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void BatchGenerate()
        {
            try
            {
                CreateLayers();
                StylizedRacialModelSystem.EnsureUnifiedAssetsReady(true);
                CharacterModelBuilder.BuildAll();
                WorldExpansionSetup.Generate();
                FinishScene();

                Directory.CreateDirectory(".code-check");
                RenderPreview("Verify-Emberwatch-Player", new Vector3(0f, 3.6f, -5.5f), new Vector3(0f, 1.3f, 8f), false);
                RenderPreview("Verify-Highspire-Keep", new Vector3(-200f, 19f, -38f), new Vector3(-200f, 14f, -75f), false);
                RenderPreview("Verify-Gorkresh-Stronghold", new Vector3(340f, 32f, 268f), new Vector3(336f, 27f, 310f), false);
                RenderPreview("Verify-Greenhollow-Frontier", new Vector3(-40f, 15f, 378f), new Vector3(-40f, 10f, 405f), false);
                RenderPreview("Verify-Silverglen-Sanctum", new Vector3(-150f, 20f, 66f), new Vector3(-150f, 16f, 92f), false);
                RenderPreview("Verify-Expanded-Overview", new Vector3(64f, 650f, 224f), new Vector3(64f, 0f, 224f), true);

                File.WriteAllText("WorldPresentationResult.txt","PASS: expanded terrain, regional textures, map routes, lighting and scene entry configured.");
                EditorApplication.Exit(0);
            }
            catch(Exception exception) { File.WriteAllText("WorldPresentationResult.txt",exception.ToString()); Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        public static void CreateLayers()
        {
            string folder="Assets/RPG/Terrain/WorldExpansionLayers/";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            string[] ids = { "forest_ground_04", "rocky_terrain_02", "snow_02", "red_mud_stones", "cobblestone_large_01" };
            string[] names = { "Forest floor", "Rocky highland", "Summit snow", "Redridge canyon", "Cobblestone highway" };
            for (int i = 0; i < ids.Length; i++)
            {
                string prefix = "Assets/RPG/Textures/WorldExpansion/" + ids[i];
                foreach (string suffix in new[] { "_diff.jpg", "_nor_gl.png" })
                {
                    var importer = AssetImporter.GetAtPath(prefix + suffix) as TextureImporter;
                    if (!importer) throw new InvalidOperationException("Missing new terrain texture " + prefix + suffix);
                    importer.textureType = suffix.Contains("nor_gl") ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    importer.textureShape = TextureImporterShape.Texture2D;
                    importer.sRGBTexture = !suffix.Contains("nor_gl"); importer.maxTextureSize = 2048;
                    importer.wrapMode = TextureWrapMode.Repeat; importer.anisoLevel = 4; importer.mipmapEnabled = true;
                    importer.SaveAndReimport();
                }
                string path = folder + names[i] + ".terrainlayer";
                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (!layer) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
                layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_diff.jpg");
                layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_nor_gl.png");
                if (!layer.diffuseTexture || !layer.normalMapTexture) throw new InvalidOperationException("Terrain textures must import as Texture2D: " + ids[i]);
                float tile = i == 1 ? 12 : i == 3 ? 8 : i == 4 ? 5 : 6;
                layer.tileSize = new Vector2(tile, tile); layer.normalScale = .45f; layer.smoothness = .06f;
                EditorUtility.SetDirty(layer);
            }
            AssetDatabase.SaveAssets();
        }

        public static void FinishScene()
        {
            var root=GameObject.Find("Expanded map route data") ?? new GameObject("Expanded map route data");
            var map=root.GetComponent<WorldMapRoutes>() ?? root.AddComponent<WorldMapRoutes>();
            map.routes=WorldExpansionLayout.Routes.Select(r=>new WorldMapRoutes.Route{name=r.name,points=r.points}).ToArray();
            var sun=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.type==LightType.Directional&&(l.cullingMask&1)!=0);
            if(sun)
            {
                sun.color=new Color(1,.95f,.85f);sun.intensity=1.65f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.8f;
                sun.transform.rotation=Quaternion.Euler(48,-30,0);
                RenderSettings.sun=sun;
            }
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.5f,.60f,.70f);
            RenderSettings.ambientEquatorColor=new Color(.36f,.4f,.43f);
            RenderSettings.ambientGroundColor=new Color(.24f,.25f,.22f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogColor=new Color(.70f,.80f,.90f);RenderSettings.fogDensity=.0018f;
            foreach(var volume in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if(!volume.isGlobal||!volume.sharedProfile) continue;
                string path="Assets/RPG/Terrain/WorldExpansion/Expanded atmosphere.asset";
                var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if(!profile)
                {
                    profile=ScriptableObject.CreateInstance<VolumeProfile>();
                    AssetDatabase.CreateAsset(profile,path);
                    foreach(var component in volume.sharedProfile.components)
                    {
                        var copy=UnityEngine.Object.Instantiate(component);profile.components.Add(copy);AssetDatabase.AddObjectToAsset(copy,profile);
                    }
                }
                if(profile.TryGet<Bloom>(out var bloom)) bloom.intensity.Override(.08f);
                if(profile.TryGet<ColorAdjustments>(out var color)) {color.postExposure.Override(.18f);color.contrast.Override(8);color.saturation.Override(8);}
                volume.sharedProfile=profile;EditorUtility.SetDirty(profile);
                break;
            }
            foreach (var text in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            {
                if (text.GetComponentInParent<TownNPC>() || text.GetComponentInParent<Merchant>() || text.GetComponentInParent<QuestGiver>() || text.name.Contains("Nameplate"))
                    continue;
                var renderer = text.GetComponent<MeshRenderer>(); if (renderer) renderer.enabled = false;
                var billboard = text.GetComponent<WorldLabel>(); if (billboard) billboard.enabled = false;
                text.gameObject.SetActive(false);
            }
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(WorldRegions.ScenePath,true),new EditorBuildSettingsScene("Assets/RPG/Scenes/Outpost-Updated.unity",true)};
            PlayModePresentation.HideGizmos();
            AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
    }
}
