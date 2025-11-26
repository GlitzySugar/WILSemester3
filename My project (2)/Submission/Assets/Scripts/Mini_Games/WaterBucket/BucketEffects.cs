using UnityEngine;


[RequireComponent(typeof(WaterBucket))]
public class BucketEffects : MonoBehaviour
{
    [Header("Spill detection")]
    public float bigSpillDeltaThreshold = 10f; // amount dropped at once to count as a big spill
    public ParticleSystem bigSpillParticles;
    public ParticleSystem emptyParticles;

    [Header("Audio")]
    public AudioClip spillClip;
    public AudioClip emptyClip;
    public AudioSource audioSource;

    private WaterBucket bucket;
    private float lastAmount;

    private void Start()
    {
        bucket = GetComponent<WaterBucket>();
        if (bucket == null) return;

        lastAmount = bucket.GetCurrentWater();
        bucket.onWaterChanged.AddListener(OnWaterChanged);
        bucket.onEmptied.AddListener(OnEmptied);

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void OnDestroy()
    {
        if (bucket != null)
        {
            bucket.onWaterChanged.RemoveListener(OnWaterChanged);
            bucket.onEmptied.RemoveListener(OnEmptied);
        }
    }

    private void OnWaterChanged(float newAmount)
    {
        float delta = lastAmount - newAmount;
        if (delta >= bigSpillDeltaThreshold)
            TriggerBigSpill(delta);
        lastAmount = newAmount;
    }

    private void TriggerBigSpill(float delta)
    {
        if (bigSpillParticles != null)
        {
            ParticleSystem ps = Instantiate(bigSpillParticles, transform.position, Quaternion.identity);
            ps.Play();
            Destroy(ps.gameObject, 4f);
        }

        if (spillClip != null && audioSource != null)
            audioSource.PlayOneShot(spillClip);
    }

    private void OnEmptied()
    {
        if (emptyParticles != null)
        {
            ParticleSystem ps = Instantiate(emptyParticles, transform.position, Quaternion.identity);
            ps.Play();
            Destroy(ps.gameObject, 4f);
        }

        if (emptyClip != null && audioSource != null)
            audioSource.PlayOneShot(emptyClip);
    }
}
