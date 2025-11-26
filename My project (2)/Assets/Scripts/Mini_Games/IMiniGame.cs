using System;

public interface IMiniGame
{
    /// <summary>
    /// Start this mini-game. The implementer MUST invoke the callback once the mini-game ends.
    /// Callback parameter is the result (won/lose etc).
    /// </summary>
    /// <param name="onComplete">Action invoked when mini-game ends. Implementer should pass a MiniGameResult.</param>
    void StartMiniGame(Action<MiniGameResult> onComplete);

    /// <summary>
    /// Optional: reset internal state. Called by manager if it intends to reuse an instance.
    /// </summary>
    void ResetMiniGame();
}
