

using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Clips")]
    public AudioClip mixSuccessClip;
    public AudioClip mixFailClip;
    public AudioClip atomGrabClip;
    public AudioClip atomReleaseClip;

    [Tooltip("Sound played the moment atoms are consumed and the mix reaction triggers")]
    public AudioClip atomMixClip;

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

    public void PlayGrab()    => PlayClip(atomGrabClip);
    public void PlayRelease() => PlayClip(atomReleaseClip);

    /// <summary>
    /// Called by MixingZone the moment atoms are consumed (particle + mix moment).
    /// Assign atomMixClip in the Inspector for a "whoosh/dissolve" type sound.
    /// </summary>
    public void PlayMix()     => PlayClip(atomMixClip);

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || _audioSource == null) return;
        _audioSource.PlayOneShot(clip, masterVolume);
    }
}