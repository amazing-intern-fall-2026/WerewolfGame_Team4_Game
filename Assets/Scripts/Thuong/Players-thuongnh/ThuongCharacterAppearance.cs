using UnityEngine;

[CreateAssetMenu(menuName = "Thuong/Character Appearance", fileName = "CharacterAppearance")]
public sealed class ThuongCharacterAppearance : ScriptableObject
{
    public Sprite sprite;
    public RuntimeAnimatorController animatorController;
    public Color tint = Color.white;
    public Vector3 visualScale = Vector3.one;
}
