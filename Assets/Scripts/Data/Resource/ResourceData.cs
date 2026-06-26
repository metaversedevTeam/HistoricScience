using System.Collections.Generic;
using UnityEngine;

public abstract class ResourceData : ScriptableObject
{
    public string Nmae => _name;
    [SerializeField] private string _name;

    public Sprite IconSprite => _iconSprite;
    [SerializeField] private Sprite _iconSprite;

    public int Id => _id;
    [SerializeField, HideInInspector] private int _id = -1;

    public List<ResourceData> Ingredient => _ingredient;
    [SerializeField] private List<ResourceData> _ingredient;
}
