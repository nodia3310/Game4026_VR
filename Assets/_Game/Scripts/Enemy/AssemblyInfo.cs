using System.Runtime.CompilerServices;

// Lets the Play Mode tests drive EnemyController.Tick directly (per-frame GC measurement),
// and the test assemblies build attacks in code (AttackDefinition.Create, EnemyData.SetAttacks).
[assembly: InternalsVisibleTo("Game4026.Enemy.PlayModeTests")]
[assembly: InternalsVisibleTo("Game4026.Enemy.EditModeTests")]
[assembly: InternalsVisibleTo("Game4026.Bridges.EditModeTests")]
