using UnityEngine;

[CreateAssetMenu(menuName = "Thuong/Medieval Cast")]
public sealed class ThuongMedievalCast : ScriptableObject
{
    public Sprite[] idleSprites;
    public RuntimeAnimatorController[] controllers;
    public int Count => Mathf.Min(idleSprites?.Length ?? 0, controllers?.Length ?? 0);
}
