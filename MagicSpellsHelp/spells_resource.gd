extends Resource
class_name SPELLS

# Enumerations for spell types and categories
enum SpellType { DIRECT, TARGETED }
enum DamageType { UNDECLARED, FIRE, ICE, EARTH, LIGHTNING, PHYSICAL, RADIANT, POISON }
enum SpellCategory { OFFENSIVE, DEFENSIVE, UTILITY }

# Core Metadata
@export var spell_name: String = "Unnamed Spell"
@export var spell_description: String = "No description available."
@export var spell_toolbar_sprite: Texture = null  # Icon for spell selection
@export var spell_noise_wav: AudioStream = null  # Sound effect for the spell
@export var spell_animation: SpriteFrames = null  # SpriteFrames for spell animations


# Spell Data
@export var spell_dmg_val: int = 10  # Damage dealt by the spell
@export var spell_recharge_time: float = 1.0  # Cooldown time
@export var spell_dmg_type: DamageType = DamageType.UNDECLARED
@export var spell_category: SpellCategory = SpellCategory.OFFENSIVE
@export var spell_type: SpellType = SpellType.DIRECT
@export var spell_level: int= 0
@export var spell_id: String="00"

# Additional Properties
@export var is_projectile: bool = true
@export var spell_speed: float = 300.0  # Speed of the projectile
@export var spell_range: float = 500.0  # Maximum range
@export var spell_aoe_radius: float = 0.0  # AoE radius (0 means no AoE)
@export var slow_down_multiplier: float = 0.5  # Movement slowdown during casting
@export var spell_charge_time: float = 0.5  # Time required to charge the spell

# New Status Effect Fields
@export var status_effect: String = ""  # Status effect applied by the spell
@export var status_duration: float = 0.0  # Duration of the status effect

# Elemental Interactions
@export var interacts_with: Array[DamageType] = []  # Types this spell interacts with

