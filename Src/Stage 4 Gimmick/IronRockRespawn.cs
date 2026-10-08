using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 奈落（キルゾーン）に落ちた鉄の岩を、置いてあった場所へ戻す。
///
/// 岩は運ぶのに必要なので、落ちて消えるとステージが詰む。
/// キルゾーンの当たり判定に頼らず、高さを毎回見て拾う。
/// トリガーのレイヤー設定に左右されないし、猛スピードで通り抜けても取りこぼさない。
///
/// シーンに何も置かなくていい。ゲーム開始時とシーン読み込み時に自分で湧く。
/// </summary>
public class IronRockRespawn : MonoBehaviour
{
    [Tooltip("何秒おきに高さを見るか")]
    [SerializeField] private float checkInterval = 0.2f;

    [Tooltip("キルゾーンが見つからなかったときに使う高さ")]
    [SerializeField] private float fallbackY = -30f;

    [Tooltip("同じ岩を戻したあと、次に戻せるようになるまでの秒数")]
    [SerializeField] private float cooldown = 0.5f;

    private class Entry
    {
        public Transform t;
        public Rigidbody rb;
        public IronArmorRock armor;
        public Transform parent;
        public Vector3 pos;
        public Quaternion rot;
        public float nextOkTime;
    }

    private readonly List<Entry> _rocks = new List<Entry>();
    private float _deadLine;
    private float _timer;

    // ------------------------------------------------------------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        Spawn();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Spawn();
    }

    private static void Spawn()
    {
        if (FindAnyObjectByType<IronRockRespawn>() != null) return;
        if (FindAnyObjectByType<IronArmorRock>() == null) return;   // 岩が無いステージでは何もしない

        var go = new GameObject("IronRockRespawn");
        go.AddComponent<IronRockRespawn>();
    }

    // ------------------------------------------------------------------

    private void Start()
    {
        _deadLine = FindKillLine();

        foreach (var r in FindObjectsByType<IronArmorRock>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r == null) continue;
            _rocks.Add(new Entry
            {
                t      = r.transform,
                rb     = r.GetComponent<Rigidbody>(),
                armor  = r,
                parent = r.transform.parent,
                pos    = r.transform.position,
                rot    = r.transform.rotation,
            });
        }

        Debug.Log($"[IronRock] 見張る岩 {_rocks.Count} 個 / この高さより下に落ちたら戻す: {_deadLine:0.#} m");
    }

    /// <summary>キルゾーンの一番上の高さ。ここを割ったら落ちたとみなす</summary>
    private float FindKillLine()
    {
        float top = float.MinValue;

        foreach (var kz in FindObjectsByType<KillZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (kz == null) continue;
            var col = kz.GetComponent<Collider>();
            if (col == null) continue;
            top = Mathf.Max(top, col.bounds.max.y);
        }

        if (top <= float.MinValue)
        {
            Debug.LogWarning("[IronRock] キルゾーンが見つかりません。高さ " + fallbackY + " m を使います");
            return fallbackY;
        }
        return top;
    }

    private void Update()
    {
        if (_rocks.Count == 0) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = checkInterval;

        for (int i = 0; i < _rocks.Count; i++)
        {
            var e = _rocks[i];
            if (e.t == null) continue;

            // プレイヤーに装着中の岩は、プレイヤーと一緒にリスポーンさせる
            if (e.armor != null && e.armor.IsAttached) continue;

            if (e.t.position.y > _deadLine) continue;
            if (Time.time < e.nextOkTime) continue;
            Respawn(e);
        }
    }

    private void Respawn(Entry e)
    {
        e.nextOkTime = Time.time + cooldown;

        // プレイヤーにくっついたまま落ちることがあるので、先に引きはがす
        if (e.armor != null) e.armor.ForceDetach();

        if (e.t.parent != e.parent) e.t.SetParent(e.parent, true);

        e.t.SetPositionAndRotation(e.pos, e.rot);

        var rb = e.rb != null ? e.rb : e.t.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }

        Physics.SyncTransforms();

        Debug.Log($"[IronRock] {e.t.name} が落ちたので {e.pos} に戻しました");
    }
}
