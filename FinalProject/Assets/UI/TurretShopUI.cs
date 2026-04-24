using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class TurretShopUI : MonoBehaviour
{
    public static TurretShopUI Instance { get; private set; }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }
    [System.Serializable]
    public class TurretItem
    {
        public string displayName;
        public GameObject prefab;
        public int cost;
        public Sprite icon;
    }
    [Header("Turret definitions")]
    public TurretItem[] turretItems;
    [Header("UI")]
    public TextMeshProUGUI moneyText;
    public int startingMoney = 500;
    [Header("Auto-generated buttons")]
    public GameObject buttonPrefab;
    public Transform buttonContainer;
    private int _money;
    private void Start()
    {
        _money = startingMoney;
        UpdateMoneyUI();
        GenerateButtons();
    }
    // Button generation
    private void GenerateButtons()
    {
        if (buttonPrefab == null || buttonContainer == null) return;
        for (int i = 0; i < turretItems.Length; i++)
        {
            int index = i;
            TurretItem item = turretItems[i];
            GameObject btnGO = Instantiate(buttonPrefab, buttonContainer);
            TextMeshProUGUI[] texts = btnGO.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (TextMeshProUGUI t in texts)
            {
                if (t.gameObject.name == "PriceText") t.text = "$" + item.cost;
                if (t.gameObject.name == "NameText")  t.text = item.displayName;
            }
            if (item.icon != null)
            {
                Image img = btnGO.GetComponentInChildren<Image>();
                if (img != null) img.sprite = item.icon;
            }
            Button btn = btnGO.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => SelectTurret(index));
        }
    }
    // Purchase logic
    public void SelectTurret(int index)
    {
        if (index < 0 || index >= turretItems.Length) return;
        TurretItem item = turretItems[index];
        if (_money < item.cost)
        {
            Debug.Log($"[TurretShopUI] Not enough money. Need {item.cost}, have {_money}.");
            return;
        }
        AddMoney(-item.cost);
        TurretPlacer.Instance.BeginPlacement(item.prefab, item.cost);
    }
    // Economy
    public void AddMoney(int amount)
    {
        _money = Mathf.Max(0, _money + amount);
        UpdateMoneyUI();
    }
    public int Money => _money;
    private void UpdateMoneyUI()
    {
        if (moneyText != null)
            moneyText.text = "$" + _money;
    }
}
