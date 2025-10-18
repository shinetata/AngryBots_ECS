using Unity.Entities;
using PGD;

// This component contains a single float Value which represents how
// much health an entity has
public struct Health : IComponent
{
    public float Value;
}

// This component contains a single float Value which represents how
// fast an entity moves
public struct MoveSpeed : IComponent
{
    public float Value;
}

// This component contains a single float Value which represents how
// long something lives before it is destroyed or further processed
public struct TimeToLive : IComponent
{
    public float Value;
}

// This "tag" component contains no data and is instead simply
// used to identify entities as "enemies"
public struct EnemyTag : IComponent
{
}

// This "tag" component contains no data and is instead simply
// used to identify entities as "players"
public struct PlayerTag : IComponent
{
}

// This "tag" component contains no data and is instead simply
// used to identify entities that need to "move forward"
public struct MoveForward : IComponent
{
}