extends Node2D

@export var noise_texture: FastNoiseLite
@export var tree_noise_texture: FastNoiseLite
@export var object_noise_texture: FastNoiseLite
@export var monster_noise_texture: FastNoiseLite

signal world_generated

# World Parameters
const CHUNK_SIZE = 16
const WORLD_WIDTH = 100
const WORLD_HEIGHT = 100

# File Path for Save/Load
const SAVE_FILE = "user://world_save.json"

# Terrain Types for BetterTerrain
@export var sand_terrain_type: int = 0
@export var grass_terrain_type: int = 0
@export var cliff_terrain_type: int = 0

# Tile settings
var water_atlas = Vector2i(0, 0)
var palm_tree_on_sand_atlas = [Vector2i(0, 28), Vector2i(3, 28), Vector2i(6, 28)]
var oak_tree_atlas = Vector2i(15, 6)
var rock_atlas_arr = [Vector2i(0, 0), Vector2i(2, 0), Vector2i(8, 0)]

# TileMapLayers and player reference
@onready var water_layer: TileMapLayer = $Grasslands_tilemaps/water
@onready var sand_layer: TileMapLayer = $Grasslands_tilemaps/beach_layer
@onready var grass_layer: TileMapLayer = $Grasslands_tilemaps/grass_on_beach_layer
@onready var cliff_layer: TileMapLayer = $Grasslands_tilemaps/cliffs
@onready var environment_layer: TileMapLayer = $Grasslands_tilemaps/environment
@onready var objects_layer: TileMapLayer = $Grasslands_tilemaps/objects

@onready var wizard= get_tree().current_scene.get_node("player_test") 


# Noise caching and tile storage
var noise_cache = {}
var sand_tiles_arr = []
var grass_tiles_arr = []

# World Seed and Modifications
var world_seed = 0
var modifications = {}

func _ready():
	print("Initializing procedural world...")
	load_world()

### Load/Save World Functions
func load_world():
	if FileAccess.file_exists(SAVE_FILE):
		var file = FileAccess.open(SAVE_FILE, FileAccess.READ)
		var data = JSON.parse_string(file.get_as_text())
		file.close()

		if data:
			world_seed = data["seed"]
			modifications = data["modifications"]
			print("Loaded world with seed:", world_seed)
		else:
			print("Invalid save data. Generating new world.")
			world_seed = randi()
	else:
		print("No save file found. Generating new world.")
		world_seed = randi()

	# Set seeds for noise generators
	noise_texture.seed = world_seed
	tree_noise_texture.seed = world_seed
	object_noise_texture.seed = world_seed
	monster_noise_texture.seed = world_seed

	await generate_terrain_chunks()
	await populate_objects()
	apply_modifications()
	place_player_on_beach()
	emit_signal("world_generated")
	print("World generation complete.")

func save_world():
	var data = {
		"seed": world_seed,
		"modifications": modifications
	}
	var file = FileAccess.open(SAVE_FILE, FileAccess.WRITE)
	file.store_string(JSON.stringify(data))
	file.close()
	print("World saved successfully.")

### Noise Caching
func get_cached_noise(x: int, y: int) -> float:
	var key = Vector2i(x, y)
	if noise_cache.has(key):
		return noise_cache[key]
	var noise_value = noise_texture.get_noise_2d(x, y)
	noise_cache[key] = noise_value
	return noise_value

### Terrain Generation with BetterTerrain Changesets
func generate_terrain_chunks():
	for chunk_x in range(-WORLD_WIDTH / 2, WORLD_WIDTH / 2, CHUNK_SIZE):
		for chunk_y in range(-WORLD_HEIGHT / 2, WORLD_HEIGHT / 2, CHUNK_SIZE):
			var chunk_rect = Rect2i(Vector2i(chunk_x, chunk_y) * CHUNK_SIZE, Vector2i(CHUNK_SIZE, CHUNK_SIZE))
			var terrain_changes = {}

			# Generate terrain within the chunk
			for x in range(chunk_rect.position.x, chunk_rect.position.x + CHUNK_SIZE):
				for y in range(chunk_rect.position.y, chunk_rect.position.y + CHUNK_SIZE):
					var coord = Vector2i(x, y)
					var altitude = get_cached_noise(x, y)

					if altitude < 0.2:
						water_layer.set_cell(coord, 0, water_atlas)
					elif altitude < 0.4:
						terrain_changes[coord] = sand_terrain_type
						sand_tiles_arr.append(coord)
					elif altitude < 0.6:
						terrain_changes[coord] = grass_terrain_type
						grass_tiles_arr.append(coord)
					else:
						terrain_changes[coord] = cliff_terrain_type

			# Create and apply changeset
			var changeset = BetterTerrain.create_terrain_changeset(sand_layer, terrain_changes)
			await wait_for_changeset(changeset)

			# Update terrain transitions
			BetterTerrain.update_terrain_area(sand_layer, chunk_rect)
			BetterTerrain.update_terrain_area(grass_layer, chunk_rect)
			BetterTerrain.update_terrain_area(cliff_layer, chunk_rect)

	print("Terrain generation complete.")

### Object Placement Using Changesets
func populate_objects():
	var environment_changes = {}
	var object_changes = {}

	for cell_position in sand_tiles_arr:
		var tree_noise = tree_noise_texture.get_noise_2d(cell_position.x, cell_position.y)
		var object_noise = object_noise_texture.get_noise_2d(cell_position.x, cell_position.y)

		if tree_noise > 0.7:
			environment_changes[cell_position] = palm_tree_on_sand_atlas.pick_random()
		elif object_noise > 0.7:
			object_changes[cell_position] = rock_atlas_arr.pick_random()

	for cell_position in grass_tiles_arr:
		var tree_noise = tree_noise_texture.get_noise_2d(cell_position.x, cell_position.y)
		if tree_noise > 0.6:
			environment_changes[cell_position] = oak_tree_atlas

	# Apply changesets
	var env_changeset = BetterTerrain.create_terrain_changeset(environment_layer, environment_changes)
	var obj_changeset = BetterTerrain.create_terrain_changeset(objects_layer, object_changes)

	await wait_for_changeset(env_changeset)
	await wait_for_changeset(obj_changeset)

	print("Object placement complete.")

### Helper: Wait for Changesets to Complete
func wait_for_changeset(changeset):
	while not BetterTerrain.is_terrain_changeset_ready(changeset):
		await get_tree().process_frame
	BetterTerrain.apply_terrain_changeset(changeset)

### Place Player on Beach
func place_player_on_beach():
	for sand_position in sand_tiles_arr:
		for offset in [Vector2i(-1, 0), Vector2i(1, 0), Vector2i(0, -1), Vector2i(0, 1)]:
			var neighbor_position = sand_position + offset
			if get_cached_noise(neighbor_position.x, neighbor_position.y) < 0.2:
				wizard.global_position = sand_layer.map_to_local(sand_position)
				print("Player placed on beach at:", sand_position)
				return

	wizard.global_position = Vector2(0, 0)
	print("No valid beach tile found. Player placed at origin.")

### Apply Modifications
func apply_modifications():
	for key in modifications.keys():
		var coord = Vector2i(key.split(",")[0].to_int(), key.split(",")[1].to_int())
		var mod = modifications[key]
		match mod.layer:
			"environment":
				environment_layer.set_cell(coord, 0, mod.atlas)
			"rocks":
				objects_layer.set_cell(coord, 0, mod.atlas)

	print("Modifications applied.")
