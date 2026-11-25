using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

// SlowMo v0.2
// LeftShift  = замедление мира, герой остаётся в нормальной скорости (как раньше)
// RightShift = полное замедление (мир + герой)
// Shift = переключатель. Иллюзии героя во время замедления.
namespace SlowMo
{
    [BepInPlugin("yarik.slowmo", "SlowMo", "0.2.0")]
    [DefaultExecutionOrder(32000)]
    public class Plugin : BaseUnityPlugin
    {
        ConfigEntry<KeyCode> keyHeroNormal;
        ConfigEntry<KeyCode> keyFullSlow;
        ConfigEntry<float> slowScale;
        ConfigEntry<float> transitionSpeed;
        ConfigEntry<float> ghostInterval;
        ConfigEntry<float> ghostLife;

        bool slowOn;
        bool heroNormal = true;
        float cur = 1f;
        float baseFixed;
        float ghostTimer;
        bool inputBroken;

        static float gravK = 1f;

        void Awake()
        {
            keyHeroNormal = Config.Bind("General", "HeroNormalKey", KeyCode.LeftShift, "Клавиша: замедление мира, герой в норме");
            keyFullSlow = Config.Bind("General", "FullSlowKey", KeyCode.RightShift, "Клавиша: полное замедление (мир + герой)");
            slowScale = Config.Bind("General", "SlowScale", 0.3f,
                new ConfigDescription("Скорость мира при замедлении", new AcceptableValueRange<float>(0.05f, 0.95f)));
            transitionSpeed = Config.Bind("General", "TransitionSpeed", 6f, "Скорость плавного перехода (1/сек)");
            ghostInterval = Config.Bind("Ghosts", "Interval", 0.06f, "Интервал между иллюзиями (реальные секунды)");
            ghostLife = Config.Bind("Ghosts", "Lifetime", 0.4f, "Время жизни иллюзии (реальные секунды)");

            baseFixed = Time.fixedDeltaTime;
            HeroTime.Clock = Time.timeAsDouble;
            HeroTime.HeroNormalMode = true;
            SceneManager.sceneLoaded += (_, __) => HeroTime.LevelStart = HeroTime.Clock;

            var h = new Harmony("yarik.slowmo");
            PatchHeroTime(h);
            PatchMovement(h);
            PatchAnimator(h);
            PatchTimeApi(h);
            PatchPlayMaker(h);
            PatchHeroAnimation(h);
        }

        // ---------------- ввод, иллюзии ----------------

        void Update()
        {
            if (!inputBroken)
            {
                try
                {
                    if (Input.GetKeyDown(keyHeroNormal.Value))
                    {
                        if (slowOn && heroNormal) slowOn = false;
                        else { slowOn = true; heroNormal = true; }
                    }
                    if (Input.GetKeyDown(keyFullSlow.Value))
                    {
                        if (slowOn && !heroNormal) slowOn = false;
                        else { slowOn = true; heroNormal = false; }
                    }
                }
                catch (Exception e)
                {
                    inputBroken = true;
                    Logger.LogError("Legacy Input не работает, нужен Unity.InputSystem: " + e.Message);
                }
            }

            HeroTime.HeroNormalMode = heroNormal;

            if (cur < 0.9f)
            {
                ghostTimer += Time.unscaledDeltaTime;
                if (ghostTimer >= ghostInterval.Value)
                {
                    ghostTimer = 0f;
                    SpawnGhost();
                }
            }
            else ghostTimer = ghostInterval.Value;
        }

        void SpawnGhost()
        {
            var hero = HeroController.SilentInstance;
            if (hero == null || hero.AnimCtrl == null || hero.AnimCtrl.animator == null) return;
            var spr = hero.AnimCtrl.animator.Sprite;
            if (spr == null || !spr.gameObject.activeInHierarchy) return;
            Ghost.Spawn(spr, ghostLife.Value, new Color(0.55f, 0.9f, 1f, 0.85f), new Color(0.55f, 0.9f, 1f, 0f));
            Ghost.Spawn(spr, ghostLife.Value * 0.7f, new Color(0.75f, 0.95f, 1f, 0.45f), new Color(0.75f, 0.95f, 1f, 0f));
        }

        // ---------------- управление временем ----------------

