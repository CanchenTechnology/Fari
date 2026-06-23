using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SearchHistoryItem : MonoBehaviour
{
    public Text nameText;
    public TMP_Text nameTMPText;
    public Button deleteBtn;

    private Button rowButton;

    public void Bind(string keyword, Action<string> onSelect, Action<string> onDelete)
    {
        keyword = (keyword ?? string.Empty).Trim();
        ResolveReferences();

        if (nameText != null) nameText.text = keyword;
        if (nameTMPText != null) nameTMPText.text = keyword;

        if (rowButton != null)
        {
            rowButton.onClick.RemoveAllListeners();
            rowButton.onClick.AddListener(() => onSelect?.Invoke(keyword));
        }

        if (deleteBtn != null)
        {
            deleteBtn.onClick.RemoveAllListeners();
            deleteBtn.onClick.AddListener(() => onDelete?.Invoke(keyword));
        }
    }

    private void ResolveReferences()
    {
        if (nameText == null)
            nameText = GetComponentInChildren<Text>(true);

        if (nameTMPText == null)
            nameTMPText = GetComponentInChildren<TMP_Text>(true);

        if (deleteBtn == null)
            deleteBtn = FindButtonByName("deleteBtn", "[Button]Delete", "[Button]Close");

        if (rowButton == null)
            rowButton = GetComponent<Button>();

        if (rowButton == null)
        {
            Image hitArea = GetComponent<Image>();
            if (hitArea == null)
                hitArea = gameObject.AddComponent<Image>();

            hitArea.enabled = true;
            hitArea.color = new Color(1f, 1f, 1f, 0f);
            hitArea.raycastTarget = true;

            rowButton = gameObject.AddComponent<Button>();
            rowButton.targetGraphic = hitArea;
        }
    }

    private Button FindButtonByName(params string[] names)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            foreach (string targetName in names)
            {
                if (button.transform.name == targetName)
                    return button;
            }
        }

        return null;
    }
}
