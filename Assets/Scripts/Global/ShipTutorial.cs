using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Tutorial al empezar una partida:
//  1. Parece solo un juego de pilotar: no se puede entrar en la nave.
//  2. A los pocos segundos se avería la electricidad y un mensaje dice qué tecla pulsar para entrar.
//  3. Al repararla, te dice que vuelvas a pilotar.
//  4. Ya pilotando, se averían una a una otras zonas al azar (cada una cuando reparas la anterior).
//  5. Después empiezan las averías normales de FailureDirector (con la curva de dificultad).
// Textos y tiempos en Resources/ShipSystems (sección Tutorial). Se crea solo y sobrevive a los cambios de escena.
public class ShipTutorial : MonoBehaviour
{
    private enum Step
    {
        WaitingFirstFailure,
        Repairing,
        GoingBackToPilot,
        WaitingNextFailure,
        Done
    }

    private static ShipTutorial instance;

    private readonly List<string> pendingZones = new List<string>();
    private Step step;
    private float timer;
    private string currentZone;
    private string interactKey;
    private Text messageText;
    private CanvasGroup messageGroup;
    private string shownMessage;

    public static void Create()
    {
        if (instance != null)
        {
            return;
        }

        GameObject tutorialObject = new GameObject("ShipTutorial");
        DontDestroyOnLoad(tutorialObject);
        instance = tutorialObject.AddComponent<ShipTutorial>();
    }

    public static void Remove()
    {
        if (instance != null)
        {
            Destroy(instance.gameObject);
            instance = null;
        }
    }

    // Para probar: termina el tutorial ya (lo usa la ventana Depurar estabilidad)
    public static void Skip()
    {
        if (instance != null)
        {
            instance.Finish();
        }
    }

    private void Awake()
    {
        ShipState.CanEnterShip = false;
        ShipState.TutorialFinished = false;
        step = Step.WaitingFirstFailure;
        timer = ShipState.Config.tutorialFirstFailureDelay;
        interactKey = GetInteractKey();
        CreateMessage();
        ChooseZones();
    }

    private void Update()
    {
        if (ShipState.IsDestroyed || ShipState.HasArrived || MainMenu.IsOpen)
        {
            ShowMessage(null);
            return;
        }

        switch (step)
        {
            case Step.WaitingFirstFailure:
            case Step.WaitingNextFailure:
                // Las averías del tutorial llegan mientras pilotas
                ShowMessage(null);
                if (ShipState.IsPiloting)
                {
                    timer -= Time.deltaTime;
                }

                if (timer <= 0f)
                {
                    BreakNextZone();
                }
                break;

            case Step.Repairing:
                ShowMessage(ShipState.IsPiloting ? ShipState.Config.tutorialEnterMessage : ShipState.Config.tutorialRepairMessage);
                if (!ShipState.IsFailing(currentZone))
                {
                    step = Step.GoingBackToPilot;
                }
                break;

            case Step.GoingBackToPilot:
                ShowMessage(ShipState.IsPiloting ? null : ShipState.Config.tutorialBackMessage);
                if (ShipState.IsPiloting)
                {
                    step = Step.WaitingNextFailure;
                    timer = ShipState.Config.tutorialStepDelay;
                }
                break;
        }
    }

    // La primera es siempre la electricidad (si existe); luego unas cuantas al azar
    private void ChooseZones()
    {
        List<string> others = new List<string>();
        foreach (ShipState.SystemState system in ShipState.Systems)
        {
            if (system.name != ShipZones.Electricity)
            {
                others.Add(system.name);
            }
        }

        if (ShipState.HasZone(ShipZones.Electricity))
        {
            pendingZones.Add(ShipZones.Electricity);
        }

        int extra = ShipState.Config.tutorialExtraFailures;
        while (extra > 0 && others.Count > 0)
        {
            int index = Random.Range(0, others.Count);
            pendingZones.Add(others[index]);
            others.RemoveAt(index);
            extra--;
        }
    }

    private void BreakNextZone()
    {
        if (pendingZones.Count == 0)
        {
            Finish();
            return;
        }

        currentZone = pendingZones[0];
        pendingZones.RemoveAt(0);

        // Su estabilidad irá bajando deprisa hasta el nivel del tutorial (lo hace FailureDirector)
        ShipState.Break(currentZone);
        ShipState.CanEnterShip = true;
        step = Step.Repairing;
    }

    private void Finish()
    {
        step = Step.Done;
        ShipState.CanEnterShip = true;
        ShipState.TutorialFinished = true;
        ShowMessage(null);
    }

    // ---------- Mensaje en pantalla ----------

    // Enseña un mensaje abajo al centro (null = ninguno). Solo se anima cuando cambia.
    private void ShowMessage(string template)
    {
        string message = template?.Replace("{zone}", currentZone).Replace("{key}", interactKey);
        if (message == shownMessage)
        {
            return;
        }

        shownMessage = message;
        messageGroup.DOKill();

        if (message == null)
        {
            messageGroup.DOFade(0f, 0.3f);
            return;
        }

        messageText.text = message;
        messageGroup.DOFade(1f, 0.3f);
        MinigameFX.Punch(messageText.transform.parent, 0.1f);
        GameAudio.PlaySfx(GameAudio.Either(GameAudio.Sounds.notification, GameAudio.Sounds.menuButton));
    }

    private void CreateMessage()
    {
        Canvas canvas = MinigameUI.CreateCanvas("TutorialCanvas", transform);
        canvas.sortingOrder = 70;

        Image panel = MinigameUI.CreateImage("Panel", canvas.transform, new Color(0.05f, 0.07f, 0.1f, 0.85f), Vector2.zero, new Vector2(1100f, 80f));
        panel.raycastTarget = false;
        panel.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        panel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        panel.rectTransform.anchoredPosition = new Vector2(0f, 150f);

        messageText = MinigameUI.CreateText("Message", panel.transform, "", 28, Vector2.zero, new Vector2(1060f, 80f));
        messageGroup = panel.gameObject.AddComponent<CanvasGroup>();
        messageGroup.alpha = 0f;
        messageGroup.blocksRaycasts = false;
    }

    // Tecla de la acción Interact en el teclado (por ejemplo "E")
    private static string GetInteractKey()
    {
        InputAction interact = InputSystem.actions.FindAction("Interact");
        string key = interact.GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard&Mouse"));
        return string.IsNullOrEmpty(key) ? interact.GetBindingDisplayString() : key;
    }
}