        void LateUpdate()
        {
            // K, действовавший в этом кадре: двигаем "часы героя"
            HeroTime.Clock += Time.deltaTime / HeroTime.K;

            var gm = GameManager.instance;
            bool paused = gm != null && gm.isPaused;
            if (paused || Time.timeScale <= 0f)
            {
                // пауза/фриз: не вмешиваемся, герой видит K = 1
                HeroTime.K = 1f;
                Time.fixedDeltaTime = baseFixed;
                return;
            }

            float target = slowOn ? slowScale.Value : 1f;
            cur = Mathf.MoveTowards(cur, target, Time.unscaledDeltaTime * transitionSpeed.Value);
            HeroTime.K = (slowOn && heroNormal) ? cur : 1f;
            Time.timeScale = cur;
            Time.fixedDeltaTime = baseFixed * cur;
        }

        // ---------------- патч 1: время героя (транспилер) ----------------

        void PatchHeroTime(Harmony h)
        {
            var types = new List<Type>();
            foreach (var t in new[] { typeof(HeroController), typeof(HeroAnimationController) })
            {
                types.Add(t);
                types.AddRange(t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic));
            }

            var tr = new HarmonyMethod(typeof(Plugin), nameof(TimeTranspiler));
            int ok = 0, fail = 0;
            foreach (var t in types)
            {
                foreach (var m in AccessTools.GetDeclaredMethods(t))
                {
                    if (m.IsAbstract || m.IsGenericMethodDefinition) continue;
                    if (!UsesTime(m)) continue;
                    try { h.Patch(m, transpiler: tr); ok++; }
                    catch (Exception e) { fail++; Logger.LogWarning($"transpile fail {t.Name}.{m.Name}: {e.Message}"); }
                }
            }
            Logger.LogInfo($"Hero time transpiler: ok={ok}, fail={fail}");
        }

        static bool UsesTime(MethodBase m)
        {
            try
            {
                foreach (var kv in PatchProcessor.ReadMethodBody(m))
                    if (kv.Value is MethodBase mb && IsTimeApi(mb)) return true;
            }
            catch { }
            return false;
        }

        static bool IsTimeApi(MethodBase mb)
        {
            if (mb.DeclaringType == typeof(Time))
                return mb.Name == "get_deltaTime" || mb.Name == "get_fixedDeltaTime" || mb.Name == "get_timeAsDouble"
                    || mb.Name == "get_time" || mb.Name == "get_timeSinceLevelLoad";
            return mb.DeclaringType == typeof(WaitForSeconds) && mb.IsConstructor;
        }

