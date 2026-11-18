using UnityEngine;

namespace Riftbound
{
    public class CombatFeedback : MonoBehaviour
    {
        Combatant stats;
        Renderer[] cachedRenderers;
        MaterialPropertyBlock flashBlock;
        float flashUntil;

        void Awake()
        {
            stats = GetComponent<Combatant>(); stats.Damaged += Damage; stats.Healed += Heal;
        }
        void OnDestroy() { if (stats) { stats.Damaged -= Damage; stats.Healed -= Heal; } }
        void Damage(float value)
        {
            Show(Mathf.CeilToInt(value).ToString(), stats.isPlayer ? new Color(1, .3f, .2f) : new Color(1, .83f, .3f));
            GameSound.Play(SoundCue.Hit, transform.position);
            TriggerHitFlash();
        }
        void Heal(float value) { Show("+" + Mathf.CeilToInt(value), new Color(.35f, 1, .45f)); GameSound.Play(SoundCue.Heal, transform.position); }
        void TriggerHitFlash()
        {
            if (!stats || stats.isPlayer) return;
            if (cachedRenderers == null || cachedRenderers.Length == 0) cachedRenderers = GetComponentsInChildren<Renderer>();
            if (flashBlock == null) flashBlock = new MaterialPropertyBlock();
            flashUntil = Time.time + 0.12f;
        }
        void LateUpdate()
        {
            if (flashUntil > 0)
            {
                if (Time.time < flashUntil)
                {
                    if (flashBlock != null && cachedRenderers != null)
                    {
                        flashBlock.SetColor("_BaseColor", new Color(1f, 0.45f, 0.45f));
                        for (int i = 0; i < cachedRenderers.Length; i++)
                            if (cachedRenderers[i]) cachedRenderers[i].SetPropertyBlock(flashBlock);
                    }
                }
                else
                {
                    flashUntil = 0;
                    if (cachedRenderers != null)
                    {
                        for (int i = 0; i < cachedRenderers.Length; i++)
                            if (cachedRenderers[i]) cachedRenderers[i].SetPropertyBlock(null);
                    }
                }
            }
        }
        void Show(string value, Color color)
        {
            if (!Application.isPlaying || FrontendMenu.IsOpen || value == "0") return;
            var go = new GameObject("Combat number"); go.transform.position = transform.position + new Vector3(Random.Range(-.25f, .25f), 2.1f, 0);
            var text = go.AddComponent<TextMesh>(); text.text = value; text.characterSize = .075f; text.fontSize = 48; text.anchor = TextAnchor.MiddleCenter; text.color = color;
            go.AddComponent<FloatingCombatNumber>();
        }
    }
    public class FloatingCombatNumber : MonoBehaviour
    {
        float age;
        TextMesh text;
        void Awake() { text = GetComponent<TextMesh>(); }
        void Update()
        {
            age += Time.deltaTime; transform.position += Vector3.up * (Time.deltaTime * .65f);
            if (Camera.main) transform.rotation = Camera.main.transform.rotation;
            var color = text.color; color.a = Mathf.Clamp01((.9f - age) * 3); text.color = color;
            if (age >= .9f) Destroy(gameObject);
        }
    }
}
