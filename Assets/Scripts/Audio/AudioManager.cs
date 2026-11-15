using System;
using System.Collections;
using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
#endif

public enum AudioBus { Master, Music, SFX, Cutscene }

[Serializable]
public class AudioEntry
{
    [Tooltip("Unique ID used to play this event. Shows up in AudioIDs for autocomplete.")]
    public string id;
    public EventReference eventRef;
}

/// <summary>
/// FMOD audio manager singleton. Persists across scenes.
/// Bus layout: MASTER -> Music, SFX, Cutscene.
/// Play things by ID, ideally via the generated constants: AudioManager.Instance.PlaySFX(AudioIDs.SFX.Chop);
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("FMOD bus paths")]
    [SerializeField] string masterBusPath = "bus:/";
    [SerializeField] string musicBusPath = "bus:/Music";
    [SerializeField] string sfxBusPath = "bus:/SFX";
    [SerializeField] string cutsceneBusPath = "bus:/Cutscene";

    [Header("Volumes (0-1, editable at runtime)")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float cutsceneVolume = 1f;

    [Header("Events (add per category)")]
    public List<AudioEntry> music = new List<AudioEntry>();
    public List<AudioEntry> sfx = new List<AudioEntry>();
    public List<AudioEntry> cutscenes = new List<AudioEntry>();

    readonly Dictionary<string, EventReference> musicLookup = new Dictionary<string, EventReference>();
    readonly Dictionary<string, EventReference> sfxLookup = new Dictionary<string, EventReference>();
    readonly Dictionary<string, EventReference> cutsceneLookup = new Dictionary<string, EventReference>();

    readonly Dictionary<AudioBus, Bus> buses = new Dictionary<AudioBus, Bus>();
    readonly Dictionary<AudioBus, Coroutine> tweens = new Dictionary<AudioBus, Coroutine>();

    // Last playback position (ms) per music ID, so each song (e.g. kitchen / hunting) can resume independently.
    readonly Dictionary<string, int> musicPositions = new Dictionary<string, int>();

    EventInstance currentMusic;
    string currentMusicId;
    EventInstance currentCutscene;
    Coroutine cutsceneRoutine;

    /// <summary>The currently playing music instance (valid check: .isValid()). Use to change parameters later.</summary>
    public EventInstance CurrentMusic => currentMusic;
    public string CurrentMusicId => currentMusicId;
    public bool IsCutscenePlaying { get; private set; }

    #region Lifecycle
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildLookups();
        buses[AudioBus.Master] = RuntimeManager.GetBus(masterBusPath);
        buses[AudioBus.Music] = RuntimeManager.GetBus(musicBusPath);
        buses[AudioBus.SFX] = RuntimeManager.GetBus(sfxBusPath);
        buses[AudioBus.Cutscene] = RuntimeManager.GetBus(cutsceneBusPath);
        ApplyInspectorVolumes();
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        if (currentMusic.isValid())
        {
            currentMusic.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            currentMusic.release();
        }
        Instance = null;
    }

    void OnValidate()
    {
        // Lets you drag the sliders in the inspector while playing.
        if (Application.isPlaying && Instance == this && buses.Count > 0) ApplyInspectorVolumes();
#if UNITY_EDITOR
        EditorApplication.delayCall -= GenerateIds;
        EditorApplication.delayCall += GenerateIds;
#endif
    }

    void BuildLookups()
    {
        Fill(music, musicLookup, "music");
        Fill(sfx, sfxLookup, "sfx");
        Fill(cutscenes, cutsceneLookup, "cutscene");
    }

    static void Fill(List<AudioEntry> src, Dictionary<string, EventReference> dst, string label)
    {
        dst.Clear();
        foreach (var e in src)
        {
            if (string.IsNullOrWhiteSpace(e.id)) continue;
            if (dst.ContainsKey(e.id)) Debug.LogWarning($"AudioManager: duplicate {label} id '{e.id}'");
            dst[e.id] = e.eventRef;
        }
    }

    void ApplyInspectorVolumes()
    {
        SetVolume(AudioBus.Master, masterVolume);
        SetVolume(AudioBus.Music, musicVolume);
        SetVolume(AudioBus.SFX, sfxVolume);
        SetVolume(AudioBus.Cutscene, cutsceneVolume);
    }
    #endregion

    #region SFX
    /// <summary>One-shot sound effect. Fire and forget.</summary>
    public void PlaySFX(string id, Vector3 position = default)
    {
        if (!sfxLookup.TryGetValue(id, out var ev) || ev.IsNull)
        {
            Debug.LogWarning($"AudioManager: unknown sfx id '{id}'");
            return;
        }
        RuntimeManager.PlayOneShot(ev, position);
    }
    #endregion

    #region Music
    /// <summary>
    /// Plays a music track (only one at a time). If resumeFromLast is true, the track starts from
    /// where it last stopped; otherwise it starts from the beginning. The current track's position is
    /// always remembered when it is replaced, so kitchen/hunting timers stay separate.
    /// Returns the instance so parameters can be changed later (also available as CurrentMusic).
    /// </summary>
    public EventInstance PlayMusic(string id, bool resumeFromLast = false)
    {
        if (!musicLookup.TryGetValue(id, out var ev) || ev.IsNull)
        {
            Debug.LogWarning($"AudioManager: unknown music id '{id}'");
            return default;
        }

        // Already playing this track: nothing to do (unless restarting from the top is wanted).
        if (currentMusic.isValid() && currentMusicId == id && resumeFromLast)
            return currentMusic;

        StopMusic();

        currentMusic = RuntimeManager.CreateInstance(ev);
        currentMusicId = id;

        if (resumeFromLast && musicPositions.TryGetValue(id, out int pos) && pos > 0)
            currentMusic.setTimelinePosition(pos);
        else
            musicPositions[id] = 0;

        currentMusic.start();
        return currentMusic;
    }

    /// <summary>Stops the current music, remembering its position for later resume.</summary>
    public void StopMusic(bool fadeOut = true)
    {
        if (!currentMusic.isValid()) return;

        if (currentMusic.getTimelinePosition(out int pos) == FMOD.RESULT.OK && currentMusicId != null)
            musicPositions[currentMusicId] = pos;

        currentMusic.stop(fadeOut ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE);
        currentMusic.release();
        currentMusic.clearHandle();
        currentMusicId = null;
    }

    /// <summary>Pauses/unpauses just the current music track.</summary>
    public void SetMusicPaused(bool paused)
    {
        if (currentMusic.isValid()) currentMusic.setPaused(paused);
    }

    /// <summary>Forget the saved position for a track (next resume starts from the top).</summary>
    public void ResetMusicPosition(string id) => musicPositions.Remove(id);

    /// <summary>Sets a parameter by name on the current music track.</summary>
    public void SetMusicParameter(string parameterName, float value)
    {
        if (!currentMusic.isValid())
        {
            Debug.LogWarning($"AudioManager: no music playing, can't set '{parameterName}'");
            return;
        }
        var result = currentMusic.setParameterByName(parameterName, value);
        if (result != FMOD.RESULT.OK)
            Debug.LogWarning($"AudioManager: setParameterByName('{parameterName}') failed: {result}");
    }
    #endregion

    #region Cutscene
    /// <summary>
    /// Plays a cutscene event and pauses music + SFX buses while it plays. They resume when it ends.
    /// </summary>
    public EventInstance PlayCutscene(string id)
    {
        if (!cutsceneLookup.TryGetValue(id, out var ev) || ev.IsNull)
        {
            Debug.LogWarning($"AudioManager: unknown cutscene id '{id}'");
            return default;
        }

        StopCutscene();

        buses[AudioBus.Music].setPaused(true);
        buses[AudioBus.SFX].setPaused(true);
        IsCutscenePlaying = true;

        currentCutscene = RuntimeManager.CreateInstance(ev);
        currentCutscene.start();
        cutsceneRoutine = StartCoroutine(WatchCutscene(currentCutscene));
        return currentCutscene;
    }

    /// <summary>Stops the current cutscene audio early and resumes music/SFX.</summary>
    public void StopCutscene()
    {
        if (cutsceneRoutine != null) StopCoroutine(cutsceneRoutine);
        cutsceneRoutine = null;

        if (currentCutscene.isValid())
        {
            currentCutscene.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            currentCutscene.release();
            currentCutscene.clearHandle();
        }
        EndCutscene();
    }

    IEnumerator WatchCutscene(EventInstance inst)
    {
        while (inst.isValid())
        {
            inst.getPlaybackState(out PLAYBACK_STATE state);
            if (state == PLAYBACK_STATE.STOPPED) break;
            yield return null;
        }
        if (inst.isValid()) inst.release();
        currentCutscene.clearHandle();
        cutsceneRoutine = null;
        EndCutscene();
    }

    void EndCutscene()
    {
        if (!IsCutscenePlaying) return;
        IsCutscenePlaying = false;
        buses[AudioBus.Music].setPaused(false);
        buses[AudioBus.SFX].setPaused(false);
    }
    #endregion

    #region Volume
    /// <summary>Sets a bus volume. Expects a 0-1 value (UI slider), clamped.</summary>
    public void SetVolume(AudioBus bus, float volume01)
    {
        volume01 = Mathf.Clamp01(volume01);
        switch (bus)
        {
            case AudioBus.Master: masterVolume = volume01; break;
            case AudioBus.Music: musicVolume = volume01; break;
            case AudioBus.SFX: sfxVolume = volume01; break;
            case AudioBus.Cutscene: cutsceneVolume = volume01; break;
        }
        SetBusVolume(bus, volume01);
    }

    /// <summary>Raw helper: sets the FMOD bus volume directly (no inspector value update).
    /// Use e.g. SetBusVolume(AudioBus.SFX, 0) at the start of a cutscene to mute normal SFX.</summary>
    public void SetBusVolume(AudioBus bus, float volume)
    {
        if (buses.TryGetValue(bus, out var b)) b.setVolume(Mathf.Max(0f, volume));
    }

    public float GetBusVolume(AudioBus bus)
    {
        return buses.TryGetValue(bus, out var b) && b.getVolume(out float v) == FMOD.RESULT.OK ? v : 0f;
    }

    /// <summary>
    /// Fades a bus from start to end over time seconds (unscaled).
    /// curve: 0 = linear, negative = front-loads the change, positive = shifts the change later in time.
    /// </summary>
    public void TweenBusVolume(AudioBus bus, float start, float end, float time, float curve = 0f)
    {
        if (tweens.TryGetValue(bus, out var running) && running != null) StopCoroutine(running);
        tweens[bus] = StartCoroutine(TweenRoutine(bus, start, end, time, curve));
    }

    IEnumerator TweenRoutine(AudioBus bus, float start, float end, float time, float curve)
    {
        float exponent = Mathf.Pow(2f, curve); // curve 0 -> 1 (linear), <0 -> <1 (fast start), >0 -> >1 (slow start)
        float t = 0f;
        SetBusVolume(bus, start);
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Pow(Mathf.Clamp01(t / time), exponent);
            SetBusVolume(bus, Mathf.LerpUnclamped(start, end, k));
            yield return null;
        }
        SetBusVolume(bus, end);
        tweens[bus] = null;
    }
    #endregion

