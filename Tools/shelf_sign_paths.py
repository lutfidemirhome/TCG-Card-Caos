"""Path helpers for Assets/Art/ShelfSigns (textures/materials grouped by type)."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TEXTURES_ROOT = ROOT / "Assets/Art/ShelfSigns/Textures"
MATERIALS_ROOT = ROOT / "Assets/Art/ShelfSigns/Materials"


def sign_prefix(stem: str) -> str:
    return stem.split("_", 1)[0]


def type_folder_name(prefix: str) -> str:
    return prefix.capitalize()


def texture_path(stem: str) -> Path:
    return TEXTURES_ROOT / type_folder_name(sign_prefix(stem)) / f"{stem}.png"


def material_path(stem: str) -> Path:
    return MATERIALS_ROOT / type_folder_name(sign_prefix(stem)) / f"{stem}.mat"
