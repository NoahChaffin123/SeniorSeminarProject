using System;
using System.Collections.Generic;

namespace AssassinsProject.Models;

public enum GameStatus
{
    Setup = 0,
    Active = 1,
    Completed = 2
}

public class Game
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    // Lifecycle
    public GameStatus Status { get; set; } = GameStatus.Setup;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }

    // Roster lock: open/close signups while in Setup
    public bool IsSignupOpen { get; set; } = true;

    // NEW: soft pause while Active — blocks eliminations
    public bool IsPaused { get; set; } = false;

    // Navigation
    public ICollection<Player> Players { get; set; } = new List<Player>();
    public ICollection<Elimination> Eliminations { get; set; } = new List<Elimination>();
}
