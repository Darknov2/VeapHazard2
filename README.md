# VeapHazard2

A procedural terrain generator for Unity with support for floating islands, cave systems, and dynamic NPC navigation.

## Features

- **Procedural Terrain Generation**: Infinite terrain generation with caves and floating islands
- **Dynamic Cave Systems**: Underground cave networks with worm-based generation
- **Floating Islands**: Stateless island generation in 3D space
- **NPC Navigation System**: Runtime NavMesh baking with multi-surface support

## NPC Navigation System

The NPC navigation system enables NPCs to navigate reliably across open terrain, between islands, and inside caves using Unity's NavMesh with automatic runtime baking and seamless transitions.

### Prerequisites

This system requires the **Unity AI Navigation** package (`com.unity.ai.navigation`). Install it via:
1. Open Unity Package Manager (Window > Package Manager)
2. Click the "+" button and select "Add package by name"
3. Enter: `com.unity.ai.navigation`
4. Click "Add"

### Core Components

#### 1. NavMeshManager
Singleton that manages all NavMeshSurface components in the scene and bakes them at runtime.

**Setup:**
- Add a GameObject to your scene (e.g., "NavMeshManager")
- Attach the `NavMeshManager` component
- Configure settings:
  - `Bake On Start`: Auto-bake all surfaces when the scene starts
  - `Async Baking`: Bake asynchronously to reduce frame hitching
  - `Initial Bake Delay`: Delay before baking starts (allows terrain to generate)

#### 2. NavMeshSurface Components
Unity's NavMeshSurface defines walkable areas for NPCs.

**Setup for Terrain:**
1. Select your main terrain GameObject
2. Add Component > NavMeshSurface (from Unity.AI.Navigation)
3. Configure:
   - Agent Type: Humanoid (or your custom agent type)
   - Collect Objects: Volume or Children as appropriate
   - Use Geometry: Render Meshes
4. Set the size to cover your terrain area

**Setup for Islands:**
1. Select each island GameObject
2. Add Component > NavMeshSurface
3. Configure similar to terrain
4. Ensure the island mesh has a collider

**Setup for Caves:**
1. Create a parent GameObject for the cave interior (e.g., "CaveInterior")
2. Add Component > NavMeshSurface
3. Configure area type if needed (e.g., use a separate cave area)
4. This surface will be baked separately from the terrain

#### 3. OffMeshLinkAutoCreator
Automatically creates NavMeshLink components between nearby surfaces to allow NPCs to jump gaps.

**Setup:**
1. Add a GameObject (e.g., "LinkCreator")
2. Attach the `OffMeshLinkAutoCreator` component
3. Configure:
   - `Max Link Distance`: Maximum gap distance (e.g., 4 meters)
   - `Samples Per Surface`: Number of test points per surface edge
   - `Min/Max Height Difference`: Traversable height range
   - `Bidirectional`: Allow two-way traversal
4. Links are created automatically after NavMesh baking

#### 4. CaveTrigger
Attach to cave entrance trigger volumes to notify NPCs when entering/exiting caves.

**Setup:**
1. Create a GameObject at the cave entrance (e.g., "CaveEntrance")
2. Add a Collider component (Box Collider or Sphere Collider)
3. Check "Is Trigger" on the collider
4. Attach the `CaveTrigger` component
5. Configure:
   - `Cave Surface`: Reference to the cave's NavMeshSurface
   - `Exterior Surface`: Reference to terrain's NavMeshSurface (optional, auto-detected)
   - `NPC Tag`: Tag to identify NPCs (default: "NPC")
6. Position and size the trigger to cover the entrance area

#### 5. NPCNavigator
Main NPC component that handles navigation, surface transitions, and fallback movement.

**Setup:**
1. Create or select an NPC GameObject
2. Add Component > NavMeshAgent (from UnityEngine.AI)
3. Configure NavMeshAgent:
   - Speed: 3.5
   - Acceleration: 8
   - Radius: 0.5
   - Height: 2
4. Add Component > NPCNavigator
5. Configure:
   - `Target Destination`: Transform to navigate toward
   - `Continuously Update Destination`: Track moving targets
   - `Use Fallback Movement`: Enable simple steering when NavMesh unavailable
   - `Debug Logs`: Enable for troubleshooting
