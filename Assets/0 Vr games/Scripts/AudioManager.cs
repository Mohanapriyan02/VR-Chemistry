// AudioManager.cs
// Simple singleton AudioManager for VR Molecular Lab.
// Plays success / failure / grab sounds via AudioSource.
// Attach to a persistent GameObject in the scene.

using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Clips")]
    public AudioClip mixSuccessClip;
    public AudioClip mixFailClip;
    public AudioClip atomGrabClip;
    public AudioClip atomReleaseClip;

    [Header("Volumes")]
    [Range(0f, 1f)] public float masterVolume = 1f;

    private AudioSource _audioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void OnEnable()
    {
        MixingZone.OnMixResult += HandleMixResult;
    }

    private void OnDisable()
    {
        MixingZone.OnMixResult -= HandleMixResult;
    }

    private void HandleMixResult(MoleculeRecipe recipe, bool success)
    {
        if (success)
            PlayClip(mixSuccessClip);
        else
            PlayClip(mixFailClip);
    }

    public void PlayGrab() => PlayClip(atomGrabClip);
    public void PlayRelease() => PlayClip(atomReleaseClip);

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || _audioSource == null) return;
        _audioSource.PlayOneShot(clip, masterVolume);
    }
}