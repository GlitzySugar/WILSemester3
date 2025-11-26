using UnityEngine;

[System.Serializable]
public class Sound
{
    public string name;
    public AudioClip clip;

    [Range(0f, 1f)] public float volume = 1f;
    [Range(.1f, 3f)] public float pitch = 1f;
    public bool loop = false;

    [Tooltip("Chance to randomize pitch +/- this value")]
    [Range(0f, 0.5f)] public float randomPitch = 0f;

    [Tooltip("If set, this Sound will route to this mixer group at runtime (optional)")]
    public UnityEngine.Audio.AudioMixerGroup outputAudioMixerGroup;
}
