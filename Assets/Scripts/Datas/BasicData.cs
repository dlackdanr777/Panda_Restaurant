using UnityEngine;

public class BasicData : ScriptableObject
{
    [Header("DefaultData")]
    [SerializeField] protected Sprite _sprite;
    public Sprite Sprite => _sprite;

    [SerializeField] protected Sprite _thumbnailSPrite;
    public Sprite ThumbnailSprite => _thumbnailSPrite;

    [SerializeField] protected string _name;
    public string Name => _name;

    [SerializeField] protected string _id;
    public string Id => _id;

    [TextArea][SerializeField] protected string _description;
    public string Description => _description;
}
