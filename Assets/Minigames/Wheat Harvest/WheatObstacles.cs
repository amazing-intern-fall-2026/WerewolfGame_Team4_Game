using UnityEngine;

public class WheatObstacle : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float fallbackSpeed = 6f;

    [Header("Visuals")]
    [SerializeField] private ParticleSystem cutParticles;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Audio")]
    [SerializeField] private AudioClip[] cutSfxVariants; // Array holding your 4 audio clips
    [SerializeField] [Range(0f, 1f)] private float cutVolume = 0.8f;
    

    private bool isCut = false;
    private Collider2D col;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        if (isCut) return;

        float speed = fallbackSpeed;
        if (WheatHarvestGameManager.Instance != null && WheatHarvestGameManager.Instance.GameSpeed > 0)
        {
            speed = WheatHarvestGameManager.Instance.GameSpeed;
        }

        transform.Translate(Vector3.left * speed * Time.deltaTime, Space.World);

        if (transform.position.x < -15f)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCut) return;

        if (other.CompareTag("Scythe"))
        {
            CutDown();
        }
        else if (other.CompareTag("Player"))
        {
            if (WheatHarvestGameManager.Instance != null)
                WheatHarvestGameManager.Instance.TriggerGameOver();
        }
    }

    private void CutDown()
    {
        isCut = true;

        PlayRandomCutSound();

        if (WheatHarvestGameManager.Instance != null)
            WheatHarvestGameManager.Instance.AddScore(10);

        if (col != null) col.enabled = false;
        if (spriteRenderer != null) spriteRenderer.enabled = false;
        if (cutParticles != null) cutParticles.Play();

        float delay = cutParticles != null ? cutParticles.main.duration : 0.05f;
        Destroy(gameObject, delay);
    }

    private void PlayRandomCutSound()
    {
        if (cutSfxVariants == null || cutSfxVariants.Length == 0) return;

        // Pick a random clip from the array
        int randomIndex = Random.Range(0, cutSfxVariants.Length);
        AudioClip chosenClip = cutSfxVariants[randomIndex];

        if (chosenClip == null) return;

        // Create temporary 2D audio source to support pitch variation
        GameObject soundObj = new GameObject("TempCutAudio");
        soundObj.transform.position = transform.position;

        AudioSource audioSource = soundObj.AddComponent<AudioSource>();
        audioSource.clip = chosenClip;
        audioSource.volume = cutVolume;
        audioSource.spatialBlend = 0f; // Pure 2D
        
        audioSource.Play();

        // Destroy temporary object once audio clip finishes
        Destroy(soundObj, chosenClip.length / Mathf.Max(0.1f, audioSource.pitch));
    }
}