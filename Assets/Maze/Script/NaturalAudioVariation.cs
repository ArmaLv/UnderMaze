using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(AudioSource))]
public class NaturalAudioVariation : MonoBehaviour
{
    [Header("Volume (dB)")]
    [Range(0f, 6f)] public float volumeVariationDb = 2f;

    [Header("Pitch (Speed)")]
    [Range(0f, 0.1f)] public float pitchVariation = 0.02f;

    [Header("Shift Boost")]
    [Tooltip("Pitch multiplier when holding Shift.")]
    [Range(1f, 2f)] public float shiftPitchMultiplier = 1.1f;

    [Tooltip("How quickly pitch transitions to boosted state.")]
    [Range(0.1f, 5f)] public float shiftSmoothSpeed = 2f;

    [Header("Update")]
    [Range(0.1f, 2f)] public float updateInterval = 0.5f;

    private AudioSource src;
    private float baseVolume;
    private float basePitch;
    private float nextUpdate;
    private float targetPitch;

    void Awake()
    {
        src = GetComponent<AudioSource>();
        baseVolume = src.volume;
        basePitch = src.pitch;
        targetPitch = basePitch;
    }

    void Update()
    {
        if (!src.isPlaying) return;

        if (Time.time >= nextUpdate)
        {
            nextUpdate = Time.time + updateInterval;
            float db = Random.Range(-volumeVariationDb, volumeVariationDb);
            float volumeFactor = Mathf.Pow(10f, db / 20f);
            src.volume = baseVolume * volumeFactor;

            float randPitch = basePitch + Random.Range(-pitchVariation, pitchVariation);
            targetPitch = randPitch;
        }

        // New Input System check
        bool shiftHeld = Keyboard.current != null && 
                        (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
        float boost = shiftHeld ? shiftPitchMultiplier : 1f;
        float desiredPitch = targetPitch * boost;

        src.pitch = Mathf.Lerp(src.pitch, desiredPitch, Time.deltaTime * shiftSmoothSpeed);
    }
}
