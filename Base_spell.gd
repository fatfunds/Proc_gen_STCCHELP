extends Node2D
class_name BASE_SPELL

@export var spell_data: SPELLS  # Spell data resource
@onready var player_test: CharacterBody2D = $player_test

# State variables
var direction: Vector2 = Vector2.ZERO
var distance_traveled: float = 0.0
enum SpellState { INACTIVE, ACTIVE, FINISHED }
var state: SpellState = SpellState.INACTIVE

# Player and spell casting zone references
var spell_casting_zone: Node2D = null  # Reference to the player's spell_casting_zone
var player: CharacterBody2D = null  # Player reference

# Private node references
var _animated_sprite: AnimatedSprite2D = null
var _hurt_box_animation: AnimationPlayer = null
var _audio_player: AudioStreamPlayer2D = null

# Connect to player signal when ready
func _ready():
	initialize_nodes()
	assert_nodes()
	connect_to_player()

# Initialize node references
func initialize_nodes():
	_animated_sprite = $AnimatedSprite2D
	_hurt_box_animation = $hurt_box/AnimationPlayer
	_audio_player = $AudioStreamPlayer2D

# Verify critical nodes exist
func assert_nodes():
	if not _animated_sprite:
		print("Warning: AnimatedSprite2D node not found!")
	if not _hurt_box_animation:
		print("Warning: AnimationPlayer node not found!")
	if not _audio_player:
		print("Warning: AudioStreamPlayer2D node not found!")

# Connect to the player's spell_casting_zone using signals
func connect_to_player():
	# Ensure player is set (you can also use `get_parent()` or another approach to find the player)
	player = get_parent()
	
	if player:
		# Listen for the signal from the player to get the spell_casting_zone
		player.connect("spell_casting_zone_ready", Callable(self, "_on_spell_casting_zone_ready"))
	else:
		print("Error: Unable to find player for spell casting zone.")

# This function is called when the player emits the 'spell_casting_zone_ready' signal
func _on_spell_casting_zone_ready(casting_zone: Node2D):
	spell_casting_zone = casting_zone
	print("Connected to player's spell casting zone:", spell_casting_zone.name)

# Initialize the spell with necessary data
func initialize(casting_position: Vector2, dir: Vector2, data: SPELLS):
	state = SpellState.ACTIVE
	spell_data = data
	direction = dir.normalized()

	# Set the spell's initial position based on the spell_casting_zone
	if spell_casting_zone:
		global_position = spell_casting_zone.global_position
	else:
		global_position = casting_position  # Fallback if no spell_casting_zone

	print("Spell initialized at position: ", global_position)

	initialize_cast_animation()
	initialize_hurt_box_animation()
	initialize_audio()

# Visual animation initialization
func initialize_cast_animation():
	if not _animated_sprite or not spell_data or not spell_data.spell_animation:
		print("Error: Missing or invalid spell animation for: ", spell_data.spell_name)
		return

	_animated_sprite.sprite_frames = spell_data.spell_animation
	if "cast_animation" in spell_data.spell_animation.get_animation_names():
		_animated_sprite.play("cast_animation")
		_animated_sprite.connect("animation_finished", Callable(self, "_on_cast_animation_finished"))
	else:
		print("Error: 'cast_animation' not found for: ", spell_data.spell_name)

# Collision animation initialization
func initialize_hurt_box_animation():
	if not _hurt_box_animation:
		print("Error: AnimationPlayer not found for hurt box!")
		return

	if _hurt_box_animation.has_animation("Hurt_box"):
		_hurt_box_animation.play("Hurt_box")
		_hurt_box_animation.connect("animation_finished", Callable(self, "_on_hurt_box_animation_finished"))
	else:
		print("Warning: 'Hurt_box' animation not found!")

# Audio playback initialization
func initialize_audio():
	if not _audio_player or not spell_data or not spell_data.spell_noise_wav:
		print("Warning: Missing or invalid audio for: ", spell_data.spell_name)
		return

	_audio_player.stream = spell_data.spell_noise_wav
	_audio_player.play()

# Process logic for active spells
func _process(delta: float):
	if state != SpellState.ACTIVE:
		return

	# Update position to follow the player's spell_casting_zone
	if spell_casting_zone:
		global_position = spell_casting_zone.global_position

	# Projectile movement (if applicable)
	if spell_data.is_projectile:
		global_position += direction * spell_data.spell_speed * delta
		distance_traveled += spell_data.spell_speed * delta

		# Check if the spell has exceeded its range
		if distance_traveled >= spell_data.spell_range:
			print("Spell expired: ", spell_data.spell_name)
			terminate()

# Cast animation finished
func _on_cast_animation_finished():
	print("Cast animation finished for: ", spell_data.spell_name)
	terminate()

# Hurt box animation finished
func _on_hurt_box_animation_finished():
	print("Hurt box animation finished for: ", spell_data.spell_name)
	if not _animated_sprite.is_playing():
		terminate()

# Explicitly terminate the spell
func terminate():
	if state == SpellState.FINISHED:
		return

	state = SpellState.FINISHED
	if _audio_player and _audio_player.playing:
		_audio_player.stop()

	queue_free()
