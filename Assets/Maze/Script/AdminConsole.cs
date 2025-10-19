using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class AdminConsole : MonoBehaviour
{
    [Header("UI References")]
    public GameObject consolePanel;
    public TMP_Text consoleOutput;
    public TMP_InputField commandInput;
    public GameObject reticle;

    [Header("Console Settings")]
    public KeyCode toggleKey = KeyCode.BackQuote;
    public int maxOutputLines = 15;
    public Color normalTextColor = Color.white;
    public Color errorTextColor = Color.red;
    public Color successTextColor = Color.green;

    [Header("Player References")]
    public CharacterController characterController;
    public MonoBehaviour playerMovementScript;
    public MonoBehaviour cameraLookScript;

    private bool isConsoleOpen = false;
    private List<string> outputLines = new List<string>();

    private bool noclipEnabled = false;
    private float noclipSpeed = 10f;
    private float sprintMultiplier = 2f;
    private float brightness = 1f;
    private bool originalCameraScriptEnabled;
    private bool originalCursorLocked;

    void Start()
    {
        if (consolePanel != null) consolePanel.SetActive(false);
        if (characterController == null)
            characterController = GameObject.FindGameObjectWithTag("Player")?.GetComponent<CharacterController>();

        AddOutput("Admin Console initialized. Press ~ to open.", successTextColor);
        AddOutput("Type 'help' for available commands.", normalTextColor);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) ToggleConsole();

        if (noclipEnabled && !isConsoleOpen) HandleNoclip();

        if (isConsoleOpen && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            if (!string.IsNullOrWhiteSpace(commandInput.text))
            {
                ExecuteCommand(commandInput.text);
                commandInput.text = "";
                commandInput.ActivateInputField();
            }
        }
    }

    void ToggleConsole()
    {
        isConsoleOpen = !isConsoleOpen;
        if (consolePanel != null) consolePanel.SetActive(isConsoleOpen);

        // Disable player movement script when console is open
        if (playerMovementScript != null)
            playerMovementScript.enabled = !isConsoleOpen;

        if (isConsoleOpen)
        {
            originalCursorLocked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (cameraLookScript != null)
            {
                originalCameraScriptEnabled = cameraLookScript.enabled;
                if (!noclipEnabled) cameraLookScript.enabled = false;
            }

            commandInput?.ActivateInputField();
        }
        else
        {
            if (cameraLookScript != null)
                cameraLookScript.enabled = noclipEnabled ? true : originalCameraScriptEnabled;

            if (!noclipEnabled && originalCursorLocked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    void ExecuteCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        AddOutput($"> {command}", normalTextColor);
        string[] parts = command.ToLower().Trim().Split(' ');
        string cmd = parts[0];

        switch (cmd)
        {
            case "help": ShowHelp(); break;
            case "clear": ClearConsole(); break;
            case "noclip": ToggleNoclip(); break;
            case "flyspeed":
                if (parts.Length > 1 && float.TryParse(parts[1], out float speed))
                {
                    noclipSpeed = speed;
                    AddOutput($"Noclip speed set to {noclipSpeed}", successTextColor);
                }
                else AddOutput("Usage: flyspeed <speed>", errorTextColor);
                break;
            case "tp":
            case "teleport":
                if (parts.Length >= 4 &&
                    float.TryParse(parts[1], out float x) &&
                    float.TryParse(parts[2], out float y) &&
                    float.TryParse(parts[3], out float z))
                {
                    TeleportPlayer(new Vector3(x, y, z));
                }
                else AddOutput("Usage: tp <x> <y> <z>", errorTextColor);
                break;
            case "speed":
                if (parts.Length > 1 && float.TryParse(parts[1], out float speedMult))
                {
                    if (playerMovementScript != null)
                    {
                        var speedField = playerMovementScript.GetType().GetField("moveSpeed");
                        if (speedField != null)
                        {
                            speedField.SetValue(playerMovementScript, speedMult);
                            AddOutput($"Movement speed set to {speedMult}", successTextColor);
                        }
                        else AddOutput("Could not find moveSpeed field", errorTextColor);
                    }
                }
                else AddOutput("Usage: speed <multiplier>", errorTextColor);
                break;
            case "pos":
            case "position":
                if (characterController != null)
                {
                    Vector3 pos = characterController.transform.position;
                    AddOutput($"Current position: ({pos.x:F2}, {pos.y:F2}, {pos.z:F2})", successTextColor);
                }
                else AddOutput("Character controller not found", errorTextColor);
                break;
            case "brightness":
                if (parts.Length > 1 && float.TryParse(parts[1], out float br))
                {
                    brightness = Mathf.Clamp(br / 100f, 0f, 2f); // 0% to 200%
                    RenderSettings.ambientLight = Color.white * brightness;
                    AddOutput($"Brightness set to {br}%", successTextColor);
                }
                else
                {
                    AddOutput("Usage: brightness <percent>", errorTextColor);
                }
                break;
            default:
                AddOutput($"Unknown command: {cmd}. Type 'help' for available commands.", errorTextColor);
                break;
        }

        // Refocus input and maintain noclip cursor
        if (commandInput != null)
        {
            commandInput.ActivateInputField();
            commandInput.Select();
        }
        if (noclipEnabled)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void ShowHelp()
    {
        AddOutput("=== Available Commands ===", successTextColor);
        AddOutput("help - Show this help message", normalTextColor);
        AddOutput("clear - Clear console output", normalTextColor);
        AddOutput("noclip - Toggle noclip mode (walk/fly through walls)", normalTextColor);
        AddOutput("flyspeed <speed> - Set noclip flying speed", normalTextColor);
        AddOutput("tp <x> <y> <z> - Teleport to coordinates", normalTextColor);
        AddOutput("speed <value> - Set movement speed", normalTextColor);
        AddOutput("pos - Show current position", normalTextColor);
        AddOutput("brightness <0-100> - Set ambient brightness", normalTextColor);
    }

    void ClearConsole()
    {
        outputLines.Clear();
        if (consoleOutput != null) consoleOutput.text = "";
    }

    void ToggleNoclip()
    {
        noclipEnabled = !noclipEnabled;

        if (characterController != null)
        {
            characterController.enabled = !noclipEnabled;
        }

        if (cameraLookScript != null) cameraLookScript.enabled = noclipEnabled ? true : originalCameraScriptEnabled;

        Cursor.lockState = noclipEnabled ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !noclipEnabled;

        reticle?.SetActive(!noclipEnabled);

        AddOutput($"Noclip: {(noclipEnabled ? "ENABLED" : "DISABLED")}", noclipEnabled ? successTextColor : normalTextColor);
    }

    void HandleNoclip()
    {
        if (characterController == null) return;

        Vector3 inputDir = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
        if (Input.GetKey(KeyCode.Space)) inputDir.y += 1f;
        if (Input.GetKey(KeyCode.LeftControl)) inputDir.y -= 1f;

        float speed = noclipSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) speed *= sprintMultiplier;

        Vector3 move = Camera.main.transform.TransformDirection(inputDir) * speed * Time.deltaTime;
        characterController.transform.position += move;
    }

    void TeleportPlayer(Vector3 position)
    {
        if (characterController != null)
        {
            // Keep noclip enabled after teleport
            bool wasEnabled = characterController.enabled;
            characterController.enabled = false;
            characterController.transform.position = position;
            characterController.enabled = noclipEnabled ? true : wasEnabled;

            // Maintain noclip state
            if (noclipEnabled)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                if (cameraLookScript != null) cameraLookScript.enabled = true;
                if (reticle != null) reticle.SetActive(false);
            }

            AddOutput($"Teleported to {position}", successTextColor);
        }
        else
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                player.transform.position = position;
                AddOutput($"Teleported to {position}", successTextColor);
            }
            else
            {
                AddOutput("Could not find player to teleport", errorTextColor);
            }
        }
    }

    void AddOutput(string text, Color color)
    {
        string coloredText = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
        outputLines.Add(coloredText);

        while (outputLines.Count > maxOutputLines)
            outputLines.RemoveAt(0);

        if (consoleOutput != null)
            consoleOutput.text = string.Join("\n", outputLines);
    }
}