#if UNITY_EDITOR
    // ---- Editor-only: generates AudioIDs.cs so event IDs autocomplete, e.g. AudioIDs.SFX.Chop ----
    [ContextMenu("Regenerate AudioIDs")]
    void GenerateIds()
    {
        if (this == null) return;
        var sb = new StringBuilder();
        sb.AppendLine("// AUTO-GENERATED by AudioManager (edit the lists on the AudioManager component, not this file).");
        sb.AppendLine("public static class AudioIDs");
        sb.AppendLine("{");
        AppendGroup(sb, "Music", music);
        AppendGroup(sb, "SFX", sfx);
        AppendGroup(sb, "Cutscene", cutscenes);
        sb.AppendLine("}");

        string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(this)));
        string path = Path.Combine(dir, "AudioIDs.cs");
        string text = sb.ToString();
        if (File.Exists(path) && File.ReadAllText(path) == text) return;
        File.WriteAllText(path, text);
        AssetDatabase.ImportAsset(path);
    }

    static void AppendGroup(StringBuilder sb, string name, List<AudioEntry> list)
    {
        sb.AppendLine($"    public static class {name}");
        sb.AppendLine("    {");
        var seen = new HashSet<string>();
        foreach (var e in list)
        {
            if (string.IsNullOrWhiteSpace(e.id)) continue;
            string ident = Regex.Replace(e.id.Trim(), @"[^A-Za-z0-9_]", "_");
            if (char.IsDigit(ident[0])) ident = "_" + ident;
            if (!seen.Add(ident)) continue;
            sb.AppendLine($"        public const string {ident} = \"{e.id.Replace("\\", "\\\\").Replace("\"", "\\\"")}\";");
        }
        sb.AppendLine("    }");
    }
#endif
}
