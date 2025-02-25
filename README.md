# GOALS
This project focuses on procedural terrain generation in Godot using set_cell_terrain_connect(), leveraging noise values generated by Godot’s FastNoiseLite. The goal is to create a dynamic, visually cohesive world while maintaining efficient performance during runtime.
# ISSUES
One of the main challenges encountered is slow load times. This is primarily due to the way set_cell_terrain_connect() functions. Instead of utilizing a true Wave Function Collapse (WFC) algorithm, Godot’s built-in system relies on constraint propagation. This method checks every adjacent cell for proper terrain connections, which becomes inefficient when applied to large tilemaps. A batch-processing approach or an optimized WFC algorithm would be significantly more performant.

Another major bottleneck is the lack of asynchronous or multithreaded processing in Godot’s scene system. SceneTree operations, including tile placement, are highly recommended to run on the main thread to avoid graphical inconsistencies such as missing tiles. Unfortunately, this means that terrain generation cannot take full advantage of multithreading for tasks related to rendering or texture placement. While attempts have been made to offload noise calculations to separate threads using mutexes, the inability to asynchronously modify the tilemap without causing issues remains a significant challenge.

Optimizations are being explored to mitigate these performance limitations. Potential solutions include precomputing terrain data before placing tiles, reducing the number of per-tile checks, and experimenting with alternative terrain generation methods outside of set_cell_terrain_connect(). Additionally, the approach of using separate biomes across different maps aims to minimize the active workload while still enabling a rich, procedurally generated world.




Extra Info:

[TileMapLayer]([url](https://docs.godotengine.org/en/stable/classes/class_tilemaplayer.html))
Inherits: Node2D < CanvasItem < Node < Object

Node for 2D tile-based maps.

Description
Node for 2D tile-based maps. A TileMapLayer uses a TileSet which contain a list of tiles which are used to create grid-based maps. Unlike the TileMap node, which is deprecated, TileMapLayer has only one layer of tiles. You can use several TileMapLayer to achieve the same result as a TileMap node.

For performance reasons, all TileMap updates are batched at the end of a frame. Notably, this means that scene tiles from a TileSetScenesCollectionSource may be initialized after their parent. This is only queued when inside the scene tree.

To force an update earlier on, call update_internals.

##### Methods of TileMapLayer

void set_cells_terrain_connect(cells: Array[Vector2i], terrain_set: int, terrain: int, ignore_empty_terrains: bool = true) 

Update all the cells in the cells coordinates array so that they use the given terrain for the given terrain_set. If an updated cell has the same terrain as one of its neighboring cells, this function tries to join the two. This function might update neighboring tiles if needed to create correct terrain transitions.

If ignore_empty_terrains is true, empty terrains will be ignored when trying to find the best fitting tile for the given terrain constraints.

Note: To work correctly, this method requires the TileMapLayer's TileSet to have terrains set up with all required terrain combinations. Otherwise, it may produce unexpected results.
## Thread Safe API's
Thread-safe APIs
Threads
Threads are used to balance processing power across CPUs and cores. Godot supports multithreading, but not in the whole engine.

Below is a list of ways multithreading can be used in different areas of Godot.

Global scope
Global Scope singletons are all thread-safe. Accessing servers from threads is supported (for RenderingServer and Physics servers, ensure threaded or thread-safe operation is enabled in the project settings!).

This makes them ideal for code that creates dozens of thousands of instances in servers and controls them from threads. Of course, it requires a bit more code, as this is used directly and not within the scene tree.

Scene tree
Interacting with the active scene tree is NOT thread-safe. Make sure to use mutexes when sending data between threads. If you want to call functions from a thread, the call_deferred function may be used:

# Unsafe:
node.add_child(child_node)
# Safe:
node.add_child.call_deferred(child_node)
However, creating scene chunks (nodes in tree arrangement) outside the active tree is fine. This way, parts of a scene can be built or instantiated in a thread, then added in the main thread:

var enemy_scene = load("res://enemy_scene.scn")
var enemy = enemy_scene.instantiate()
enemy.add_child(weapon) # Set a weapon.
world.add_child.call_deferred(enemy)
Still, this is only really useful if you have one thread loading data. Attempting to load or create scene chunks from multiple threads may work, but you risk resources (which are only loaded once in Godot) tweaked by the multiple threads, resulting in unexpected behaviors or crashes.

Only use more than one thread to generate scene data if you really know what you are doing and you are sure that a single resource is not being used or set in multiple ones. Otherwise, you are safer just using the servers API (which is fully thread-safe) directly and not touching scene or resources.

Rendering
Instancing nodes that render anything in 2D or 3D (such as Sprite) is not thread-safe by default. To make rendering thread-safe, set the Rendering > Driver > Thread Model project setting to Multi-Threaded.

Note that the Multi-Threaded thread model has several known bugs, so it may not be usable in all scenarios.

You should avoid calling functions involving direct interaction with the GPU on other threads, such as creating new textures or modifying and retrieving image data, these operations can lead to performance stalls because they require synchronization with the RenderingServer, as data needs to be transmitted to or updated on the GPU.
