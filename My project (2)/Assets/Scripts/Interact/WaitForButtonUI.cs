using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
 
public class WaitForUIButtons : CustomYieldInstruction, System.IDisposable
{
    private struct ButtonCallback
    {
        public Button button;
        public UnityAction listener;
    }
    private List<ButtonCallback> mButtons = new List<ButtonCallback>();
    private System.Action<Button> mCallback = null;

    public override bool keepWaiting { get { return buttonPressed == null; } }
    public Button buttonPressed { get; private set; } = null;

    public WaitForUIButtons(System.Action<Button> aCallback, params Button[] aButtons)
    {
        mCallback = aCallback;
        mButtons.Capacity = aButtons.Length;
        foreach (var x in aButtons)
        {
            if (x == null)
                continue;
            var y = new ButtonCallback { button = x };
            y.listener = () => OnButtonPressed(y.button);
            mButtons.Add(y);
        }
        Reset();
    }
    public WaitForUIButtons(params Button[] aButtons) : this(null, aButtons) { }

    private void OnButtonPressed(Button button)
    {
        buttonPressed = button;
        RemoveListeners();
        if (mCallback != null)
            mCallback(button);
    }
    public new WaitForUIButtons Reset()
    {
        RemoveListeners();
        buttonPressed = null;
        AddingListners();
        base.Reset();
        return this;
    }
    private void AddingListners()
    {
        foreach (var bc in mButtons)
            if (bc.button != null)
                bc.button.onClick.AddListener(bc.listener);
    }
    private void RemoveListeners()
    {
        foreach (var bc in mButtons)
            if (bc.button != null)
                bc.button.onClick.RemoveListener(bc.listener);
    }

    public void Dispose()
    {
        RemoveListeners();
        mCallback = null;
        mButtons.Clear();
    }
}