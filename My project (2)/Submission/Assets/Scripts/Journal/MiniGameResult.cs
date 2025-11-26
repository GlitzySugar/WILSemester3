using System;

[Serializable]
public class MiniGameResult
{
    public string miniGameId; // unique id or name
    public string objective;
    public bool success;
    public float score;
    public int stars;
    public string additionalData; // JSON string or notes
    public bool won;          // <-- THIS MUST EXIST
   

    // Day/time metadata filled by DaySystem/Manager before saving:
    public int dayIndex;
    public int weekNumber;
    public string dayState; // Morning/Afternoon/Evening
    public string timestamp; // e.g., System.DateTime.UtcNow.ToString("o")

    // NEW: whether the player was hungry during this mini-game
    public bool hungry;
}
