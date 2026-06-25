using UnityEngine;

public abstract class ResourceData : ScriptableObject
{
    public string Nmae => _name; 
    [SerializeField] private string _name;

    public Sprite IconSprite => _iconSprite;
    [SerializeField] private Sprite _iconSprite;
}
