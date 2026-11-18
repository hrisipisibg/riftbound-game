using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    public static class MedievalPropsSetup
    {
        const string PropsArtDir = "Assets/RPG/Art/MedievalProps/";
        const string TexturesDir = "Assets/RPG/Art/MedievalProps/Textures/";
        const string MatDir = "Assets/RPG/Materials/MedievalProps/";
        const string PrefabDir = "Assets/RPG/Prefabs/Props/";

        [MenuItem("Riftbound/Set up Medieval Props and World Building")]
        public static void Setup()
        {
            try
            {
                Directory.CreateDirectory(MatDir);
                Directory.CreateDirectory(PrefabDir);
                AssetDatabase.Refresh();

                // 1. Configure texture import settings
                ConfigureTextures();

                // 2. Configure model import settings
                ConfigureModels();

                // 3. Create URP Lit materials
                var matWell = CreateLitMaterial("Mat_Medieval_Well", TexturesDir + "Well_Finish_Diffuse.png", TexturesDir + "Well_Finish_Normal.png", 0.15f, 0f);
                var matWellHandle = CreateLitMaterial("Mat_Medieval_WellHandle", TexturesDir + "Well_Finish_Handle_Diffuse.png", TexturesDir + "Well_Finish_Handle_Normal.png", 0.2f, 0f);
                var matBarrel = CreateLitMaterial("Mat_Medieval_Barrel", TexturesDir + "T_Barrel_BaseColor.tif", TexturesDir + "T_Barrel_Bump.tif", 0.25f, 0.05f);
                var matCrate = CreateLitMaterial("Mat_Medieval_Crate", TexturesDir + "T_Crate_Basecolor.tif", TexturesDir + "T_Crate_Bump.tif", 0.18f, 0.05f);
                var matRack = CreateLitMaterial("Mat_Medieval_WeaponRack", TexturesDir + "T_Rack_Basecolor.tif", null, 0.25f, 0.05f);
                var matSword = CreateLitMaterial("Mat_Medieval_Sword", TexturesDir + "T_Sword_BaseColor.tif", TexturesDir + "T_Sword_Bump.tif", 0.6f, 0.85f);
                var matSpear = CreateLitMaterial("Mat_Medieval_Spear", TexturesDir + "T_Spear_BaseColor.tif", null, 0.45f, 0.6f);
                var matCrossbow = CreateLitMaterial("Mat_Medieval_Crossbow", TexturesDir + "T_Crossbow_Basecolor.tif", null, 0.35f, 0.3f);

                // Anvil & Bench materials
                var matAnvil = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "Mat_Medieval_Anvil.mat");
                if (!matAnvil)
                {
                    matAnvil = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    matAnvil.SetColor("_BaseColor", new Color(0.18f, 0.18f, 0.20f));
                    matAnvil.SetFloat("_Metallic", 0.85f);
                    matAnvil.SetFloat("_Smoothness", 0.45f);
                    AssetDatabase.CreateAsset(matAnvil, MatDir + "Mat_Medieval_Anvil.mat");
                }
                var matBench = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "Mat_Medieval_Bench.mat");
                if (!matBench)
                {
                    matBench = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    matBench.SetColor("_BaseColor", new Color(0.32f, 0.20f, 0.12f));
                    matBench.SetFloat("_Metallic", 0.0f);
                    matBench.SetFloat("_Smoothness", 0.25f);
                    AssetDatabase.CreateAsset(matBench, MatDir + "Mat_Medieval_Bench.mat");
                }

                // Assign materials to Medieval_Props FBX
                AssignPropsMaterials(matBarrel, matCrate, matCrossbow, matRack, matSpear, matSword);
                AssignWellMaterials(matWell, matWellHandle);

                // 4. Create standalone prefabs with appropriate colliders
                CreatePrefabs(matWell, matWellHandle, matBarrel, matCrate, matRack, matSword, matSpear, matCrossbow, matAnvil, matBench);

                // 5. Populate the active scene (Outpost-Updated.unity)
                PopulateScene();

                AssetDatabase.SaveAssets();
                File.WriteAllText("MedievalPropsResult.txt", "PASS: Medieval props configured, materials created, prefabs built, and scene populated.");
                Debug.Log("Medieval Props Setup Completed Successfully!");
            }
            catch (Exception ex)
            {
                File.WriteAllText("MedievalPropsResult.txt", "FAILED: " + ex);
                Debug.LogException(ex);
            }
        }

        static void ConfigureTextures()
        {
            string[] normalMaps = { "Well_Finish_Normal.png", "Well_Finish_Handle_Normal.png", "T_Barrel_Bump.tif", "T_Crate_Bump.tif", "T_Sword_Bump.tif" };
            foreach (var file in Directory.GetFiles(TexturesDir))
            {
                string normPath = file.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(normPath) as TextureImporter;
                if (!importer) continue;

                string filename = Path.GetFileName(normPath);
                bool isNormal = normalMaps.Any(n => n.Equals(filename, StringComparison.OrdinalIgnoreCase));

                importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !isNormal && !filename.Contains("Specular") && !filename.Contains("Roughness");
                importer.maxTextureSize = 2048;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }
        }

        static void ConfigureModels()
        {
            string[] models = { PropsArtDir + "Medieval_Props.FBX", PropsArtDir + "Well-Handle_Finish.fbx", PropsArtDir + "Anvil.obj", PropsArtDir + "Bench.obj", PropsArtDir + "Bucket.obj" };
            foreach (var path in models)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!importer) continue;
                importer.isReadable = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.SaveAndReimport();
            }
        }

        static Material CreateLitMaterial(string name, string diffusePath, string normalPath, float smoothness, float metallic)
        {
            string path = MatDir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            if (!string.IsNullOrEmpty(diffusePath))
            {
                var diffuseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
                if (diffuseTex) mat.SetTexture("_BaseMap", diffuseTex);
            }
            if (!string.IsNullOrEmpty(normalPath))
            {
                var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (normalTex)
                {
                    mat.SetTexture("_BumpMap", normalTex);
                    mat.EnableKeyword("_NORMALMAP");
                }
            }
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void AssignPropsMaterials(Material barrel, Material crate, Material crossbow, Material rack, Material spear, Material sword)
        {
            var propsGo = AssetDatabase.LoadAssetAtPath<GameObject>(PropsArtDir + "Medieval_Props.FBX");
            if (!propsGo) return;
            foreach (var r in propsGo.GetComponentsInChildren<MeshRenderer>())
            {
                string n = r.name.ToLowerInvariant();
                if (n.Contains("barrel")) r.sharedMaterial = barrel;
                else if (n.Contains("crate")) r.sharedMaterial = crate;
                else if (n.Contains("crossbow")) r.sharedMaterial = crossbow;
                else if (n.Contains("rack")) r.sharedMaterial = rack;
                else if (n.Contains("spear")) r.sharedMaterial = spear;
                else if (n.Contains("sword")) r.sharedMaterial = sword;
            }
        }

        static void AssignWellMaterials(Material well, Material handle)
        {
            var wellGo = AssetDatabase.LoadAssetAtPath<GameObject>(PropsArtDir + "Well-Handle_Finish.fbx");
            if (!wellGo) return;
            foreach (var r in wellGo.GetComponentsInChildren<MeshRenderer>())
            {
                string n = r.name.ToLowerInvariant();
                if (n.Contains("handle")) r.sharedMaterial = handle;
                else r.sharedMaterial = well;
            }
        }

        static void CreatePrefabs(Material well, Material handle, Material barrel, Material crate, Material rack, Material sword, Material spear, Material crossbow, Material anvil, Material bench)
        {
            // 1. Town Well Prefab
            var wellSource = AssetDatabase.LoadAssetAtPath<GameObject>(PropsArtDir + "Well-Handle_Finish.fbx");
            if (wellSource)
            {
                var wellRoot = new GameObject("Prefab_TownWell");
                var inst = UnityEngine.Object.Instantiate(wellSource, wellRoot.transform);
                inst.name = "Model";
                foreach (var r in inst.GetComponentsInChildren<MeshRenderer>())
                {
                    r.sharedMaterial = r.name.ToLowerInvariant().Contains("handle") ? handle : well;
                }
                // Normalize scale so height is ~2.8m
                var b = CalculateBounds(inst);
                if (b.size.y > 0.1f) inst.transform.localScale = Vector3.one * (2.8f / b.size.y);
                b = CalculateBounds(inst);
                inst.transform.localPosition = new Vector3(0, -b.min.y, 0);

                // Add box collider for stone well
                var box = wellRoot.AddComponent<BoxCollider>();
                box.center = new Vector3(0, 0.9f, 0);
                box.size = new Vector3(2.2f, 1.8f, 2.2f);

                // Add water surface inside the well
                var water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                water.name = "Well Water Surface";
                water.transform.SetParent(wellRoot.transform, false);
                water.transform.localPosition = new Vector3(0, 0.45f, 0);
                water.transform.localScale = new Vector3(1.3f, 0.05f, 1.3f);
                var matWater = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "Mat_Medieval_WellWater.mat");
                if (!matWater)
                {
                    matWater = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    matWater.SetColor("_BaseColor", new Color(0.12f, 0.35f, 0.42f, 0.85f));
                    matWater.SetFloat("_Smoothness", 0.95f);
                    matWater.SetFloat("_Metallic", 0.1f);
                    AssetDatabase.CreateAsset(matWater, MatDir + "Mat_Medieval_WellWater.mat");
                }
                water.GetComponent<Renderer>().sharedMaterial = matWater;
                UnityEngine.Object.DestroyImmediate(water.GetComponent<Collider>());

                PrefabUtility.SaveAsPrefabAsset(wellRoot, PrefabDir + "Prefab_TownWell.prefab");
                UnityEngine.Object.DestroyImmediate(wellRoot);
            }

            // 2. Weapon Rack Populated
            var propsSource = AssetDatabase.LoadAssetAtPath<GameObject>(PropsArtDir + "Medieval_Props.FBX");
            if (propsSource)
            {
                var rackRoot = new GameObject("Prefab_WeaponRack_Loaded");
                var rackInst = UnityEngine.Object.Instantiate(propsSource, rackRoot.transform);
                rackInst.name = "PropsModel";

                // In Medieval_Props.FBX, make only Weapon_rack, spears and swords active, or position them nicely
                foreach (var r in rackInst.GetComponentsInChildren<MeshRenderer>(true))
                {
                    string n = r.name.ToLowerInvariant();
                    if (n.Contains("barrel") || n.Contains("crate"))
                    {
                        r.gameObject.SetActive(false);
                    }
                    else if (n.Contains("rack")) r.sharedMaterial = rack;
                    else if (n.Contains("spear")) r.sharedMaterial = spear;
                    else if (n.Contains("sword")) r.sharedMaterial = sword;
                    else if (n.Contains("crossbow")) r.sharedMaterial = crossbow;
                }

                var b = CalculateBounds(rackRoot);
                if (b.size.y > 0.1f) rackInst.transform.localScale = Vector3.one * (1.8f / b.size.y);
                b = CalculateBounds(rackRoot);
                rackInst.transform.localPosition = new Vector3(0, -b.min.y, 0);

                var box = rackRoot.AddComponent<BoxCollider>();
                box.center = new Vector3(0, 0.9f, 0);
                box.size = new Vector3(1.6f, 1.8f, 0.8f);

                PrefabUtility.SaveAsPrefabAsset(rackRoot, PrefabDir + "Prefab_WeaponRack_Loaded.prefab");
                UnityEngine.Object.DestroyImmediate(rackRoot);
            }

            // 3. Medieval Barrel
            if (propsSource)
            {
                var barrelRoot = new GameObject("Prefab_Medieval_Barrel");
                var barrelInst = UnityEngine.Object.Instantiate(propsSource, barrelRoot.transform);
                barrelInst.name = "BarrelModel";
                foreach (var r in barrelInst.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.name.ToLowerInvariant() == "barrel")
                    {
                        r.gameObject.SetActive(true);
                        r.sharedMaterial = barrel;
                    }
                    else
                    {
                        r.gameObject.SetActive(false);
                    }
                }
                var b = CalculateBounds(barrelRoot);
                if (b.size.y > 0.1f) barrelInst.transform.localScale = Vector3.one * (1.15f / b.size.y);
                b = CalculateBounds(barrelRoot);
                barrelInst.transform.localPosition = new Vector3(0, -b.min.y, 0);

                var col = barrelRoot.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0, 0.58f, 0);
                col.height = 1.15f;
                col.radius = 0.42f;

                PrefabUtility.SaveAsPrefabAsset(barrelRoot, PrefabDir + "Prefab_Medieval_Barrel.prefab");
                UnityEngine.Object.DestroyImmediate(barrelRoot);
            }

            // 4. Medieval Crate
            if (propsSource)
            {
                var crateRoot = new GameObject("Prefab_Medieval_Crate");
                var crateInst = UnityEngine.Object.Instantiate(propsSource, crateRoot.transform);
                crateInst.name = "CrateModel";
                foreach (var r in crateInst.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.name.ToLowerInvariant() == "crate")
                    {
                        r.gameObject.SetActive(true);
                        r.sharedMaterial = crate;
                    }
                    else
                    {
                        r.gameObject.SetActive(false);
                    }
                }
                var b = CalculateBounds(crateRoot);
                if (b.size.y > 0.1f) crateInst.transform.localScale = Vector3.one * (0.85f / b.size.y);
                b = CalculateBounds(crateRoot);
                crateInst.transform.localPosition = new Vector3(0, -b.min.y, 0);

                var box = crateRoot.AddComponent<BoxCollider>();
                box.center = new Vector3(0, 0.42f, 0);
                box.size = new Vector3(0.85f, 0.85f, 0.85f);

                PrefabUtility.SaveAsPrefabAsset(crateRoot, PrefabDir + "Prefab_Medieval_Crate.prefab");
                UnityEngine.Object.DestroyImmediate(crateRoot);
            }

            // 5. Anvil
            var anvilSource = AssetDatabase.LoadAssetAtPath<GameObject>(PropsArtDir + "Anvil.obj");
            if (anvilSource)
            {
                var anvilRoot = new GameObject("Prefab_Medieval_Anvil");
                var anvilInst = UnityEngine.Object.Instantiate(anvilSource, anvilRoot.transform);
                anvilInst.name = "AnvilModel";
                foreach (var r in anvilInst.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = anvil;
                var b = CalculateBounds(anvilRoot);
                if (b.size.y > 0.1f) anvilInst.transform.localScale = Vector3.one * (0.75f / b.size.y);
                b = CalculateBounds(anvilRoot);
                anvilInst.transform.localPosition = new Vector3(0, -b.min.y, 0);

                var box = anvilRoot.AddComponent<BoxCollider>();
                box.center = new Vector3(0, 0.38f, 0);
                box.size = new Vector3(0.5f, 0.75f, 0.9f);

                PrefabUtility.SaveAsPrefabAsset(anvilRoot, PrefabDir + "Prefab_Medieval_Anvil.prefab");
                UnityEngine.Object.DestroyImmediate(anvilRoot);
            }

            // 6. Bench
            var benchSource = AssetDatabase.LoadAssetAtPath<GameObject>(PropsArtDir + "Bench.obj");
            if (benchSource)
            {
                var benchRoot = new GameObject("Prefab_Medieval_Bench");
                var benchInst = UnityEngine.Object.Instantiate(benchSource, benchRoot.transform);
                benchInst.name = "BenchModel";
                foreach (var r in benchInst.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = bench;
                var b = CalculateBounds(benchRoot);
                if (b.size.y > 0.1f) benchInst.transform.localScale = Vector3.one * (0.9f / b.size.y);
                b = CalculateBounds(benchRoot);
                benchInst.transform.localPosition = new Vector3(0, -b.min.y, 0);

                var box = benchRoot.AddComponent<BoxCollider>();
                box.center = new Vector3(0, 0.45f, 0);
                box.size = new Vector3(1.8f, 0.9f, 0.6f);

                PrefabUtility.SaveAsPrefabAsset(benchRoot, PrefabDir + "Prefab_Medieval_Bench.prefab");
                UnityEngine.Object.DestroyImmediate(benchRoot);
            }
        }

        static void PopulateScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/RPG/Scenes/Outpost-Updated.unity");
            var existingGroup = GameObject.Find("10 - Medieval Town Props");
            if (existingGroup) UnityEngine.Object.DestroyImmediate(existingGroup);

            var root = new GameObject("10 - Medieval Town Props");

            var wellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_TownWell.prefab");
            var rackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WeaponRack_Loaded.prefab");
            var barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_Medieval_Barrel.prefab");
            var cratePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_Medieval_Crate.prefab");
            var anvilPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_Medieval_Anvil.prefab");
            var benchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_Medieval_Bench.prefab");

            // 1. Town Well in the central courtyard near the fountain/square
            if (wellPrefab)
            {
                var well = (GameObject)PrefabUtility.InstantiatePrefab(wellPrefab, root.transform);
                well.transform.position = new Vector3(2.5f, -0.11f, 5.5f);
                well.transform.rotation = Quaternion.Euler(0, 45, 0);
            }

            // 2. Blacksmith / Armory Workshop area outside right house (x=8, z=10)
            if (anvilPrefab)
            {
                var anvil = (GameObject)PrefabUtility.InstantiatePrefab(anvilPrefab, root.transform);
                anvil.transform.position = new Vector3(8.2f, -0.11f, 8.5f);
                anvil.transform.rotation = Quaternion.Euler(0, 30, 0);
            }
            if (rackPrefab)
            {
                var rack = (GameObject)PrefabUtility.InstantiatePrefab(rackPrefab, root.transform);
                rack.transform.position = new Vector3(9.5f, -0.11f, 10.5f);
                rack.transform.rotation = Quaternion.Euler(0, -75, 0);
            }
            if (cratePrefab)
            {
                var c1 = (GameObject)PrefabUtility.InstantiatePrefab(cratePrefab, root.transform);
                c1.transform.position = new Vector3(7.2f, -0.11f, 9.8f);
                c1.transform.rotation = Quaternion.Euler(0, 15, 0);

                var c2 = (GameObject)PrefabUtility.InstantiatePrefab(cratePrefab, root.transform);
                c2.transform.position = new Vector3(7.4f, -0.11f, 10.6f);
                c2.transform.rotation = Quaternion.Euler(0, -10, 0);

                var c3 = (GameObject)PrefabUtility.InstantiatePrefab(cratePrefab, root.transform);
                c3.transform.position = new Vector3(7.3f, 0.74f, 10.2f); // stacked on top
                c3.transform.rotation = Quaternion.Euler(0, 5, 0);
            }

            // 3. Gatehouse Guard Post (z=17.5f)
            if (rackPrefab)
            {
                var gateRack1 = (GameObject)PrefabUtility.InstantiatePrefab(rackPrefab, root.transform);
                gateRack1.transform.position = new Vector3(-3.2f, -0.11f, 17.2f);
                gateRack1.transform.rotation = Quaternion.Euler(0, 0, 0);

                var gateRack2 = (GameObject)PrefabUtility.InstantiatePrefab(rackPrefab, root.transform);
                gateRack2.transform.position = new Vector3(3.2f, -0.11f, 17.2f);
                gateRack2.transform.rotation = Quaternion.Euler(0, 180, 0);
            }
            if (barrelPrefab)
            {
                var b1 = (GameObject)PrefabUtility.InstantiatePrefab(barrelPrefab, root.transform);
                b1.transform.position = new Vector3(-4.6f, -0.11f, 17.0f);
                b1.transform.rotation = Quaternion.Euler(0, 20, 0);

                var b2 = (GameObject)PrefabUtility.InstantiatePrefab(barrelPrefab, root.transform);
                b2.transform.position = new Vector3(4.6f, -0.11f, 17.0f);
                b2.transform.rotation = Quaternion.Euler(0, -20, 0);
            }

            // 4. Tavern / House Exterior Props (x=-12.2 against wall, z=3.5 to 8.5)
            if (benchPrefab)
            {
                var bench1 = (GameObject)PrefabUtility.InstantiatePrefab(benchPrefab, root.transform);
                bench1.transform.position = new Vector3(-12.2f, -0.11f, 6.5f);
                bench1.transform.rotation = Quaternion.Euler(0, 90, 0);

                var bench2 = (GameObject)PrefabUtility.InstantiatePrefab(benchPrefab, root.transform);
                bench2.transform.position = new Vector3(-12.2f, -0.11f, 3.5f);
                bench2.transform.rotation = Quaternion.Euler(0, 90, 0);
            }
            if (barrelPrefab)
            {
                var tb1 = (GameObject)PrefabUtility.InstantiatePrefab(barrelPrefab, root.transform);
                tb1.transform.position = new Vector3(-11.8f, -0.11f, 7.8f);
                var tb2 = (GameObject)PrefabUtility.InstantiatePrefab(barrelPrefab, root.transform);
                tb2.transform.position = new Vector3(-11.2f, -0.11f, 8.4f);
            }

            // 5. Greenhollow Post Props (around x=-25, z=160)
            if (wellPrefab)
            {
                var well2 = (GameObject)PrefabUtility.InstantiatePrefab(wellPrefab, root.transform);
                well2.transform.position = new Vector3(-20.5f, -0.11f, 158.0f);
                well2.transform.rotation = Quaternion.Euler(0, -60, 0);
            }
            if (rackPrefab)
            {
                var ghRack = (GameObject)PrefabUtility.InstantiatePrefab(rackPrefab, root.transform);
                ghRack.transform.position = new Vector3(-17.5f, -0.11f, 163.5f);
                ghRack.transform.rotation = Quaternion.Euler(0, 180, 0);
            }
            if (barrelPrefab)
            {
                var ghb1 = (GameObject)PrefabUtility.InstantiatePrefab(barrelPrefab, root.transform);
                ghb1.transform.position = new Vector3(-28.0f, -0.11f, 164.5f);
                var ghb2 = (GameObject)PrefabUtility.InstantiatePrefab(barrelPrefab, root.transform);
                ghb2.transform.position = new Vector3(-27.2f, -0.11f, 164.2f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static Bounds CalculateBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
