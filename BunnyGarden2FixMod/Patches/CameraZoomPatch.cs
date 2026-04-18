using System;
using System.Reflection;
using BunnyGarden2FixMod.Utils;
using GB;
using GB.Bar.MiniGame;
using HarmonyLib;
using UnityEngine;

namespace BunnyGarden2FixMod.Patches;

/// <summary>
/// DrinkPurchase と Cheki のカメラズームをフレームレート非依存に修正する。
///
/// async ステートマシンの MoveNext() は Harmony でパッチできないため、
/// ZoomCorrectionBehaviour（MonoBehaviour）の LateUpdate() でズーム差分を補正する。
///
/// DrinkPurchase : GBBehaviour（MonoBehaviour）
///   → FindAnyObjectByType で初回のみ検索してキャッシュ。
///     2回目以降はキャッシュ済みインスタンスを使うので軽量。
///
/// Cheki : MiniGameBase（通常クラス、MonoBehaviour でない）
///   → FindObjectsByType が使えないため、MiniGameBase.Setup/Release の
///     Postfix でインスタンスを追跡する。
/// </summary>
public static class CameraZoomPatch
{
    internal static Type      DpType;
    internal static FieldInfo DpZoom;
    internal static FieldInfo ChekiZoom;
    internal static object    ActiveCheki;

    public static void Initialize(GameObject pluginObject)
    {
        DpType = AccessTools.TypeByName("GB.Bar.DrinkPurchase");
        if (DpType != null)
        {
            DpZoom = AccessTools.Field(DpType, "m_zoom");
            PatchLogger.LogInfo("[CameraZoomPatch] DrinkPurchase ズーム補正を登録しました（初回 Find キャッシュ方式）");
        }
        else
        {
            PatchLogger.LogWarning("[CameraZoomPatch] DrinkPurchase: 型が見つかりませんでした");
        }

        pluginObject.AddComponent<ZoomCorrectionBehaviour>();
    }
}

// ───────────────────────────────────────────────
// MiniGameBase.Setup() の Postfix で Cheki インスタンスを登録
// ───────────────────────────────────────────────
[HarmonyPatch(typeof(MiniGameBase), "Setup")]
public static class MiniGameBaseSetupPatch
{
    private static bool Prepare()
    {
        var chekiType = AccessTools.TypeByName("GB.Bar.MiniGame.Cheki");
        if (chekiType == null)
        {
            PatchLogger.LogWarning("[CameraZoomPatch] Cheki: 型が見つかりませんでした");
            return false;
        }
        CameraZoomPatch.ChekiZoom = AccessTools.Field(chekiType, "m_zoom");
        if (CameraZoomPatch.ChekiZoom == null)
        {
            PatchLogger.LogWarning("[CameraZoomPatch] Cheki: m_zoom が見つかりませんでした");
            return false;
        }
        PatchLogger.LogInfo("[CameraZoomPatch] MiniGameBase.Setup をパッチしました（Cheki ズームのフレームレート非依存化）");
        return true;
    }

    private static void Postfix(MiniGameBase __instance)
    {
        if (__instance.GetType().Name == "Cheki")
            CameraZoomPatch.ActiveCheki = __instance;
    }
}

// ───────────────────────────────────────────────
// MiniGameBase.Release() の Postfix で Cheki インスタンスを解除
// ───────────────────────────────────────────────
[HarmonyPatch(typeof(MiniGameBase), "Release")]
public static class MiniGameBaseReleasePatch
{
    private static void Postfix(MiniGameBase __instance)
    {
        if (CameraZoomPatch.ActiveCheki == (object)__instance)
            CameraZoomPatch.ActiveCheki = null;
    }
}

// ───────────────────────────────────────────────
// LateUpdate でズーム差分を補正するコンポーネント
// ───────────────────────────────────────────────
public sealed class ZoomCorrectionBehaviour : MonoBehaviour
{
    private float              _dpPrev;
    private float              _chekiPrev;
    private UnityEngine.Object _dpCached;     // DrinkPurchase インスタンスのキャッシュ
    private bool               _dpSearchDone; // 今シーンで検索済みなら true（null でも再検索しない）

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;
    }

    // シーン切り替え時にキャッシュをリセットして再検索を許可する
    private void OnSceneChanged(
        UnityEngine.SceneManagement.Scene from,
        UnityEngine.SceneManagement.Scene to)
    {
        _dpCached     = null;
        _dpSearchDone = false;
        _dpPrev       = 0f;
    }

    private void LateUpdate()
    {
        // GBSystem 未初期化時（起動直後）は GBInput が NullReferenceException を出すため早期リターン
        if (GBSystem.Instance == null) return;

        // スクロールホイールはイベント駆動（1フレームのみ非ゼロ）のため補正不要。
        // ボタン長押し (LPressing / RPressing) のみ補正する。
        bool pressing = GBInput.LPressing || GBInput.RPressing;

        // DrinkPurchase: シーン内に存在しない場合は毎フレーム Find しないよう _dpSearchDone フラグで制御
        if (CameraZoomPatch.DpType != null && CameraZoomPatch.DpZoom != null)
        {
            if (_dpCached == null && !_dpSearchDone)
            {
                _dpCached     = FindAnyObjectByType(CameraZoomPatch.DpType, FindObjectsInactive.Include);
                _dpSearchDone = true; // 見つからなくても次のシーン変更まで再検索しない
            }
            CorrectZoom(_dpCached, CameraZoomPatch.DpZoom, ref _dpPrev, pressing);
        }

        // Cheki: MiniGameBase.Setup/Release で追跡済みのインスタンスを使う
        CorrectZoom(CameraZoomPatch.ActiveCheki, CameraZoomPatch.ChekiZoom, ref _chekiPrev, pressing);
    }

    private static void CorrectZoom(object inst, FieldInfo zoom, ref float prev, bool pressing)
    {
        if (inst == null || zoom == null) return;

        float cur = (float)zoom.GetValue(inst);

        if (pressing)
        {
            float delta = cur - prev;
            if (Mathf.Abs(delta) > float.Epsilon)
            {
                float corrected = Mathf.Clamp01(prev + delta * (Time.deltaTime * 60f));
                zoom.SetValue(inst, corrected);
                cur = corrected;
            }
        }

        prev = cur;
    }
}
