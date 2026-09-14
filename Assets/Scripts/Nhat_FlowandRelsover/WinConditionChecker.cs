using UnityEngine;

public class WinConditionChecker : MonoBehaviour
{
    public static WinConditionChecker Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public bool CheckWin()
    {
        // Khi ghép dữ liệu thực tế sẽ quét qua PlayerManger của Thương
        return false; // Trả về false để game tiếp tục vòng lặp
    }
}