        static IEnumerable<CodeInstruction> TimeTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var ins in instructions)
            {
                if (ins.operand is MethodBase mb && IsTimeApi(mb))
                {
                    string? target = null;
                    if (mb.DeclaringType == typeof(Time))
                    {
                        switch (mb.Name)
                        {
                            case "get_deltaTime": target = nameof(HeroTime.Dt); break;
                            case "get_fixedDeltaTime": target = nameof(HeroTime.FixedDt); break;
                            case "get_timeAsDouble": target = nameof(HeroTime.NowD); break;
                            case "get_time": target = nameof(HeroTime.NowF); break;
                            case "get_timeSinceLevelLoad": target = nameof(HeroTime.SinceLoad); break;
                        }
                    }
                    else if (ins.opcode == OpCodes.Newobj) target = nameof(HeroTime.Wait);

                    if (target != null)
                    {
                        ins.opcode = OpCodes.Call;
                        ins.operand = AccessTools.Method(typeof(HeroTime), target);
                    }
                }
                yield return ins;
            }
        }

        // ---------------- патч 2: движение и гравитация героя ----------------

        void PatchMovement(Harmony h)
        {
            foreach (var name in new[] { "Update", "FixedUpdate", "LateUpdate" })
            {
                var m = AccessTools.Method(typeof(HeroController), name);
                if (m == null) { Logger.LogWarning("HeroController." + name + " не найден"); continue; }
                h.Patch(m,
                    prefix: new HarmonyMethod(typeof(Plugin), nameof(PreHero)),
                    postfix: new HarmonyMethod(typeof(Plugin), name == "FixedUpdate" ? nameof(PostFixed) : nameof(PostHero)));
            }
        }

        static void PreHero(HeroController __instance)
        {
            HeroTime.Enter();
            var rb = __instance.Body;
            if (rb == null) return;
            if (gravK != 1f)
            {
                rb.gravityScale *= gravK;
                gravK = 1f;
            }
        }

        static void PostHero(HeroController __instance)
        {
            var rb = __instance.Body;
            if (rb != null)
            {
                float k = HeroTime.K;
                if (k < 0.999f)
                {
                    rb.gravityScale /= k;
                    gravK = k;
                }
            }
            HeroTime.Exit();
        }

        static readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        static ContactFilter2D filter;

        static void PostFixed(HeroController __instance)
        {
            PostHero(__instance);
            Compensate(__instance);
        }

        static void Compensate(HeroController hero)
        {
            float k = HeroTime.K;
            if (k >= 0.999f) return;
            var rb = hero.Body;
            if (rb == null || !rb.simulated) return;

            Vector2 move = rb.linearVelocity * Time.fixedDeltaTime;
            Vector2 total = move / k;
            float tl = total.magnitude;
            if (tl < 1e-5f) return;
            Vector2 dir = total / tl;

            filter.useTriggers = false;
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(rb.gameObject.layer));
            int n = rb.Cast(dir, filter, hits, tl);
            float allowed = tl;
            for (int i = 0; i < n; i++)
                allowed = Mathf.Min(allowed, Mathf.Max(0f, hits[i].distance - 0.01f));

            float extra = Mathf.Max(0f, allowed - move.magnitude);
            if (extra > 0f) rb.position += dir * extra;
        }

        // ---------------- патч 3: аниматоры героя ----------------

        void PatchAnimator(Harmony h)
        {
            var m = AccessTools.Method(typeof(tk2dSpriteAnimator), nameof(tk2dSpriteAnimator.UpdateAnimation));
            if (m == null) { Logger.LogWarning("tk2dSpriteAnimator.UpdateAnimation не найден"); return; }
            h.Patch(m, prefix: new HarmonyMethod(typeof(Plugin), nameof(PreAnim)));
        }

        static void PreAnim(tk2dSpriteAnimator __instance, ref float deltaTime)
        {
            float k = HeroTime.K;
            if (k >= 0.999f || deltaTime <= 0f) return;
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            if (__instance.transform.IsChildOf(hero.transform)) deltaTime /= k;
        }

        // ---------------- патч 4: глобальный Time API (контекст героя) ----------------

        void PatchTimeApi(Harmony h)
        {
            var t = typeof(UnityEngine.Time);
            h.Patch(AccessTools.PropertyGetter(t, "deltaTime"), prefix: new HarmonyMethod(typeof(Plugin), nameof(TimeDeltaPrefix)));
            h.Patch(AccessTools.PropertyGetter(t, "fixedDeltaTime"), prefix: new HarmonyMethod(typeof(Plugin), nameof(TimeFixedDeltaPrefix)));
            h.Patch(AccessTools.PropertyGetter(t, "time"), prefix: new HarmonyMethod(typeof(Plugin), nameof(TimeTimePrefix)));
            h.Patch(AccessTools.PropertyGetter(t, "timeAsDouble"), prefix: new HarmonyMethod(typeof(Plugin), nameof(TimeTimeAsDoublePrefix)));
            h.Patch(AccessTools.PropertyGetter(t, "timeSinceLevelLoad"), prefix: new HarmonyMethod(typeof(Plugin), nameof(TimeSinceLevelLoadPrefix)));
        }

        static bool TimeDeltaPrefix(ref float __result)
        {
            if (HeroTime.InHeroContext && HeroTime.HeroNormalMode)
            {
                __result = Time.unscaledDeltaTime;
                return false;
            }
            return true;
        }

        static bool TimeFixedDeltaPrefix(ref float __result)
        {
            if (HeroTime.InHeroContext && HeroTime.HeroNormalMode)
            {
                __result = Time.fixedUnscaledDeltaTime;
                return false;
            }
            return true;
        }

        static bool TimeTimePrefix(ref float __result)
        {
            if (HeroTime.InHeroContext && HeroTime.HeroNormalMode)
            {
                __result = (float)HeroTime.Clock;
                return false;
            }
            return true;
        }

        static bool TimeTimeAsDoublePrefix(ref double __result)
        {
            if (HeroTime.InHeroContext && HeroTime.HeroNormalMode)
            {
                __result = HeroTime.Clock;
                return false;
            }
            return true;
        }

        static bool TimeSinceLevelLoadPrefix(ref float __result)
        {
            if (HeroTime.InHeroContext && HeroTime.HeroNormalMode)
            {
                __result = (float)(HeroTime.Clock - HeroTime.LevelStart);
                return false;
            }
            return true;
        }

        // ---------------- патч 5: PlayMaker FSM героя ----------------

        void PatchPlayMaker(Harmony h)
        {
            var pm = typeof(PlayMakerFSM);
            foreach (var name in new[] { "Update", "FixedUpdate", "LateUpdate" })
            {
                var m = AccessTools.Method(pm, name);
                if (m == null) { Logger.LogWarning("PlayMakerFSM." + name + " не найден"); continue; }
                h.Patch(m,
                    prefix: new HarmonyMethod(typeof(Plugin), nameof(PrePlayMaker)),
                    postfix: new HarmonyMethod(typeof(Plugin), nameof(PostPlayMaker)));
            }
        }

        static void PrePlayMaker(PlayMakerFSM __instance)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null || __instance.Fsm == null) return;
            if (__instance.Fsm.GameObject == hero.gameObject)
                HeroTime.Enter();
        }

        static void PostPlayMaker()
        {
            HeroTime.Exit();
        }

        // ---------------- патч 6: HeroAnimationController ----------------

        void PatchHeroAnimation(Harmony h)
        {
            foreach (var name in new[] { "Update", "LateUpdate" })
            {
                var m = AccessTools.Method(typeof(HeroAnimationController), name);
                if (m == null) continue;
                h.Patch(m,
                    prefix: new HarmonyMethod(typeof(Plugin), nameof(PreHeroAnim)),
                    postfix: new HarmonyMethod(typeof(Plugin), nameof(PostHeroAnim)));
            }
        }

        static void PreHeroAnim(HeroAnimationController __instance)
        {
            var hero = HeroController.SilentInstance;
            if (hero != null && __instance.heroCtrl == hero)
                HeroTime.Enter();
        }

        static void PostHeroAnim()
        {
            HeroTime.Exit();
        }
    }

    internal static class HeroTime
    {
        public static float K = 1f;
        public static double Clock;
        public static double LevelStart;
        public static bool HeroNormalMode = true;

        static int depth;
        public static bool InHeroContext => depth > 0;
        public static void Enter() { depth++; }
        public static void Exit() { if (depth > 0) depth--; }

        public static float Dt() => K >= 0.9999f ? Time.deltaTime : Time.deltaTime / K;
        public static float FixedDt() => K >= 0.9999f ? Time.fixedDeltaTime : Time.fixedDeltaTime / K;
        public static double NowD() => Clock;
        public static float NowF() => (float)Clock;
        public static float SinceLoad() => (float)(Clock - LevelStart);
        public static WaitForSeconds Wait(float seconds) => new WaitForSeconds(seconds * K);
    }

    internal class Ghost : MonoBehaviour
    {
        tk2dSprite spr;
        float t, life;
        Color c0, c1;

        public static void Spawn(tk2dBaseSprite src, float life, Color c0, Color c1)
        {
            var go = new GameObject("SlowMoGhost");
            go.transform.position = src.transform.position + new Vector3(0f, 0f, 0.01f);
            go.transform.localScale = src.transform.lossyScale * 1.03f;

            var s = go.AddComponent<tk2dSprite>();
            s.SetSprite(src.Collection, src.spriteId);
            s.scale = src.scale;
            s.FlipX = src.FlipX;

            var sr = src.GetComponent<MeshRenderer>();
            var r = go.GetComponent<MeshRenderer>();
            if (sr != null && r != null)
            {
                r.sortingLayerID = sr.sortingLayerID;
                r.sortingOrder = sr.sortingOrder - 1;
            }

            var g = go.AddComponent<Ghost>();
            g.spr = s; g.life = life; g.c0 = c0; g.c1 = c1;
            s.color = c0;
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            if (t >= life) { Destroy(gameObject); return; }
            spr.color = Color.Lerp(c0, c1, t / life);
        }
    }
}
