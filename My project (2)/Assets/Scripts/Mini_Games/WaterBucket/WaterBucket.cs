using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class WaterBucket : MonoBehaviour
{
    [Header("Water Settings")]
    public float maxWater = 100f;
    [SerializeField] private float currentWater = 100f;

    [Header("Spill / Leak Settings")]
    public float baseLeakPerSecond = 0f;
    public float tiltLeakMultiplier = 0.5f;
    public float velocityLeakMultiplier = 0.2f;
    public float maxTiltAngle = 75f;

    [Header("Starvation Mode (periodic fractional spill)")]
    public bool starvationMode = false;
    public float starvationInterval = 3f;
    [Range(0f, 1f)] public float starvationSpillFraction = 0.05f; // 5% every interval

    [Header("References")]
    public Transform bucketVisualTransform;   // used to compute tilt (assign the mesh root)
    public Rigidbody carrierRigidbody;        // set when picked up (player's rb) � optional

    [Header("Events")]
    public UnityEvent onEmptied;              // when water reaches zero
    public UnityEvent<float> onWaterChanged;  // passes new currentWater
    public UnityEvent onCarryStart;           // invoked on StartCarry()
    public UnityEvent onCarryEnd;             // invoked on StopCarry()

    // internals
    private bool isCarried = false;
    private Coroutine starvationCoroutine;

    private void Reset()
    {
        currentWater = maxWater;
    }

    private void Start()
    {
        currentWater = Mathf.Clamp(currentWater, 0f, maxWater);

        if (starvationMode)
            StartStarvation();

        NotifyChange();
    }

    private void Update()
    {
        if (!isCarried) return;

        float dt = Time.deltaTime;

        float tiltAngle = GetTiltAngle();
        float tiltFactor = Mathf.Clamp01(tiltAngle / Mathf.Max(0.0001f, maxTiltAngle));
        float tiltLeak = tiltFactor * tiltLeakMultiplier;

        float velLeak = 0f;
        if (carrierRigidbody != null)
        {
            float speed = carrierRigidbody.linearVelocity.magnitude;
            velLeak = speed * velocityLeakMultiplier;
        }

        float leakPerSecond = baseLeakPerSecond + tiltLeak + velLeak;
        if (leakPerSecond > 0f && currentWater > 0f)
        {
            float amt = leakPerSecond * dt;
            ReduceWater(amt);
        }

        // Optional: huge tilt instant spill (tunable). Remove if undesired.
        if (tiltAngle >= 120f && currentWater > 0f)
        {
            float bigSpill = maxWater * 0.3f;
            ReduceWater(bigSpill);
        }
    }

    private float GetTiltAngle()
    {
        if (bucketVisualTransform == null) return 0f;
        float dot = Vector3.Dot(bucketVisualTransform.up.normalized, Vector3.up);
        dot = Mathf.Clamp(dot, -1f, 1f);
        float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
        return angle;
    }

    public float GetNormalizedWater() => Mathf.Clamp01(currentWater / maxWater);
    public float GetCurrentWater() => currentWater;
    public bool IsBeingCarried() => isCarried;

    public void FillTo(float amount)
    {
        currentWater = Mathf.Clamp(amount, 0f, maxWater);
        NotifyChange();
    }

    public void AddWater(float amount)
    {
        currentWater = Mathf.Clamp(currentWater + Mathf.Abs(amount), 0f, maxWater);
        NotifyChange();
    }

    public void ReduceWater(float amount)
    {
        if (amount <= 0f) return;
        currentWater = Mathf.Max(0f, currentWater - amount);
        NotifyChange();
        if (currentWater <= 0f)
        {
            currentWater = 0f;
            onEmptied?.Invoke();
        }
    }

    // Call when player picks up the bucket
    public void StartCarry(Rigidbody carrierRb = null)
    {
        isCarried = true;
        if (carrierRb != null)
            carrierRigidbody = carrierRb;
        onCarryStart?.Invoke();
    }

    // Call when player drops the bucket
    public void StopCarry()
    {
        isCarried = false;
        carrierRigidbody = null;
        onCarryEnd?.Invoke();
    }

    public void StartStarvation()
    {
        if (starvationCoroutine != null) StopCoroutine(starvationCoroutine);
        starvationCoroutine = StartCoroutine(StarvationTick());
        starvationMode = true;
    }

    public void StopStarvation()
    {
        if (starvationCoroutine != null) StopCoroutine(starvationCoroutine);
        starvationCoroutine = null;
        starvationMode = false;
    }

    private IEnumerator StarvationTick()
    {
        while (true)
        {
            yield return new WaitForSeconds(starvationInterval);
            if (currentWater > 0f)
            {
                float spillAmount = currentWater * starvationSpillFraction;
                ReduceWater(spillAmount);
            }
        }
    }

    private void NotifyChange()
    {
        onWaterChanged?.Invoke(currentWater);
    }
}
