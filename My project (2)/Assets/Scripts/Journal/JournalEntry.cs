using System;
using UnityEngine;

[Serializable]
public class JournalEntry
{
    public int weekNumber = 1;
    public int day = 1;
    public string taskName;
    public string status;         // e.g. "Task Successful" or "Task Failed"
    public string hungerLevel;    // e.g. "Normal", "Hungry", "Starving"
    public DateTime timestamp;

    public JournalEntry() { timestamp = DateTime.Now; }

    public JournalEntry(int day, int weekNumber, string taskName, string status, string hungerLevel)
    {
        this.weekNumber = weekNumber > 0 ? weekNumber : 1;
        this.day = day;
        this.taskName = taskName;
        this.status = status;
        this.hungerLevel = hungerLevel;
        timestamp = DateTime.Now;
    }

    public override string ToString()
    {
        // Example:
        // Week 1 Day 1
        // Fetching Bucket
        // Task Successful
        // hunger level: Starving
        return $"Week {weekNumber}\nDay {day}\n{taskName}\n{status}\nhunger level: {hungerLevel}";
    }
}
