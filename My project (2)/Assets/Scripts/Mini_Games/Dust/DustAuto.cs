using UnityEngine;

/// <summary>
/// Lightweight helper to attach to individual dust objects.
/// - Auto-registers with the nearest DustContainerCheckerAuto on parent chain (if found)
/// - Notifies the container on OnDestroy so the container doesn't require explicit calls
/// - Exposes Sweep() helper to call when your sweep code wants to sweep this dust (calls Notify then destroys)
/// </summary>
[DisallowMultipleComponent]
public class DustAuto : MonoBehaviour
{
    [Tooltip("Optional explicit container. If null the script will search parents for a DustContainerCheckerAuto.")]
    public DustContainerCheckerAuto parentContainer;

    bool registered = false;

    void Awake()
    {
        if (parentContainer == null)
        {
            parentContainer = GetComponentInParent<DustContainerCheckerAuto>();
        }

        if (parentContainer != null)
        {
            parentContainer.RegisterDust(gameObject);
            registered = true;
        }
    }

    /// <summary>
    /// Call this to sweep the dust (notifies parent and destroys the GameObject).
    /// If your sweep code already calls Destroy elsewhere, you don't have to call this - OnDestroy will notify.
    /// </summary>
    public void Sweep()
    {
        // notify parent BEFORE destroying so parent can inspect the object reference if needed
        try
        {
            parentContainer?.NotifyDustRemoved(gameObject);
        }
        catch { }
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // Notify the parent if not already notified (useful if external Destroy called)
        // Note: OnDestroy is called whether scene unload or destroy; guard with registered to reduce noise
        if (parentContainer != null)
        {
            try
            {
                parentContainer.NotifyDustRemoved(gameObject);
            }
            catch { }
        }
    }
}
