using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BountyRow : MonoBehaviour
{
    [SerializeField] Image icon;
    [SerializeField] TMP_Text speciesName;
    [SerializeField] TMP_Text price;
    [SerializeField, Tooltip("Off: the letter shows just the item icon and the amount.")]
    bool showName;

    public void Bind(CreatureData species)
    {
        icon.sprite = species.itemIcon;
        icon.enabled = icon.sprite != null;
        speciesName.gameObject.SetActive(showName);
        speciesName.text = species.displayName;
        price.text = $"${species.bountyPrice}";
    }
}
