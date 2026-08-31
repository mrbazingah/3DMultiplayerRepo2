using UnityEngine;
using UnityEngine.UIElements;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    UIDocument uiDocument;
    VisualElement root;

    bool isOpen;

    void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
        root = uiDocument.rootVisualElement.Q<VisualElement>("Panel");

        Instance = this;

        CloseMenu();
    }

    public void OnMenuButton()
    {
        if (isOpen)
        {
            CloseMenu();
        }
        else
        {
            OpenMenu();
        }

        isOpen = !isOpen;
    }

    public void OpenMenu()
    {
        root.RemoveFromClassList("hidden");
    }

    public void CloseMenu()
    {
        root.AddToClassList("hidden");
    }
}