6. Tag the GameObject with "NPC" if using CaveTrigger tag filtering

### Scene Setup Workflow

1. **Setup NavMeshManager:**
   - Create a "NavMeshManager" GameObject
   - Add NavMeshManager component
   - Set initial bake delay to 1-2 seconds (allow terrain generation)

2. **Add NavMeshSurfaces:**
   - Add NavMeshSurface to terrain GameObject
   - Add NavMeshSurface to each island cluster
   - Create cave parent objects with NavMeshSurface for interiors

3. **Setup Link Creator:**
   - Create a "LinkCreator" GameObject
   - Add OffMeshLinkAutoCreator component
   - Set max link distance to 4-5 meters

4. **Mark Cave Entrances:**
   - Create trigger volumes at cave entrances
   - Add CaveTrigger component
   - Reference the appropriate cave NavMeshSurface

5. **Create NPCs:**
   - Create NPC GameObjects with NavMeshAgent
   - Add NPCNavigator component
   - Set target destinations or implement AI logic
   - Tag as "NPC"

6. **Test Navigation:**
   - Play the scene
   - Verify NavMesh bakes (check console logs)
   - Observe NPCs navigating terrain, entering caves, and crossing gaps
   - Use debug visualization (enable Draw Debug Path on NPCNavigator)

### Troubleshooting

**NavMesh not baking:**
- Check that Unity.AI.Navigation package is installed
- Verify NavMeshSurface components are present
- Check console for errors during baking
- Ensure colliders exist on terrain/island meshes

**NPCs not moving:**
- Verify NavMeshAgent is enabled
- Check that NPC starts on NavMesh (use debug logs)
- Ensure destination is set and reachable
- Check NavMeshAgent's areaMask matches surface areas

**Links not creating:**
- Ensure surfaces are baked before link creation
- Check max link distance is appropriate
- Verify surfaces are within distance of each other
- Enable debug visualization on OffMeshLinkAutoCreator

**Cave transitions not working:**
- Verify CaveTrigger collider is set as trigger
- Check that cave surface is referenced
- Ensure NPC has correct tag
- Enable debug logs on CaveTrigger and NPCNavigator

### API Usage Examples

**Set NPC destination:**
```csharp
NPCNavigator navigator = npc.GetComponent<NPCNavigator>();
navigator.SetDestination(targetPosition);
```

**Check if destination reached:**
```csharp
if (navigator.HasReachedDestination())
{
    Debug.Log("NPC reached destination!");
}
```

**Manually trigger surface rebuild:**
```csharp
NavMeshManager.Instance.BakeAllSurfaces();
// or rebuild specific surface:
NavMeshManager.Instance.RebuildSurface(mySurface);
```

**Manually create off-mesh links:**
```csharp
OffMeshLinkAutoCreator creator = FindObjectOfType<OffMeshLinkAutoCreator>();
creator.CreateLinks();
```

### Performance Considerations

- Runtime baking can be expensive; use async mode for better frame rates
- Limit the number of NavMeshSurfaces (combine nearby surfaces when possible)
- Reduce samplesPerSurface on OffMeshLinkAutoCreator for faster link creation
- Use appropriate chunk sizes for procedural terrain NavMesh baking
- Consider baking only nearby chunks for large worlds

### Advanced Configuration

**Custom Agent Types:**
Create custom NavMesh agent types for different NPC sizes:
1. Window > AI > Navigation (Obsolete) > Agents tab
2. Create new agent type with appropriate size
3. Reference in NavMeshSurface and NavMeshAgent components

**Area Types:**
Define custom area types for different terrain costs:
1. Window > AI > Navigation (Obsolete) > Areas tab
2. Configure costs for different area types
3. Use in NavMeshSurface and CaveTrigger components

**Off-Mesh Link Curves:**
Customize the jump arc in NPCNavigator:
- Modify offMeshLinkCurve for different motion paths
- Adjust offMeshLinkSpeed for faster/slower traversal

## License

[Add your license information here]

## Contributing

[Add contribution guidelines here]
