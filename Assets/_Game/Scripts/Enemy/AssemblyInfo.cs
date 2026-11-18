using System.Runtime.CompilerServices;

// Lets the Play Mode tests drive EnemyController.Tick directly (per-frame GC measurement).
[assembly: InternalsVisibleTo("Game4026.Enemy.PlayModeTests")]
