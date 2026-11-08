using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Timeline;

// Lays the drag on the cigarette into the desk intro: the inhale while the hand holds the cigarette in front of the
// face (IntroHandClearance pose), the exhale after the hand comes down. Played by the intro's SFX AudioSource (2D, mixer).
public static class IntroSmokeSoundSetup
{
    const string SoundPath = "Assets/Sounds/Intro_SigaraCekme.wav";
    const string TrackName = "Audio Track (Smoke)";
    const float Start = 8.25f; // inhale starts 0.1 s into the clip, as the hand settles in front of the face

    [MenuItem("Senzora/Intro/Sigara Çekme Sesini Uygula")]
    public static void Apply()
    {
        var sequence = Object.FindFirstObjectByType<InGameMenuController>();
        var sound = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundPath);
        if (sequence == null || sequence.introTimeline == null || sequence.sfxSource == null || sound == null)
        {
            Debug.LogError("INTRO SMOKE SOUND INVALID: InGameMenuController / introTimeline / sfxSource / " + SoundPath + " yok.");
            return;
        }
        var director = sequence.introTimeline;
        var timeline = (TimelineAsset)director.playableAsset;
        var track = timeline.GetOutputTracks().OfType<AudioTrack>().FirstOrDefault(item => item.name == TrackName) ?? timeline.CreateTrack<AudioTrack>(null, TrackName);
        foreach (var stale in track.GetClips().ToList()) timeline.DeleteClip(stale);
        var clip = track.CreateClip(sound);
        clip.displayName = "Sigara çekme"; clip.start = Start; clip.clipIn = 0; clip.duration = sound.length;
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();

        director.SetGenericBinding(track, sequence.sfxSource);
        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        EditorSceneManager.SaveScene(director.gameObject.scene);
        Debug.Log($"INTRO SMOKE SOUND PASS: {sound.length:0.00} s @ {Start:0.00}, timeline {timeline.duration:0.00} s");
    }
}
