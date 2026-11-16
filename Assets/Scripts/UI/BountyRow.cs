using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BountyRow : MonoBehaviour
{
    [SerializeField] Image icon;
    [SerializeField] TMP_Text speciesName;
    [SerializeField] TMP_Text price;

    public void Bind(CreatureData species)
    {
        icon.sprite = species.itemIcon;
        icon.enabled = icon.sprite != null;
        speciesName.text = species.displayName;
        price.text = $"${species.bountyPrice}";
    }
}
