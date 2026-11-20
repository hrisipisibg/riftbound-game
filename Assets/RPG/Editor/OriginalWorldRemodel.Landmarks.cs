using System;
using System.Linq;
using UnityEngine;
using static Riftbound.Editor.OriginalSiteLayout;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    internal static partial class OriginalWorldRemodel
    {
        static void Ashwood()
        {
            site = "Ashwood quest route";
            var camp = Find("Ashwood supply camp");
            var stockade = ZoneWork(camp, "Original supply camp stockade");
            if (stockade)
            {
                var fences = camp.Cast<Transform>().Where(t => t.name.StartsWith("Prop_WoodenFence")).ToArray();
                foreach (var fence in fences)
                {
                    // The quest road enters from the south-west. Keep the original supply and guard lanes open.
                    if (fence.position.z < 58f && fence.position.x < 23f) continue;
                    Rebuild(fence, Role.Fence, Region.Ashwood);
                }
                foreach (var roof in camp.Cast<Transform>().Where(t => t.name.StartsWith("Roof_RoundTiles")).ToArray())
                {
                    Vector3 roofPosition = roof.position;
                    Vector3 centre = new Vector3(roofPosition.x,
                        WorldRegions.GroundHeight(roofPosition), roofPosition.z - .8f);
                    var bay = CreateRoot("Raiders' existing shelter bay", stockade, centre, Quaternion.Euler(0, 180, 0));
                    if (centre.x < 25f)
                        Place(bay, "Briar_Camp_Hut", Region.Ashwood, new Vector3(0, 0, -.6f), new Vector3(4.2f, 3.35f, 3.2f));
                    else
                    {
                        Place(bay, "Slate_Roof_Module", Region.Ashwood, new Vector3(0, 2.53f, 0), new Vector3(5.3f, 1.75f, 4.7f), collision: false);
                        Place(bay, "Stone_Wall", Region.Ashwood, new Vector3(0, 0, -2.06f), new Vector3(5.0f, 2.58f, .45f));
                        foreach (float side in new[] { -1f, 1f })
                            Beam(bay, "Loading shelter timber upright", new Vector3(side * 2.12f, 1.34f, 1.75f),
                                new Vector3(.27f, 2.68f, .29f), "Oak Edge", Region.Ashwood);
                    }
                    DisableOriginalSingle(roof, false);
                    originals++;
                }
                foreach (var old in camp.Cast<Transform>().Where(c => c.name == "Shelter back wall" ||
                    c.name == "Shelter post" || c.name == "Raider banner" || c.name == "Camp banner pole").ToArray())
                {
                    if (old.name == "Camp banner pole")
                    {
                        var pos = old.position;
                        var standard = CreateRoot("Camp standard", stockade, new Vector3(pos.x,
                            WorldRegions.GroundHeight(pos), pos.z), old.rotation);
                        Place(standard, "Briar_Banner", Region.Ashwood, Vector3.zero, new Vector3(1.45f, 4.2f, .58f));
                    }
                    DisableOriginalSingle(old, false);
                }
                Transform hearth = camp.Cast<Transform>().First(t => t.name == "Warm campfire glow");
                Vector3 cargoPosition = hearth.position + new Vector3(1.7f, 0, 1.4f);
                cargoPosition.y = WorldRegions.GroundHeight(cargoPosition) - .02f;
                var supplyCorner = CreateRoot("Camp cargo by the existing hearth", stockade, cargoPosition, Quaternion.identity);
                Place(supplyCorner, "Barrel", Region.Ashwood, Vector3.zero, new Vector3(.73f, 1.05f, .73f));
            }

            var watchtower = Find("Captain's ruined watchtower");
            var court = ZoneWork(watchtower, "Ruined captain's court");
            if (court)
            {
                ReplaceSurface(Find("Watchtower courtyard"), Region.Ashwood, "Limestone");
                var rear = CreateRoot("Collapsed chamber above the arena", court,
                    new Vector3(38.7f, WorldRegions.GroundHeight(new Vector3(38.7f, 0, 100)), 100), Quaternion.identity);
                Place(rear, "Collapsed_Shed", Region.Ashwood, Vector3.zero, new Vector3(3.8f, 2.3f, 3.6f), 28f);
                Place(rear, "Rubble", Region.Ashwood, new Vector3(-1.2f, 0, -2.1f), new Vector3(2.8f, .46f, 1.65f), -21f);
            }

            ReplaceSurface(Find("Camp approach"), Region.Ashwood, "Trail");
            ReplaceSurface(Find("Watchtower trail"), Region.Ashwood, "Trail");
            report.AppendLine("PASS Original Ashwood shelter bays, supply crates, and captain courtyard rebuilt around the quest anchors.");
        }

        static void Greenhollow()
        {
            site = "Greenhollow Hamlet and Willow Crossing";
            var root = Find("08 - Greenhollow Frontier");
            var district = ZoneWork(root, "Original Greenhollow riding post");
            if (district)
            {
                var roofs = root.Cast<Transform>().Where(t => t.name.StartsWith("Roof_RoundTiles")).ToArray();
                if (roofs.Length != 2) throw new InvalidOperationException("The two Greenhollow open lodges are missing.");
                foreach (var roof in roofs)
                {
                    var p = roof.position;
                    var floor = WorldRegions.GroundHeight(new Vector3(p.x, 0, p.z));
                    var lodge = CreateRoot("Original open lodge bay", district, new Vector3(p.x, floor, p.z), Quaternion.identity);
                    Place(lodge, "Slate_Roof_Module", Region.Greenhollow, new Vector3(0, 2.82f, 0), new Vector3(7.4f, 2.15f, 7f), collision: false);
                    Place(lodge, "Stone_Wall", Region.Greenhollow, new Vector3(0, 0, 3.3f), new Vector3(7.35f, 2.95f, .62f));
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Beam(lodge, "Chamfered lodge post", new Vector3(side * 3.15f, 1.53f, -2.48f),
                            new Vector3(.37f, 3.17f, .39f), "Oak Edge", Region.Greenhollow);
                        Beam(lodge, "Lodge lintel knee", new Vector3(side * 2.87f, 2.56f, -2.48f),
                            new Vector3(.25f, .95f, .27f), "Oak Edge", Region.Greenhollow, side * 33f);
                        Place(lodge, "Street_Lantern", Region.Greenhollow, new Vector3(side * 3.75f, 0, -2.5f),
                            new Vector3(.55f, 2.54f, .56f));
                    }
                    DisableOriginalSingle(roof, false);
                    originals++;
                }
                foreach (var old in root.Cast<Transform>().Where(t =>
                    t.name == "Lodge support" || t.name == "Open lodge rear wall").ToArray())
                    DisableOriginalSingle(old, false);

                foreach (var item in root.Cast<Transform>().Where(t => PropRole(t.name).HasValue).ToArray())
                    Rebuild(item, PropRole(item.name).Value, Region.Greenhollow);
            }

            var oldDeck = Find("Willow crossing deck");
            var bridge = ZoneWork(oldDeck.parent, "Original Willow crossing timberwork");
            if (bridge)
            {
                var floor = oldDeck.GetComponent<Renderer>().bounds;
                bridge.SetPositionAndRotation(new Vector3(floor.center.x, floor.max.y - .04f, floor.center.z), oldDeck.rotation);
                var wood = Place(bridge, "Ashwater_Bridge", Region.Greenhollow, Vector3.zero,
                    new Vector3(floor.size.x * 1.03f, 2.16f, floor.size.z * 1.015f));
                // This prefab's AABB includes its deep supports. Align the walkable planks to the old deck top.
                Physics.SyncTransforms();
                var set = wood.GetComponentsInChildren<MeshCollider>();
                float top = float.NegativeInfinity;
                foreach (var collider in set)
                    if (collider.Raycast(new Ray(wood.position + Vector3.up * 5, Vector3.down), out var hit, 8)) top = Mathf.Max(top, hit.point.y);
                if (!float.IsFinite(top)) throw new InvalidOperationException("The new Willow crossing has no deck under its center.");
                wood.position += Vector3.up * (floor.max.y + .27f - top);
                DisableOriginalSingle(oldDeck, true); // level collision fills the small expansion joints.
                foreach (var old in root.Cast<Transform>().Where(t => t.name == "Bridge post" || t.name == "Bridge handrail").ToArray())
                    DisableOriginalSingle(old, false);
                originals++;
            }

            foreach (var name in new[] { "Greenhollow village square", "Greenhollow approach", "Northern frontier trail", "Ancient grove trail" })
                ReplaceSurface(Find(name), Region.Greenhollow, name.Contains("square") ? "Limestone" : "Trail");
            report.AppendLine("PASS Greenhollow's original riding lodges, market surfaces, and Willow Brook crossing rebuilt.");
        }

        static void Westbank()
        {
            site = "Westbank and Willow Rest";
            var original = Find("Westbank River and Willow Rest");
            var wooden = original.Find(BridgeGroup);
            if (!wooden) throw new InvalidOperationException("Westbank's replacement bridge must sit inside the original location.");
            foreach (var renderer in wooden.GetComponentsInChildren<MeshRenderer>(false))
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => Finish(Region.Westbank, m)).ToArray();
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            ReplaceSurface(Find("Willow Rest cache footing"), Region.Westbank, "Pale Stone");
            var rest = ZoneWork(original, "Willow Rest cargo nook");
            if (rest)
            {
                var cache = Find("Willow Rest river cache");
                var pos = cache.position + new Vector3(-1.35f, 0, 1.5f);
                pos.y = WorldRegions.GroundHeight(pos);
                rest.position = pos;
                Place(rest, "Supply_Crate", Region.Westbank, Vector3.zero, new Vector3(1.08f, 1.06f, 1.05f), -12);
                Place(rest, "Barrel", Region.Westbank, new Vector3(-.83f, 0, -.38f), new Vector3(.65f, .9f, .65f), 13);
            }
            report.AppendLine("PASS Westbank bridge is authored under the existing crossing and Willow Rest cache anchors.");
        }

        static void InteriorAndArenas()
        {
            site = "Scarlet Crypts and Wyrmcrest";
            var cathedral = Find("12 - The Scarlet Crypts Dungeon");
            var crypt = ZoneWork(cathedral, "Original Scarlet Crypts architecture");
            if (crypt)
            {
                foreach (var part in cathedral.Cast<Transform>().Where(t =>
                    t.name.StartsWith("Cathedral") && (t.name.EndsWith("Wall") || t.name.Contains("Floor") || t.name.Contains("Ceiling"))).ToArray())
                    ReplaceSurface(part, Region.Duskwood, part.name.Contains("Floor") ? "Pale Stone" : "Basalt");

                var columns = cathedral.Cast<Transform>().Where(t => t.name.StartsWith("Cathedral Column")).ToArray();
                foreach (var pillar in columns)
                    Rebuild(pillar, Role.Wall, Region.Duskwood);

                // Elongated stone arcade sits above the original nave and leaves the center aisle clear.
                foreach (var pillar in columns.Where(c => c.name.StartsWith("Cathedral Column L")))
                {
                    var position = new Vector3(cathedral.position.x, cathedral.position.y + .06f, pillar.position.z);
                    var arch = CreateRoot("Nave vault rib", crypt, position, Quaternion.identity);
                    Place(arch, "Stone_Arch", Region.Duskwood, Vector3.zero, new Vector3(12.35f, 10.25f, 1.18f));
                }
                ReplaceSurface(Find("Altar Dais"), Region.Duskwood, "Limestone");
            }

            var western = Find("07 - Western Crypt");
            var courtyard = ZoneWork(western, "Original Western Crypt masonry");
            if (courtyard)
            {
                var old = Find("Ruined stonework");
                var stones = old.Cast<Transform>().Where(t => t.name.Contains("stone") || t.name.Contains("Stone") || t.name.Contains("Wall")).ToArray();
                foreach (var stone in stones)
                {
                    if (OriginalArt(stone).Length == 0) continue;
                    Rebuild(stone, Role.Rock, Region.Duskwood);
                }
                ReplaceSurface(Find("Ruined courtyard"), Region.Duskwood, "Basalt");
            }

            var summit = Find("13 - Wyrmcrest Summit Roost");
            var lair = ZoneWork(summit, "Original summit masonry");
            if (lair)
            {
                foreach (var column in summit.Cast<Transform>().Where(t => t.name.StartsWith("Summit Pillar")).ToArray())
                    Rebuild(column, Role.Wall, Region.Wyrmcrest);
                ReplaceSurface(Find("Summit Dais Platform"), Region.Wyrmcrest, "Basalt");
                Place(lair, "Rubble", Region.Wyrmcrest, new Vector3(-9.6f, .04f, -10.3f), new Vector3(4.2f, .62f, 2.9f), 24);
                Place(lair, "Rubble", Region.Wyrmcrest, new Vector3(9.25f, .04f, 11.5f), new Vector3(3.7f, .48f, 3.1f), -36);
            }
            report.AppendLine("PASS Existing Scarlet Crypts nave, Western Crypt courtyard, and Wyrmcrest boss arena rebuilt in place.");
        }

        static void SecondaryLandmarks()
        {
            site = "Original quest landmarks and approaches";
            var moonwell = Find("Moonwell Stone Basin");
            ReplaceSurface(moonwell, Region.Silverglen, "Pale Stone");
            var sun = Find("17 - High Elf Starter Silverglen Sanctum");
            foreach (var old in sun.Cast<Transform>().Where(t => t.name.StartsWith("Moonstone Obelisk")).ToArray())
                Rebuild(old, Role.Wall, Region.Silverglen);

            var graveyard = Find("18 - Undead Starter Deathknell Hollow");
            foreach (var old in graveyard.Cast<Transform>().Where(t => t.name.StartsWith("Weathered Gravestone")).ToArray())
                Rebuild(old, Role.Wall, Region.Deathknell);

            var scrap = Find("19 - Goblin Starter Rustwater Scrapworks");
            foreach (var old in scrap.Cast<Transform>().Where(t => t.name.StartsWith("TNT Barrel")).ToArray())
                Rebuild(old, Role.Barrel, Region.Rustwater);

            var crater = Find("20 - Redridge Volcanic Crater World Boss");
            ReplaceSurface(Find("Crater Bedrock"), Region.Wyrmcrest, "Basalt");
            var geology = ZoneWork(crater, "Original Redridge crater rim");
            if (geology)
            {
                foreach (float side in new[] { -1f, 1f })
                    Place(geology, "Rubble", Region.Wyrmcrest, new Vector3(side * 19, .05f, side * 5.1f),
                        new Vector3(5.7f, .85f, 3.1f), side * 31f, false);
            }

            var forest = Find("03 - Forest Road");
            var edges = ZoneWork(forest, "Established Ashwood road shoulders");
            if (edges)
            {
                var rocks = forest.Cast<Transform>().Where(t => t.name.StartsWith("Rock_") &&
                    t.position.z > 22 && t.position.z < 89 && Mathf.Abs(t.position.x) < 23 && t.gameObject.activeSelf).ToArray();
                foreach (var old in rocks) Rebuild(old, Role.Rock, Region.Ashwood);
            }
            report.AppendLine("PASS Existing Silverglen moonwell, Deathknell graves, Rustwater cargo, Redridge crater, and Ashwood roadside retouched.");
        }

        static void Beam(Transform parent, string name, Vector3 pos, Vector3 size, string material, Region region, float diagonal = 0)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = name;
            post.transform.SetParent(parent, false);
            post.transform.localPosition = pos;
            post.transform.localScale = size;
            post.transform.localRotation = Quaternion.Euler(0, 0, diagonal);
            post.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor(region, material);
        }

        static Transform ZoneWork(Transform location, string label) =>
            location.Find(label) ? null : CreateRoot(label, location, location.position, Quaternion.identity);

        static void DisableOriginalSingle(Transform old, bool keepCollision)
        {
            foreach (var renderer in OriginalArt(old))
            {
                renderer.enabled = false;
                retired.Add(renderer);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            if (keepCollision) return;
            foreach (var col in old.GetComponentsInChildren<Collider>())
            {
                if (col.isTrigger) continue;
                col.enabled = false;
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(col);
            }
        }

        static void ReplaceSurface(Transform objectRoot, Region region, string finish)
        {
            var renderer = objectRoot.GetComponent<MeshRenderer>();
            if (!renderer || !renderer.enabled) return;
            renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => MaterialFor(region, finish)).ToArray();
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
    }
}
