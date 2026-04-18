using BunnyGarden2FixMod.Utils;
using GB;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BunnyGarden2FixMod.Patches;

/// <summary>
/// Karuta.freeCamera() のズーム操作をフレームレート非依存に修正するパッチ。
///
/// 元の実装では LPressing / RPressing 中に毎フレーム m_zoom ± 0.05f が加算されるため、
/// 高 FPS ほどズームが速くなる。スクロールホイール (ScrollAxis) は1フレームのみ
/// 非ゼロになるイベント駆動なので補正不要。ボタン長押し時のみ
/// Time.deltaTime * 60f でスケーリングすることで 60FPS 基準に正規化する。
/// </summary>
[HarmonyPatch]
public static class KarutaZoomPatch
{
    private static FieldInfo s_zoomField;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        var type = AccessTools.TypeByName("GB.Bar.MiniGame.Karuta");
        if (type == null)
        {
            PatchLogger.LogWarning("[KarutaZoomPatch] GB.Bar.MiniGame.Karuta が見つかりませんでした");
            yield break;
        }

        s_zoomField = AccessTools.Field(type, "m_zoom");

        var method = AccessTools.Method(type, "freeCamera");
        if (method == null)
        {
            PatchLogger.LogWarning("[KarutaZoomPatch] Karuta.freeCamera が見つかりませんでした");
            yield break;
        }

        PatchLogger.LogInfo("[KarutaZoomPatch] Karuta.freeCamera をパッチしました（ズームのフレームレート非依存化）");
        yield return method;
    }

    private static void Prefix(object __instance, out float __state)
    {
        __state = s_zoomField != null ? (float)s_zoomField.GetValue(__instance) : 0f;
    }

    private static void Postfix(object __instance, float __state)
    {
        // スクロールホイールはイベント駆動（1フレームのみ非ゼロ）なので補正不要。
        // ボタン長押し (LPressing / RPressing) のみフレームレート補正を適用する。
        if (!GBInput.LPressing && !GBInput.RPressing) return;
        if (s_zoomField == null) return;

        float current = (float)s_zoomField.GetValue(__instance);
        float delta = current - __state;
        if (Mathf.Abs(delta) < float.Epsilon) return;

        s_zoomField.SetValue(__instance, Mathf.Clamp01(__state + delta * (Time.deltaTime * 60f)));
    }
}
