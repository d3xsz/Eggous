using UnityEngine;
using UnityEngine.InputSystem;

// Oylama alani: insan oyuncu alanin (trigger) icindeyken E'ye basinca oylama baslar.
[RequireComponent(typeof(Collider))]
public class VoteStation : MonoBehaviour
{
    public VoteSystem vote;
    bool inside;
    BaleCarrier carrier;

    void Reset() { GetComponent<Collider>().isTrigger = true; }

    void Start() { if (vote == null) vote = FindFirstObjectByType<VoteSystem>(); }

    void OnTriggerEnter(Collider other)
    {
        var pc = other.GetComponentInParent<PlayerController>();
        if (pc == null) return;
        if (pc != PlayerController.Local) return;   // ag: uzak oyuncular sayilmaz
        inside = true;
        carrier = pc.GetComponent<BaleCarrier>();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == PlayerController.Local) inside = false;
    }

    bool Holding { get { return carrier != null && carrier.IsCarrying; } }

    void Update()
    {
        if (!inside || vote == null || Holding || !vote.CanStart()) return;
        var kb = Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame) vote.Begin();
    }

    // Gecici ipucu (Faz 6'da guzellestirilecek)
    void OnGUI()
    {
        if (!inside || vote == null) return;
        if (vote.HumanUsedCall) { if (vote.IsVoting) return; string used = "Toplanti hakkini kullandin"; GUI.Label(GameHUD.Prompt(used, new Rect(Screen.width / 2f - 130, Screen.height / 2f + 30, 300, 30)), used); return; }
        if (!vote.CanStart()) return;
        string msg = Holding ? "Once balyayi birak" : "E: zili cal (toplanti cagir)";
        GUI.Label(GameHUD.Prompt(msg, new Rect(Screen.width / 2f - 100, Screen.height / 2f + 30, 250, 30)), msg);
    }
}
