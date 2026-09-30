using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Clips")]
    [SerializeField] private AudioClip dropSound;

    [Tooltip("Add all your different catch SFX here")]
    [SerializeField] private List<AudioClip> catchSounds = new List<AudioClip>();

    [SerializeField] private AudioClip gameOverSound;

    private AudioSource sfxSource;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        sfxSource = GetComponent<AudioSource>();
    }

    public void PlayDropSound()
    {
        if (dropSound != null)
        {
            sfxSource.PlayOneShot(dropSound, 0.6f);
        }
    }

    public void PlayCatchSound()
    {
        if (catchSounds.Count == 0) return;

        int randomIndex = Random.Range(0, catchSounds.Count);
        AudioClip clip = catchSounds[randomIndex];

        if (clip != null)
        {
            // Slight pitch shift keeps repeated catches sounding lively
            sfxSource.pitch = Random.Range(0.95f, 1.05f);
            sfxSource.PlayOneShot(clip, 0.9f);
            sfxSource.pitch = 1.0f; // Reset pitch to default
        }
    }

    public void PlayGameOverSound()
    {
        if (gameOverSound != null)
        {
            sfxSource.pitch = 1.0f;
            sfxSource.PlayOneShot(gameOverSound, 1f);
        }
    }
}