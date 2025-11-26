using UnityEngine;

public class SimpleHungerStub : MonoBehaviour, IPlayerHunger
{
    [Tooltip("0=Normal,1=Hungry,2=Starving")]
    public HungerState state = HungerState.Normal;

    public HungerState GetHungerState()
    {
        return state;
    }
}
