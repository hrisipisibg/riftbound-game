using UnityEngine;

namespace Riftbound
{
    public static class ExplorationSites
    {
        public static readonly string[] SurveyIds = { "westbank-overlook", "bloodrock-vista", "wyrmcrest-lookout" };
        public const string CartographerName = "Cartographer Elara";
        public static void Ensure()
        {
            if (!WorldRegions.Expanded) return;
            var existing = GameObject.Find("Frontier exploration sites");
            if (existing) { EnsureCartographer(existing.transform); return; }
            Landmark template = null;
            foreach (var landmark in Object.FindObjectsByType<Landmark>(FindObjectsSortMode.None))
                if (landmark.landmarkId == "ancient-grove") { template = landmark; break; }
            if (!template) return;

            var root = new GameObject("Frontier exploration sites").transform;
            var chest = Resources.Load<GameObject>("Props/TreasureChest");
            Add(template, chest, root, "westbank-overlook", "Westbank Overlook", new Vector3(-241, 6, 44));
            Add(template, chest, root, "bloodrock-vista", "Bloodrock Vista", new Vector3(268, 18, 193));
            Add(template, chest, root, "wyrmcrest-lookout", "Wyrmcrest Lookout", new Vector3(253, 75, 485));
            EnsureCartographer(root);
        }

        static void EnsureCartographer(Transform parent)
        {
            if (parent.Find(CartographerName)) return;
            var position = WorldRegions.Greenhollow + new Vector3(-6, 0, -3);
            position.y = WorldRegions.GroundHeight(position);
            var root = new GameObject(CartographerName);
            root.transform.SetParent(parent); root.transform.position = position;
            CharacterVisualFactory.CreateVisualInstance("Human", "Mage", root.transform);
            var collider = root.AddComponent<CapsuleCollider>();
            collider.height = 1.9f; collider.radius = .4f; collider.center = Vector3.up * .95f;
            var npc = root.AddComponent<TownNPC>();
            npc.npcName = CartographerName; npc.roleTag = "Cartographer";
            npc.greeting = "The frontier needs a proper map. Chart the Westbank river, Bloodrock Vista and Wyrmcrest Lookout, then bring your survey back to me.";
        }

        static void Add(Landmark template, GameObject chestPrefab, Transform parent, string id, string name, Vector3 point)
        {
            point.y = WorldRegions.GroundHeight(point);
            var marker = Object.Instantiate(template, point, template.transform.rotation, parent);
            marker.name = name; marker.landmarkId = id; marker.displayName = name;
            foreach (var text in marker.GetComponentsInChildren<TextMesh>())
            {
                var r = text.GetComponent<MeshRenderer>(); if (r) r.enabled = false;
                var b = text.GetComponent<WorldLabel>(); if (b) b.enabled = false;
                text.gameObject.SetActive(false);
            }
            AddCampfire(parent, point + Vector3.forward * 2.2f);
            if (!chestPrefab) return;

            var chestPoint = point + Vector3.right * 2.5f;
            chestPoint.y = WorldRegions.GroundHeight(chestPoint);
            var instance = Object.Instantiate(chestPrefab, chestPoint, Quaternion.identity, parent);
            instance.name = name + " supplies";
            var chest = instance.GetComponent<LootChest>() ?? instance.AddComponent<LootChest>();
            chest.chestId = id + "-cache"; chest.gold = 20; chest.experience = 40;
            chest.requiresCaptainDefeated = false; chest.requiresWardenDefeated = false;
        }

        static void AddCampfire(Transform parent, Vector3 pos)
        {
            var fire = new GameObject("Frontier Campfire");
            fire.transform.SetParent(parent);
            pos.y = WorldRegions.GroundHeight(pos) + 0.25f;
            fire.transform.position = pos;
            var light = fire.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1.0f, 0.62f, 0.18f);
            light.range = 8f;
            light.intensity = 2.4f;
            fire.AddComponent<CozyCampfireFlicker>();
        }
    }

    public class CozyCampfireFlicker : MonoBehaviour
    {
        Light fireLight;
        float baseIntensity;
        void Start()
        {
            fireLight = GetComponent<Light>();
            if (fireLight) baseIntensity = fireLight.intensity;
        }
        void Update()
        {
            if (!fireLight) return;
            fireLight.intensity = baseIntensity + Mathf.Sin(Time.time * 7f) * 0.35f + Mathf.Cos(Time.time * 13f) * 0.2f;
        }
    }
}
